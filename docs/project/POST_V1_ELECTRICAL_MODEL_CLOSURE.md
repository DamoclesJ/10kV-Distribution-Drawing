# Post-V1 Electrical Model Closure

> 状态：Scope Frozen / Implementation Not Started
>
> 本文是 Post-V1 第一个已确认实施阶段的正式范围与执行顺序。它不定义 V1.1、V1.2 或 V2.0，也不表示任何下述功能已经实现。

## 1. 阶段定位

V1.0 已正式发布。Post-V1 Requirement Reassessment / Planning 至此结束，Post-V1 路线图下第一个已确认实施阶段为 **Post-V1 Electrical Model Closure**。

本阶段目标是在进入未来的 Annotation / Work-ticket Presentation Layer 之前：

- 补齐当前实际 10kV 工作票绘制所需的主要 Electrical Model 缺口；
- 建立统一的新一代 Persistence baseline；
- 通过独立、可验证的 Work Package 完成实现与集成验证。

本阶段不绑定或预告后续产品版本号。当前代码与工程文件仍为 V1.0 / FormatVersion 6 基线；只有相应 Work Package 完成后，本文定义的目标能力才成为实现事实。

## 2. 不可破坏的架构边界

- Electrical Model / topology 是电气事实源。
- Layout / Rendering 不成为电气事实源。
- Presentation / Annotation 不参与电气 topology。
- 真实电气对象不得为了显示方便退化为普通绘图图元。
- Stable ID 必须跨 Command、Undo/Redo、迁移和 Persistence round-trip 保持合同语义。
- 旧格式迁移属于 DTO / Persistence 层；Domain constructor 不判断旧 `FormatVersion`。

## 3. 已确认 Scope

### 3.1 RingCabinet Optional CableTerminal

普通 `RingCabinet` 间隔：

- `LoadSwitchInterval`；
- `IntegratedFeederInterval`；

允许有或没有电缆终端。`PTInterval` 不适用该能力。

目标结构使用 nullable identity：

```text
CableTerminalId: Guid?
HasCableTerminal = CableTerminalId is not null
```

`HasCableTerminal` 是派生值，不持久化第二个可能冲突的 boolean。无电缆终端表示真实空间隔：不存在 External Cable Terminal，Cable Tool 不允许连接，Rendering 不显示电缆终端，但内部 circuit node、switch 和 assembly 继续存在。

结构变更合同：

- 有 → 无：若 `Cable`、`GroundingPoint`、`WorkScope` 或其它正式对象仍引用该 Terminal，拒绝操作；不得静默断线、删除或迁移引用。
- 无 → 有：创建新的 Terminal ID。
- Undo：恢复原 Terminal ID。
- Redo：保持命令首次确定的 ID，不得再次调用 `Guid.NewGuid()`。

### 3.2 Transformer

`Transformer` 是顶层 Electrical Device，当前只建模 10kV 高压侧，并正式包含三种 `TransformerKind`：

| TransformerKind | 业务含义 | 合法连接 | 安装与布局边界 |
| --- | --- | --- | --- |
| `PublicPoleMounted` | 柱上公变 | `OverheadLine` only | 独立顶层设备，不属于 `PoleAttachment` |
| `DedicatedPoleMounted` | 柱上专变 | `OverheadLine` only | 独立顶层设备，不属于 `PoleAttachment` |
| `PublicIndoor` | 站内公变 | `Cable` only | `TransformerLayout` 支持 Horizontal / Vertical |

三种 Transformer 当前均只有一个正式 10kV External Terminal，不实现 0.4kV / 低压侧。

柱上公变和柱上专变的上游通常存在 `DropoutFuse`，但它继续是独立 `SwitchDevice`：

```text
OverheadLine
→ DropoutFuse
→ short OverheadLine
→ Transformer
```

不得把 `DropoutFuse` 内嵌进 `Transformer`。

### 3.3 CustomerStation

`CustomerStation` 是正式 Electrical / Professional aggregate，不是 Annotation。正式 `StationKind` 为：

- `BoxStation`：箱式用户站；当前固定一个 `IncomingFeeder`。
- `IndoorStation`：室内用户站；允许一个或两个 `IncomingFeeder`。

`CustomerStation` 不允许零 feeder。双电源表示一个 `CustomerStation` 拥有两个 `IncomingFeeder`，不是两个独立 `CustomerStation`。

当前只允许 Cable 进线，不支持 OverheadLine 进线。每个 `IncomingFeeder`：

- 拥有独立 Stable ID、Terminal、Node、SwitchState 和 GroundingPoint possibility；
- 包含真实 `IsolationSwitch`，其 `SwitchKind` 固定为 `IsolationSwitch`，使用正常 `SwitchState.Open` / `SwitchState.Closed`；
- 在电缆侧允许 `GroundingPoint`；
- 为未来独立 Energization result 保留身份边界，但本阶段不实现 Energization Analysis。

当前不建立两个 feeder 之间的内部电气连接，也不建模用户站内部母线、母联或站内网络。

### 3.4 GroundingAccessPoint 与 GroundingTarget

验电接地环 / 接地线夹正式建模为 `GroundingAccessPoint`。它是具有 Stable ID 的轻量 Electrical / Professional entity，不是 Device、SwitchDevice、Terminal 或 Annotation。

`GroundingAccessPoint` 引用：

- `OverheadLine` / `Connection`；
- `Pole`；
- `LineSide`，只使用 `SmallerNumberSide` / `LargerNumberSide` 专业语义，不保存画布 Left / Right。

它不截断 `OverheadLine`，不分割 `Connection`，不创建 `ElectricalNode`，不改变正常线路导通，并可在没有 `GroundingPoint` 时独立存在。同一 `OverheadLine + Pole + LineSide` 当前最多一个 `GroundingAccessPoint`。

`GroundingPoint` 的目标扩展为单一类型化引用：

```text
GroundingTarget
├── Terminal
└── GroundingAccessPoint
```

不得使用两个 nullable target ID。当前一个 `GroundingTarget` 最多对应一个 `GroundingPoint`。

### 3.5 Pole-mounted equipment grounding presentation

对于柱上 `SwitchDevice`，`GroundingPoint` 的 Electrical target 继续使用真实 Switch Terminal，但明确：

```text
Electrical anchor != Grounding presentation anchor
```

接地线图面表达必须位于 `Pole + attached Switch` 整个专业组合图元的外侧，不得机械使用 raw Terminal coordinate 而将地线画在 Pole 与 Switch 之间。

本阶段增加派生的专业 grounding presentation anchor 机制，默认不持久化人工显示坐标。若 persisted `GroundingPoint` 无法解析专业显示 anchor：

- 必须产生显式诊断；
- 不得静默不显示；
- 正式导出不得无声遗漏安全相关专业事实。

## 4. FormatVersion 7 与迁移合同

本阶段统一引入 `FormatVersion 7`。Optional CableTerminal、Transformer、CustomerStation 和 GroundingAccessPoint 不分别升级格式；V7 是整个阶段统一的 Persistence baseline。

V7 至少容纳：

- nullable interval CableTerminal identity；
- `Transformer`；
- `CustomerStation` 与 `IncomingFeeder`；
- typed `SwitchOwner`；
- `GroundingAccessPoint`；
- typed `GroundingTarget`；
- `TransformerLayout`；
- `CustomerStationLayout`。

### 4.1 V6 → V7 无损迁移

| V6 事实 | V7 迁移结果 |
| --- | --- |
| 普通 interval `ExternalTerminalId` | `CableTerminalId = existing ExternalTerminalId`，即所有 V6 普通间隔默认有电缆终端 |
| `GroundingPoint.TerminalId` | `GroundingTarget.Kind = Terminal`，`GroundingTarget.TargetId = existing TerminalId` |
| cabinet switch `ParentId` | typed owner = `RingCabinetInterval(existing ParentId)` |
| pole switch | aggregate owner = none；`PoleAttachment` 原样保留 |
| 新增顶层集合 | `Transformers = []`、`CustomerStations = []`、`GroundingAccessPoints = []` |

迁移必须保持所有已有 `DeviceId`、`IntervalId`、`SwitchId`、`TerminalId`、`ElectricalNodeId`、`ConnectionId`、`SwitchState`、`GroundingPoint`、`WorkScope` 和 Layout 原值。

### 4.2 V6 文件升级保存行为

- 新软件继续支持读取 V6，并在内存中迁移到 V7。
- 第一次尝试保存由 V6 打开的工程时，必须要求 Save As 为新的 V7 文件，不得默认覆盖原 V6 文件。
- V7 文件首次保存成功后，后续可正常 Save。
- 不提供 V7 → V6 降级导出；V6 无法表达本阶段新增结构事实。
- Persistence / Session 层保留 `OpenedFormatVersion` 或等效 source-version 信息，供 UI 决定首次升级保存行为。

该合同保留原 V6 工程，使其仍可由 V1.0 打开。

## 5. Switch ownership

现有 `SwitchDevice.ParentId` 对 `RingCabinetInterval` 语义绑定过强。目标合同为有限、类型化的 owner reference：

```text
SwitchOwnerReference?
├── OwnerKind
│   ├── RingCabinetInterval
│   └── CustomerStationIncomingFeeder
└── OwnerId
```

规则：

- RingCabinet switch：owner = `RingCabinetInterval`。
- CustomerStation incoming `IsolationSwitch`：owner = `CustomerStationIncomingFeeder`。
- Pole switch：aggregate owner = none，通过 `PoleAttachment` 表达物理安装。
- 独立 switch：owner = none。

不得扩展为通用、任意多层 ownership graph。

## 6. Layout 与 Anchor

不建立任意 `DeviceLayout` property bag。本阶段仅使用明确的 typed layout：

- `TransformerLayout`；
- `CustomerStationLayout`。

`PublicIndoor` 的 Horizontal / Vertical orientation 属于 `TransformerLayout`，不是 Transformer Domain 业务类型。

CustomerStation 内部 feeder 排布由 `StationKind + IncomingFeeder.Count + DrawingMetrics` 派生，默认不保存每个 feeder 的自由坐标。

继续保留：

- `TerminalAnchorIndex` → Cable / OverheadLine routing and picking；
- `GroundingPresentationAnchorResolver` → Grounding professional rendering。

## 7. 明确 Out of Scope

本阶段不包含：

- Annotation / Work-ticket Presentation Layer；
- Text、TextBox、Line、Rectangle、Arrow 等自由标注；
- Energization Analysis 实现或 persisted energized/red state；
- 0.4kV / low-voltage network；
- CustomerStation internal bus、bus coupler 或 internal detailed network；
- OverheadLine incoming CustomerStation；
- dual-supply BoxStation；
- 其它未经重新确认的专业设备；
- JPG export、Windows Print、PDF export；
- advanced manual routing / waypoint；
- DWG、cloud sync、multi-user；
- plugin device system；
- universal station framework；
- arbitrary N-feeder infrastructure；
- arbitrary line accessory framework；
- generic presentation property bag。

Annotation 和 Energization 保留为 Post-V1 Candidate，但不属于 Post-V1 Electrical Model Closure。

## 8. Work Package 顺序

每个 Work Package 必须独立实施、验证和审查，不得借相邻切片扩展业务范围。

### WP-EM-01 — Grounding Presentation Anchor Separation

目标：

- 分离 electrical anchor 与 grounding presentation anchor；
- 修复 `Pole + attached Switch` 外侧接地表达；
- Professional anchor 缺失时产生显式诊断。

该 WP 不需要 V7，不改变 persisted topology，是本阶段第一个实施 WP。

### WP-EM-02 — V7 Format & Migration Foundation

这是基础设施 WP，不一次性实现四个完整功能。范围只包括：

- FormatVersion 7 foundation；
- V6 → V7 migration pipeline 与测试；
- source / `OpenedFormatVersion` tracking；
- V7 DTO / schema contract；
- typed switch owner persistence contract；
- nullable CableTerminal DTO contract；
- `GroundingTarget` persistence contract；
- Transformer / CustomerStation / GroundingAccessPoint 顶层 schema slots；
- typed layout schema；
- V7 serialization / round-trip foundation；
- V6 first-save-as-V7 所需 persistence / session 基础。

不包含 Transformer UI / professional symbol、CustomerStation UI / professional symbol、GroundingAccessPoint interaction、OptionalTerminal Inspector 或各 feature 的完整 Domain behavior。

### WP-EM-03 — RingCabinet Optional CableTerminal Vertical Slice

完成 Domain behavior、structural command、dependency guard、Inspector、rendering、TerminalAnchor、Cable picking、interval type-change interaction、Clipboard、Undo / Redo、V7 integration 和 regression tests。

### WP-EM-04 — GroundingAccessPoint & GroundingTarget Vertical Slice

完成 Domain behavior、create/delete、GroundingPoint target binding、Pole / OverheadLine deletion guards、selection、inspector、rendering、clipboard、Undo / Redo 和 V7 integration。不得分割 `OverheadLine`。

### WP-EM-05 — Transformer Vertical Slice

完成 three Transformer kinds、10kV terminal、create/delete、Cable / OverheadLine endpoint validation、professional symbols、`PublicIndoor` orientation、layout、selection、inspector、clipboard、Undo / Redo、V7 integration、topology graph compatibility，以及完整 `DropoutFuse → OverheadLine → Transformer` 场景。

### WP-EM-06 — CustomerStation Vertical Slice

完成 `BoxStation`、`IndoorStation`、one/two `IncomingFeeder`、feeder-owned `IsolationSwitch`、cable-only connection、independent feeder topology、GroundingPoint integration、aggregate create/delete、professional rendering、selection、inspector、clipboard、Undo / Redo 和 V7 integration。

### WP-EM-07 — Electrical Model Closure Integration

只进行：

- V6 / V7 regression matrix；
- save/open、copy/paste、undo/redo；
- delete dependency；
- topology graph；
- grounding diagnostics；
- Windows runtime 与 professional visual validation；
- file upgrade validation；
- integration defect fixes。

不得在 WP-EM-07 扩展新业务范围。

## 9. 阶段执行状态

Scope Freeze 完成后：

- Post-V1 Requirement Reassessment / Planning 结束；
- Post-V1 Electrical Model Closure 成为当前开发目标；
- 功能实现尚未开始；
- 下一个 Work Package 是 WP-EM-01 Grounding Presentation Anchor Separation；
- 后续 WP 必须按 WP-EM-01 → WP-EM-07 顺序推进，任何范围变化需重新治理确认。
