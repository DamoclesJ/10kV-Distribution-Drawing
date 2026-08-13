# F-2-B-2-C 10kV Work Ticket Topology Model

> 状态：架构设计；本阶段不修改生产代码、Domain、Persistence 或测试。
> 基线：`14422d3 docs: finalize switch device architecture decision`。
> 目标：定义支撑 10kV 工作票绘图的最小电气拓扑核心模型。

## 1. Context

本项目不是以自由图元编辑为中心的 CAD，而是具有基础电气拓扑识别能力的 10kV 工作票绘图工具。

系统需要理解的最小事实包括：

- 工程中有哪些设备和结构单元；
- 设备通过哪些 Terminal 连接；
- Connection 连接了哪两个 Terminal；
- SwitchDevice 的类型与分、合状态；
- 柜内和杆塔附属结构的明确所有权；
- 工作票中目标设备、操作动作和操作前后状态；
- Save/Reload、Command、Undo/Redo 中保持不变的 Stable IDs。

本设计建立对象身份、端子和连接三层模型，不通过画布位置、线条接触、显示名称或图例外观推断电气关系。

## 2. Design Principles

最小拓扑模型遵循以下原则：

1. Device/结构对象表达“是什么”；
2. Terminal 表达“在哪里可以连接”；
3. Connection 表达“哪两个端子实际相连”；
4. ElectricalNode 表达设备内部或固定结构中的等电位关系；
5. Attachment 表达所有权或安装关系，不表达电气导通；
6. SwitchState 只决定 SwitchDevice 两端的局部导通关系；
7. Rendering 只投影 Domain/Layout 事实，不成为拓扑事实源；
8. Stable ID 是跨 Command、Undo/Redo 和 Persistence 的身份合同；
9. 当前模型不重新引入 `BayFunction`，也不推断 Incoming、Outgoing 或 Tie；
10. 只实现当前工作票绘图已有明确消费者的能力。

## 3. Device, Terminal, and Connection

### 3.1 Three-Layer Relationship

```text
Device / Structural Owner
        |
        | owns
        v
Terminal ---------------- Connection ---------------- Terminal
        |                                               |
        | belongs to                                    | belongs to
        v                                               v
ElectricalNode                                  ElectricalNode
```

三层职责必须分离：

| 层 | 负责 | 不负责 |
| --- | --- | --- |
| Device / structural owner | 身份、类型、状态、内部结构和 Terminal 所有权 | 不以画布坐标表达连接 |
| Terminal | 可连接点身份、owner、ElectricalNode、允许的 ConnectionType 和连接数规则 | 不保存远端设备状态 |
| Connection | 两个 endpoint Terminal、连接类型和自身 Stable ID | 不复制设备、Terminal 或 Node |

### 3.2 Terminal Contract

每个可参与拓扑的 Terminal 至少需要：

- Stable TerminalId；
- 明确 owner；
- ElectricalNodeId；
- 允许的 ConnectionType；
- 是否允许多重外部连接；
- 与其他 Terminal 不重复的身份。

内部 Terminal 通常不接受外部 Connection。面向线路或电缆的 external terminal 才允许相应外部连接，并由 Domain invariant 限制重复占用。

### 3.3 Connection Contract

每条 Connection：

- 连接两个不同 Terminal；
- 两端必须都允许该 ConnectionType；
- 不得违反 Terminal 的单连接/多连接规则；
- 不得以视觉线段代替 endpoint identity；
- 不得通过名称、距离或坐标自动选择 endpoint；
- Save/Reload 后继续引用同一组 Terminal IDs。

Connection 只表达外部连接。设备内部固定导通关系由 ElectricalNode、设备结构和 SwitchState 表达，不额外创建虚假的 Cable 或 OverheadLine。

## 4. ElectricalNode and Local Conductivity

ElectricalNode 表达结构内部的固定等电位关系。多个 Terminal 指向同一个 ElectricalNode，表示它们在该结构中固定连接。

SwitchDevice 是受状态控制的例外：

| SwitchState | 两端局部导通关系 |
| --- | --- |
| `Open` | 两端不导通 |
| `Closed` | 两端导通 |

这一规则只提供局部拓扑识别，不等于全网潮流、带电范围或负荷状态计算。系统不得由 Closed 自动宣称设备带电，也不得由 Open 自动推导完整停电范围。

## 5. Ring Cabinet Topology

### 5.1 Ownership Hierarchy

```text
RingCabinet
  MainBus ElectricalNode
  Interval[Sequence]
    BayIndex
    IntervalKind
    SwitchDevice(s)
      internal terminals
    SwitchAssembly (when defined by the fixed interval structure)
    Circuit / Earth ElectricalNode(s)
    external terminal
```

RingCabinet 是聚合根。Interval 是柜内固定结构单元，不是独立 Project Device。Interval 的设备组合和 Terminal topology 由 `IntervalKind` 决定，调用方不得自由拼装。

### 5.2 Cabinet

Cabinet 至少保存：

- CabinetId；
- DisplayName；
- MainBusNodeId；
- 按物理顺序排列的 Interval collection；
- 聚合级结构校验。

Cabinet 不保存 BayFunction、Incoming/Outgoing/Tie、WPF Symbol 或画布图元。

### 5.3 Interval

每个 Interval 至少保存：

- IntervalId；
- Sequence；
- BayIndex；
- IntervalKind；
- 固定结构产生的 SwitchDevice、Terminal、ElectricalNode 和可选 SwitchAssembly。

Sequence 表达柜内物理排列顺序。BayIndex 表达现场/图纸业务编号。二者必须独立：例如 BayIndex 为 `10, 3, 8` 时，Sequence 仍为 `1, 2, 3`，不得排序或重新连续化 BayIndex。

### 5.4 Switches and Terminals

当前固定 IntervalKind 的真实结构为：

| IntervalKind | SwitchDevice structure | External connection point |
| --- | --- | --- |
| `LoadSwitchInterval` | LoadSwitch + GroundSwitch | interval external terminal |
| `IntegratedFeederInterval` | Disconnector + Breaker + GroundSwitch | interval external terminal |

SwitchDevice 的内部 terminals 指向 MainBus、Circuit 或 Earth node。外部 Cable/OverheadLine 只能连接 interval external terminal，不直接连接柜内 switch terminal。

`Conventional10kVRingCabinet` 继续使用三个 `LoadSwitchInterval`，不因本文档增加 Disconnector，也不改变已冻结模板 schema。

## 6. Pole Topology

### 6.1 Ownership Model

```text
Pole
  PoleAttachment
    SwitchDevice
      terminal A
      terminal B
    or CableTerminal capability
      cable-side terminal
      overhead-side terminal
```

各对象职责如下：

- Pole 是主体设备，保存杆塔 Stable ID、杆号、杆型和必要锚点信息；
- PoleAttachment 表达设备或能力附属于哪根 Pole；
- SwitchDevice 是可控制开断设备，独立保存 Kind、State 和 terminals；
- CableTerminal 是不可操作的连接端子能力，不是独立设备；
- Attachment 不等于 Connection，也不表达电气导通。

### 6.2 PoleAttachment

PoleAttachment 至少需要稳定表达：

- AttachmentId；
- PoleId；
- 被附属 SwitchDevice 的 identity，或附属 CableTerminal capability 的 identity；
- 必要的安装语境。

Attachment 不复制 SwitchState、SwitchKind 或 Connection endpoints。一个 SwitchDevice 安装在 Pole 上，不会成为 Pole 的内嵌开关状态字段。

### 6.3 CableTerminal Capability

CableTerminal/CableTermination 的目标模型是 Pole 的连接端子能力，用于表达电缆侧与架空侧在杆位的固定连接边界。它至少包括：

- cable-side terminal；
- overhead-side terminal；
- 表达固定内部关系的 ElectricalNode；
- PoleAttachment ownership；
- Terminal、Node 和 Attachment Stable IDs。

它不具有 DeviceType、SwitchKind、SwitchState、分合闸 Command 或保护语义。

当前生产代码仍存在 `CableTermination : Device`。本文只定义目标拓扑边界，不修改 Domain 或 V4；未来迁移必须单独评审并保持旧文件与 Stable IDs 兼容。

## 7. SwitchDevice Model

第一阶段继续使用统一 `SwitchDevice`，通过 `SwitchKind` 保留设备类型：

| 业务类型 | 当前 SwitchKind | 第一阶段状态 |
| --- | --- | --- |
| Breaker | `CircuitBreaker` | Open / Closed |
| LoadSwitch | `LoadSwitch` | Open / Closed |
| Disconnector | `IsolationSwitch` | Open / Closed |
| Fuse | `DropoutFuse` | Open / Closed |

每台 SwitchDevice 至少具有：

- Stable SwitchId；
- SwitchKind；
- SwitchInstallationType；
- SwitchState；
- 两个不同的 Terminal IDs；
- DisplayName 和当前已有必要业务属性。

设备类型、状态和图例相互分离：Domain 保存 Kind/State，Rendering.Wpf 映射 Symbol。Breaker 与 LoadSwitch 第一阶段行为可以相同，但身份不能合并。

Disconnector 的不允许带负荷操作和操作顺序属于受限工作票规则扩展边界。没有明确拓扑范围和专业规则时，不对任意网络实施全局硬校验。

Fuse 第一阶段复用 Open/Closed，不引入熔断、缺相或拆除状态。

## 8. Cable Connections

Cable 是两个合法 external terminals 之间的 Connection，不是通过画布线段碰撞形成的关系。

```text
external terminal A
        |
        | Cable Connection
        |
external terminal B
```

创建 Cable 必须满足：

- 两端 Terminal 均允许 `ConnectionType.Cable`；
- 两端 Terminal IDs 不相同；
- 两端均未违反连接数限制；
- endpoint owner 已存在于当前 Project；
- Connection 使用明确 Stable ID；
- 创建、删除通过 Command 进入 Undo/Redo/Dirty；
- 删除设备前必须处理现有 Connection，不能留下悬空 endpoint。

典型合法 endpoint 包括 RingCabinet interval external terminal 和 Pole 的 CableTerminal cable-side terminal。Cable 不直接连接 SwitchDevice internal terminal，也不通过虚拟 CableTermination Device 补齐拓扑。

## 9. OverheadLine Connections

OverheadLine 表达杆塔之间或合法架空 endpoint 之间的外部线路连接。

```text
Pole A overhead terminal
        |
        | OverheadLine Connection
        |
Pole B overhead terminal
```

创建 OverheadLine 必须满足：

- 两端 Terminal 均允许 `ConnectionType.OverheadLine`；
- endpoint 明确属于 Pole、柱上 SwitchDevice 或 CableTerminal 的架空侧能力；
- Attachment ownership 已明确，但 Attachment 本身不作为 Connection endpoint；
- 不根据杆塔屏幕距离自动连接；
- 不跨过 SwitchDevice 直接合并其两侧 ElectricalNode；
- Save/Reload、Undo/Redo 保持 Connection 与 endpoint Stable IDs。

若柱上 SwitchDevice 位于线路路径中，OverheadLine 分别连接设备两侧 Terminal。开关 Open/Closed 决定局部导通，而不是修改 OverheadLine 对象。

## 10. Transformer Boundary

Transformer 当前只作为未来末端设备的拓扑扩展位置，不进入当前 10kV 工作票第一阶段运行时模型。

“占位”仅表示架构允许未来某个合法 external terminal 连接 Transformer 的专用 terminal，不表示现在新增：

- Transformer Domain 类型或空对象；
- TransformerKind、容量、变比、接线组别或分接头字段；
- Transformer Symbol、Command、Persistence DTO 或 Template；
- Transformer 的内部低压拓扑；
- 将 Transformer 放入 SwitchKind。

未来实现时，Transformer 应是非 SwitchDevice 的专业末端设备，通过自己的 terminals 参与拓扑，并通过独立设计明确高压侧、低压侧、内部节点、Persistence 和工作票语义。

## 11. Work Ticket Information Scope

当前工作票绘图最小信息范围为：

### 11.1 Required Information

- Device/structure Stable ID 和可读名称；
- DeviceType、IntervalKind、SwitchKind；
- Pole/Attachment、Cabinet/Interval 等所有权关系；
- Terminal、ElectricalNode 和 Connection endpoints；
- SwitchState；
- 已确认 SwitchAssembly 内的有限联锁事实；
- 图纸位置和 RuntimeLayout identity；
- 工作票操作目标、操作类型与预期状态；
- Command history、Undo/Redo 和 Dirty 状态；
- V4 Persistence 所保存的当前工程事实。

### 11.2 Permitted Local Reasoning

系统可以：

- 判断两个 Terminal 是否由明确 Connection 相连；
- 判断同一固定结构内的 Terminal 是否共享 ElectricalNode；
- 根据 SwitchState 判断单台 SwitchDevice 两端是否局部导通；
- 区分 Breaker、LoadSwitch、Disconnector 和 Fuse；
- 对明确 SwitchAssembly 应用已确认的局部联锁；
- 为人工编制工作票提供设备选择、状态展示和有限操作提示。

### 11.3 Information Not Required

当前不保存或推断：

- Incoming、Outgoing、Tie、BayFunction；
- 电源侧、负荷侧、潮流方向和运行方式；
- 实时遥测、遥信、遥控；
- 保护定值、故障电流和动作原因；
- GIS 坐标、空间网络分析；
- 任意图元之间的自动拓扑推断。

## 12. Rendering and Persistence Boundaries

Rendering.Wpf 负责把 Domain 与 RuntimeLayout 投影为 Symbol、Scene 和 HitTest，不拥有拓扑真相。移动图形不能改变 Terminal/Connection identity；Symbol 变化不能改变 SwitchKind。

Persistence 保存生成后的工程事实和 Stable IDs。Project Restore 不依赖 TemplateLibrary，不根据 TemplateId 重新 Build，也不从图形坐标重建 Connection。

本阶段保持 FormatVersion 4 不变。CableTermination 向 CableTerminal capability 的未来迁移、Transformer 实现或新增结构字段均需要独立格式兼容评审。

## 13. Extension Interfaces

### 13.1 Low-Voltage Distribution Area

未来低压台区可以沿用同一核心关系：

```text
specialized device
  owns terminals
  terminals reference electrical nodes
  connections join compatible terminals
```

但低压侧设备、相制、回路、保护和计量规则不得提前塞入当前 10kV 核心。新增能力需要明确 Domain 消费者、Terminal contract、Rendering 和 Persistence 迁移。

### 13.2 Additional Device Types

新增设备类型必须满足：

- 是稳定物理设备事实，而不是运行方向标签；
- 有明确 Terminal topology；
- 有真实状态或结构消费者；
- 不可由现有 Kind 和固定结构无损表达；
- 明确 Domain、Rendering、Command、Inspector 和 Persistence 边界；
- 不通过自由 metadata 字典绕过类型设计。

新的可开断设备优先评估是否可扩展 SwitchKind；具有不同生命周期或拓扑合同的设备再使用专用模型。Attachment 只解决安装和所有权，不作为所有新设备的通用数据袋。

## 14. Explicit Non-Goals

本模型明确不做：

- 配网潮流、短路、可靠性或完整仿真；
- 继电保护定值、动作逻辑和故障分析；
- SCADA、遥测、遥信、遥控和事件顺序记录；
- GIS 定位、空间查询和地理网络分析；
- 自由 CAD 图元、任意连线和从几何反推拓扑；
- 自动生成完整工作票；
- 任意拓扑上的全局带电判断；
- BayFunction、Direction、Role 或 Source/Load 分类；
- Transformer 当前实现；
- 用户自由定义拓扑或设备脚本。

## 15. Risks and Guardrails

- 把 Attachment 当作 Connection 会混淆所有权与导通关系；
- 把视觉线段当作 Connection 会产生不可恢复的隐式拓扑；
- 把 ElectricalNode 和外部 Connection 合并为同一概念会破坏设备内部边界；
- 把 CableTerminal 建成可操作设备会引入虚假状态和 Command；
- 把 Switch Open/Closed 扩展为全网带电推理会超出当前能力；
- 把 Transformer “占位”实现为空类型会污染 Domain 和 V4；
- 修改 IntervalKind 固定结构时若不升级兼容合同，会破坏历史工程；
- Stable ID 迁移不得通过重新创建对象完成。

## 16. Final Architecture Decision

支撑 10kV 工作票绘图的最小拓扑核心冻结为：

```text
Device / structural owner
        owns
Terminal + ElectricalNode
        connected by
Connection (Cable / OverheadLine)

SwitchDevice Kind + State
        controls only
its local two-terminal conductivity
```

RingCabinet 使用 Cabinet → Interval → Switch/Terminal 的固定聚合结构。Pole 是主体设备，PoleAttachment 表达附属关系，柱上 SwitchDevice 是可控制开断设备，CableTerminal 是不可操作的连接端子能力。Transformer 只保留未来末端设备扩展边界，不进入当前实现。

该模型服务于具有基础电气拓扑识别能力的 10kV 工作票绘图工具，明确不是配网仿真、继保、SCADA、GIS 或自由 CAD 系统。
