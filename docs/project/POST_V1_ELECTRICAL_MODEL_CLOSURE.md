# Post-V1 Electrical Model Closure

> 状态：Scope Frozen / WP-EM-01 Completed / WP-EM-02 Completed / WP-EM-03 Closed / WP-EM-04 Next / Grounding Scope Amendment Completed
>
> 本文是 Post-V1 第一个已确认实施阶段的正式范围与执行顺序。它不定义 V1.1、V1.2 或 V2.0，也不表示任何下述功能已经实现。

## 1. 阶段定位

V1.0 已正式发布。Post-V1 Requirement Reassessment / Planning 至此结束，Post-V1 路线图下第一个已确认实施阶段为 **Post-V1 Electrical Model Closure**。

本阶段目标是在进入未来的 Annotation / Work-ticket Presentation Layer 之前：

- 补齐当前实际 10kV 工作票绘制所需的主要 Electrical Model 缺口；
- 建立统一的新一代 Persistence baseline；
- 通过独立、可验证的 Work Package 完成实现与集成验证。

本阶段不绑定或预告后续产品版本号。当前代码与工程文件为 V1.0 / FormatVersion 7 基线；WP-EM-02 已建立 V7 persistence foundation，只有相应后续 Work Package 完成后，本文定义的对应 feature capability 才成为实现事实。

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

### 3.4 GroundingAccessPoint 与架空接地合同

验电接地环 / 接地线夹正式建模为 `GroundingAccessPoint`。它是具有 Stable ID 的永久、轻量 Electrical / Professional entity，不是 Device、`SwitchDevice`、`Terminal` 或 Annotation。架空侧新建工作地线统一采用：

```text
Overhead conductor
    ↓
GroundingAccessPoint
    ↓
GroundingPoint
```

该规则适用于普通支撑杆，以及存在柱上 `SwitchDevice`、隔离刀闸、跌落式熔断器、`CableTermination` 或其它柱上设备的杆塔；不得因现场存在设备 `Terminal`，就将该 Terminal 作为新建架空工作地线的主要目标。

`GroundingAccessPoint` 至少稳定引用：

- associated `OverheadLine` / `Connection`；按当前模型以其 stable `ConnectionId` 表达；
- `PoleId`；
- `LineSide`，值仅为 `SmallerNumberSide` / `LargerNumberSide`。

同一 `ConnectionId + PoleId + LineSide` 最多一个 `GroundingAccessPoint`。`Left`、`Right`、`Up`、`Down` 只是 Rendering 结果，不得持久化为业务事实。

`GroundingAccessPoint` 不创建新的 `ElectricalNode`，不分割 `OverheadLine` / `Connection`，不改变 conduction，不成为 Switch 或 Terminal。它与临时 `GroundingPoint` 生命周期独立，可在没有 `GroundingPoint` 时存在；删除 `GroundingPoint` 不得自动删除 `GroundingAccessPoint`。

### 3.5 GroundingTarget 与 Terminal compatibility

V7 中 `GroundingPoint` 的目标为单一类型化引用：

```text
GroundingTarget
├── Terminal
└── GroundingAccessPoint
```

不得使用两个 nullable target ID。一个 `GroundingTarget` 最多对应一个 `GroundingPoint`。

V7 persistence 的最小 typed contract 为：

```text
ProjectGroundingTargetDto
├── Kind: Terminal | GroundingAccessPoint
└── TargetId

ProjectGroundingAccessPointDto
├── GroundingAccessPointId
├── ConnectionId
├── PoleId
└── LineSide: SmallerNumberSide | LargerNumberSide
```

`ProjectGroundingPointDto` 以单一 `GroundingTarget` 取代 V6 的 `TerminalId`。V7 顶层 schema 必须为 `GroundingAccessPoint` 保留 typed collection；不得用自由字典、两个 nullable target ID 或 rendering direction 代替这些字段。

`Terminal` target 继续合法，但只承担：

- V1.0 / V6 legacy compatibility；
- 明确建模的真实电缆侧接地场景；
- 必要的特殊 Terminal grounding。

允许新建接地的 Terminal 类型必须由相应 Vertical Slice 按真实设备语义明确，不得把所有 Terminal 自动视为合法创建目标。当前确认的电缆侧目标包括 `CableTermination.CableSideTerminalId`，以及后续模型中的 `RingCabinet` cable-side terminal 和 `CustomerStation` incoming cable-side terminal。

对于 `CableTermination`，必须区分两侧：

```text
Cable side grounding    → CableSideTerminal (`Terminal` target)
Overhead side grounding → adjacent `GroundingAccessPoint`
```

`CableTermination.OverheadSideTerminalId` 不作为未来架空工作地线的主要创建目标。

WP-EM-01 已实现的 `Pole + Switch` legacy Terminal-target presentation anchor 必须保留，用于旧文件、既有 Terminal-target `GroundingPoint` 和特殊 Terminal grounding。该 resolver 继续遵守 `Electrical anchor != Grounding presentation anchor`，但不再代表未来架空接地的主要创建工作流。若 persisted `GroundingPoint` 无法解析专业显示 anchor，必须产生显式诊断，不得静默不显示或在正式导出中无声遗漏。

### 3.6 Grounding professional presentation 与交互

`GroundingAccessPoint` 的默认专业位置由 `Pole + associated overhead segment + LineSide` 派生。存在柱上设备时，位置仍在设备外侧的真实架空导线上：从 Pole 或 `Pole + relevant device` 专业组合外边界，沿该侧导线方向向外保留约 1～2 mm 间距，推荐视觉目标约 1.5 mm。最终数值由 Rendering `DrawingMetrics` 中的 `GroundingAccessClearance` 或等价 typed metric 决定，不进入 Domain，也不持久化 screen / physical pixel 坐标。

工作地线使用标准矢量 grounding symbol：一根竖向主 stem，下端三条以 stem 为中心、由上到下逐渐变短的水平横线。不得继续使用含义不明确的小方框或 bitmap。符号尺度与 Pole 保持合理专业比例，并由 Rendering metric 控制；用户调整 leader 时符号本身大小固定，不保存 symbol scale。

架空 GAP-target `GroundingPoint` 的默认路径从架空导线接地点引至 grounding symbol。用户选择后可拖动 symbol，以增减 leader 长度并在水平、竖直方向避让标签和其它专业信息；该操作只修改 `GroundingPointLayout`，不得改变 `GroundingTarget`、`GroundingAccessPoint` 或 topology。

Cable-side Terminal-target 路径必须支持：

```text
Terminal → horizontal / outward leader → orthogonal bend
         → vertical leader → Grounding Symbol
```

outward 方向由设备 orientation 与 Rendering geometry 派生，不持久化 screen `Left` / `Right`；人工调整可同时影响水平和竖直方向。

Grounding leader 穿过其它线路时，应优先复用当前 `LineJumpDecorator` 或等价的月牙式 / bridge crossing 表达。crossing 由当前 geometry 派生，不创建 electrical node、不改变 topology、不持久化假连接，原则上不单独保存 crossing waypoint；不得借此扩展为通用 diagram routing engine。

### 3.7 Grounding 创建与字段交互

WP-EM-04 必须提供可用的 `GroundingAccessPoint` 创建、删除、选择和展示，并允许在合法 GAP 上创建 `GroundingPoint`。Grounding workflow 可在实现审计时采用“缺少时快捷创建 GAP”的最小 UX，但 GAP 不得成为 `GroundingPoint` 的瞬态内部对象。

对于仍允许的 Cable-side Terminal grounding，后续交互应提供合理 hit tolerance、target affordance / highlight 和 nearest-target resolution；不得把任意 device body 或 line geometry 隐式映射为不确定的 Terminal，也不得为旧架空 Terminal workflow 建立复杂的通用 picking framework。

`GroundingPoint.Location` 继续是工作票文字 / 位置说明。UI 提供“小号侧 / 大号侧 / 自定义”，默认“小号侧”。GAP target 的默认显示值直接由唯一结构事实 `GroundingAccessPoint.LineSide` 派生：`SmallerNumberSide` → “小号侧”，`LargerNumberSide` → “大号侧”；不得再持久化第二套 side enum。只有选择“自定义”时输入自定义文本。Terminal target 的 `Location` 仅为 descriptive text，不因这些文字创造 Electrical `LineSide` 语义。

新建 `GroundingPoint` 的 `Number` 默认 `L01`；创建输入为 null、empty 或 whitespace 时归一化为 `L01`，且用户可修改。本阶段不引入 `L02`、`L03` 自动递增或通用 numbering framework。

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
- `CustomerStationLayout`；
- typed `GroundingPointLayout`。

### 4.1 V6 → V7 无损迁移

| V6 事实 | V7 迁移结果 |
| --- | --- |
| 普通 interval `ExternalTerminalId` | `CableTerminalId = existing ExternalTerminalId`，即所有 V6 普通间隔默认有电缆终端 |
| `GroundingPoint.TerminalId` | `GroundingTarget.Kind = Terminal`，`GroundingTarget.TargetId = existing TerminalId`；保持 GroundingPoint stable ID、Location、Number、Note，不推断 GAP |
| cabinet switch `ParentId` | typed owner = `RingCabinetInterval(existing ParentId)` |
| pole switch | aggregate owner = none；`PoleAttachment` 原样保留 |
| 新增顶层集合 | `Transformers = []`、`CustomerStations = []`、`GroundingAccessPoints = []` |
| V6 无 `GroundingPointLayout` | 无人工 override，使用默认派生布局；不得生成随机 offset |

迁移必须保持所有已有 `DeviceId`、`IntervalId`、`SwitchId`、`TerminalId`、`ElectricalNodeId`、`ConnectionId`、`SwitchState`、`GroundingPoint`、`WorkScope` 和 Layout 原值。旧 Switch Terminal grounding 不得自动转换为 GAP，也不得猜测现场真实位置并重写电气语义。

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
- `CustomerStationLayout`；
- `GroundingPointLayout`。

`PublicIndoor` 的 Horizontal / Vertical orientation 属于 `TransformerLayout`，不是 Transformer Domain 业务类型。

CustomerStation 内部 feeder 排布由 `StationKind + IncomingFeeder.Count + DrawingMetrics` 派生，默认不保存每个 feeder 的自由坐标。

按当前 `ProjectLayoutDto`、`ProjectPointDto` 和毫米逻辑坐标风格，V7 冻结以下最小 Grounding layout 合同：

```text
ProjectGroundingPointLayoutDto
├── GroundingPointId
└── SymbolOffset: ProjectPointDto   // drawing logical-space, mm
```

运行时等价 typed layout 使用 `GroundingPointId + SymbolOffset`。无对应 layout record 表示无人工 override，Rendering 使用自动派生位置；人工拖动后只保存相对默认位置的 `SymbolOffset`。不得保存 Terminal / GAP 的假坐标、`Left` / `Right`、symbol scale、crossing waypoint 或电气事实。Undo 恢复前一 offset，Redo 重用同一 `GroundingPointId`，不得产生新 identity；Save / reopen 必须保持人工 offset。

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

Grounding 所需的最小 typed leader adjustment 与派生 bridge crossing 是已确认范围，不将 `advanced manual routing / waypoint` 的排除项扩展为通用 routing 能力。

Annotation 和 Energization 保留为 Post-V1 Candidate，但不属于 Post-V1 Electrical Model Closure。

## 8. Work Package 顺序

每个 Work Package 必须独立实施、验证和审查，不得借相邻切片扩展业务范围。

### WP-EM-01 — Grounding Presentation Anchor Separation

**状态：Completed**

目标：

- 分离 electrical anchor 与 grounding presentation anchor；
- 修复 `Pole + attached Switch` 外侧接地表达；
- Professional anchor 缺失时产生显式诊断。

该 WP 不需要 V7，不改变 persisted topology，是本阶段第一个实施 WP。

### WP-EM-02 — V7 Format & Migration Foundation

**状态：Completed**

这是基础设施 WP，不一次性实现四个完整功能。范围只包括：

- FormatVersion 7 foundation；
- V6 → V7 migration pipeline 与测试；
- source / `OpenedFormatVersion` tracking；
- V7 DTO / schema contract；
- typed switch owner persistence contract；
- nullable CableTerminal DTO contract；
- `GroundingTarget` persistence contract；
- Transformer / CustomerStation / GroundingAccessPoint 顶层 schema slots；
- typed `TransformerLayout`、`CustomerStationLayout` 和 `GroundingPointLayout` schema；
- V6 `GroundingPoint.TerminalId` → V7 `GroundingTarget.Terminal` migration；
- V7 serialization / round-trip foundation；
- V6 first-save-as-V7 所需 persistence / session 基础。

不包含 Transformer UI / professional symbol、CustomerStation UI / professional symbol、GroundingAccessPoint interaction、GroundingPoint drag UI、grounding symbol、grounding leader routing、crossing bridge、OptionalTerminal Inspector 或各 feature 的完整 Domain behavior。

### WP-EM-03 — RingCabinet Optional CableTerminal Vertical Slice

**状态：Closed**

完成 Domain behavior、structural command、dependency guard、Inspector、rendering、TerminalAnchor、Cable picking、interval type-change interaction、Clipboard、Undo / Redo、V7 integration 和 regression tests。

Closure evidence:

- Implementation commit: `979c55fd405ccc1d2dd6f9481758f00e72ef7249` — `feat(electrical-model): support optional interval cable terminals`
- Rendering fix commit: `f60ea9aa299dfb8151c60ff62ba5aff0e2c7b7d6` — `fix(electrical-model): preserve interval lead without cable terminal`
- Windows build: passed.
- Windows automated tests: Domain.Tests 101/101, Infrastructure.Tests 76/76, Rendering.Wpf.Tests 358/358, Desktop.Tests 173/173, ProjectPersistenceRoundTrip 24/24; failed = 0, skipped = 0.
- Windows manual validation: passed. With no external cable, present → absent removes only the terminal triangle and preserves the interval internal lead; absent → present restores the triangle with stable lead geometry. With an external Cable / Connection, terminal removal remains blocked and Cable / topology are preserved. GroundingPoint and WorkScope dependency protection also remains enforced.
- Absent-terminal rendering produces no triangle, terminal anchor, or cable target.
- FormatVersion remains V7; no new migration was added; WP-EM-04 implementation has not started.

### WP-EM-04 — GroundingAccessPoint & GroundingTarget Vertical Slice

完成 GAP Domain behavior、stable identity、唯一性、create/delete、`GroundingTarget` behavior、GroundingPoint target binding、Pole / OverheadLine deletion guards、commands、selection、Inspector、basic GAP rendering、clipboard、Undo / Redo、V7 integration 和 legacy Terminal-target compatibility。不得分割 `OverheadLine`，不得将复杂 presentation layout 塞入本 WP。

### WP-EM-05 — Grounding Layout & Interaction Closure

完成 standard grounding symbol、`GroundingAccessClearance`、`GroundingPointLayout`、drag / leader adjustment、架空 GAP professional placement、Cable-side Terminal elbow route、Location selector、Number 默认 `L01`、target affordance / tolerance、crossing bridge、Canvas / PNG consistency，以及 Windows professional visual acceptance。

### WP-EM-06 — Transformer Vertical Slice

完成 three Transformer kinds、10kV terminal、create/delete、Cable / OverheadLine endpoint validation、professional symbols、`PublicIndoor` orientation、layout、selection、inspector、clipboard、Undo / Redo、V7 integration、topology graph compatibility，以及完整 `DropoutFuse → OverheadLine → Transformer` 场景。

### WP-EM-07 — CustomerStation Vertical Slice

完成 `BoxStation`、`IndoorStation`、one/two `IncomingFeeder`、feeder-owned `IsolationSwitch`、cable-only connection、independent feeder topology、GroundingPoint integration、aggregate create/delete、professional rendering、selection、inspector、clipboard、Undo / Redo 和 V7 integration。

### WP-EM-08 — Electrical Model Closure Integration

只进行：

- V6 / V7 regression matrix；
- save/open、copy/paste、undo/redo；
- delete dependency；
- topology graph；
- grounding diagnostics、layout 与 interaction regression；
- Windows runtime 与 professional visual validation；
- file upgrade validation；
- integration defect fixes。

不得在 WP-EM-08 扩展新业务范围。

## 9. 阶段执行状态

当前执行状态：

- Post-V1 Requirement Reassessment / Planning 结束；
- Post-V1 Electrical Model Closure 成为当前开发目标；
- WP-EM-01 Grounding Presentation Anchor Separation 已完成代码 Review、自动验证和 Windows 实机验证；
- Post-V1 Grounding Scope Amendment 已完成并冻结；
- WP-EM-02 V7 Format & Migration Foundation 已完成代码 Review、自动验证和 Windows 最终验证；
- WP-EM-03 RingCabinet Optional CableTerminal Vertical Slice 已完成并 Closed，包含 Windows 最终验证；
- 下一个阶段为 WP-EM-04 requirements refinement / planning only，尚未开始实现；
- 当前生产实现和工程文件格式为 V7；`GroundingAccessPoint`、Transformer、CustomerStation 尚未实现；
- 后续 WP 必须按 WP-EM-01 → WP-EM-08 顺序推进，任何范围变化需重新治理确认。
