# Post-V1 Electrical Model Closure

> 状态：Scope Frozen / WP-EM-01 Closed / WP-EM-02 Closed / WP-EM-03 Closed / WP-EM-04 Closed / WP-EM-05 Closed / WP-EM-06 Closed / WP-EM-07 Closed / WP-EM-07A Closed / Archived / WP-EM-07B Closed / Archived / WP-EM-08 Implementation In Progress / WP-EM-08 Slice A Closed / Accepted / WP-EM-08 Slice B Requirements Refined / Not Started / WP-EM-08 Slice C Requirements Refined / Characterization Pending / WP-EM-09 Planned / Integration-only / Grounding Scope Amendment Completed / Interaction Stabilization Amendment Completed / WP-EM-08 Scope Amendment Completed / Post-EM-07 Sequencing Amendment Completed
>
> 本文是 Post-V1 第一个已确认实施阶段的正式范围与执行顺序。它不定义 V1.1、V1.2 或 V2.0；已完成 Work Package 的实现事实仅以相应 Closure Evidence 记录为准。

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

| TransformerKind | 业务含义 | professional glyph | 合法连接 | 安装与布局边界 |
| --- | --- | --- | --- | --- |
| `PublicPoleMounted` | 柱上公变 | 架空变台 | `OverheadLine` only | 独立顶层设备，不属于 `PoleAttachment` |
| `DedicatedPoleMounted` | 柱上专变 | 用户架空变台 | `OverheadLine` only | 独立顶层设备，不属于 `PoleAttachment` |
| `PublicIndoor` | 站内公变 | 配电变压器 | `Cable` only | `TransformerLayout` 支持 `Horizontal` / `Vertical` |

#### 3.2.1 Electrical model 与 identity

WP-EM-06 冻结的最小 Domain 事实为：

```text
Transformer : Device
├── Device.Id
├── TransformerKind
└── HvTerminalId
```

三种 Transformer 当前均只有一个正式 10kV External Terminal，并以 `HvTerminalId` 稳定标识。`Transformer` 在 WP-EM-06 中是 one-terminal topology leaf，因此不创建 Transformer `ElectricalNode`、LV Terminal、second Terminal、internal winding topology 或 low-voltage network。所有 glyph geometry 都只属于 professional presentation，不得创造 electrical facts。

`Device.Id` 与 `HvTerminalId` 必须遵守既有 Stable ID 合同，并在 Command、Undo / Redo、Clipboard remap 和 Persistence round-trip 中保持各自身份语义。

#### 3.2.2 Professional glyph contract

三种 `TransformerKind` 必须使用三个独立 professional glyph：

```text
PublicPoleMounted    → 架空变台
DedicatedPoleMounted → 用户架空变台
PublicIndoor         → 配电变压器
```

不得以“同一个 Transformer glyph + 不同文字 label”作为三种业务类型的主要视觉区分。glyph 差异属于正式 professional presentation contract，但不表示三种 Transformer 具有不同 electrical topology；三者仍各自只有一个正式 `HvTerminalId`。

`PublicPoleMounted` 使用 reference 中的“架空变台”专业图元：大型空心圆主体、圆内 T 形结构和下方两个附属小圆。唯一正式 HV presentation anchor 是 T 形结构朝线路方向的竖向 stem 末端，即图元底部中央的线路接入点；只有该位置映射 `Transformer.HvTerminalId`。大型圆、T 形结构和附属小圆均为 presentation geometry；附属小圆不得产生 `Pole`、`PoleId`、Terminal、`ElectricalNode` 或 `PoleAttachment`。

`DedicatedPoleMounted` 使用 reference 中的“用户架空变台”专业图元：三角形主体和三角形下方两个附属小圆。唯一正式 HV presentation anchor 是三角形 apex；只有该位置映射 `Transformer.HvTerminalId`。其它几何不得产生 LV Terminal、fake `ElectricalNode`、fake `DropoutFuse`、`PoleAttachment` 或 second connection target。

`PublicIndoor` 使用 reference 中的“配电变压器”专业图元，其核心为两个相交、尺寸相近的空心圆：

- `Horizontal`：两圆左右排列，HV anchor 位于左侧中心；右侧 visual winding / lead 不形成 LV Terminal。
- `Vertical`：同一专业图元采用上下排列，HV anchor 位于顶部中心；下侧 visual winding / lead 不形成 LV Terminal。

双圆图形不创建第二 Terminal；两种 orientation 下均只有一个 10kV `HvTerminalId`。

#### 3.2.3 Orientation contract

Orientation 只属于 `TransformerLayout`，不得进入 Domain `Transformer` facts。`PublicIndoor` 支持 `Horizontal` 与 `Vertical`，正式 default 为 `Horizontal`，因为它直接对应当前 reference professional glyph baseline；`Vertical` 是正式允许的第二 layout orientation。

`PublicPoleMounted` 与 `DedicatedPoleMounted` 没有用户可编辑的 Orientation 业务语义，各自 professional glyph 使用固定 canonical presentation。为兼容 V7 required typed `ProjectTransformerLayoutDto`，两种 pole-mounted kind 的 persisted `Orientation` 固定为 `Vertical`：

- creation 写入 `Vertical`；
- `Vertical` 是 canonical valid DTO value；
- `Horizontal` 是 invalid value，runtime / persistence validation 必须拒绝；
- mapper 不得静默忽略非法 value；
- Inspector 不显示可编辑 Orientation。

这里的 `Vertical` 只是统一 DTO contract 的 canonical value，不表示 pole-mounted glyph 是可旋转图元。WP-EM-06 不支持 arbitrary rotation、mirror、free angle 或 quarter-turn integer persistence。

#### 3.2.4 Canvas label 与 naming boundary

WP-EM-06 第一版不在 Canvas 固定增加“公变”“专变”“站内公变”label；三种业务类型由三个不同 professional glyph 表达。Inspector 可以显示由 `TransformerKind` 派生的中文业务含义：

- `PublicPoleMounted` → 柱上公变；
- `DedicatedPoleMounted` → 柱上专变；
- `PublicIndoor` → 站内公变。

这些中文名称是 derived UI text，不是独立 persisted field。WP-EM-06 不新增 `TransformerNumber`、`DeviceNumber`、editable `DisplayName`、capacity、model label 或 dispatch number，`ProjectTransformerDto` 不增加 naming field。未来若实际业务需要 Transformer 名称、编号、容量或铭牌信息，必须另行完成 typed model 与 persistence requirement refinement。

#### 3.2.5 Pole affiliation 与 CustomerStation separation

`PublicPoleMounted` 与 `DedicatedPoleMounted` 都是 top-level `Transformer` Device，不是 `PoleAttachment`。WP-EM-06 不增加 `PoleId`、`ParentPoleId`、`AttachmentId` 或其它 persisted affiliation。专业图中的“柱上”关系由 `TransformerLayout.Position`、邻近 Pole 的 presentation、`DropoutFuse` 与 short `OverheadLine` 共同表达；不得为了 Rendering 创建假的 Pole ownership。

`CustomerStation` 的 `BoxStation` 不是 `TransformerKind`，用户箱变抽象图元属于 WP-EM-07 CustomerStation Vertical Slice。必须明确区分：

```text
DedicatedPoleMounted       → 用户架空变台
CustomerStation.BoxStation → 用户箱变
```

两者不是同一个业务对象。`CustomerStation` 的 `IncomingFeeder`、`IsolationSwitch`、cable terminal 及其它 aggregate 结构不得进入 WP-EM-06 `Transformer`。

#### 3.2.6 Connection、Grounding 与 WorkScope boundary

连接合法性必须由正式 Domain connection validation 保证，不能只依赖 UI：

- `PublicPoleMounted` 与 `DedicatedPoleMounted` 只允许作为 `OverheadLine` endpoint，不得直接作为 `Cable` endpoint；
- `PublicIndoor` 只允许作为 `Cable` endpoint，不得直接作为 `OverheadLine` endpoint。

WP-EM-06 不将 `Transformer.HvTerminalId` 加入新建 `GroundingPoint` 的 Terminal whitelist，不得修改 `ProfessionalCommandFactory.IsEligibleNewTerminalTarget` 或等价 whitelist 来开放 Transformer grounding。既有 defensive dependency protection 继续保留。

WP-EM-06 也不主动开放 Transformer HV Terminal 作为新建 `WorkScope` boundary 的 UI / picking workflow，且不因本 WP 主动收紧或重构当前 Domain 通用 boundary validation。删除 Transformer 时，如已存在 `WorkScope` 或其它正式 dependency，必须通过既有 dependency guard 拒绝删除。是否正式开放 Transformer WorkScope boundary 留待独立 requirement decision。

#### 3.2.7 DropoutFuse boundary

柱上公变和柱上专变的上游通常存在 `DropoutFuse`，但它继续是独立 `SwitchDevice`：

```text
OverheadLine
→ DropoutFuse
→ short OverheadLine
→ Transformer
```

`DropoutFuse` 保持独立 `SwitchDevice`、独立 `SwitchState`、独立 Terminal，其柱上安装保持独立 `PoleAttachment`。不得将它内嵌进 `Transformer`，不得把 fuse state 加入 `Transformer`，也不得把两者组成新的 aggregate。`Transformer` 只作为该链路最终的 one-terminal endpoint。

#### 3.2.8 V7 与 implementation boundary

WP-EM-06 的 `FormatVersion` 继续为 V7，并复用已预留的 typed `ProjectTransformerDto`、`ProjectTransformerKind`、`ProjectTransformerLayoutDto`、`ProjectTransformerOrientation`、`ProjectDomainDto.Transformers` 和 `ProjectLayoutDto.TransformerLayouts`。不得升级 V8，不得增加 property bag，不得使用 untyped Guid dictionary；现有 V6 → V7 empty Transformer migration 保持不变。

WP-EM-06 不实现 `CustomerStation`，也不引入 generic route hysteresis、generic drag stabilization、last-valid-position、generic collision routing、Transformer grounding creation、Transformer WorkScope creation UX、Annotation、Work-ticket Presentation Layer、Energization Analysis、load-flow、0.4kV side 或 arbitrary rotation。

### 3.3 CustomerStation

`CustomerStation` 是正式 Electrical / Professional aggregate，不是 Annotation。正式 `StationKind` 为：

- `BoxStation`：箱式用户站；当前固定一个 `IncomingFeeder`。
- `IndoorStation`：室内用户站；允许一个或两个 `IncomingFeeder`。

`CustomerStation` 不允许零 feeder。双电源表示一个 `IndoorStation` 拥有两个独立 `IncomingFeeder`，不是两个独立 `CustomerStation`。当前只允许 Cable 进线，不支持 OverheadLine 进线。

#### 3.3.1 Aggregate identity 与 naming

WP-EM-07 冻结的最小 aggregate identity 为：

```text
CustomerStation : Device
├── Device.Id                     // CustomerStationId
├── StationKind                   // BoxStation | IndoorStation
└── IncomingFeeders               // 1；或 IndoorStation 为 2
    └── IncomingFeeder
        ├── IncomingFeederId
        ├── Sequence              // 1..Count；固定且连续
        ├── DisplayName           // required formal business name
        ├── CableTerminalId
        ├── StationTerminalId
        ├── ElectricalNodeId
        └── IsolationSwitch
            ├── SwitchDevice.Id
            ├── SwitchKind = IsolationSwitch
            ├── SwitchState = Open | Closed
            ├── first Terminal = CableTerminalId
            ├── second Terminal = StationTerminalId
            └── owner = CustomerStationIncomingFeeder(IncomingFeederId)
```

feeder 名称 typed field 采用 `DisplayName`。理由是它与当前 `Device.DisplayName`、`RingCabinetInterval.DisplayName` 的 Domain naming convention 一致，并表示正式、可持久化的业务名称，不是临时 Canvas text。`FeederName` 在 `IncomingFeeder` 类型内语义重复，`Name` 则偏离最接近的 aggregate-child convention。

每路 `DisplayName` 必须 non-empty、trim 后保存，并在 Command、Undo / Redo、Clipboard remap 和 Persistence round-trip 中保留。双 feeder 可以使用不同名称；本 WP 不额外冻结名称唯一性规则。`CustomerStation` 不增加重复的 `StationName`、`StationNumber` 或 editable `Device.DisplayName` 来保存同一业务信息。若继承 `Device.DisplayName` 是实现所必需的基类槽位，CustomerStation runtime 必须保持其为空或仅使用不持久化的 derived UI text，不得形成第二个业务名称事实。

`Sequence` 是 aggregate 的结构顺序事实，不是自由布局坐标。单 feeder 为 `1`；双 feeder 必须为连续且唯一的 `1`、`2`，分别派生为 feeder A / feeder B，并在 professional presentation 中固定左 / 右顺序。不得依赖偶然的 collection serialization order 代替该合同。

#### 3.3.2 Incoming isolation switch 与最小 topology

每个 `IncomingFeeder` 包含一个真实 `SwitchDevice`，不是 rendering decoration。该 switch 的 `SwitchKind` 固定为 `IsolationSwitch`，拥有独立 stable `SwitchDevice.Id`、独立 `SwitchState` 和两个 distinct Terminal。它是 CustomerStation aggregate 内部设备；可以像 RingCabinet 内部 switch 一样注册进 `DrawingDocument.Devices` 供通用 topology 与 switch workflow 使用，但不得被解释为可脱离 feeder 独立创建、删除或持久化的 top-level business object。

WP-EM-07 复用现有 `ElectricalConnectivityGraphBuilder` 的成熟 conduction pattern：同一 `ElectricalNode` 上的 Terminal 由 `ElectricalNodeInternal` edge 连接；`SwitchDevice` 仅在 `SwitchState.Closed` 时产生两个 switch Terminal 之间的 `ClosedSwitch` edge，`Open` 时不产生该 edge。不得为 CustomerStation 创建第二套 switch traversal 规则。

每路 feeder 的最小正确 topology 为：

```text
Cable
  → CableTerminalId
    // SwitchDevice first Terminal; External; Cable only
  → IsolationSwitch
    // Closed: ClosedSwitch edge; Open: no cross-switch edge
  → StationTerminalId
    // SwitchDevice second Terminal; Internal
  → ElectricalNodeId
    // Circuit node owned by IncomingFeeder; station-side boundary only
```

具体合同：

- `CableTerminalId` 与 `StationTerminalId` 必须 non-empty、distinct，并分别等于 `IsolationSwitch.FirstTerminalId` 与 `IsolationSwitch.SecondTerminalId`；
- `CableTerminalId` 对应 Terminal 为唯一 External endpoint，`AllowedConnectionTypes = [Cable]`，`AllowsMultipleConnections = false`，并且是该 feeder 唯一合法的 Cable 与新建 Terminal-target `GroundingPoint` 目标；
- `StationTerminalId` 对应 Terminal 为 Internal，不允许外部 Connection，并绑定 `ElectricalNodeId`；
- `ElectricalNodeId` 对应 `ElectricalNodeType.Circuit`，typed owner 为该 `IncomingFeeder` internal aggregate；它只表示 station-side electrical boundary，不表示 station internal bus、transformer 或 LV topology；
- 一个 feeder 在业务上拥有两个 Terminal；其直接 topology owner 仍为内嵌 `SwitchDevice`，feeder 通过稳定 ID 明确引用这两个端点；
- 双 feeder 的 switch、两个 Terminal 与 `ElectricalNode` 必须全部使用不同 Stable ID；两个 `ElectricalNode` 之间不得建立 edge，也不得共享 Terminal，因此两路在本 WP 保持不连接。

现有 runtime 仍将 `SwitchDevice.ParentId` 与 `RingCabinetInterval`、`SwitchInstallationType` 与 `CabinetInterval | Pole` 绑定，`DrawingDocument.ChangeSwitchState` 也只处理这两类。这与 V7 DTO 已预留的 `ProjectSwitchOwnerKind.CustomerStationIncomingFeeder` 尚未闭合。WP-EM-07 应完成有限 typed owner runtime contract（`RingCabinetInterval` / `CustomerStationIncomingFeeder` / none）并使 CustomerStation switch 进入现有 `ChangeSwitchStateCommand` 与 graph builder；不得扩展为任意 ownership graph。

#### 3.3.3 Professional glyph contract

`BoxStation` 使用独立 professional glyph：矩形主体框、框内一个三角形、顶部一个“人字形”屋顶，以及左侧固定集成的 incoming isolation switch。屋顶仅用于区别 `BoxStation` 与 `IndoorStation`。框内三角形仅是 professional presentation，不得创建 `Transformer` Device、Transformer Terminal、`ElectricalNode` 或 LV topology。`BoxStation` 固定一个 feeder；incoming switch 永远显示并按真实 `SwitchState.Open` / `SwitchState.Closed` 绘制。

`IndoorStation` 的 station unit 使用矩形主体框和框内一个三角形，没有“人字形”屋顶。单电源显示一个 station unit 和一个 feeder；双电源显示两个 station unit 左右并列，仍属于同一个 `IndoorStation`，并分别对应 `Sequence = 1` / `2` 的 feeder A / feeder B。框内三角形同样只属于 presentation。两路 feeder 的 identity、名称、switch state、Cable connection、GroundingPoint 与 switch visibility 均独立，但不创建两路之间的内部母线或其它 electrical edge。

IndoorStation incoming presentation 的 canonical placement 正式冻结如下：

- single feeder：`Sequence = 1`，station unit 使用左侧进线，incoming switch 固定在 unit 左侧；visible `CableTerminalId` presentation anchor 位于 switch 外侧 / cable-side point，hidden anchor 位于同一 unit 左侧 body-edge cable-entry point；
- dual feeder 的 `Sequence = 1`：对应左侧 station unit，incoming switch 固定在整个 CustomerStation 的左侧外缘，cable-side presentation anchor 朝左；
- dual feeder 的 `Sequence = 2`：对应右侧 station unit，incoming switch 固定在整个 CustomerStation 的右侧外缘，cable-side presentation anchor 朝右。

以上 side / direction 全部由 `StationKind + IncomingFeeder.Count + Sequence` 派生。WP-EM-07 不单独持久化 `Left`、`Right`、`Direction` 或 `Orientation`，也不增加 arbitrary orientation。

#### 3.3.4 Typed layout、visibility 与 anchor

`ShowIncomingSwitch` 是每路 IndoorStation feeder 的 Layout / Presentation fact，不是 Domain electrical fact。建议 runtime typed layout 与 V7 DTO 使用：

```text
CustomerStationLayout
├── CustomerStationId
├── Position
└── IncomingFeeders
    └── CustomerStationIncomingFeederLayout
        ├── IncomingFeederId
        └── ShowIncomingSwitch
```

station unit 尺寸、BoxStation 屋顶、三角形、one / two feeder 排布、switch 尺寸与 feeder A / B 左右位置均由 `StationKind + Sequence + DrawingMetrics` 派生，不保存自由 feeder coordinates。WP-EM-07 不增加 arbitrary `Orientation`、rotation、mirror 或 generic Device drag；CustomerStation drag / route stabilization 继续属于 WP-EM-08。

`IndoorStation` 创建时每路 `ShowIncomingSwitch` default 为 `true`，之后允许分别修改。`false` 只隐藏该 switch 的 professional geometry，不删除 feeder、Terminal、`ElectricalNode` 或 `SwitchDevice`，不改变 `SwitchState`、Connection 或 topology。

V7 对 `BoxStation` 采用统一、non-null、strict typed representation：其唯一 feeder 也必须有一个 `CustomerStationIncomingFeederLayout` record，并持久化 `ShowIncomingSwitch = true`。`BoxStation + persisted false` 为非法 layout，DTO / runtime validation 必须拒绝，不得静默归一化。该方案保持 one typed shape、完整 feeder layout coverage 和无 nullable ambiguity；UI 不向 BoxStation 用户暴露 visibility 编辑入口，其 effective `ShowIncomingSwitch` 恒为 `true`。

`TerminalAnchorIndex` 保持“同一个 electrical Terminal ID → 当前 layout 下的 transient presentation anchor”合同，因此可自然支持 hidden-switch anchor 切换：

- visible：`CableTerminalId` 映射到 incoming switch 外侧 / cable-side formal anchor；
- hidden：同一个 `CableTerminalId` 映射到对应 station unit body edge 的 canonical cable-entry point；
- visibility 切换不得替换或 remap `CableTerminalId`、Cable、`SwitchDevice.Id` 或 `GroundingTarget`；
- Cable routing、picking、GroundingTarget affordance 与 `GroundingPresentationAnchorResolver` 必须读取同一个更新后的 `TerminalAnchorIndex`，从而使 Cable 和 GroundingPoint presentation 一致移动，而 electrical target identity 不变。

#### 3.3.5 Grounding 与 Energization boundary

每路 feeder 的 `CableTerminalId` 是真实 cable-side Terminal，也是新建 `GroundingPoint` 的合法 `GroundingTarget.Terminal`。`ProfessionalCommandFactory.IsEligibleNewTerminalTarget`、`GroundingTargetPicker`、default location text 和 professional anchor resolver 必须按 CustomerStation aggregate identity 扩展，不得把 station body 或任意 glyph geometry 隐式当作接地目标。switch hidden 后仍保留同一个 Terminal target，只改变 presentation anchor。Grounding 与 `SwitchState` 是两个独立 electrical facts，任一方不得自动修改另一方。

WP-EM-07 不增加 `SupplyState`、`Energized`、`DeEnergized`、`HasPower`、`IsEnergized` 或等价 persisted field，也不实现 energized / de-energized 自动着色。“某一路有电、另一路没电”未来由统一 Electrical Model / topology 的 Energization Analysis 推导。本 WP 只建立 feeder identity、Terminal、`ElectricalNode`、switch state 与 topology relationship。

#### 3.3.6 Creation UX 与明确 exclusions

最小创建对话框输入为：

- `StationKind`：用户箱变 / 用户室内站；
- `IndoorStation` 才显示 feeder count：单电源 / 双电源；
- 每路输入 required `DisplayName`；
- `BoxStation` 固定一条 feeder，incoming switch 固定显示；
- `IndoorStation` 各 feeder 的 `ShowIncomingSwitch` 初始值为 `true`；
- 不允许创建零 feeder。

创建对话框不要求用户输入 CustomerStation 级名称 / 编号、Orientation、SupplyState、初始 `SwitchState` 或内部设备。每路 incoming `IsolationSwitch` 的正式创建初始值冻结为 `SwitchState.Open`。该规则遵循当前全部 production switch creation workflow：`RingCabinetTemplateDomainBuilder` 对 LoadSwitch、IntegratedFeeder 和 PT interval 的全部成员 switch 显式传入 `Open`；`SwitchDevice.CreateForPole` 的 Domain factory default 为 `Open`，`PoleSwitchAttachmentCreationFactory` 与 `PoleCreationFactory` 均沿用该默认值，包含 pole-mounted `IsolationSwitch` 与 `DropoutFuse`；相应 Desktop 创建 UI 只选择 template / `SwitchKind`，不选择初始 state。Demo / restore / clipboard 中保留既有状态不属于新建默认规则。

明确不进入 WP-EM-07：CustomerStation internal bus、bus coupler、transformer internal modeling、LV network、OverheadLine incoming、Energization Analysis、SupplyState persistence、automatic energized / de-energized color、generic Device drag、generic route stabilization、Transformer multi-presentation-port、Annotation / Work-ticket Presentation Layer 与 V8。Transformer deferred scope 继续属于 WP-EM-08，不得借本 WP 修改。

### 3.4 GroundingAccessPoint 与架空接地合同

验电接地环正式建模为 `GroundingAccessPoint`。它是具有 Stable ID 的永久、轻量 Electrical / Professional entity，不是 Device、`SwitchDevice`、`Terminal` 或 Annotation。架空侧新建工作地线统一采用：

```text
Overhead conductor
    ↓
GroundingAccessPoint
    ↓
GroundingPoint
```

该规则适用于普通支撑杆，以及存在柱上 `SwitchDevice`、隔离刀闸、跌落式熔断器、`CableTermination` 或其它柱上设备的杆塔；不得因现场存在设备 `Terminal`，就将该 Terminal 作为新建架空工作地线的主要目标。

`GroundingAccessPoint` 的稳定 Domain identity 为：

```text
GroundingAccessPoint
├── GroundingAccessPointId
├── ConnectionId
├── PoleId
├── AdjacentEndpoint
│   ├── Kind: Pole | Terminal
│   └── TargetId
├── LineSide: SmallerNumberSide | LargerNumberSide | TransformerSide
└── PlacementSide: PoleSide | AdjacentEndpointSide
```

`AdjacentEndpoint.Kind = Pole` 保留 WP-EM-04 的全部既有语义：`TargetId` 是 `PoleId` 在对应 `OverheadLine.SupportPoleIds` 有序列表中的直接 predecessor 或 successor。`AdjacentEndpoint.Kind = Terminal` 是 WP-EM-07A 新增的窄范围 endpoint half-edge identity：`TargetId` 必须是同一 `OverheadLine.Connection` 的实际 endpoint Terminal，并且当前只允许该 endpoint 为 pole-mounted Transformer 的正式 `Transformer.HvTerminalId`。不得由任意 External Terminal、`SwitchDevice` Terminal 或 `CableTermination.OverheadSideTerminalId` 推断 Terminal endpoint eligibility。

物理位置身份与专业侧别严格分离：typed `AdjacentEndpoint` 固化实际 conductor half-edge，`LineSide` 固化“小号侧 / 大号侧 / 变压器侧”业务标签，`PlacementSide` 固化 GAP 靠近该 half-edge 的 `PoleSide` 或 `AdjacentEndpointSide`。Pole endpoint 只允许小/大号侧与 `PoleSide`；合法 pole-mounted Transformer Terminal endpoint 必须为 `TransformerSide`，并允许两个 placement。唯一性为 `(ConnectionId, PoleId, AdjacentEndpoint.Kind, AdjacentEndpoint.TargetId, PlacementSide)`；同一 short OHL 的两端可各有一个独立 GAP，同一 placement 不得重复。`Left`、`Right`、`Up`、`Down` 只属于创建交互，不得持久化为业务事实。

创建 `AdjacentEndpoint.Kind = Pole` 的 GAP 时，可比较 `PoleId` 与 `AdjacentEndpoint.TargetId` 对应 Pole 的明确简单杆号主整数，为 `LineSide` 提供保守推荐；无法可靠解析时必须由用户明确选择，不得猜测。`AdjacentEndpoint.Kind = Terminal` 不存在相邻杆号可供比较，其 `LineSide` 自动固定为 `TransformerSide`，不得人工选择小号侧或大号侧。用户选择或覆盖 Pole endpoint 的 `LineSide` 不改变 typed `AdjacentEndpoint`。PoleNumber rename 不自动修改、移动、删除或重建已有 GAP。

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
├── AdjacentPoleId: Guid?                         // legacy V7 Pole form only
├── AdjacentEndpoint: ProjectGroundingAdjacentEndpointDto?
│   ├── Kind: ProjectGroundingAdjacentEndpointKind
│   │   ├── Pole
│   │   └── Terminal
│   └── TargetId
├── LineSide: SmallerNumberSide | LargerNumberSide | TransformerSide
└── PlacementSide: PoleSide | AdjacentEndpointSide  // optional additive V7 field
```

V7 使用 backward-compatible additive representation。既有 JSON property `adjacentPoleId` 保留为 required-but-nullable legacy slot；新增 optional typed `adjacentEndpoint` object 与 optional `placementSide`。读取时 `AdjacentPoleId` 与 `AdjacentEndpoint` 必须恰好一个有效：旧 V7 `(ConnectionId, PoleId, AdjacentPoleId)` 无歧义归一化为 `AdjacentEndpoint.Kind = Pole`、`AdjacentEndpoint.TargetId = AdjacentPoleId`，其治理层语义记法等价于 `AdjacentEndpoint.ForPole(AdjacentPoleId)`；`AdjacentPoleId` 的既有 Pole 语义不改变。`PlacementSide` property absent / null 是 pre-Fix-2 V7 compatibility shape 的唯一可判定 discriminator：旧 Pole record 归一化为 `PoleSide`；pre-Fix-2 typed Transformer Terminal GAP 仅在 `LineSide = SmallerNumberSide | LargerNumberSide` 且 placement 缺失时归一化为 `TransformerSide + PoleSide`。Terminal record 一旦明确带有 placement field 就属于新 shape，其 `LineSide` 必须为 `TransformerSide`；`Terminal + TransformerSide` 缺少 placement 也不是 legacy shape，必须拒绝。不声称能区分两个结构完全相同的 V7 文件。旧数据不得推断新的 Terminal endpoint；原 `GroundingAccessPointId` 以及关联 `GroundingPoint` 的 identity、`Location`、`Number`、`Note` 均保持不变。新 typed record 写入 `AdjacentEndpoint` 与明确 `PlacementSide`，legacy field 写入 null。两者同时有效、同时缺失、empty ID、invalid enum 或非法 line/placement 组合均必须由 strict validation 拒绝。

旧 V7 restore 必须保持原 `GroundingAccessPointId`，并保持关联 `GroundingPoint` 的 `Location`、`Number`、`Note` 和 typed target identity；不得从旧记录自动生成 Terminal-endpoint GAP，不得重新解释旧 `AdjacentPoleId`。新 V7 mapper 应统一输出 typed `AdjacentEndpoint`，从而使新 Terminal-endpoint GAP 能够 Save / Reopen；同一记录不得同时保存 legacy 与 typed 两个有效 endpoint source。

`ProjectGroundingPointDto` 以单一 `GroundingTarget` 取代 V6 的 `TerminalId`。V7 顶层 schema 必须为 `GroundingAccessPoint` 保留 typed collection；不得用自由字典、两个 nullable target ID 或 rendering direction 代替这些字段。

`Terminal` target 继续合法，但只承担：

- V1.0 / V6 legacy compatibility；
- 明确建模的真实电缆侧接地场景；
- 必要的特殊 Terminal grounding。

允许新建接地的 Terminal 类型必须由相应 Vertical Slice 按真实设备语义明确，不得把所有 Terminal 自动视为合法创建目标。当前确认的电缆侧目标包括 `CableTermination.CableSideTerminalId`，以及后续模型中的 `RingCabinet` cable-side terminal 和 `CustomerStation` incoming cable-side terminal。

WP-EM-07A 将在不改写 WP-EM-06 历史 Closure Evidence 的前提下，正式开放三种 Transformer 的现有 `HvTerminalId` 作为新建 `GroundingTarget.Terminal`，并补齐 pole-mounted switch / `IsolationSwitch` / `DropoutFuse` 两侧相邻真实 `OverheadLine` conductor 的 GAP candidate 能力。架空侧语义继续为 `Overhead conductor → GroundingAccessPoint → GroundingPoint`；不得新增 `TransformerGroundingTarget` 或 `SwitchGroundingTarget`，也不得将 SwitchDevice Terminal 作为未来架空工作地线的主要 target。

对于 `CableTermination`，必须区分两侧：

```text
Cable side grounding    → CableSideTerminal (`Terminal` target)
Overhead side grounding → adjacent `GroundingAccessPoint`
```

`CableTermination.OverheadSideTerminalId` 不作为未来架空工作地线的主要创建目标。

WP-EM-01 已实现的 `Pole + Switch` legacy Terminal-target presentation anchor 必须保留，用于旧文件、既有 Terminal-target `GroundingPoint` 和特殊 Terminal grounding。该 resolver 继续遵守 `Electrical anchor != Grounding presentation anchor`，但不再代表未来架空接地的主要创建工作流。若 persisted `GroundingPoint` 无法解析专业显示 anchor，必须产生显式诊断，不得静默不显示或在正式导出中无声遗漏。

### 3.6 Grounding professional presentation 与交互

`GroundingAccessPoint` 的默认专业位置由 `ConnectionId + PoleId + AdjacentEndpoint` 对应的 local conductor half-edge 派生，`LineSide` 不承担物理 half-edge 恢复。`AdjacentEndpoint.Kind = Pole` 继续使用有序 `OverheadLine.SupportPoleIds`：例如 `[P10, P11, P12]` 必须表现为线路经过 `P10 → P11 → P12`，从而在中间杆形成稳定 incoming / outgoing conductor geometry。`AdjacentEndpoint.Kind = Terminal` 则从同一 Connection 的正式 endpoint 与 final route 派生朝 endpoint 的 half-edge direction。该修正只属于 Presentation / Routing，不得拆分 Connection、新增 ElectricalNode 或 Terminal、改变 electrical connectivity，亦不得引入通用 waypoint editor。

WP-EM-04 basic GAP marker 为对应架空导线上的实心小圆点，直径约为 conductor stroke width 的 2.5 倍，并使用 Rendering typed metric。从 Pole 沿 typed `AdjacentEndpoint` 对应 half-edge 方向使用固定 visual clearance 放置；不保存 screen coordinates、offset 或 clearance，不支持拖动。存在柱上设备时仍以该真实 half-edge 为定位基础。高级 `GroundingAccessClearance`、leader 与可调布局继续留给 WP-EM-05。

WP-EM-04 已完成标准矢量 grounding symbol：一根竖向主 stem，下端三条以 stem 为中心、由上到下逐渐变短的水平横线。不得继续使用含义不明确的小方框或 bitmap。符号尺度与 Pole 保持合理专业比例，并由 Rendering metric 控制；用户调整 leader 时符号本身大小固定，不保存 symbol scale。WP-EM-05 只 refinement 其 layout、routing 与 interaction，不重新实现该 symbol。

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

`GroundingPoint.Location` 继续是工作票文字 / 位置说明。普通 Pole GAP 的默认显示值由 `LineSide` 派生：`SmallerNumberSide` → “小号侧”，`LargerNumberSide` → “大号侧”。Transformer Terminal-endpoint GAP 结合 stable placement 派生：`PoleSide` → `{PoleNumber}杆变压器侧`，`AdjacentEndpointSide` → `变压器高压侧导线`；直接 Transformer HV Terminal target 保持 `变压器高压侧`。只有用户明确编辑时才保存自定义文字；既有持久化 `Location` 不因 normalization 被改写。

从 WP-EM-04 开始，新创建 `GroundingPoint` 必须自动获得非空、trim 后在当前 `DrawingDocument` 内唯一的 `Number`。默认分配扫描现存合法标准 `Lxx` 编号并选择可用序号；不持久化 global sequence counter，历史删除形成的空号允许未来复用。Inspector 编辑必须通过 CommandStack，重复编号修改原子拒绝并支持 Undo / Redo。

正式 V6 / legacy Terminal-target 文件继续允许加载；若旧记录的 Number 为空，不为兼容而猜号、自动重编号或建立复杂 grandfather 双模型。required / unique 约束应用于 WP-EM-04 新创建及新修改状态；旧空 Number 可保持只读兼容并在用户明确编辑后进入新约束。

### 3.8 WP-EM-07A Transformer & Pole-Device Grounding Amendment

**状态：Closed / Archived / Final accepted implementation `c852d7a1f2477628664f8aeca8bfb23cf9ee3b06`**

#### 3.8.0 Windows Professional Acceptance Amendment

Windows 专业验收冻结以下 Fix-2 closure contract：`GroundingAccessLineSide` 增加 `TransformerSide`；`GroundingAccessPoint` 增加 stable typed `GroundingAccessPlacementSide { PoleSide, AdjacentEndpointSide }`。Pole endpoint 只能使用 `SmallerNumberSide | LargerNumberSide` 与 `PoleSide`；Terminal endpoint 只允许 `PublicPoleMounted | DedicatedPoleMounted` 的真实 `HvTerminalId` short-OHL endpoint，必须使用 `TransformerSide`，且两种 placement 均合法。`PublicIndoor` 仍为 Cable-only，不得形成 OHL Terminal GAP。

同一 `(ConnectionId, PoleId, AdjacentEndpoint)` 的 `PoleSide` 与 `AdjacentEndpointSide` 是两个不同的 conductor grounding identities，可以独立创建、挂接 `GroundingPoint`、删除、Undo / Redo、Clipboard 与 Save / Reopen。Pole workflow 创建 `TransformerSide + PoleSide`；选中 pole-mounted Transformer 的独立 workflow 反向解析唯一合法 short OHL，并创建 `TransformerSide + AdjacentEndpointSide`。两者不得与直接 `Transformer.HvTerminalId → GroundingPoint` 合并。

GAP marker 从所选物理端沿 transient oriented final route 按 conductor path 清距；清距可以跨越 90° bend，不得因第一段过短而出现 `GroundingAccessPointAnchorMissing` 或 `GroundingPresentationAnchorMissing`。两端 GAP 同时存在时，route constraint 必须同时为 pole/attachment envelope 与 Transformer body 留出空间。Transformer Terminal-target `GroundingPoint` 的默认 presentation 不变；effective manual `SymbolOffset` 位于 anchor 左/右时，leader 的第一条有效线段必须相应向左/右，不得 U-turn，且不增加 persisted direction。

新建默认 `Location` 明确区分：Terminal-endpoint `PoleSide` GAP 为 `{PoleNumber}杆变压器侧`，`AdjacentEndpointSide` GAP 为 `变压器高压侧导线`，直接 Transformer HV Terminal grounding 仍为 `变压器高压侧`。FormatVersion 保持 V7；只增加 additive optional placement DTO field，不保存坐标、segment index、leader direction 或第二套 anchor fact。WP-EM-07B、WP-EM-08 及 Transformer naming 继续排除。

WP-EM-07A 是 Post-EM-07 / Pre-EM-08 的独立 amendment Work Package，不重新打开 WP-EM-06 或 WP-EM-07。Complete Vertical Slice implementation、Code Review、Windows automated verification 与 Windows professional acceptance 均已通过；最终验收实现为 `c852d7a1f2477628664f8aeca8bfb23cf9ee3b06`，本 WP 现 Closed / Archived。

#### 3.8.1 Pole-device adjacent OHL grounding

Pole-mounted switch / `IsolationSwitch` / `DropoutFuse` 两侧相邻的真实 `OverheadLine` conductor 都必须具备合法的 `GroundingAccessPoint` 接地能力。正式语义继续为：

```text
Overhead conductor
→ GroundingAccessPoint
→ GroundingPoint
```

该能力必须复用现有 `GroundingAccessPoint` 与 `GroundingTarget.GroundingAccessPoint`。不得新增 `SwitchGroundingTarget`，也不得将 SwitchDevice Terminal 作为未来架空工作地线的主要 target。

WP-EM-07A 独立 Repo Audit 已确认 pole-mounted switch / `DropoutFuse` 朝 Transformer 一侧的 short `OverheadLine` 未进入 GAP candidate picker 的根因；以下合同据此冻结，不再将问题预判为单一 UI、Rendering 或 Connection defect。

Repo Audit 已确认该 short `OverheadLine` 的 `SupportPoleIds = [PoleId]`，因此旧 `(ConnectionId, PoleId, AdjacentPoleId)` invariant 无法表达朝 Transformer endpoint 的 conductor half-edge。WP-EM-07A 冻结使用 3.4 节定义的 typed `AdjacentEndpoint`：

- 普通杆间 half-edge：`Kind = Pole`，`TargetId = direct adjacent PoleId`；
- non-Pole endpoint half-edge：`Kind = Terminal`，`TargetId = same Connection 的实际 endpoint TerminalId`；
- 当前 Terminal endpoint eligibility 只允许正式 `Transformer.HvTerminalId`，不得泛化为任意 Terminal。

`Kind = Pole` validation 必须确认 `ConnectionId` 对应 `OverheadLine`、`PoleId` 与不同的 `TargetId` 均为真实 Pole、两者均属于 `SupportPoleIds`，且 `TargetId` 是 `PoleId` 的直接 predecessor 或 successor。既有行为不得改变。

`Kind = Terminal` validation 必须确认：`ConnectionId` 对应 `OverheadLine`；`PoleId` 是该 line 的真实 support pole；当前 07A 场景必须是 `SupportPoleIds.Count == 1` 且唯一值为 `PoleId`；`TargetId` 是真实 Terminal，并且是同一 `Connection` 的实际 endpoint；该 Connection 的另一 endpoint 必须通过现有 Pole / `PoleAttachment` physical-owner resolution 落在所选 `PoleId`，从而证明 terminal endpoint half-edge 在 topology 上与该 Pole 直接相邻；Terminal owner 是 `Transformer`，`TargetId == Transformer.HvTerminalId`，且 `TransformerKind` 属于 `PublicPoleMounted | DedicatedPoleMounted`。`PublicIndoor` 仍为 Cable-only，只属于 Transformer HV direct-grounding whitelist，不属于 OHL Terminal GAP eligibility。不得使用 generic External Terminal inference；route geometry 只用于 presentation direction，不得替代上述 Domain identity validation。

typed uniqueness invariant 为 `(ConnectionId, PoleId, AdjacentEndpoint.Kind, AdjacentEndpoint.TargetId, PlacementSide)`。禁止将 `Transformer.Id`、`HvTerminalId` 或当前 `PoleId` 塞入 `AdjacentPoleId`，也禁止 untyped Guid dual meaning、route segment index、screen/glyph coordinate、transient presentation anchor、fake Pole、fake `PoleAttachment` 或 fake Terminal。

#### 3.8.2 Transformer HV grounding

`PublicPoleMounted`、`DedicatedPoleMounted`、`PublicIndoor` 三种 Transformer 的现有唯一 `HvTerminalId` 全部必须成为合法的新建 `GroundingTarget.Terminal` 目标。Grounding identity 必须绑定 `Transformer.HvTerminalId`，不得绑定 persisted presentation coordinate；不得新增 second grounding identity、`TransformerGroundingTarget`、`SwitchGroundingTarget`、fake Terminal 或 fake `ElectricalNode`。

Transformer Terminal-target `GroundingPoint` 的默认 `Location` 冻结为“变压器高压侧”。

这是对 3.4 节 conductor-side GAP 通用规则的明确、窄范围 special Terminal grounding exception，只表达 Transformer 本体正式 HV terminal 处的接地，不将任何相邻 `OverheadLine` conductor 或其它设备 Terminal 泛化为 Terminal grounding target。对 pole-mounted Transformer 必须按用户实际选择的位置区分：在相邻 `OverheadLine` conductor 上接地时，必须使用 `GroundingAccessPoint`；在 Transformer 本体正式 HV terminal 处接地时，才可以使用 `Transformer.HvTerminalId` 作为 `GroundingTarget.Terminal`。

在 `DropoutFuse → short OverheadLine → Transformer` 场景中，short `OverheadLine` 两端合法的 conductor-side GAP eligibility 仍必须由 WP-EM-07A Repo Audit、实现与 closure 独立确认；Transformer terminal grounding 不得替代该 eligibility。`Transformer.HvTerminalId` 只表达 Transformer HV terminal grounding；`SwitchDevice`、`IsolationSwitch` 或 `DropoutFuse` Terminal 不因本 Amendment 自动成为新的 overhead Terminal `GroundingTarget`。

Transformer grounding presentation 必须从正式 HV presentation anchor 派生。允许使用小距离 `DrawingMetrics` / presentation offset，但该 offset 不属于 Domain、不新增 persisted electrical fact，也不形成第二 anchor identity。以下规则是必须通过的 professional presentation acceptance requirement：

- `HvTerminalId` 存在唯一 connected route 且 incoming conductor horizontal：grounding leader 默认纵向向下；
- `HvTerminalId` 存在唯一 connected route 且 incoming conductor vertical：grounding leader 默认横向引出，并优先远离 Transformer body；
- Transformer 无连接：使用 formal `HvDirection` 并结合 Transformer body bounds 派生 outward default leader。

leader offset、body avoidance 和 presentation anchor adjustment 全部为 transient / `DrawingMetrics` derived，不得持久化。`GroundingPointLayout.SymbolOffset` 继续作用于上述派生后的默认 presentation。

如果 WP-EM-08 或未来 Work Package 调整 Transformer presentation port / anchor，`GroundingPoint` 必须继续通过同一个 `HvTerminalId` 解析新的 transient professional anchor。presentation port 改变不得替换 `HvTerminalId`、替换 `GroundingPoint` target 或重建 `GroundingPoint` identity。Transformer multi-presentation-port 的具体设计继续属于 WP-EM-08 / future evaluation；本 amendment 只冻结 `Grounding identity = stable HvTerminalId`。

#### 3.8.3 最小 Scope 与 exclusions

WP-EM-07A 的最小能力范围仅包括：pole-mounted switch / `IsolationSwitch` / `DropoutFuse` 两侧相邻真实 OHL 的 GAP eligibility、三种 Transformer 的 `HvTerminalId` grounding eligibility、稳定 grounding identity，以及从正式 HV anchor 派生并能随 future presentation anchor 变化自然跟随的 professional grounding presentation。

明确不进入 WP-EM-07A：

- `LvTerminalId`、0.4kV topology、LV grounding、LV network；
- cross-voltage Transformer model、second Transformer Terminal、Transformer internal winding topology；
- second grounding identity、`SwitchGroundingTarget`、`TransformerGroundingTarget`、fake Terminal、fake `ElectricalNode`；
- Transformer drag、generic Device drag、generic routing stabilization、route-family hysteresis、last-valid-position、continuous-drag feedback、generic routing engine；
- Transformer naming / station number、capacity、model / typeplate information；
- WorkScope boundary expansion、Energization、Annotation；
- Transformer multi-presentation-port 的具体设计、triangle-edge dynamic attachment；
- WP-EM-07B 与 WP-EM-08 implementation。

#### 3.8.4 Lifecycle 与 compatibility freeze

- occupied Transformer `HvTerminalId` grounding 必须阻止 Transformer 删除；
- occupied GAP 必须继续阻止 dependent `OverheadLine` 删除；free GAP 沿用现有 line-delete policy；
- 删除 `GroundingPoint` 不得删除 Transformer，也不得隐式删除 GAP；
- Undo / Redo、Save / Reopen 必须保持 stable typed target identity；
- Clipboard 沿用现有 Professional dependency policy，不在 WP-EM-07A 重写总策略；
- Overhead conductor GAP 与 Transformer body/HV Terminal grounding 是两个可同时存在、不得互相替代的 identity；
- Transformer 不因本 WP 成为新建 `WorkScope` boundary。

### 3.9 WP-EM-07B Transformer Naming Amendment

**状态：Closed / Archived**

WP-EM-07B 是 Post-EM-07 / Pre-EM-08 的独立 naming amendment Work Package，不重新打开 WP-EM-06、WP-EM-07 或已归档的 WP-EM-07A。该 WP 的历史名称可以继续使用 Transformer Station Number Amendment，但正式业务字段统一称为 Transformer `DisplayName` / “变压器名称”。不得将 UI 字段标题写成“站号”“变压器编号”或“设备编号”，也不得建立 `StationNumber`、`StationNo`、`TransformerNumber`、`DeviceNumber`、`AssetNumber` 等第二套重复业务事实。

#### 3.9.1 Naming semantic

`Device.DisplayName` 是 `Transformer` 唯一正式 naming fact，并适用于 `PublicPoleMounted`、`DedicatedPoleMounted`、`PublicIndoor`。它是正式 Domain business fact，并与 `Transformer.Id`、`TransformerKind`、`HvTerminalId`、HV Terminal identity、`Pole.PoleNumber`、CustomerStation naming 和 `SwitchDevice.DispatchNumber` 分离。

正常 current-data invariant 冻结如下：

- 新建前必须取得 `DisplayName`；
- `DisplayName` 必须 trim，trim 后必须为非空字符串；
- whitespace-only input 必须拒绝；
- 不提供自动默认业务名称，也不允许新建后保持 unnamed state；
- 同一 `DrawingDocument` 内不要求唯一；
- 三种 `TransformerKind` 使用相同 naming invariant。

`DisplayName` 不是 station number、Transformer number、dispatch number 或 identity；它不影响 topology、connection eligibility 或 conductivity。不得新增 `StationNumber`、`TransformerNumber`、`DeviceNumber` 等第二 naming fact。

三种 Kind 全部允许 rename；rename 同样执行 trim、non-empty 和 whitespace-only rejection，且不要求 uniqueness。Rename 必须进入统一 `CommandStack` 并支持 Undo / Redo，不得修改 `Transformer.Id`、`TransformerKind`、`HvTerminalId`、HV Terminal identity、`Connection`、`GroundingAccessPoint`、`GroundingPoint`、`WorkScope`、electrical topology 或 conductivity。`DisplayName` 不参与 connection eligibility、`GroundingTarget` identity 或 `WorkScope` eligibility。

#### 3.9.2 Creation、Inspector 与 Rendering

Transformer creation dialog 必须在 aggregate 正式创建前取得 `TransformerKind + DisplayName` 并验证名称；无效名称不得进入正常 creation completion，不得先创建 unnamed Transformer 再要求后补。

Transformer Professional Inspector 对三种 Kind 增加标题为“变压器名称”的 editable Domain row。`PublicIndoor` 既有 Orientation editor 保留，并与 naming editor 独立。修改名称必须进入 `CommandStack`，支持 Undo / Redo、scene refresh、Inspector refresh 和 dirty state。

三种 Kind 的正常 named Transformer 在 Canvas 均始终显示真实 `DisplayName`。Label 是 derived rendering，由 Transformer glyph bounds 和统一 `NameLabelGap` 派生；所有 `PublicPoleMounted`、`DedicatedPoleMounted`、`PublicIndoor Horizontal` 与 `PublicIndoor Vertical` 均统一显示为 glyph 下方居中。它不进入 `TransformerLayout`，不保存 arbitrary label coordinates、user free-text position 或 visibility toggle。Canvas 与 PNG export 必须共用同一 Scene truth。选择 glyph 或文字必须继续解析到原 `Transformer` 的 `SelectionTargetKind.Device`；文字不形成第二份业务 identity。

Legacy incomplete Transformer 是上述 label 要求的唯一 compatibility exception：Canvas 不显示名称 label，也不得显示或生成伪业务名称；Inspector 的“变压器名称” editable row 保持空值，并利用现有 UX 能力明确表现“历史工程待补录”或等效 incomplete 状态。具体视觉形式由 implementation 决定，但不得改变业务事实。

#### 3.9.3 V7 persistence 与 legacy completion

`FormatVersion` 保持 V7。为解决同一 V7 内的 temporal / schema ambiguity，`document.json` 根 `ProjectFilePayload` 层 additive 增加 nullable integer discriminator `TransformerNamingContractVersion`；按现有 camelCase JSON policy，其 persisted property name 为 `transformerNamingContractVersion`。该字段不是 Domain fact、Transformer business fact 或新的 `FormatVersion`，只用于判定 Transformer naming persistence contract。

Discriminator 语义冻结如下：

- `TransformerNamingContractVersion` absent：表示 pre-WP-EM-07B V7，reader 对整个 document 进入 legacy Transformer naming compatibility mode；
- `TransformerNamingContractVersion = 1`：表示 WP-EM-07B current V7 naming contract enabled，reader 对整个 document 进入 current Transformer naming mode；
- Property 存在但值为 `null`、unsupported integer 或 unsupported JSON type：必须 reject restore / open，不得猜测、fallback 或静默按 legacy 处理。Reader 必须基于现有 raw `JsonObject` / presence-aware boundary 区分 property absent 与 explicit `null`。

`ProjectTransformerDto` additive 增加 optional `DisplayName` JSON representation，以读取 legacy V7。Current-vs-legacy 判定只能来自 `TransformerNamingContractVersion`，不得依赖 nullable `DisplayName`、missing-vs-null inference、Transformer fields、GUID、Kind、position 或关联对象。

Legacy compatibility mode 的 restore truth table 为：

| `TransformerNamingContractVersion` | Transformer `DisplayName` JSON shape | Restore 结果 |
| --- | --- | --- |
| absent | property missing | controlled legacy incomplete naming state |
| absent | `null` | controlled legacy incomplete naming state |
| absent | `""` | controlled legacy incomplete naming state |
| absent | whitespace-only string | controlled legacy incomplete naming state |
| absent | trimmed non-empty string，例如 `"T1"` | 保留真实名称并恢复为 named Transformer |

Legacy incomplete Transformer 允许 open、view、selection、rendering、Inspector 查看和补录名称，但不得根据 `TransformerKind`、`Transformer.Id` / GUID、`HvTerminalId`、`PoleNumber`、CustomerStation、`Connection`、邻接设备或 position 猜测或生成名称，也不得生成“未命名变压器”“Transformer 1”、GUID-derived name、Pole-derived name 等伪业务事实。Compatibility restore 不得把 nullable Transformer name 扩大为新的正常 Domain contract。

Current mode 的 strict restore truth table 为：

| `TransformerNamingContractVersion` | Transformer `DisplayName` JSON shape | Restore 结果 |
| --- | --- | --- |
| `1` | property missing | malformed current V7；reject restore / open |
| `1` | `null` | malformed current V7；reject restore / open |
| `1` | `""` | malformed current V7；reject restore / open |
| `1` | whitespace-only string | malformed current V7；reject restore / open |
| `1` | trimmed non-empty string，例如 `"T1"` | 恢复为 valid current named Transformer |

WP-EM-07B 实施后的正常 writer 必须始终写出 `TransformerNamingContractVersion = 1`，包括没有 Transformer 的工程，并为所有 Transformer 写出 trimmed non-empty `DisplayName`。Current writer 禁止输出 missing、null、empty 或 whitespace `DisplayName`，也禁止在存在任意 legacy incomplete Transformer 时写文件。

Legacy document 中即使 marker absent，也必须保留每个已有的 trimmed non-empty 真实 `DisplayName`；其它 unnamed Transformer 仍为 legacy incomplete。只要存在任意 legacy incomplete Transformer，Save / Save As 必须被阻止并返回明确、非崩溃的 validation message。全部补录完成后，Save / Save As 才允许执行；保存后的 document 保持 `FormatVersion = V7`、写出 `TransformerNamingContractVersion = 1`，从此按 current strict contract 读取。这是 legacy → current 的唯一收敛路径，不新增传统 V7 → V8 migration。

#### 3.9.4 Clipboard、Undo / Redo 与 lifecycle

Clipboard snapshot 必须包含 `DisplayName`。Paste 保留原 `DisplayName`，remap `Transformer.Id` 和 `HvTerminalId`，并保持既有 external `Connection` policy。由于名称不唯一，paste 不自动 rename、不追加“(1)”、不拒绝 duplicate name，也不提示冲突；Clipboard 不得产生 unnamed current Transformer。

如果 copy closure 中包含任何 legacy incomplete Transformer，必须拒绝整个 copy 操作并提供明确、非崩溃提示；不得静默跳过该 Transformer，不得产生 partial topology copy，也不得允许 paste 创建 unnamed current Transformer。Legacy document 中已有合法真实名称的 Transformer 可按正常 named data copy。

Create、Rename、Delete、Paste 的 Undo / Redo 必须正确保留或恢复 `DisplayName`，且不得重新生成 `Transformer.Id` 或 `HvTerminalId`。既有 delete dependency policy 保持不变，naming 不扩大任何 cascade。

Legacy incomplete Transformer 的补录 rename 必须进入正常 `CommandStack`。Execute 后它成为 valid named current state；Undo 允许恢复原 legacy incomplete state，并保持 `Transformer.Id`、`HvTerminalId` 和 topology 不变，恢复后 Save / Save As 再次被阻止且 Canvas 再次不显示 name label；Redo 恢复同一个合法名称。不得为保持 Save eligibility 而破坏标准 Undo semantics。

#### 3.9.5 Electrical-model boundary

WP-EM-07B 是纯 naming vertical slice，不创建 `ElectricalNode`，不新增 Terminal，不改变 `HvTerminalId` 语义、`Connection`、Transformer connection eligibility、`GroundingTarget`、`GroundingAccessPoint`、`WorkScope`、conductivity 或任何 `TransformerKind` electrical semantics。

#### 3.9.6 Acceptance

WP-EM-07B implementation 至少必须通过以下验收：

- Domain：三 Kind 新建名称必填、trim、whitespace rejection、rename，并证明 identity / topology 不变；
- Creation：三 Kind 均通过合法名称创建，无效名称阻止创建；
- Inspector：三 Kind 均提供 editable “变压器名称”，Undo / Redo 与 immediate refresh 正确；
- Persistence：保持 V7；marker absent 的 legacy truth table 与 marker `1` 的 current strict truth table 全部通过；explicit `null` 或未知 integer marker reject；current round-trip 写出 `TransformerNamingContractVersion = 1` 与 non-empty `DisplayName`；legacy V7 无名称可打开且不伪造名称，legacy incomplete 阻止 Save / Save As，补录后 Save / Reopen 成功；
- Clipboard：copy / paste 保留 `DisplayName`、remap IDs、允许 duplicate names；包含 legacy incomplete Transformer 的 copy closure 整体拒绝；
- Rendering：三 Kind label 可见，覆盖 `PublicIndoor` Horizontal / Vertical，Canvas / PNG 一致，selection identity 不变；
- Electrical regression：`Connection`、`HvTerminalId`、grounding、`WorkScope` 和 topology 不变；
- Windows：automated tests PASS，professional visual acceptance PASS。

WP-EM-07B closure evidence：Windows automated verification、professional acceptance 与 Acceptance Fix-1 均通过。最终实现覆盖 creation naming、Inspector rename、`RenameTransformerCommand`、legacy completion Undo / Redo、Canvas / PNG shared `SceneText`、统一下方居中 label placement、label hit-test、Clipboard、Save / Save As eligibility、short OHL、grounding、GAP 以及 `HvAnchor` / `HvDirection` regression。

最终提交链：Requirements Freeze `c1271378abba9a63bbec0e3e11243a57997e3515`；Compatibility Amendment `b76be91ddceb7c1485d9405d1f4dec8839c15e42`；Slice A `92e7f95ab540b12d434de60c7fb707077ab799ce`；Final Implementation `6aed2735df65520f2bab223fd167b244ce87d984`；Professional Acceptance Fix-1 `6861db3e8275f31636c744f57afb81f20d53e2e9`。

#### 3.9.7 Explicit exclusions

明确不进入 WP-EM-07B：

- second station-number field 或 uniqueness requirement；
- Transformer capacity、model、manufacturer、voltage label、LV-side electrical model；
- asset metadata framework、generic property bag；
- CustomerStation naming redesign、`PoleNumber` redesign；
- Transformer grounding redesign、Transformer electrical-model refactor；
- Annotation text object、Energization；
- arbitrary label drag、generic typography framework rewrite；
- WP-EM-08 interaction stabilization。

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

本次 Post-EM-07 Sequencing Amendment 保持 `FormatVersion = V7`，不授权 V8。WP-EM-07A 复用现有 `GroundingTarget.Terminal`、`GroundingAccessPoint` 与 `Transformer.HvTerminalId`，并为 GAP adjacent endpoint 使用 3.5 节冻结的 backward-compatible additive V7 representation。当前 serializer 允许 additive fields；旧 V7 `AdjacentPoleId` 可在 mapper / restore 中无歧义归一化为 typed Pole endpoint；新 Terminal-endpoint GAP 可保存并 reopen。该兼容路径不增加 V7 migration step，不重新解释旧 `AdjacentPoleId`，不自动从旧数据生成 Terminal endpoint GAP，也不升级 V8。

WP-EM-07B 保持 V7，并按 3.9.3 节采用 additive `DisplayName`、document-level `TransformerNamingContractVersion` discriminator 与受控 legacy incomplete naming state。Marker absent 表示 pre-WP-EM-07B legacy compatibility mode，marker `1` 表示 current strict naming mode；property 存在但为 `null`、unsupported integer 或 unsupported JSON type 必须 reject。旧 V7 的 missing / null / empty / whitespace 名称恢复为 controlled incomplete，真实 non-empty 名称保留；在全部补录前不得 Save / Save As。New writer 只写出满足 current naming invariant 的 Transformer，并始终写出 marker `1`，包括没有 Transformer 的工程。该 additive capability marker 解决同一 V7 内的 compatibility ambiguity，不是新 `FormatVersion`，不增加传统 V7 → V8 migration。

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

CustomerStation 内部 feeder 排布及 incoming side / direction 由 `StationKind + IncomingFeeder.Count + IncomingFeeder.Sequence + DrawingMetrics` 派生，不保存每个 feeder 的自由坐标，也不持久化 `Left`、`Right`、`Direction` 或 `Orientation`。`CustomerStationLayout` 除 station `Position` 外，必须按 `IncomingFeederId` 为每路 feeder 保存一个 typed layout record；IndoorStation 的 `ShowIncomingSwitch` 可独立编辑，BoxStation 唯一 record 的 `ShowIncomingSwitch` 必须为 `true`，persisted `false` 必须拒绝。该值只决定 professional geometry 与 transient Terminal anchor。WP-EM-02 仅预留 `CustomerStationId + Position`，WP-EM-07 在同一 V7 typed schema 内补齐 feeder layout collection，不升级 V8。

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

**状态：Closed**

目标：

- 分离 electrical anchor 与 grounding presentation anchor；
- 修复 `Pole + attached Switch` 外侧接地表达；
- Professional anchor 缺失时产生显式诊断。

该 WP 不需要 V7，不改变 persisted topology，是本阶段第一个实施 WP。

### WP-EM-02 — V7 Format & Migration Foundation

**状态：Closed**

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
- FormatVersion remains V7; no new migration was added; WP-EM-04 is now Closed as recorded below.

### WP-EM-04 — GroundingAccessPoint & GroundingTarget Vertical Slice — Closed

**Implementation:** Complete
**Review:** Passed
**Automated Validation:** Passed
**Windows Runtime Validation:** Passed
**GUI Acceptance:** Passed
**Closure baseline:** `b15166c96bcf03a38acd2a27d98b597d04b60d4d` — `fix(grounding): use local cable termination location`

Final closure contract:

- `GroundingAccessPoint` is an independent Professional entity with stable `(ConnectionId, PoleId, AdjacentPoleId)` physical half-edge identity, `LineSide` as display semantics, no `ElectricalNode`, no topology split, and protected occupied-GAP lifecycle.
- `GroundingTarget` is one typed `Terminal` or `GroundingAccessPoint` target. New GroundingPoint creation follows the frozen whitelist; legacy terminal-target load/render compatibility remains.
- GroundingPoint numbering remains independent first-free `Lxx` for overhead GAP targets and `Sxx` for cable-side targets, with hole reuse, `99 → 100`, global string uniqueness, custom edit support, and legacy/custom persistence.
- GroundingPoint `LocationDescription` is a human-readable derived field. Creation resolves it from the typed target without manual input; RingCabinet uses the actual `{DisplayName}{Interval.DisplayName}间隔` fields, while local CableTermination uses `{PoleNumber}杆电缆终端` with the documented readable fallback. Inspector edits do not change target identity.
- Routing authority remains `ConnectionRouteRequest → RequiredRouteWaypoint → OrthogonalRoutePlanner → OrthogonalRouter → final OrthogonalRoute`. Mounted-switch endpoint substitution is ownership-based (`PoleAttachment.PoleId` plus `SwitchDevice.OwnsTerminal`) for first/last support endpoints, not collinearity-based; intermediate support facts remain intact and no-backtracking constraints remain scoped to the formal mounted endpoint path.
- GAP clearance is resolved from the final route and the complete pole-plus-mounted-device composite envelope. Canvas and PNG use the same solid marker and centralized metrics; rotated mounted-switch cases require the earliest legal point on the correct half-edge with clearance at least the minimum.
- GroundingPoint rendering uses the formal three-bar symbol with centered Number; manual symbol dragging and layout persistence are outside this WP.
- Clipboard and persistence contracts remain intact: GroundingPoints are not copied, occupied GAP dependencies are protected, GAP identity remapping preserves `LineSide`, FormatVersion remains V7, and no migration was added.
- Final Windows runtime validation reported all discovered tests passed with failed = 0 and skipped = 0. Domain.Tests 113/113, Infrastructure.Tests 80/80, and the full solution build passed.
- GUI acceptance passed for mounted-switch pole movement without loop/U-turn/backtracking, GAP and mounted-switch coexistence, vertical GAP half-edge presentation, RingCabinet and local CableTermination GroundingPoint locations, and Lxx/Sxx numbering. No separate GUI Save/Reopen result is asserted here beyond the automated persistence coverage.

Deferred / Post-EM-04 UX findings are formally routed as follows and are not WP-EM-04 closure blockers:

- Grounding-specific GAP / GroundingPoint presentation continuity during legal route rebuilds belongs to WP-EM-05.
- Generic pole/device drag route continuity, legal route-family hysteresis, illegal-geometry last-valid-position, and continuous-drag non-modal feedback belong to WP-EM-08.

An existing presentation behavior in which some polyline routes do not pass through the visual pole center was reverified as predating GAP and is out of scope for WP-EM-04.

### WP-EM-05 — Grounding Layout & Interaction Closure

**状态：Closed（Windows accepted with Known Limitation）**

完成 Grounding 自身的专业显示、可调布局和 interaction closure，使 WP-EM-04 已完成的 Grounding electrical model 达到日常工作票绘制可用状态。范围包括：

- `GroundingPointLayout` runtime 与 V7 integration；
- grounding symbol drag、`SymbolOffset` Undo / Redo 及 Save / Reopen；
- grounding leader length / position adjustment、grounding-specific clearance refinement 与 GAP-target advanced presentation；
- Cable-side Terminal grounding route；
- target affordance / hit tolerance / highlight 与 Location interaction closure；
- grounding leader crossing bridge / `LineJumpDecorator`；
- Canvas / PNG consistency 与 Windows professional visual acceptance；
- grounding-specific GAP / GroundingPoint presentation continuity。

Grounding-specific continuity 在合法 route 重算后保持 `GroundingTarget` identity、GAP identity、`AdjacentPoleId` 与 `GroundingPointLayout.SymbolOffset`；GAP marker 不跳到另一 half-edge，GroundingPoint anchor / leader 不错误反向、丢失或重新绑定。WP-EM-05 已通过实现、代码 Review、自动验证和 Windows GUI acceptance。Windows acceptance 为 **Passed with Known Limitation**：RingCabinet cable-side GroundingPoint 在低频手工拖动到 cable terminal 上方时，grounding leader 仍可能与 interval internal vertical lead 发生视觉干涉。该问题仅属于 presentation，不改变 electrical facts、`GroundingTarget`、`RingCabinetLayout`、interval 或 Terminal geometry，不阻塞本次 closure，Disposition 为 Deferred。未来可独立评估将该场景限制在 terminal 上方区域之外，但当前不实现、不冻结为正式需求，也不重新打开 WP-EM-05。为实现 grounding-specific 结果，WP-EM-05 未建立通用 route hysteresis infrastructure。

WP-EM-05 implementation baselines：`be7f2bd29875a112f01b8c93b7c85df6927b8871` (`feat(grounding): close layout and interaction workflow`)、`17a1dba489bcf37a21693d6b55df653153979002` (`test(grounding): adapt symbol hit regression`)、`3d35ed30909cfb8e57ccabca093431e178cc7627` (`fix(grounding): refine manual routing behavior`)、`ebd062ae876e85d7581ba8ebe3b894fd66b1b835` (`test(grounding): align routing regressions`) 和 `4f6b314a883d29b9c9afa1a385b5ec4c406809dc` (`fix(grounding): avoid ring cabinet lead overlap`)。

Standard three-bar grounding symbol、Lxx / Sxx numbering、basic GAP marker 以及 `GroundingTarget` / GAP model 均为 WP-EM-04 已完成事实，不得在 WP-EM-05 作为新业务能力重复实现。

### WP-EM-06 — Transformer Vertical Slice

**状态：Closed / Completed**

正式需求合同以 3.2 节为准。WP-EM-06 已完成 implementation、review、Windows automated validation 和 Windows professional GUI acceptance。实施范围包括 three Transformer kinds、各自独立的 professional glyph、单一 10kV HV terminal、create/delete、Cable / OverheadLine endpoint validation、`PublicIndoor` orientation、typed layout、selection、inspector、clipboard、Undo / Redo、V7 integration、topology graph compatibility，以及完整 `OverheadLine → DropoutFuse → short OverheadLine → Transformer` 场景。不得借该 Vertical Slice 引入 3.2 节排除的 CustomerStation、低压侧、额外 electrical facts 或通用 interaction infrastructure。

#### WP-EM-06 Closure Evidence

- 三种正式 `TransformerKind` 均已完成独立 professional glyph：`PublicPoleMounted`（柱上公变）、`DedicatedPoleMounted`（柱上专变）和 `PublicIndoor`（站内公变）。
- `CustomerStation.BoxStation` 不是 `TransformerKind`；用户箱变属于 WP-EM-07，未进入 WP-EM-06 `Transformer` aggregate。
- `Transformer` remains a top-level `Device`，仅有一个 `10kV HvTerminalId`；不创建 Transformer `ElectricalNode`、LV Terminal、second Terminal 或 internal winding topology。Stable identity 已覆盖 Commands、Undo / Redo、Clipboard 和 V7 persistence。
- `PublicPoleMounted` 与 `DedicatedPoleMounted` 只允许 `OverheadLine`，`PublicIndoor` 只允许 `Cable`。`DropoutFuse` 继续是独立 `SwitchDevice`；正式链路为 `OverheadLine → DropoutFuse → short OverheadLine → Transformer`。Windows professional acceptance 确认 short `OverheadLine` 从 `DropoutFuse` terminal 正确引出，不再先回到 support pole center；真实 `SupportPoleIds` 语义保持，未创建 fake `Pole` 或 `PoleAttachment`。
- 最终 professional presentation：`PublicPoleMounted` 为大空心圆、圆内完整 T 形结构与两个放大的相切小圆，canonical HV anchor 为主圆底部点；`DedicatedPoleMounted` 为三角形与两个分离的相切小圆，canonical HV anchor 为 triangle apex；`PublicIndoor` 为相交双圆，支持 Horizontal / Vertical，Horizontal 为默认，HV anchor 分别为左侧中心 / 顶部中心。Canvas 不增加固定业务 label。
- `PublicIndoor` orientation 仅属于 Layout fact；创建 dialog 不要求选择 orientation，创建后由 Inspector 通过 `SetTransformerOrientationCommand` 编辑 Horizontal / Vertical，并支持 Execute、Undo、Redo、scene rebuild、formal anchor update、connected Cable route update 和 V7 persistence。Pole-mounted orientation 不提供用户编辑。
- creation、selection、Inspector、unified delete、dependency protection、clipboard、Undo / Redo、Save / Reopen 均已集成。左侧 `ToolPalette` 的 Transformer entry 使用共享双圆 `Icon.Transformer`，Windows 实机确认正常。
- Transformer formal HV anchor 不得作为新建 `GroundingPoint` target，也不得作为新建 `WorkScope` boundary picker target。`WorkScopeBoundaryTerminalEligibility` 已闭合 Slice C P1，同时不影响正常 Cable / OverheadLine anchor usage。
- `FormatVersion` remains V7；typed Transformer persistence、typed TransformerLayout persistence、Save / Reopen、Clipboard remap 和 generic topology graph integration 已完成，不升级 V8。
- Windows automated validation = PASS；Windows professional GUI acceptance = PASS。验收覆盖 three Transformer glyphs、`PublicIndoor` Horizontal / Vertical、Inspector orientation editing、`DropoutFuse → short OHL → Transformer`、Transformer toolbox icon、connection behavior 和 save / reopen behavior。已知稳定基础测试计数为 Domain 133/133、Infrastructure 100/100、Application 115/115。
- 主要 WP-EM-06 baselines：Requirements Freeze `55b5aa4437c1d02519855212b040d023cfa3dd51`；Slice A `435c807dcf3b01bafe75958c2276612d2de91b95`；Slice B `b6623e8a77637eedbf07eb7f0e3cb3d83bcb6a15`；Slice C `eeedc5a5df49b586109d0a2f3fc8036c364f290a`；Windows acceptance Fix-1 `68a53ee152858c1bb6149a52e202979acff95f6f`；Toolbox icon integration `6ec10fc2f2ef939222120cfba25a3084a5c47860`；Toolbox icon Windows rendering fix `ff6cae6e78ea75401e202a50627fa3fead5ddd2d`。
- Deferred to WP-EM-08：Transformer drag 与 generic Device drag、route following、last-valid-position 统一治理；Transformer multi-presentation-port 仍不改变单一 `HvTerminalId`，当前 canonical anchor 为 `PublicPoleMounted` 主圆底部点和 `DedicatedPoleMounted` triangle apex。未来可评估一个 electrical `HvTerminalId` 对应多个 presentation connection candidates（`PublicPoleMounted` 按来线方向选择圆周 port，`DedicatedPoleMounted` 至少 triangle three vertices）；triangle edge dynamic attachment 仅 future evaluation，尚未冻结。上述 deferred items 不是当前 defect 或 blocker，且不得产生 second Terminal。

### WP-EM-07 — CustomerStation Vertical Slice

**状态：Closed**

正式需求合同以 3.3 节为准。WP-EM-07 已完成 implementation、code review、Windows automated verification 和 Windows professional visual acceptance。

#### WP-EM-07 Closure Evidence

- `CustomerStation` 已实现为正式 Electrical / Professional aggregate，支持 `StationKind.BoxStation` 与 `StationKind.IndoorStation`。
- `BoxStation` 固定 exactly 1 个 `IncomingFeeder`；`IndoorStation` 支持 1 或 2 个 `IncomingFeeder`。每路 feeder 均拥有 stable `IncomingFeederId`、`Sequence`、`DisplayName`、`CableTerminalId`、`StationTerminalId`、`ElectricalNodeId` 和 feeder-owned `IsolationSwitch`。
- feeder topology 已闭合为 `Cable → CableTerminalId → IsolationSwitch → StationTerminalId → ElectricalNodeId`。仅支持 Cable incoming；不支持 OverheadLine incoming；双 feeder 之间无 bus、coupler 或 electrical edge。
- `CableTerminalId` 是合法的 terminal-based `GroundingTarget`。shown incoming switch 使用 CustomerStation-specific direct vertical downward grounding leader；hidden switch 回退到 body-edge presentation anchor。`GroundingTarget`、`GroundingPointId` 与 `CableTerminalId` identity 保持不变。
- WP-EM-07 未增加 `SupplyState`、`Energized`、`DeEnergized`、`HasPower`、`IsEnergized` 或其它人工 Energization fact；Energization Analysis 继续 deferred，不在本 WP 维护第二套“有电 / 没电”事实。
- 未实现 CustomerStation internal bus、bus coupler、station internal Transformer、LV topology 或 feeder-to-feeder electrical connection；这些边界保持明确排除。
- `BoxStation` professional glyph 为 body + triangle + roof；最终 `RoofHeight = 15 mm`、`RoofOverhang = 5 mm`，屋面斜率约 `39.8°`，斜边经过 body top corners，eaves 向外并向下延伸。`IndoorStation` 无屋顶，单/双 feeder unit 保持相邻布局。
- incoming switch 支持每路独立显示/隐藏；使用 2 mm short leads，不渲染 contact-circle presentation；formal CableTerminal anchor 随 shown/hidden presentation 派生切换。
- CustomerStation toolbox icon 已与 `RingCabinet` 区分；用户界面使用“用户站号1 / 用户站号2”，底层正式业务事实仍为 `IncomingFeeder.DisplayName`；CustomerStation label 已接入统一 typography settings。
- creation workflow、Inspector、feeder switch state control、aggregate delete、Selection、scene、hit-test、Clipboard、Undo / Redo、Save / Reopen 均已完成。
- `FormatVersion` 保持 V7；typed CustomerStation persistence 已完成；未引入 V8 migration。
- 验证结果：Domain tests PASS；Application tests PASS；Infrastructure tests PASS；Rendering.Wpf Windows tests PASS；Desktop Windows tests PASS；Windows professional visual acceptance PASS。
- Closure 前最终实现基线：`ff1f76d6c88161e815ba295008ecfcf4f88e2c0c`。

### WP-EM-07A — Transformer & Pole-Device Grounding Amendment

**状态：Closed / Archived / Final accepted implementation `c852d7a1f2477628664f8aeca8bfb23cf9ee3b06`**

正式 requirement contract 以 3.8 节为准。Complete Vertical Slice implementation、Code Review、Windows automated verification 与 Windows professional acceptance 均已通过，WP-EM-07A 现 Closed / Archived。FormatVersion 保持 V7；WP-EM-07B 现 Closed / Archived；WP-EM-08 整体为 Implementation In Progress，Slice A 已 Closed / Accepted，Slice B 与 Slice C 仍待推进。

### WP-EM-07B — Transformer Naming Amendment

**状态：Closed / Archived**

正式 requirement contract 以 3.9 节为准。该 WP 已完成 implementation、Windows automated verification、professional acceptance 与 Acceptance Fix-1。它只建立 `Device.DisplayName` 这一项 Transformer naming fact，正式中文 UI 名称为“变压器名称”；三种 Kind 的 current data 均要求名称非空，所有 Transformer 名称 label 统一显示在 glyph 下方居中。`document.json` 根 `ProjectFilePayload` 层的 additive `TransformerNamingContractVersion` discriminator 已解决同一 V7 内的 compatibility ambiguity；旧 V7 legacy incomplete Transformer 在补录完成前禁止 Save / Save As。FormatVersion 保持 V7，WP-EM-07B 不新增第二 naming fact、不改变 topology 或 conductivity。

### WP-EM-08 — Electrical Model Interaction Stabilization

**状态：Implementation In Progress；Slice A Closed / Accepted；Slice B Requirements Refined / Not Started；Slice C Requirements Refined / Characterization Pending**

Implementation Audit & Scope Revalidation 已在 baseline `c4f61653f923f985bcd8835d9735c68decee783e` 通过。WP-EM-08 Scope Amendment 已完成；Slice A 已完成 implementation、Windows automated verification 与 professional acceptance 并闭环，整体 WP 仍因 Slice B / Slice C 未完成而保持 Implementation In Progress。

WP-EM-08 始终是一个 Work Package，并在一个 Codex Thread 中推进。下述三个 Implementation Slice 是该 Work Package 内部、可独立评审的增量，不产生额外 Work Package，也不要求单独 Codex Thread。

WP-EM-08 不建立 generic routing framework。其目标是在已经完成的 Electrical Model 基础上：

1. 补齐正式 Device move capability；
2. 统一 transactional drag legality；
3. 提供 transient `LastValid`；
4. 消除正常用户 drag legality failure 的 modal failure；
5. 稳定连续合法拖动中的 route presentation；
6. 完成 cross-device interaction regression；
7. 保持 grounding、GAP、Transformer 与 CustomerStation 已关闭 Work Package 的业务事实和 identity 不变。

#### WP-EM-08 move-capability policy

- Pole 支持直接 drag。
- RingCabinet 支持直接 drag。
- CableTermination PoleAttachment 保持现有 pole-orbit drag。
- GroundingPoint 保持现有独立 symbol-offset drag。
- GroundingAccessPoint 不支持直接 drag；其 position 从 typed electrical route 派生。
- Transformer 由 WP-EM-08 Slice A 补齐直接 drag capability。
- CustomerStation 由 WP-EM-08 Slice A 补齐直接 drag capability。
- SwitchDevice / ordinary PoleAttachment 不开放独立自由 drag。Switch 随父 Pole 移动，自身专业布局继续通过既有 attachment layout、rotation 或 property command 调整。

不得为了表面上的交互一致而强制所有对象支持直接 drag。

#### WP-EM-08 illegal geometry taxonomy

“Illegal geometry”严格限于当前架构能够证明无法形成合法 Layout / Routing result 的情况：

- non-finite layout coordinate；
- route 无法保留两个 distinct points；
- required stub capacity insufficient；
- required stub direction / no-backtracking constraints impossible to satisfy；
- scoped routing invariant failure；
- current typed layout invariant violation。

必须继续区分 Domain-invalid、Layout-invalid、Routing-invalid 与 Presentation-undesirable。WP-EM-08 不新增 device overlap、Transformer / CustomerStation body overlap、canvas bounds、Pole screen-coordinate ordering、arbitrary collision prohibition 或 route crossing itself 作为业务非法规则；视觉不理想不得自动升级为 electrical invalidity。

#### WP-EM-08 `LastValid` and feedback contract

`LastValid` 是 transient interaction state，不是 Domain fact，也不是 persisted Layout fact。每个 drag gesture 从 `Before` 开始；每个通过验证且发生实际移动的 candidate 成为新的 `LastValid`。非法 candidate 不终止 gesture，不污染 current / persisted Layout，正式 Layout 恢复或保持 `LastValid`，并使用 non-modal feedback 报告原因。用户可以继续拖动。

在非法 candidate 上 release 时，最终 after state 为 `LastValid`。如果 gesture 从未产生实际合法移动，则结果为 NoChange，不创建 CommandStack history entry。只有当 `LastValid` 仍是起点时，最终位置才等于 gesture 起点。

正常用户 drag legality failure 不得继续以 modal `MessageBox` 作为主要反馈。最低 UX contract 是：合法 candidate 使用 normal preview；非法 candidate 保持 gesture active 和 Layout `LastValid`，通过现有 `MainWindowViewModel.ShowFeedback()` / status bar 以及必要的轻量 preview indicator 提供反馈；gesture 结束后清除 invalid feedback。本 WP 不建立通用 notification framework。

#### WP-EM-08 transaction and history contract

Drag preview、Layout mutation、scene rebuild 和 command recording 必须形成事务型边界。Commit 只能提交已验证的 `LastValid`。不得出现 Command 已进入 `CommandStack` 但 final scene rebuild 失败，也不得让 failed candidate 污染 current 或 persisted Layout。Undo / Redo 失败时也必须保持 `CommandStack` 与 Layout 状态一致。具体实现方式留给 WP-EM-08 Slice B design。

#### WP-EM-08 route continuity contract

当前不存在正式 `RouteFamily` enum、previous route-family state、cross-frame route cache 或 hysteresis；每次 scene rebuild 都重新选择 route candidate。WP-EM-08 Slice C 应处理连续、小幅度、合法拖动中因评分临界点导致的无必要 HV / VH / channel / fallback route突然翻转，但不得在 characterization 前冻结 hysteresis threshold。

WP-EM-08 Slice C 必须先建立 reproducible route-flip characterization，再决定 switching policy与margin。若证明确有需要，可以引入 internal typed transient `RouteFamilyKey` 或架构等价 identity，最小语义可覆盖 direct、HV、VH、preferred-guide、X-channel、Y-channel、obstacle-path 与 fallback。具体 enum / shape 留待该 Slice design。

该 continuity state 按 `ConnectionId` 保存于 runtime/session transient context；mouse-up 后可以继续保持当前 session continuity；project close / reload 时清空；Connection endpoint、topology 或 route constraint materially changes 时重新验证或失效。Command、Undo / Redo 不持有 route-family business fact。该状态不得持久化。

Transformer / CustomerStation presentation bounds 当前不属于 routing obstacle。线路穿越其 glyph 是 Presentation-undesirable candidate，不是 electrical invalidity。是否保持当前 obstacle set，或将两类 body 加入 presentation routing obstacle，是 WP-EM-08 Slice C characterization decision point；必须比较 short OHL、Transformer grounding / GAP、CustomerStation single / dual feeder、Cable route与route-family continuity后决定。Slice A、Slice B 不得提前加入该规则。

WP-EM-05 已接受的 RingCabinet cable-side GroundingPoint above-terminal visual interference 继续保持 Accepted Known Limitation，明确排除出 WP-EM-08，且不得重定义为 routing invalidity。未来如处理，必须另行完成独立 requirement decision。

#### WP-EM-08 closed-WP regression boundaries

WP-EM-08 不得破坏：

- WP-EM-05：`GroundingPointLayout.SymbolOffset`、GroundingPoint drag、grounding leader、GAP presentation、Canvas / PNG shared scene及accepted RingCabinet known limitation；
- WP-EM-06：Transformer one-terminal topology、`Transformer.Id`、`HvTerminalId`、`TransformerKind`与orientation contract；
- WP-EM-07：CustomerStation aggregate、one/two `IncomingFeeder` topology、feeder-owned `IsolationSwitch`与Cable-only incoming contract；
- WP-EM-07A：`GroundingAccessPlacementSide`、`TransformerSide`、dual-end GAP identity、short OHL grounding semantics与typed `GroundingTarget`；
- WP-EM-07B：`Device.DisplayName`、`TransformerNamingContractVersion`、legacy incomplete behavior、Creation / Inspector / Clipboard / Save naming与below-center Transformer name label。

Move / routing stabilization 不得创建新 Terminal、新 ElectricalNode、改变 Connection endpoints、推断新 grounding target、改变 electrical conductivity或改变 business identity。

#### WP-EM-08 Implementation Slice A — Move Capability Closure

**状态：Closed / Accepted — Implemented / Windows Verified / Professionally Accepted**

Slice A 已关闭正式 electrical / professional objects 的 direct move capability matrix，并补齐 Transformer 与 CustomerStation 缺失的 direct drag。WP-EM-08 整体保持 Implementation In Progress；该 slice-level closure 不推进 Slice B 或 Slice C。

##### Slice A move-capability matrix

| Object | Frozen move capability |
| --- | --- |
| Pole | direct drag supported |
| RingCabinet | direct drag supported |
| CableTermination PoleAttachment | direct pole-orbit drag supported |
| GroundingPoint | independent symbol-offset drag supported |
| Transformer | direct drag implemented and accepted |
| CustomerStation | direct drag implemented and accepted |
| GroundingAccessPoint | direct drag not supported；position 从 typed electrical route 派生 |
| SwitchDevice / ordinary PoleAttachment | independent free drag not supported；随父 Pole 移动，或使用既有 rotation / property / attachment-layout commands |

不得为了表面交互统一开放 Switch free drag，也不得把 GroundingAccessPoint 变成用户可移动对象。

##### Slice A Transformer direct-drag contract

该合同覆盖 `PublicPoleMounted`、`DedicatedPoleMounted`、`PublicIndoor` Horizontal 与 `PublicIndoor` Vertical。拖动只修改 `TransformerLayout.Position`，不得修改 `Transformer.Id`、`TransformerKind`、`HvTerminalId`、`Device.DisplayName`、Orientation、Terminal identity、Connection endpoint、`GroundingTarget`、GroundingAccessPoint identity、electrical topology 或 conductivity。

Preview 期间，Transformer glyph 必须实时移动，name label 必须继续位于 glyph 下方居中，HV Terminal anchor 必须实时更新，connected Cable / OverheadLine 必须通过当前 routing path 实时重建，Transformer-side GAP / GroundingPoint presentation 必须从最新 route / anchor 派生，selection 必须继续指向同一 `Transformer.Id`。

##### Slice A CustomerStation direct-drag contract

该合同覆盖 `BoxStation`、`IndoorStation` single feeder 与 `IndoorStation` dual feeder。拖动只修改 `CustomerStationLayout.Position`，不得修改 `CustomerStation.Id`、`StationKind`、IncomingFeeder identity、`IncomingFeeder.Sequence`、DisplayName、IsolationSwitch identity / state、Terminal identity、ElectricalNode identity、Connection endpoint、`GroundingTarget`、electrical topology 或 conductivity。

Preview 期间，station body 与全部 feeder Terminal anchors 必须实时移动，shown / hidden incoming switch presentation 必须保持既有合同，全部 connected Cable 必须实时重建，cable-side grounding presentation 必须继续从正式 target / anchor 派生，selection 必须继续指向同一 `CustomerStation.Id`。Dual-feeder station 的两条 Cable 必须随 station move 同步重建。

##### Slice A interaction, command, and Cancel contract

Transformer / CustomerStation direct drag 必须从现有 Canvas pointer interaction 进入。Body hit area 可以启动 drag；既有 selectable name、text 或 sub-presentation hit-test 映射必须继续解析到同一 parent Device，不得创建 secondary move identity。CustomerStation 内部 IncomingFeeder / IsolationSwitch 不得成为独立 station-position drag root；aggregate root folding 必须避免重复 group root。

实现应优先复用现有 `DeviceDragController`、`DragState`、MainWindow pointer lifecycle、`CommandStack` 与 `RefreshDrawingScene()`，不得建立第二套移动 UI；除非实现阶段证明确实无法复用，否则不得建立 `TransformerDragController` 或 `CustomerStationDragController`。任何新增 state 必须 typed，不得使用 `object` / property bag。

Transformer 与 CustomerStation 必须分别拥有符合现有 command architecture 的 before / after position command，例如 `MoveTransformerCommand` 与 `MoveCustomerStationCommand` 或架构等价实现。Command 必须保存 stable target ID、before position 与 after position，并保证 Execute / Undo / Redo deterministic、不得重新生成 Guid、NoChange 不进入 history。Command 不得保存 route、anchor、route family、grounding geometry 或 drag pointer trajectory。

Slice A 沿用当前 gesture semantics：right click、mouse capture lost、explicit drag cancel，以及 Undo / Redo 前取消当前 gesture，均恢复 `Before`。本 Slice 不引入 `LastValid`。

##### Slice A live rebuild, group move, and selection contract

Preview 继续采用 candidate Layout → `RefreshDrawingScene()` → rebuild anchors / routes / presentation 的现有路径。Slice A 不引入 transactional preview validation、hysteresis 或 route cache；如果现有 scene rebuild 异常，保持既有 failure behavior，不在本 Slice 修改 modal `MessageBox`。

Transformer 与 CustomerStation 必须成为合法 group move roots，各自 position 使用相同 delta，所有 ID 与 aggregate topology 保持不变，connected routes 实时重建。CustomerStation feeder / IsolationSwitch 和 Transformer name label 不得重复成为 group roots。既有 Pole、Pole + attachment、RingCabinet 与 multi-Pole group move 行为不得回归。

Transformer drag 前后使用 `SelectionTargetKind.Device + Transformer.Id`；CustomerStation 使用当前正式 CustomerStation selection identity。不得引入 layout selection object、text selection object 或 drag-only selection identity。Undo / Redo 后 selection 行为遵循既有 move command 习惯。

##### Slice A grounding and closed-WP regression contract

Transformer move 后，Terminal-target GroundingPoint 必须继续绑定同一 target；Transformer-side GAP 必须继续绑定同一 Connection / Pole / AdjacentEndpoint；grounding symbol / leader 从最新 route 重算，不得新建 `GroundingTarget`。CustomerStation move 后，cable-side grounding 必须继续绑定同一 Terminal target，leader / anchor 随最新 route 更新，grounding layout facts 不得被重写。Pole、RingCabinet 与 CableTermination 的既有行为不得回归。

Transformer move 只是 Position 变化，不得破坏 WP-EM-06 one-terminal topology、`TransformerKind`、`HvTerminalId` 与 Orientation contract；不得破坏 WP-EM-07A short OHL、`TransformerSide`、`GroundingAccessPlacementSide`、dual-end GAP identity 与 Transformer grounding semantics；不得破坏 WP-EM-07B `Device.DisplayName`、naming compatibility、Inspector / Clipboard / Save 与 below-center name label。

CustomerStation move 只是 Position 变化，不得破坏 WP-EM-07 aggregate、`BoxStation` single feeder、`IndoorStation` one / two feeder、IncomingFeeder identity / Sequence、feeder-owned IsolationSwitch、shown / hidden switch presentation、Cable-only incoming contract、grounding behavior、Clipboard 或 persistence。

##### Slice A persistence and Clipboard contract

Slice A 不修改 persistence schema，直接复用 `TransformerLayout.Position` 与 `CustomerStationLayout.Position`；FormatVersion 保持 V7。不得新增 move state、drag state、before / after state、route cache、route family 或 DTO。Save / Open round-trip 必须自然保存移动后的 position。

Slice A 不修改 Clipboard 业务语义。移动后复制继续使用当前 layout position，不得改变 ID remap、name handling、topology closure 或 connection copy policy。

##### Slice A required automated verification

Transformer tests 至少覆盖 `PublicPoleMounted`、`DedicatedPoleMounted`、`PublicIndoor` Horizontal 与 `PublicIndoor` Vertical 的 pointer / controller begin、preview position、live anchor update、connected route rebuild、Commit、Cancel、Undo、Redo、stable `Transformer.Id`、stable `HvTerminalId`、unchanged DisplayName 与 below-center label；pole-mounted 使用 OverheadLine，indoor 使用 Cable。

Transformer grounding tests 至少覆盖 pole-mounted short OHL + Transformer move，并验证 support Pole identity、Transformer endpoint identity、GAP identity、`TransformerSide` semantics 不变，以及 route / GAP presentation rebuild。若已有 direct Terminal GroundingPoint 场景，应复用并验证 target identity 不变。

CustomerStation tests 至少覆盖 `BoxStation`、`IndoorStation` single feeder 与 `IndoorStation` dual feeder 的 direct drag、preview、Terminal anchor update、Cable reroute、Commit、Cancel、Undo、Redo，以及 stable station / feeder / Terminal / IsolationSwitch identities 和 unchanged switch states；dual feeder 的两条 Cable 均须验证。已有 cable-side grounding 场景必须验证 move 后 `GroundingTarget` 与 Terminal identity 不变、anchor / leader presentation 更新。

Group-move tests 至少覆盖 Transformer 与 CustomerStation 的 same delta、stable IDs、connected route rebuild、Undo / Redo，并保留 Pole、Pole + attachment、RingCabinet 与 multi-Pole regressions。Unsupported-drag tests 必须明确验证 SwitchDevice direct drag 仍 unsupported，GroundingAccessPoint direct drag 仍 unsupported。

##### Slice A Windows professional acceptance

Windows acceptance 必须覆盖：三种 Transformer Kind、`PublicIndoor` Horizontal / Vertical、name 随 glyph 移动且保持下方居中、short OHL 实时跟随、grounding / GAP identity 不跳变；`BoxStation`、`IndoorStation` single / dual feeder、Cable 实时跟随、incoming switch shown / hidden state 不变、grounding leader 正常；以及 Cancel 恢复起点、Undo / Redo、selection、group move、Switch 不可自由拖动、GAP 不可自由拖动。

##### Slice A exclusions

Slice A 禁止实现 `LastValid`、invalid-candidate continuation、non-modal invalid feedback、drag transaction redesign、CommandStack atomicity redesign、route hysteresis、`RouteFamilyKey`、route switching margin、Transformer / CustomerStation obstacle policy、generic collision、canvas bounds、waypoint editor、manual route editor 或 routing engine rewrite。这些分别留在 Slice B、Slice C 或 WP-EM-08 范围之外。

##### Slice A closure evidence

Slice A closure chain is: Requirements Freeze `3e4dfe5fbada919b85fa1c84c1d1796f8376955e`; implementation `bfc551d358458433f37e517bf45e28138c24987d`; Windows Verification Fix-1 `4bb4887e6a75f56756ab45f48820ad799b396ec6`; Windows Verification Fix-2 / final accepted baseline `e84ff340cbf570d7ed6bb8b5dd7f4fc0befc6ec9`.

Transformer closure covers `PublicPoleMounted`, `DedicatedPoleMounted`, `PublicIndoor` Horizontal / Vertical direct drag, group move, preview / Cancel / Commit / Undo / Redo / NoChange, live Cable / OverheadLine rebuild, stable `Transformer.Id`, `HvTerminalId`, `Device.DisplayName`, Orientation, below-center label, and Transformer-side GAP / grounding identity. CustomerStation closure covers `BoxStation`, `IndoorStation` single / dual feeder direct drag, group move, preview / Cancel / Commit / Undo / Redo / NoChange, Cable rebuild, stable station / feeder / Terminal / `ElectricalNode` / `IsolationSwitch` identity and state, and cable-side grounding identity.

Existing move policies remain unchanged: Pole direct drag, RingCabinet direct drag, CableTermination pole-orbit drag, GroundingPoint symbol-offset drag, SwitchDevice / ordinary PoleAttachment no independent free drag, and GroundingAccessPoint no direct drag. The implementation reuses `DeviceDragController`, `SelectionMovePlanner`, `GroupMoveCommand`, MainWindow pointer lifecycle, `CommandStack`, `RuntimeLayoutDocument`, and `DrawingSceneBuilder`, with typed `MoveTransformerCommand` and `MoveCustomerStationCommand`; it does not create a second drag or routing framework.

Moving either object changes only the existing `TransformerLayout.Position` or `CustomerStationLayout.Position`. Domain identity, Terminal identity, `ElectricalNode` identity, Connection identity / endpoints, `GroundingTarget`, GAP identity, topology, conductivity, naming, orientation, and aggregate state remain unchanged; routes, anchors, and grounding presentation are derived by scene rebuild. Persistence schema impact is none: existing layout positions are reused, no DTO / drag / route / history state is persisted, and FormatVersion remains V7.

Windows automated verification is **PASS**, including Rendering.Wpf runtime tests and relevant Desktop / solution verification. Fix-1 corrected test-only GroundingPoint multi-hit-entry and Transformer label hit-priority assumptions; Fix-2 corrected the test-only Transformer body hit fixture so production `HitTest` selects `SelectionTargetKind.Device` rather than a CableSegment-covered connection point. No runtime-blocked state remains. Windows professional acceptance is **PASS** for direct drags, route rebuild, naming, short OHL / GAP / grounding, group move, Cancel, Undo / Redo, selection, and unsupported Switch / GAP drag behavior.

Regression boundaries remain WP-EM-05 grounding layout / accepted limitation, WP-EM-06 Transformer topology, WP-EM-07 CustomerStation aggregate, WP-EM-07A GAP / short OHL / Transformer-side grounding, WP-EM-07B naming, Clipboard, Save / Open, Undo / Redo, selection identity, and existing Pole / RingCabinet / CableTermination behavior. Slice B is next at Requirements Freeze; Slice C remains Requirements Refined / Characterization Pending.

#### WP-EM-08 Implementation Slice B — Transactional Drag Stabilization

**状态：Requirements Refined / Not Started**

建立统一合法/非法 candidate 的事务型处理：explicit drag preview result、`LastValid`、candidate validation、invalid candidate rollback、gesture continuation、invalid release、non-modal feedback、移除正常 drag-time modal `MessageBox`、commit/history atomicity及Undo / Redo failure atomicity。

本 Slice 不实现 route-family hysteresis、generic router rewrite或new collision model。

#### WP-EM-08 Implementation Slice C — Route Continuity Stabilization

**状态：Requirements Refined / Characterization Pending**

本 Slice 必须按以下顺序推进：

1. characterization tests；
2. reproduce route flips；
3. 如确有需要，定义 typed transient route-family identity；
4. freeze switching policy / margin；
5. implement transient continuity context；
6. connection / cache invalidation；
7. grounding / GAP / short-OHL regression；
8. Windows cross-device acceptance。

Transformer / CustomerStation 是否加入 routing obstacle set 在本 Slice characterization 后决定。本 Slice不得建立 generic waypoint editor、manual route editor、persistent route family或generic diagram routing rewrite。

#### WP-EM-08 persistence and remaining decision

WP-EM-08 persistence schema impact 预期为 none，FormatVersion 保持 V7。`LastValid`、invalid candidate、drag feedback、`RouteFamilyKey`、hysteresis cache与drag transaction state均不得持久化。已有合法 persisted Layout facts继续保留，包括object positions、attachment layouts、Transformer orientation、`CableRouteGuide`与GroundingPoint symbol offset；不得将 `CableRouteGuide` 泛化为generic waypoint system。

Refinement 后唯一延后到 WP-EM-08 Slice C characterization 的presentation policy决定，是Transformer / CustomerStation是否加入routing obstacle set。该问题不阻止Slice A或Slice B分别进入slice-level Requirements Freeze / implementation review sequence。

该 WP 不包含 waypoint editor、manual route editor、generic diagram routing engine rewrite、Annotation、Energization、new Electrical Device、arbitrary layout framework、WP-EM-07A business model changes或WP-EM-07B naming model changes。

### WP-EM-09 — Electrical Model Closure Integration

**状态：Planned / Integration-only**

只进行：

- V6 / V7 regression matrix；
- persistence 与 save/open；
- clipboard 与 Undo / Redo；
- dependency deletion；
- topology、grounding、Transformer 与 CustomerStation regression；
- WP-EM-07A grounding amendment regression；
- WP-EM-07B Transformer naming amendment regression；
- WP-EM-08 interaction stabilization regression；
- V6 → V7 upgrade；
- Windows runtime validation 与 professional visual acceptance；
- integration defect fixes。

不得在 WP-EM-09 增加新业务能力。

WP-EM-09 保持 Integration-only，不得承担 Transformer drag、CustomerStation drag、`LastValid`、hysteresis或其它新业务能力的首次实现。其范围仅限 V6 / V7 regression、save/open、clipboard、Undo / Redo、dependency deletion、electrical topology、grounding、Transformer、CustomerStation、WP-EM-07A、WP-EM-07B、WP-EM-08 regression、V6 → V7 upgrade、Windows final professional acceptance与integration defect fixes。

## 9. 阶段执行状态

当前执行状态：

- Post-V1 Requirement Reassessment / Planning 结束；
- Post-V1 Electrical Model Closure 成为当前开发目标；
- WP-EM-01 Grounding Presentation Anchor Separation 已完成代码 Review、自动验证和 Windows 实机验证；
- Post-V1 Grounding Scope Amendment 已完成并冻结；
- WP-EM-02 V7 Format & Migration Foundation 已完成代码 Review、自动验证和 Windows 最终验证；
- WP-EM-03 RingCabinet Optional CableTerminal Vertical Slice 已完成并 Closed，包含 Windows 最终验证；
- WP-EM-04 requirements refinement、implementation、review、Windows runtime validation 和 GUI acceptance 已完成，WP-EM-04 Closed；
- 当前生产实现和工程文件格式为 V7；`GroundingAccessPoint`、Transformer 与 CustomerStation vertical slice 均已完成并 Closed；
- Interaction Stabilization Amendment 已将 grounding-specific presentation continuity 分流至 WP-EM-05，将 generic drag / routing stabilization 分流至 WP-EM-08，并将最终 Integration 顺延为 WP-EM-09；
- WP-EM-05 已 Closed；Windows professional acceptance 为 Passed with Known Limitation；RingCabinet above-terminal visual interference 已记录为 Deferred；WP-EM-06 已 Closed，Windows professional acceptance = PASS；WP-EM-07 已 Closed，Windows automated verification 和 professional visual acceptance = PASS；
- Post-EM-07 Sequencing Amendment 已插入两个独立 amendment Work Package，且不重新打开 WP-EM-06 或 WP-EM-07；WP-EM-07A 已完成 implementation、Code Review、Windows automated verification 与 Windows professional acceptance，并以 `c852d7a1f2477628664f8aeca8bfb23cf9ee3b06` Closed / Archived；WP-EM-07B 已完成 implementation、Windows automated verification、professional acceptance 与 Acceptance Fix-1，并以 `6861db3e8275f31636c744f57afb81f20d53e2e9` Closed / Archived；WP-EM-08 Implementation Audit & Scope Revalidation 已通过，Scope Amendment 已完成，整体为 Implementation In Progress，并在同一个 Work Package 与 Codex Thread 内划分为三个内部 implementation slices：Slice A 为 Closed / Accepted / Implemented / Windows Verified / Professionally Accepted，Slice B 为 Requirements Refined / Not Started，Slice C 为 Requirements Refined / Characterization Pending；下一步为 Slice B Requirements Freeze；WP-EM-09 为 Planned / Integration-only；
- 后续 WP 必须按 WP-EM-01 → WP-EM-02 → WP-EM-03 → WP-EM-04 → WP-EM-05 → WP-EM-06 → WP-EM-07 → WP-EM-07A → WP-EM-07B → WP-EM-08 → WP-EM-09 顺序推进，任何范围变化需重新治理确认。
