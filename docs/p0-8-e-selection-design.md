# P0-8-E-1 Selection Model Design

## 1. Context and Goals

项目已经具备 RingCabinet、Pole、CableSegment、IntermediateTerminal 及其 Rendering。下一步需要建立从图形元素到 Domain 对象的稳定选择映射。

本阶段只冻结 Selection 模型，不实现选择运行时、命中测试增强或 Inspector UI。

目标是让用户点击图形后，系统能够得到明确的 `SelectionTarget`，并通过 Stable ID 定位对应对象。

## 2. Selection Boundary

Selection 属于 Rendering/Application 交互层，负责：

- 图形命中结果到 Domain 身份的映射；
- 当前选择状态；
- 选择变化通知；
- 为未来 Inspector 提供解析入口。

Selection 不属于 Domain。选择操作不得修改：

- Domain 对象；
- Terminal、Connection 或 ElectricalNode；
- SwitchState；
- 拓扑关系；
- Stable ID。

Selection 变化不是 Domain Command，不进入 Undo/Redo 历史，也不导致工程 Dirty。

## 3. SelectionTarget Model

第一版定义不可变的 `SelectionTarget`，至少包含：

- `TargetKind`：被选择对象的类别；
- `TargetId`：对象 Stable ID；
- 可选的 `ParentId`：用于 Interval、SwitchDevice 或 PoleAttachment 的层级上下文；
- 可选的命中区域或 Scene 引用信息。

目标类别覆盖：

- `RingCabinet`；
- `RingCabinetInterval`；
- `SwitchDevice`；
- `Pole`；
- `PoleAttachment`；
- `CableSegment`；
- `IntermediateTerminal`。

当前代码已有 `SelectionReference`、`SelectionTargetKind` 和 `SelectionHitTestEntry`。后续 Runtime 应在不破坏既有 Device、Connection、Terminal 等选择兼容性的前提下，扩展这些类型以表达上述目标。SelectionTarget 不复制 Domain 对象，也不生成新的 ID。

## 4. Stable ID Contract

每个可选择目标必须使用其既有 Domain Stable ID：

| 图形对象 | Selection Target | TargetId |
| --- | --- | --- |
| 柜体图形 | RingCabinet | RingCabinet.Id |
| 间隔图形 | RingCabinetInterval | Interval.Id |
| 开关符号 | SwitchDevice | SwitchDevice.Id |
| 杆体图形 | Pole | Pole.Id |
| 附属关系图形 | PoleAttachment | AttachmentId |
| 电缆线 | CableSegment | CableSegment.Id |
| 接头符号 | IntermediateTerminal | IntermediateTerminal.Id |

同一 Domain 对象可以对应多个 SceneElement 或多个命中区域，但它们应解析到同一个 SelectionTarget。Rendering 不通过坐标、显示名称或图形顺序推导对象身份。

## 5. SceneElement Mapping

当前 `SceneElement` 是图形几何记录，现有 `DrawingScene` 另有 `SelectionHitTestIndex`。设计上，选择元数据应通过以下任一等价方式与图形关联：

1. 为 SceneElement 增加不可变的 Selection metadata；或
2. 由 Scene 构建阶段同步生成 `SelectionHitTestEntry`，以相同的 SelectionTarget 关联几何区域。

第一版优先复用现有 `SelectionHitTestEntry` / `SelectionReference`，避免为了保存元数据而改变所有既有 SceneElement 构造器。无论采用哪种实现，选择映射必须包含：

- `SelectionId` 或等价稳定映射键；
- `TargetKind`；
- `TargetId`；
- 必要时的 `ParentId`。

一个设备的外框、符号、标签可以共享一个 TargetId；电缆线段应使用 CableSegment.Id，Joint 应使用 IntermediateTerminal.Id，而不是使用 Connection.Id 代替业务对象身份。

## 6. HitTest Design

未来点击流程为：

```text
Pointer position
    ↓
SelectionHitTestIndex.HitTest
    ↓
SelectionHitTestEntry
    ↓
SelectionTarget
    ↓
Application/Inspector resolver
```

命中区域按图形类型建立：

- RingCabinet：柜体外框；
- RingCabinetInterval：间隔区域；
- SwitchDevice：开关符号及其可点击范围；
- Pole：杆体；
- PoleAttachment：附属设备符号或安装区域；
- CableSegment：电缆线段的容差区域；
- IntermediateTerminal：Joint 符号区域。

当多个区域重叠时，使用明确的 Priority：更具体的目标优先于容器目标，例如 SwitchDevice 优先于 Pole，IntermediateTerminal 优先于 CableSegment。命中排序只影响交互，不改变 Domain 顺序或拓扑。

## 7. Selection State

设计 `SelectionService` 或等价交互服务，负责：

- 保存当前 `SelectionTarget?`；
- `Select(target)`；
- `Clear()`；
- 在实际目标变化时发出 SelectionChanged 事件；
- 避免重复选择产生无意义通知。

当前代码已有 `SelectionManager`，其 `Selected`、`Select`、`Clear` 和 `SelectionChanged` 已体现最小状态边界。后续 Runtime 可在保持该行为的基础上将 `SelectionReference` 逐步对齐为更完整的 SelectionTarget，不应让服务直接持有可变 Domain 状态。

SelectionService 不负责：

- 创建或删除设备；
- 修改 SwitchState；
- 修改 CableSegment 端点；
- 执行 Split/Reconnect；
- 写入 Persistence；
- 管理 CommandStack。

## 8. Inspector Preparation

SelectionTarget 是未来 Inspector 的唯一解析入口。Inspector 可以根据 `TargetKind` 和 `TargetId`，在当前 `DrawingDocument` 中查找：

- RingCabinet；
- Interval；
- SwitchDevice；
- Pole；
- PoleAttachment；
- CableSegment；
- IntermediateTerminal。

Inspector 只读取选择目标对应的 Domain 和 Layout 信息。属性编辑若未来需要修改 Domain，必须通过既有 Application/Command 边界完成；本阶段不实现任何 Inspector UI 或属性命令。

## 9. Domain and Topology Safety

Selection 不能因为点击图形而改变电气事实：

- 不创建 Connection；
- 不删除 Terminal；
- 不改变 ElectricalNode；
- 不改变 SwitchState；
- 不触发 Connectivity Graph 重建以外的 Domain 行为；
- 不把选择关系持久化为拓扑关系。

Graph 查询仍然读取当前 Domain 状态。选择某个 CableSegment 或 Joint 只表示用户关注该对象，不代表建立、断开或修改电气连接。

## 10. Rendering Integration

RingCabinetRenderer、PoleRenderer、CableRenderer 和 JointRenderer 继续负责生成图形。后续 Scene 构建阶段应为它们生成相应的命中区域和 SelectionTarget：

- 柜体与间隔符号映射到各自容器对象；
- Switch Symbol 映射到独立 SwitchDevice；
- Pole Attachment Symbol 映射到 PoleAttachment 或其附属 SwitchDevice；
- Cable Symbol 映射到 CableSegment；
- Joint Symbol 映射到 IntermediateTerminal。

Renderer 不执行选择，也不在渲染期间修改 SelectionService。Selection 输入来自 HitTest，Selection 状态由交互层管理。

## 11. Undo/Redo and Dirty Boundary

Selection 变化是临时交互状态：

- 不创建 Domain Command；
- 不进入 Undo 栈或 Redo 栈；
- 不修改 Project Persistence；
- 不导致 DrawingDocument Dirty；
- 清除选择不会影响工程内容。

真正的设备创建、状态修改、Cable Split/Reconnect 等操作仍通过各自 Command，并由 CommandStack 管理。

## 12. Future Selection Scenarios

第一版后续验证应覆盖：

1. 点击 RingCabinet，得到 RingCabinet Target；
2. 点击 Interval，得到 Interval Target；
3. 点击开关符号，得到 SwitchDevice Target；
4. 点击 Pole 或 PoleAttachment，得到对应 Target；
5. 点击 CableSegment，得到 CableSegment Target；
6. 点击 Joint，得到 IntermediateTerminal Target；
7. 空白处点击后清除当前选择；
8. 选择变化不改变 Domain、Graph、SwitchState、Dirty 或 Undo/Redo。

## 13. Non-Goals

本阶段不实现：

- Inspector UI；
- Selection Runtime；
- Hit Testing Runtime 扩展；
- 鼠标编辑操作；
- Selection Persistence；
- Selection Undo/Redo；
- 多选、框选、套索选择；
- 拓扑编辑；
- 自动修改 SwitchState。

## 14. Follow-up Slices

### P0-8-E-2 Selection Runtime

实现 SelectionTarget、SelectionService 与现有 SelectionManager 的最小运行时闭环。

### P0-8-E-3 Hit Testing

为 RingCabinet、Pole、SwitchDevice、CableSegment 和 IntermediateTerminal 建立图形命中区域及优先级。

### P0-8-E-4 Inspector

基于 SelectionTarget 解析 Domain 对象并显示只读属性；属性编辑继续通过 Command 边界实现。

## 15. Final Design Decision

第一版 Selection 采用：

```text
SceneElement / HitTestEntry
    ↓ stable mapping
SelectionTarget
    ↓ TargetKind + TargetId
SelectionService
    ↓
Inspector / future interaction
```

Selection 是 Rendering/Application 交互层的只读对象定位机制，不是 Domain 状态，不是拓扑关系，也不是 Command。所有目标均复用既有 Stable ID；选择变化不修改 Domain、不进入 Undo/Redo、不持久化。
