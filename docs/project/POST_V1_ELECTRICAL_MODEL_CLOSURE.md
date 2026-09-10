# Post-V1 Electrical Model Closure

> 状态：Scope Frozen / WP-EM-01 Completed / WP-EM-02 Completed / WP-EM-03 Closed / WP-EM-04 Closed / WP-EM-05 Closed / WP-EM-06 Closed / Grounding Scope Amendment Completed / Interaction Stabilization Amendment Completed
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

`CustomerStation` 不允许零 feeder。双电源表示一个 `CustomerStation` 拥有两个 `IncomingFeeder`，不是两个独立 `CustomerStation`。

当前只允许 Cable 进线，不支持 OverheadLine 进线。每个 `IncomingFeeder`：

- 拥有独立 Stable ID、Terminal、Node、SwitchState 和 GroundingPoint possibility；
- 包含真实 `IsolationSwitch`，其 `SwitchKind` 固定为 `IsolationSwitch`，使用正常 `SwitchState.Open` / `SwitchState.Closed`；
- 在电缆侧允许 `GroundingPoint`；
- 为未来独立 Energization result 保留身份边界，但本阶段不实现 Energization Analysis。

当前不建立两个 feeder 之间的内部电气连接，也不建模用户站内部母线、母联或站内网络。

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

`GroundingAccessPoint` 至少稳定引用：

- associated `OverheadLine` / `Connection`；按当前模型以其 stable `ConnectionId` 表达；
- `PoleId`；
- `AdjacentPoleId`，表示该 GAP 实际位于 `PoleId` 朝哪个直接相邻支撑杆方向的 local conductor half-edge；
- `LineSide`，值仅为 `SmallerNumberSide` / `LargerNumberSide`。

`AdjacentPoleId` 必须是 `PoleId` 在对应 `OverheadLine.SupportPoleIds` 有序列表中的直接 predecessor 或 successor。物理位置身份与专业侧别严格分离：`AdjacentPoleId` 固化实际 conductor half-edge，`LineSide` 固化用户确认的“小号侧 / 大号侧”业务标签。同一 `ConnectionId + PoleId + AdjacentPoleId` 最多一个 `GroundingAccessPoint`；中间支撑杆可以在两个不同相邻杆方向各有一个 GAP。`Left`、`Right`、`Up`、`Down` 只属于创建交互，不得持久化为业务事实。

创建时可比较 `PoleId` 与 `AdjacentPoleId` 的明确简单杆号主整数，为 `LineSide` 提供保守推荐；无法可靠解析时必须由用户明确选择，不得猜测。用户覆盖推荐不改变 `AdjacentPoleId`。PoleNumber rename 不自动修改、移动、删除或重建已有 GAP。

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
├── AdjacentPoleId
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

`GroundingAccessPoint` 的默认专业位置由 `ConnectionId + PoleId + AdjacentPoleId` 对应的 local conductor half-edge 派生，`LineSide` 不再承担物理 half-edge 恢复。WP-EM-04 获得有限授权，使有序 `OverheadLine.SupportPoleIds` 参与架空线路 presentation：例如 `[P10, P11, P12]` 必须表现为线路经过 `P10 → P11 → P12`，从而在中间杆形成稳定 incoming / outgoing conductor geometry。该修正只属于 Presentation / Routing，不得拆分 Connection、新增 ElectricalNode 或 Terminal、改变 electrical connectivity，亦不得引入通用 waypoint editor。

WP-EM-04 basic GAP marker 为对应架空导线上的实心小圆点，直径约为 conductor stroke width 的 2.5 倍，并使用 Rendering typed metric。从 Pole 沿 `AdjacentPoleId` 对应 half-edge 方向使用固定 visual clearance 放置；不保存 screen coordinates、offset 或 clearance，不支持拖动。存在柱上设备时仍以该真实 half-edge 为定位基础。高级 `GroundingAccessClearance`、leader 与可调布局继续留给 WP-EM-05。

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

`GroundingPoint.Location` 继续是工作票文字 / 位置说明。UI 提供“小号侧 / 大号侧 / 自定义”，默认“小号侧”。GAP target 的默认显示值直接由唯一结构事实 `GroundingAccessPoint.LineSide` 派生：`SmallerNumberSide` → “小号侧”，`LargerNumberSide` → “大号侧”；不得再持久化第二套 side enum。只有选择“自定义”时输入自定义文本。Terminal target 的 `Location` 仅为 descriptive text，不因这些文字创造 Electrical `LineSide` 语义。

从 WP-EM-04 开始，新创建 `GroundingPoint` 必须自动获得非空、trim 后在当前 `DrawingDocument` 内唯一的 `Number`。默认分配扫描现存合法标准 `Lxx` 编号并选择可用序号；不持久化 global sequence counter，历史删除形成的空号允许未来复用。Inspector 编辑必须通过 CommandStack，重复编号修改原子拒绝并支持 Undo / Redo。

正式 V6 / legacy Terminal-target 文件继续允许加载；若旧记录的 Number 为空，不为兼容而猜号、自动重编号或建立复杂 grandfather 双模型。required / unique 约束应用于 WP-EM-04 新创建及新修改状态；旧空 Number 可保持只读兼容并在用户明确编辑后进入新约束。

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

完成 `BoxStation`、`IndoorStation`、one/two `IncomingFeeder`、feeder-owned `IsolationSwitch`、cable-only connection、independent feeder topology、GroundingPoint integration、aggregate create/delete、professional rendering、selection、inspector、clipboard、Undo / Redo 和 V7 integration。

### WP-EM-08 — Electrical Model Interaction Stabilization

候选范围包括：

- pole/device drag route continuity 与 legal route-family hysteresis；
- last-valid-position 与 illegal geometry handling；
- continuous-drag non-modal feedback；
- cross-device interaction regression；
- stabilized drag/routing 下的 grounding presentation regression；
- Pole / Switch / Transformer / CustomerStation drag behavior。

最终详细 Scope 可在 WP-EM-05～WP-EM-07 实施过程中继续收集真实 interaction case 后冻结，但 WP-EM-08 已正式进入阶段计划。该 WP 不包含 waypoint editor、manual route editor、generic diagram routing engine rewrite、Annotation、Energization、new Electrical Device 或 arbitrary layout framework。

### WP-EM-09 — Electrical Model Closure Integration

只进行：

- V6 / V7 regression matrix；
- save/open、copy/paste、Undo/Redo；
- dependency deletion；
- topology、grounding、Transformer 与 CustomerStation regression；
- WP-EM-08 interaction stabilization regression；
- V6 → V7 upgrade；
- Windows runtime validation 与 professional visual acceptance；
- integration defect fixes。

不得在 WP-EM-09 增加新业务能力。

## 9. 阶段执行状态

当前执行状态：

- Post-V1 Requirement Reassessment / Planning 结束；
- Post-V1 Electrical Model Closure 成为当前开发目标；
- WP-EM-01 Grounding Presentation Anchor Separation 已完成代码 Review、自动验证和 Windows 实机验证；
- Post-V1 Grounding Scope Amendment 已完成并冻结；
- WP-EM-02 V7 Format & Migration Foundation 已完成代码 Review、自动验证和 Windows 最终验证；
- WP-EM-03 RingCabinet Optional CableTerminal Vertical Slice 已完成并 Closed，包含 Windows 最终验证；
- WP-EM-04 requirements refinement、implementation、review、Windows runtime validation 和 GUI acceptance 已完成，WP-EM-04 Closed；
- 当前生产实现和工程文件格式为 V7；`GroundingAccessPoint` vertical slice 与 Transformer vertical slice 已完成，CustomerStation 尚未实现；
- Interaction Stabilization Amendment 已将 grounding-specific presentation continuity 分流至 WP-EM-05，将 generic drag / routing stabilization 分流至 WP-EM-08，并将最终 Integration 顺延为 WP-EM-09；
- WP-EM-05 已 Closed；Windows professional acceptance 为 Passed with Known Limitation；RingCabinet above-terminal visual interference 已记录为 Deferred；WP-EM-06 已 Closed，Windows professional acceptance = PASS，WP-EM-07 为 Next Work Package；
- 后续 WP 必须按 WP-EM-01 → WP-EM-02 → WP-EM-03 → WP-EM-04 → WP-EM-05 → WP-EM-06 → WP-EM-07 → WP-EM-08 → WP-EM-09 顺序推进，任何范围变化需重新治理确认。
