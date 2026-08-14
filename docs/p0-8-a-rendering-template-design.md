# P0-8-A Rendering Template Design

## 1. Context

项目已经具备 Domain、Terminal-centric Connectivity Graph、SwitchDevice、CableSegment、IntermediateTerminal，以及 FormatVersion 6 Persistence。本阶段开始定义绘图层的第一版模板边界。

目标是为 10kV 工作票绘图提供稳定、可读、可选择的设备图形表达。Rendering 消费 Domain 中已经存在的电气事实，不重新定义这些事实。

## 2. Rendering 总体边界

Domain 负责：

- 电气事实；
- 设备与附属关系；
- Terminal、Connection、ElectricalNode；
- SwitchState；
- Stable ID。

Rendering 负责：

- 图形符号；
- 坐标与尺寸；
- 设备和附属对象的布局；
- 开关状态的显示；
- 命中区域和未来的选择映射。

Rendering 不得创建、修改或删除 Domain 对象。模板、符号和布局对象属于显示层；用户操作产生的 Domain 变化必须通过 Application/Command 边界完成。

Rendering 可以读取 Domain 状态并生成临时的 Scene/Render Model，但不持久化自己的布局事实，除非未来明确增加独立的图形文档模型。

## 3. Template、Symbol 与 Layout 模型

第一版 Rendering 使用三类概念：

- Template：描述一类设备或设备组合应如何显示；
- Symbol：描述单个设备、开关、端子或连接的图形表示；
- Layout：描述符号的相对位置、尺寸、锚点和连接路径。

Rendering Template 的输入是已经构造完成的 Domain 对象或现有 Application Template 实例。Rendering Template 不负责 Domain Build、Stable ID 生成或设备创建。

### 3.1 设备模板

第一版规划以下显示模板：

- `RingCabinetTemplate`：根据 RingCabinet、Interval、SwitchDevice 和 Terminal 生成环网柜图形；
- `PoleTemplate`：生成杆塔主体及其安装位置；
- `AttachmentTemplate`：在 Pole 的局部坐标系中生成一个或多个 PoleAttachment 的图形；
- `CableTemplate`：根据 CableSegment 及其端点生成电缆线段。

同一 Domain 类型可以对应多个 Rendering Symbol 变体。显示名称、图例样式和坐标不作为 Domain 身份。

## 4. RingCabinet Rendering Template

RingCabinet 的外框、母线、间隔和设备符号按 Domain 的聚合结构生成。每个 Interval 使用稳定的间隔顺序和 BayIndex 作为显示输入，但 Rendering 不修改这两个 Domain 值。

### 4.1 普通复合开关环网柜

普通复合开关间隔包含：

- LoadSwitch：主回路开关；
- EarthSwitch：接地刀。

建议显示编号：

- `负X`：主开关；
- `负X-7`：接地刀。

其中 `X` 来自该间隔的显示编号或业务编号映射。编号映射属于 Rendering/显示规则，不重新引入 BayFunction。

状态显示规则：

| Interval 状态 | LoadSwitch | EarthSwitch |
| --- | --- | --- |
| Running | Closed（合） | Open（分） |
| Grounded | Open（分） | Closed（合） |
| Open | Open（分） | Open（分） |

状态组合必须根据当前 Interval 内相关开关的真实 `SwitchState` 判断。Rendering 不自行改变状态，也不替 Domain 执行联锁校验。

### 4.2 一二次融合断路器环网柜

一二次融合断路器间隔包含：

- Disconnector：隔离刀；
- EarthSwitch：接地刀；
- CircuitBreaker：断路器。

建议显示编号：

- `负X-4`：隔离刀；
- `负X-47`：接地刀；
- `负X`：断路器。

Interval 状态由三个开关的实际状态组合决定。Rendering 只负责将组合投影为图形状态；分合操作、隔离和接地约束仍属于 Domain/Application 的操作规则。

### 4.3 PT 间隔

PT 间隔作为未来结构型间隔的 Rendering 边界预留：

- `负7-2`：PT 隔离刀；
- `负7`：PT；
- `负7-7`：PT 接地刀。

PT 不是 BayFunction，也不应通过旧的 Function 字段表达。当前 PT Domain 已通过 `IntervalKind.PTInterval` 和独立的 `SwitchDevice` 结构表达；Rendering 只消费这些既有对象，不创建 PT Domain 对象。

## 5. Pole Rendering Template

Pole 是主体设备，PoleAttachment 表示安装关系和附属能力。Rendering 不把 PoleType 解释为互斥的设备功能类型。

第一版显示模板覆盖：

- 普通杆；
- 电缆终端杆；
- 隔离刀闸杆；
- 断路器杆；
- 隔离刀闸 + 电缆终端杆。

最后一种是组合场景：一个 Pole 上同时显示 SwitchDevice Attachment 和 CableTerminal Attachment。一个 Pole 可以拥有多个 Attachment，Attachment 的显示顺序和局部坐标由 Rendering Layout 决定，不能改变 Domain 中的安装关系。

## 6. Switch Symbol 映射

Rendering 建立 `SwitchKind` 到符号的映射：

| SwitchKind | Rendering Symbol |
| --- | --- |
| CircuitBreaker | 断路器符号 |
| LoadSwitch | 负荷开关符号 |
| Disconnector | 隔离刀闸符号 |
| EarthSwitch | 接地刀符号 |

设备类型和显示符号保持分离。同一种 SwitchKind 未来可以根据图纸标准使用不同 Symbol Style，但不会改变 Domain 设备类型。

每个符号根据真实 `SwitchState` 显示：

- Closed：合；
- Open：分。

符号显示不构成状态修改入口。鼠标或命令交互若在后续实现，必须调用已有的 Domain/Application 操作边界。

## 7. 接地刀显示规则

EarthSwitch 不是主回路串联设备。它表示线路侧 Terminal 到 GroundingPoint 的接地支路。

Rendering 应将接地刀绘制为独立的接地支路，并显示其 Open/Closed 状态。它不应被排列为普通主回路串联开关，也不应将其图形位置误读为主回路经过设备。

该显示规则与 Graph 边界一致：Graph 不生成“母线—地”的普通主回路边。接地状态的专业语义由 Domain/Topology 规则负责；Rendering 只投影已有状态和结构，不自行创建 ElectricalNode、Connection 或 GroundingPoint。

## 8. Layout 模型

布局模型不进入 Domain。第一版规划三个显示层次：

- `DeviceLayout`：设备外框、主体符号、主端子锚点和整体尺寸；
- `AttachmentLayout`：PoleAttachment 相对 Pole 的位置、方向、间距和附属端子锚点；
- `CableLayout`：CableSegment 两端之间的路径、端点锚点和拆分点显示。

Layout 应使用稳定的局部坐标规则：

- RingCabinet 先确定柜体边界，再按 Interval 顺序布置间隔；
- Pole 先确定杆体和主锚点，再布置 Attachments；
- Cable 由两端 Terminal 的 Render Anchor 连接，不改变端点关系；
- IntermediateTerminal 未来可显示为拓扑断点或接头圆点，但不是设备符号。

布局计算可以生成临时的 Scene 数据，但不能把坐标写回 Domain，也不能在 Rendering 中通过坐标推断 Incoming、Outgoing 或其他业务角色。

## 9. Selection 准备

后续 Selection 需要能够命中并解析以下对象：

- 环网柜；
- 间隔；
- 开关；
- 杆；
- 电缆；
- IntermediateTerminal（作为拓扑点而非设备）。

每个可选择图形应携带对应 Domain/Application Stable ID 和对象类别。Selection 不在本阶段实现，Rendering 也不通过视觉层级创建新的 Domain 对象。

## 10. Domain 与 Rendering 的责任映射

| Layer | Responsibility |
| --- | --- |
| Domain | 设备、Terminal、Connection、SwitchState、拓扑事实 |
| Application | Template 消费、Build/Command、查询和操作入口 |
| Rendering.Wpf | Symbol、Layout、Scene、HitTest 映射 |
| Desktop | Window、交互编排、未来选择和命令调用 |
| Infrastructure | V6 工程文件保存/恢复，不保存临时 Rendering Layout |

Rendering 可以消费 V6 恢复后的 Domain 对象并重新生成 Scene。保存/恢复的是工程事实，而不是 Undo/Redo 历史或临时绘制对象。

## 11. 明确非目标

本阶段不实现：

- Domain 对象创建或修改；
- 自由 CAD 编辑；
- 任意拓扑生成；
- 用户自定义模板编辑器；
- 潮流计算、短路计算或配网仿真；
- SCADA、GIS 或继保系统；
- PT Domain 实现；
- Cable Split/Reconnect 的 Rendering 交互。

## 12. 后续开发计划

### P0-8-B RingCabinet Rendering

实现环网柜外框、Interval、开关符号、状态显示和接地支路显示。

### P0-8-C Pole Rendering

实现 Pole 主体、组合 PoleAttachment 和杆上 SwitchDevice/CableTerminal 的布局。

### P0-8-D Cable Rendering

实现 CableSegment、Connection、IntermediateTerminal 的图形路径和端点锚点。

### P0-8-E Selection

实现图形命中到 Stable ID 的解析，并通过 Application 命令边界进入后续编辑操作。

## 13. Final Design Decision

第一版 Rendering 采用“Domain 事实 + Rendering Template/Symbol/Layout”的分层模型：

- Domain 保存电气和设备事实；
- Rendering 根据事实生成图形；
- Template 决定设备类别的显示结构；
- Symbol 决定单个设备的图例；
- Layout 决定坐标、尺寸和锚点；
- Selection 未来通过 Stable ID 回到 Domain/Application。

该设计支持普通复合开关柜、一二次融合断路器柜、Pole 多 Attachment 组合和 Cable 拓扑显示，同时不重新引入 BayFunction，也不把绘图层扩展为电网仿真或自由 CAD 系统。
