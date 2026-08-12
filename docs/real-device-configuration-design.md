# Drawing Core P0-6-A：真实设备配置与附属设备能力审查

## 1. 文档目的

本文基于提交 `af1ae5c502bfac1d0353588ac715f94c8165abb6` 的实际代码，审查完成第一张真实 10kV 配电工作票图所需的设备专业表达能力。
本次只做能力审查和下一阶段设计，不修改生产代码，不定义尚未由配电专业人员确认的规则。
审查范围包括：

- 环网柜、间隔、开关、端子和内部电气节点；
- 杆塔、杆塔附属关系和电缆终端；
- 外部连接与架空线路；
- Layout、Rendering、Symbol、Persistence；
- 当前设备创建工厂；
- PropertyInspector 和 Command 编辑入口。
本文中的“支持”严格区分：

- Domain 可表达；
- 工程拓扑可建立；
- Layout 可表达并持久化；
- Rendering 可显示；
- Editor/Command 可修改；
- Desktop 用户可直接完成。
代码中能构造对象，不等于用户已经能在真实工程中完成该操作。
## 2. 总体结论

当前 Drawing Core 已经具备较强的环网柜内部拓扑和动态渲染基础，也已具备 Pole、PoleAttachment、CableTermination 的领域及持久化骨架。
但当前 Desktop 的真实设备配置能力仍很窄：

- 用户放置环网柜时只能得到固定的三间隔普通负荷开关柜；
- 用户不能配置间隔类型、间隔名称、接地结构和开关状态；
- 用户不能创建或删除 PoleAttachment；
- 用户不能创建可用的 CableTermination 聚合及其 Layout；
- 当前没有独立 Cable 专业明细对象，只有 `ConnectionType.Cable`；
- PropertyInspector 对多数设备属性只读；
- 附属设备虽能渲染和保存，但仅存在演示构造路径，没有真实编辑闭环。
因此，下一阶段不应继续增加孤立模型，而应把已有 Domain 能力变成用户可用的原子 Command 工作流。
推荐顺序是：
1. P0-6-B：最小可配置环网柜创建闭环；
2. P0-6-C：CableTermination + PoleAttachment 创建、删除、选择、保存闭环；
3. P0-6-D：经专业确认后的柱上开关附属设备闭环；
4. P0-7：在电缆业务语义确认后实现 Cable 连线。
## 3. 当前环网柜领域能力

### 3.1 RingCabinet 聚合

`RingCabinet` 继承 `Device`，是环网柜聚合根。
当前聚合保存稳定 ID、名称、主母线节点、派生组成类型、有序间隔，以及柜内节点、端子、开关和开关组合。
`RingCabinet` 自身不保存单一 `SwitchState`，开关状态保存在每个 `SwitchDevice`。
聚合通过私有构造和工厂创建，外部不能逐个拼装不完整间隔。
`DrawingDocument.AddDevice` 对 `RingCabinet` 走专用聚合注册路径，将内部开关、节点、端子、组合及内部所有者一并纳入工程。

### 3.2 当前支持的间隔类型

当前 `IntervalKind` 实际支持 `LoadSwitchInterval` 和 `IntegratedFeederInterval`。
当前代码没有 `PTInterval` 实现，也没有可由用户选择的其他特殊间隔。
`RingCabinetDefinition` 支持逐间隔定义，因此 Domain 能表达混合柜。
纯普通负荷开关柜必须为 3、4、5、6 间隔。
纯一二次融合柜必须为 4 或 6 间隔。
混合柜由各间隔类型决定 `CompositionKind.Mixed`，当前不由柜体类型强制所有间隔一致。
现场已确认允许普通负荷开关间隔与断路器/融合类间隔混合存在。六间隔“前四个负荷开关、后两个（负5、负6）断路器”是常见实例之一，但不是固定规则；第一版不能写死三间隔或 4+2 组合。
常用组合未来可作为模板或快捷配置，但模板不能成为 Domain 限制。

### 3.3 普通负荷开关间隔的真实结构

每个 `LoadSwitchInterval` 固定创建一个负荷开关、一个接地刀闸、一个三工位开关组合、回路/大地节点、外部回路端子，以及两台开关各自的内部端子。
内部拓扑为：

```text
主母线节点
  → 负荷开关
  → 回路节点
  → 外部端子
回路节点
  → 接地刀闸
  → 大地节点
```
主母线节点归属顶层 RingCabinet。
回路节点和大地节点归属 `InternalAggregate/IntervalId`。
开关内部端子归属对应 `SwitchDevice`，并绑定固定 `ElectricalNode`。

### 3.4 一二次融合间隔的真实结构

每个 `IntegratedFeederInterval` 固定创建隔离刀闸、断路器、接地刀闸、融合开关组合、相应主回路/接地节点和一个外部回路端子。
当前支持 `UpperIsolationGrounding`、`UpperLowerGrounding`、`LowerLowerGrounding` 三种接地结构。
三种结构的设备顺序和接地刀连接节点由 Domain 工厂决定，不由 Rendering 推断。

### 3.5 外部 Terminal

每个现有间隔固定有一个 `ExternalTerminalId`。
该端子归属 `InternalAggregate/IntervalId`，绑定间隔回路节点，标记为外部端子，不允许多连接，同时允许 Cable 和 OverheadLine。
这意味着当前 Domain 已允许环网柜间隔外部端子直接作为电缆或架空线端点。
是否允许真实业务中每种间隔都直接接架空线，仍属于专业使用约束，当前 Domain 未细分。

### 3.6 开关状态与联锁

`SwitchState` 只表示单台开关的 `Open/Closed` 机械状态。
`SwitchAssembly.ChangeSwitchState` 是当前安全入口：先确认成员并评估目标组合，非法时拒绝，合法时才修改成员并返回派生评估。
普通 UI 不应直接调用 `Device.SetSwitchState`，也不应绕过 `SwitchAssembly`。
运行状态与有效接地仍是派生计算结果，不应写入 Symbol 或工程 DTO 作为第二事实源。
## 4. 当前固定三间隔创建路径

Desktop Placement 通过 `DeviceCommandFactory.CreateAddRingCabinet` 创建环网柜。
该入口当前硬编码：

- 自动生成柜 ID；
- 名称为 `环网柜-N`；
- 使用 `CreateNormalLoadSwitchCabinet`；
- 固定 3 个间隔；
- 三个间隔全部是 `LoadSwitchInterval`；
- 所有负荷开关初始为 `Open`；
- 所有接地刀闸初始为 `Open`。
同时创建固定布局：

- 每间隔宽 42 mm；
- 间隔高 90 mm；
- 柜体高 110 mm；
- 每间隔只创建负荷开关和接地刀闸 Layout；
- 不支持融合间隔 Layout 的创建配置分支。
因此当前用户可直接创建的不是“可配置环网柜”，而是一个合法但固定的三间隔普通柜模板。
混合柜与融合柜虽可由 Domain、Layout 和 Rendering 表达，但当前没有 Desktop 创建入口。
## 5. 环网柜 Layout、Rendering 与 Persistence

### 5.1 Layout

`RingCabinetLayout` 保存柜体毫米坐标、尺寸、母线位置、标签偏移和间隔布局。
`RingCabinetIntervalLayout` 保存间隔相对位置、尺寸和开关布局。
`RingCabinetSwitchLayout` 通过稳定 Switch ID 关联内部开关。
当前 Layout 能表达不同间隔数量以及普通/融合间隔所需的不同开关位置。
### 5.2 Rendering

`RingCabinetSymbol` 按实际间隔集合动态组合，不使用固定图片。
当前已实现：

- `LoadSwitchIntervalSymbol`；
- `IntegratedFeederIntervalSymbol`；
- 混合间隔组合；
- 三种融合接地结构的图形排列；
- `SwitchSymbol` 状态显示复用。
Rendering 读取 Domain 的间隔结构和开关状态，不计算运行状态或联锁结果。
当前缺少 PTInterval，以及 DTU 柜对应的图元；PT 间隔的具体 Domain/Layout/Rendering 表达仍需后续设计，DTU 柜术语已确认，但其具体 Domain/Layout/Rendering/Persistence 表达仍需后续设计。
### 5.3 Persistence

FormatVersion 2 已保存现有环网柜的：

- 柜 ID、名称、主母线节点；
- 间隔 ID、顺序、名称、类型；
- 接地结构；
- 中间/回路/大地节点 ID；
- 外部端子 ID；
- SwitchAssembly ID；
- 开关 ID、种类、状态、安装类型、端子、名称、调度编号；
- 全部内部节点和端子；
- 柜体、间隔、开关 Layout。
恢复路径保持稳定 ID，并重新验证聚合拓扑。
因此现有 DTO 足以保存“当前 Domain 已支持的可配置环网柜”，P0-6 不需要升级 FormatVersion。
## 6. 环网柜最小配置器设计

### 6.1 第一版目标

第一版配置器应解决“放置前创建合法且接近现场的柜体结构”，而不是建设完整设备数据库。
建议采用小型创建对话框或等价 ViewModel，输出 `RingCabinetDefinition` 和对应 Layout 方案。
创建阶段至少应允许用户明确：

- 柜体基本名称信息（当前仅有 `DisplayName`）；
- 间隔数量；
- 每个间隔的顺序；
- 每个间隔的 `DisplayName`；
- 每个间隔的现有 `IntervalKind`；
- 融合间隔创建所必需的现有 `GroundingStructureKind`。
配置器只能提供当前 Domain 已有枚举和合法工厂参数。
创建柜体时不要求用户一次性配置最终开关状态。当前 `RingCabinetIntervalDefinition` 工厂仍强制接收各开关的初始 `SwitchState`，因此 P0-6-B 的创建协调层必须提供合法技术初始化值；该值不代表用户已确认最终运行状态，也不得借此新增操作顺序、状态机或联锁规则。
创建 Commit 必须原子完成：

```text
用户配置
→ RingCabinetDefinition
→ Domain 工厂校验并创建聚合
→ LayoutFactory 按实际间隔创建布局
→ AddRingCabinetCommand
→ DrawingDocument + RuntimeLayout
→ Scene/Selection/Inspector 刷新
```
若 Domain 或 Layout 任一步失败，不得留下半个柜体。
### 6.2 能直接开放的现有语义（A）

可直接开放的现有语义包括：柜/间隔 `DisplayName`、间隔数量、两个现有 `IntervalKind`、融合间隔创建所需的三种接地结构和柜体 Layout 位置。
第一版不增加 Incoming、Outgoing、Tie、Transformer、Spare 或“进线/出线/联络/备用”等 Interval Usage。保留间隔序号/名称、间隔类型和当前 Domain 已有内部结构即可。
### 6.3 需要专业确认后再开放的语义（B）

不能仅凭代码决定：PT 间隔的具体 Domain/Layout/Rendering 表达、DTU 柜的具体 Domain/Layout/Rendering/Persistence 表达、具体开关操作顺序与完整联锁、设备自动编号/命名规则、调度编号适用范围，以及哪些间隔允许直连架空线。
确认前，P0-6-B 不应创建新枚举或自动规则。
### 6.4 当前 Domain 不支持的能力（C）

当前不支持 PTInterval、DTU 柜的具体领域/布局/渲染/持久化表达、其他特殊间隔、独立用途/柜编号字段、产品数据库和未确认的开关状态机；这些不能伪装成配置器选项。
## 7. 已存在环网柜的安全修改策略

### 7.1 可原地修改

从模型变更风险看，柜体名称、开关调度编号、合法开关状态和 Layout 位置都可原地修改；但 P0-6-B 只负责创建闭环，开关图形操作与联锁留给后续独立设计。
每项修改都必须使用保存 Before/After 的类型化 Command，经过聚合/组合校验，支持 Undo/Redo，并统一刷新 Scene、Selection 与 Inspector。
当前 `Device.Rename` 和 `SwitchDevice.SetDispatchNumber` 已有领域入口。
当前尚缺柜名、调度编号和开关状态对应的 Property Command 接线。
### 7.2 结构修改

增加、删除或改变间隔类型会影响 Interval、Switch、Terminal、ElectricalNode、SwitchAssembly、Layout 子对象及外部引用。
不得通过删除整个 RingCabinet 再创建新柜来完成普通配置编辑。
未来结构修改 API 应遵循：

- 未改变的间隔保持原稳定 ID；
- 新增间隔只为新增对象分配 ID；
- 删除间隔前检查其外部 Terminal 是否被 Connection 引用；
- 删除间隔前检查 WorkScope/GroundingPoint 是否引用其 Terminal；
- 类型改变视为受控结构替换，不能静默复用语义已改变的 Terminal ID；
- 失败时聚合与 Layout 同时回滚；
- Command 快照必须足以恢复原聚合子对象和 Layout。
### 7.3 P0-6-B 范围收敛

建议 P0-6-B 只实现：

- 输入基本柜体信息；
- 指定间隔数量；
- 按顺序为每个间隔选择当前 Domain 支持的合法类型；
- 输入间隔序号/名称，并为融合间隔提供工厂必需的接地结构；
- 创建完整合法聚合和匹配的 RuntimeLayout；
- 完成 Rendering、Selection、Undo/Redo、Dirty、Save/Reload。
P0-6-B 不实现完整开关图形点击操作、完整联锁、PT/DTU 柜、Interval Usage、自动命名、Cable、PoleAttachment、CableTermination 或模板系统。
已有柜体结构重配，以及图上点击开关后的状态操作，应分别单独设计和验收。
后续应新增独立的 Switch State Interaction / Interlock Design，流程为：用户操作 → Domain/联锁校验 → 允许或拒绝 → 状态修改 → Rendering 刷新。在规则未完整确认前，不定义操作顺序、状态机或新业务枚举。
## 8. Pole 当前专业能力

`Pole` 继承 `Device`，当前能表达：
- 稳定设备 ID；
- 杆号 `PoleNumber`；
- 可选 `DisplayName`；
- `PoleType`；
- 架空锚点 Terminal ID 集合。
当前 `PoleType` 只有 `Cement`。
Placement 创建 Pole 时：

- 自动生成 `P-NN` 杆号；
- 创建一个真实架空锚点 Terminal；
- 该 Terminal 只允许 OverheadLine；
- 当前创建入口允许该锚点多连接；
- 同时创建 `PoleLayout`。
Pole 已支持：

- Desktop 放置与安全删除；
- 选择、高亮；
- 移动、Undo/Redo；
- 保存与恢复；
- PropertyInspector 查看；
- 杆号编辑。
当前 Property 编辑只开放 `Pole.PoleNumber`。
Pole 名称和 PoleType 都是只读；PoleType 构造后不可变。
对于第一张真实图，杆号能力已可用，但杆型分类是否需要扩展必须由专业人员确认。
## 9. PoleAttachment 当前状态

### 9.1 Domain

`PoleAttachment` 保存：
- 稳定 `AttachmentId`；
- `PoleId`；
- `AttachedDeviceId`。
它本身不拥有 Terminal，不保存开关状态，也不创造电气拓扑。
`DrawingDocument.AddPoleAttachment` 当前只接受：
- `SwitchDevice`，且 `InstallationType == Pole`；
- `CableTermination`。
一个 AttachedDevice 只能挂到一个 Pole。
PoleAttachment 是安装/归属关系；实际 Terminal 和电气意义属于 AttachedDevice。
### 9.2 Layout 与 Rendering

`AttachmentLayout` 已存在，保存相对杆塔的毫米偏移、尺寸和标签偏移。
`DrawingSceneBuilder` 已按 Pole + PoleAttachment + AttachedDevice + AttachmentLayout 组合显示。
`SymbolLibrary` 已映射：
- 柱上断路器；
- 柱上负荷开关；
- 柱上隔离开关；
- 接地刀闸；
- 跌落式熔断器；
- CableTermination。
Attachment 已有 HitTest 和 SelectionReference。
但 Attachment 的 PropertyInspector 当前只显示关系 ID，未解析并展示 AttachedDevice 的专业属性。
### 9.3 Persistence

PoleAttachment DTO 和 AttachmentLayout DTO 均已存在。
保存/恢复使用稳定 AttachmentId、PoleId、AttachedDeviceId，不依赖数组顺序。
### 9.4 Editor 缺口

当前没有：

- AddPoleAttachmentCommand；
- RemovePoleAttachmentCommand；
- DrawingDocument.RemovePoleAttachment；
- 附属设备创建工厂；
- Desktop 添加/删除入口；
- Attachment Layout 的动态创建/删除入口；
- AttachedDevice 属性编辑入口。
当前 MainWindow 中存在硬编码 CableTermination/Attachment 的测试内容构造，但它不写入真实工程会话，不能算用户工作流。
## 10. PoleAttachment 第一版创建/删除设计

第一版应使用选中 Pole 作为明确安装目标：

```text
选择 Pole
→ 选择经专业确认的附属设备类型
→ 输入当前 Domain 已支持的必要字段
→ AddAttachmentCommand
→ 创建 AttachedDevice 及其内部拓扑
→ DrawingDocument.AddDevice
→ DrawingDocument.AddPoleAttachment
→ 创建 AttachmentLayout
→ Scene/Selection/Inspector 刷新

```
Command 必须原子管理：

- AttachedDevice；
- AttachedDevice 的 Terminal/ElectricalNode；
- PoleAttachment；
- AttachmentLayout。
删除顺序应与创建相反，并先做引用检查。
若 AttachedDevice 的 Terminal 仍被 Connection 或 Professional 对象引用，删除必须拒绝。
不得默认级联删除 Connection、WorkScope 或 GroundingPoint。
Undo 必须恢复相同 DeviceId、TerminalId、NodeId、AttachmentId 和 Layout。
## 11. CableTermination 当前状态

### 11.1 Domain

`CableTermination` 继承 `Device`，固定为 10kV。
它保存：

- 稳定 Device ID；
- CableSideTerminalId；
- OverheadSideTerminalId；
- InternalNodeId；
- DisplayName。
两个外部端子通过同一个内部 `ElectricalNode` 表达固定导通。
DrawingDocument 校验要求：

- 内部节点归属该 CableTermination；
- 电缆侧 Terminal 只允许 Cable；
- 架空侧 Terminal 只允许 OverheadLine；
- 两端均绑定内部节点；
- 作为柱上物理端点时通过 PoleAttachment 解析所属 Pole。
CableTermination 自身不保存坐标，位置来自 AttachmentLayout。
### 11.2 当前各层状态

- Domain：已支持对象和拓扑约束；
- Topology：已支持双 Terminal + 内部节点；
- Layout：AttachmentLayout 可表达相对杆塔位置；
- Rendering：已有 CableTermination Symbol；
- TerminalAnchor：当前把附属设备的端子解析到 Attachment 中心；
- Selection：当前选择对象是 PoleAttachment，而不是独立 CableTermination；
- PropertyInspector：只显示 Attachment 关系，未显示终端两侧信息；
- Persistence：已保存 Device、双 Terminal、内部节点、Attachment 与 Layout；
- Editor：没有真实 Add/Remove/Edit Command。
当前 `CableTermination` 构造函数只接收稳定 ID，不负责创建对应 Terminal 和 ElectricalNode。
因此 P0-6-C 需要一个受控聚合工厂/Command，不能让 UI 手工拼装四个对象。
## 12. Cable 的当前表达与 P0-7 前置条件

### 12.1 当前事实

当前存在 `ConnectionType.Cable`，因此外部拓扑可以标记为电缆连接。
但当前没有与 `OverheadLine` 对等的 Cable 专业明细对象。
因此尚无独立位置保存：

- 电缆型号；
- 电缆长度；
- 延续信息；
- 其他经确认的电缆专业属性。
也没有 Cable Layout、Cable Symbol、Cable Selection 或 Cable Command。
### 12.2 Endpoint 能力

当前模型允许 RingCabinet 外部 Terminal 直接接受 `ConnectionType.Cable`。
Pole 自身的架空锚点只允许 OverheadLine，不能直接作为 Cable endpoint。
Pole 上的电缆落点应由 CableTermination 的 CableSideTerminal 表达；其 OverheadSideTerminal 再接架空线。
是否所有电缆都必须经过 CableTermination，不能由代码统一断言：

- 柜到柜电缆可使用两个 RingCabinet 外部 Terminal；
- 柜到杆的电缆在 Pole 一侧需要一个合法 Cable endpoint；
- 当前模型为该目的提供 CableTermination，但实际使用规则需专业确认。
### 12.3 P0-6 必须准备的接口

在 P0-7 Cable Editor 前，至少需要：

- CableTermination 完整创建工厂；
- CableTermination 安装到 Pole 的原子 Command；
- 对称安全删除 API；
- CableSide/OverheadSide Terminal 的明确 Anchor 或 Pick 信息；
- Attachment 的选择与属性投影；
- Terminal 可连接类型过滤；
- 删除时 Connection/Professional 引用保护；
- 保存/恢复验收。
当前 TerminalAnchorIndex 把附属设备的全部端子放在同一 Attachment 中心。
对简单连线可工作，但电缆侧与架空侧视觉端点重叠，会影响用户明确选择哪一侧。
P0-6-C 应为 CableTermination 定义两个明确但仍属于 Layout/Rendering 的端子锚点，不改变 Domain 拓扑语义。
## 13. PropertyInspector 与 Property Command 缺口

| 对象 | 当前可查看 | 当前可编辑 | P0-6 最小缺口 |
|---|---|---|---|
| Pole | ID、杆号、名称、杆型、锚点数、Layout | 杆号 | 名称是否开放待确认；其余可保持只读 |
| RingCabinet | ID、名称、组成、主母线、间隔数、Layout | 无 | 柜名编辑 |
| RingCabinetInterval | ID、父柜、序号、名称、类型、接地结构、外部端子、开关数、Layout | 无 | 创建时配置名称/类型；已有结构先只读 |
| SwitchDevice | ID、名称、类型、状态、调度编号、端子 | 无 | P0-6-B 保持只读；后续独立设计状态交互与联锁 |
| PoleAttachment | Attachment/Pole/Device ID、Layout | 无 | 解析 AttachedDevice 并显示类型、名称、Terminal |
| CableTermination | 未作为独立选择投影 | 无 | 通过 Attachment 选择显示双端子、内部节点、名称 |
当前 `PropertyCommandFactory` 的非 Professional 设备编辑只支持 `Pole.PoleNumber`。
P0-6 不应建设任意反射式属性编辑器。
后续属性阶段可增加少量明确的类型化 Command：

- `ChangeRingCabinetNameCommand`；
- `ChangeSwitchStateCommand`；
- `ChangeSwitchDispatchNumberCommand`；
- 必要时 `ChangeCableTerminationNameCommand`。
所有业务校验仍在 Domain 聚合或 DrawingDocument，UI 只处理输入和错误展示。
## 14. Persistence 适用性结论

FormatVersion 2 已能表达：

- 当前两类 RingCabinetInterval；
- 混合环网柜；
- 三种融合接地结构；
- 开关状态和调度编号；
- 内部节点、端子和外部 Connection；
- PoleAttachment；
- CableTermination 双端子与内部节点；
- 对应 RingCabinet、Attachment Layout。
因此 P0-6-B/C 在不新增业务字段的前提下不需要升级 FormatVersion。
当前不足是创建、修改和删除入口，而不是 DTO 容量。
若后续新增独立 Cable 明细或新的柜编号/间隔用途字段，需要单独评估格式升级；本阶段不预判版本号。
## 15. 第一张代表性工作票图仍缺的设备表达能力

在不考虑 WorkTicketData、自动停电分析和模板效率的前提下，仍缺：
1. 可按现场结构创建普通、融合或混合环网柜；
2. 可设置间隔名称，并在不要求用户配置最终状态的前提下创建 Domain 合法聚合；
3. 后续可通过独立图形交互并经完整联锁校验修改开关状态；
4. 可把 CableTermination 明确安装到 Pole；
5. 可创建、选择和安全删除 PoleAttachment；
6. 可区分 CableTermination 的电缆侧和架空侧端子；
7. 可创建并显示真实 Cable；
8. 可编辑完成图纸所需的最小设备和线路标签；
9. 可最终导出/打印，但该能力属于后续 Drawing Core 阶段。
其中 1～6 是 P0-6 的主要准备范围；7 属于 P0-7。
## 16. 已确认决策与剩余专业确认项

### 16.1 已确认决策

- 混合柜：允许用户决定间隔数量并逐间隔选择当前 Domain 支持的类型；六间隔 4+2 只是常见实例，不是固定规则；未来模板不约束 Domain。
- 开关状态：创建时不要求用户配置最终状态；长期采用图上单独操作开关并经过 Domain/联锁校验的方式，具体顺序和完整规则另行设计。
- Interval Usage：第一版不建立进线、出线、联络、变压器、备用等用途枚举，只保留序号/名称、类型和已有内部结构。

### 16.2 Professional Decisions Required Before Implementation

以下剩余问题仍需专业确认，不重复询问上述已确认事项：

| 问题 | 为什么需要确认 | 当前软件模型 | 可选方向 | 影响范围 |
|---|---|---|---|---|
| PT 间隔和 DTU 柜的具体 Domain、Layout、Rendering 与 Persistence 表达是什么？ | 两类对象的术语/存在已确认，但具体模型边界和图面表达尚未设计 | 当前仅有 LoadSwitch/IntegratedFeeder 两种 Interval，尚无 DTU 柜模型 | 分别进行 PT 间隔与 DTU 柜专项设计 | Domain、Layout、Rendering、Persistence |
| 从运行转接地等操作的真实顺序与完整联锁是什么？ | 现有硬联锁不足以代表完整现场操作过程 | SwitchAssembly 可修改单台状态并校验已实现规则 | 独立 Switch State Interaction / Interlock Design | Domain 规则、Command、图形交互、测试 |
| “负1…负7”及上刀/下刀/上接地/下接地如何形成编号？ | 具体规则未完整确认，不能硬编码或推导 | 当前只有 DisplayName/DispatchNumber | 结构数据 → Naming Rule → 建议名称，并允许人工确认/修改 | Naming 设计、标签、Inspector；不属于 P0-6-B |
| 首批柱上附属设备包括哪些？ | Symbol 存在不等于 Terminal 与连接规则已确定 | Attachment 可挂 Pole SwitchDevice 或 CableTermination | 先做 CableTermination；或再做经确认的柱上开关集合 | P0-6-C/D、拓扑工厂、测试矩阵 |
| 柜到杆电缆是否必须经 CableTermination，柜到柜能否直连？ | 当前端子能力是技术约束，不是现场使用政策 | Pole 不接受 Cable；终端电缆侧接受 Cable；柜外端子接受 Cable | 直接按端子能力开放；或增加确认后的 endpoint 组合规则 | P0-7 Pick 过滤与非法连接规则 |
| 第一版 Cable 必须保存和显示哪些属性？ | 当前没有 Cable 明细对象 | Connection 仅有名称、电压和端点 | 仅用 Connection；或新增最小 Cable 实体 | P0-7 Domain、Layout、Symbol、DTO/版本 |
## 17. 推荐实现拆分

### 17.1 P0-6-B：最小环网柜配置闭环

实现 `Add RingCabinet → 基本信息 → 间隔数量 → 逐间隔选择当前 Domain 类型 → 合法聚合 → RuntimeLayout → Rendering/Selection → Undo/Redo → Save/Reload`。不包含完整 Switch 图形操作或联锁、PT/DTU 柜、用途枚举、自动命名、Cable、Attachment、CableTermination 和模板。
独立验收：从空工程创建一个非固定三间隔、且可包含现有两类间隔的合法柜，恢复后 ID、拓扑和图形一致。
### 17.2 P0-6-C：CableTermination + PoleAttachment 闭环

实现终端聚合工厂、双 Terminal/内部节点、安装 Command、AttachmentLayout、双侧 Anchor、选择/Inspector、安全删除、Undo/Redo 和 Save/Reload。
独立验收：在 Pole 上添加电缆终端，分别识别两侧端子，保存后完整恢复。
### 17.3 P0-6-D：柱上开关附属设备闭环

经专业确认首批类型和 Terminal 策略后，实现 Pole SwitchDevice 工厂、Attachment Command、状态/调度编号编辑、连接约束和完整保存闭环；不一次覆盖所有 SwitchKind。
### 17.4 P0-7：Cable Editor

在 CableTermination 使用边界和 Cable 必要属性确认后实施，不用 OverheadLine 或自由线模拟 Cable。
## 18. 推荐首先实现的阶段

推荐首先实现 P0-6-B：当前固定三普通间隔柜不能代表大量现场设备，而 Domain、Rendering、Persistence 已具备主要基础，缺口集中在配置入口、按实际间隔结构创建 Layout 和 Command 原子闭环，且无需升级格式。
当前通用 `RingCabinetDefinition.Create` 与 `RingCabinet.Create` 已支持逐间隔组合现有两种类型，因此自由混合创建没有根本 Domain 缺口；缺口是 Desktop `DeviceCommandFactory` 和 Layout 创建逻辑仍硬编码三间隔普通柜。另一个明确 API 边界是 Interval Definition 必须传入初始开关状态，P0-6-B 需提供合法技术初始化值，但不把它解释为用户最终状态配置。
随后实施 P0-6-C，为 P0-7 提供 Pole 侧合法 endpoint。若首张人工确认的验收图不含混合/融合柜但必须电缆上杆，可由产品负责人调整 B/C 顺序。
## 19. 架构风险与控制边界

- `MainWindow.xaml.cs`：不得加入配置状态和 Attachment 拓扑事务；只打开对话框、转发结果和显示错误。硬编码测试场景不能复用为生产入口。
- `DrawingSceneBuilder`：复用现有 AttachmentSymbol/IntervalSymbol，不加入业务校验；P0-6 不需要因动态创建而整体拆分 Builder。
- `PropertyInspector`：只增加明确类型投影，不引入反射式任意属性编辑；Attachment 应投影 AttachedDevice 值快照。
- `RingCabinet Factory`：Domain 已支持 Definition 驱动创建；应在 Rendering.Wpf 增加按 IntervalKind 生成 Layout 的专用 Factory，不能把坐标写入 Domain。
- `RuntimeLayout/CommandStack`：Command 原子修改 DrawingDocument 与 RuntimeLayout；禁止编辑 Persistence Snapshot，保存仍单向生成快照。
- 安全删除：DrawingDocument 当前仅删除 Pole/RingCabinet；P0-6-C/D 需最小对称 API，先检查 Connection/Professional 引用，再删除 Attachment 与 AttachedDevice，不在 UI 复制规则。
## 20. 实施验收与非目标

每个子阶段都必须从真实空工程走通：`New → Create/Configure → Select → Inspect/Edit → Undo/Redo → Delete/Undo → Save → Close → Open`。
恢复后检查稳定 ID、Terminal/Node/Connection、RuntimeLayout 毫米坐标、Symbol/HitTest/Inspector，并确认新会话 CommandStack 为空、Dirty 为 false。Demo Scene 不能代替验收。
本阶段不实现生产代码、完整设备数据库、PT/DTU 柜具体模型、已连接柜体结构重配、完整开关交互/联锁、自动命名、Cable、未确认柱上设备、自动工作范围/停电分析/安全措施、WorkTicketData、模板、Export/Print 或 FormatVersion 升级。
