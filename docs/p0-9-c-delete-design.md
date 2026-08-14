# P0-9-C-1 Delete Design

## 1. 删除边界

删除是对当前工程对象及其图形布局的原子编辑操作。删除必须同时考虑 Domain 引用、Layout 条目、Selection 和后续 Persistence，而不是只从画布列表移除图元。

第一版允许删除：

- `RingCabinet`；
- `Pole`；
- `PoleAttachment`。

`CableSegment` 需要谨慎处理。它连接两个 Terminal，并对应一个 Connection；在未定义完整电缆删除、拓扑断开和工作票语义前，不提供普通直接删除入口。未来应使用专门的 Cable 删除/断开 Command，并明确 Graph 和历史影响。

以下对象禁止直接删除：

- `Terminal`：由设备或拓扑元素拥有，不能脱离 Owner 单独删除；
- `ElectricalNode`：表示内部电气结构，必须随合法 Owner 生命周期处理；
- `Connection`：表示电气拓扑事实，不能通过普通图形删除操作直接移除。

## 2. 聚合删除策略

删除 `RingCabinet` 时，必须以其 Domain Aggregate 为边界处理内部 Interval、SwitchDevice、Terminal、ElectricalNode 和 SwitchAssembly，同时删除对应 `RingCabinetLayout`。如果任一外部 Connection、工作票边界或其他专业对象仍引用其对象，操作应失败并保持工程不变。

删除 `Pole` 时，必须检查 PoleAttachment、附属 SwitchDevice/CableTermination、Terminal、架空线和专业边界引用。Pole 的图形布局和合法子对象只能作为一个一致的删除快照处理。

删除 `PoleAttachment` 时，应删除安装关系对应的 Layout 条目；附属设备本身是否删除必须由明确的设备生命周期策略决定，不能因为删除安装关系而隐式删除仍被其他对象引用的 Device。

## 3. DeleteCommand

未来可按对象类型提供明确 Command，例如：

- `DeleteRingCabinetCommand`；
- `DeletePoleCommand`；
- `DeletePoleAttachmentCommand`。

每个 Command 构造时捕获稳定 ID、Before Snapshot 和必要的 Layout 快照。构造阶段不改变工程。

`Execute` 应执行引用检查，然后原子移除 Domain 对象和相应 Layout。任何检查失败都不得留下部分删除结果。

`Undo` 使用 Before Snapshot 恢复原对象、原引用、原 Layout 和原 Stable ID。`Redo` 重复首次删除，不生成新 ID。删除历史不应通过 CreationFactory 重新构造出不同对象，也不应改变未被删除对象的拓扑或状态。

`After State` 表示删除完成后的工程状态：目标对象及其允许删除的布局条目不存在，其他对象保持原状态。CommandStack 只保存 Command，不保存不可重建的 UI 选择状态。

## 4. 引用保护

删除前必须检查至少以下引用：

- Connection 是否使用目标设备拥有的 Terminal；
- CableSegment 或 OverheadLine 是否引用相关 Connection/Terminal；
- PoleAttachment 是否引用 Pole 或附属设备；
- WorkScope、BoundaryPoint、GroundingPoint 等专业对象是否引用目标；
- Layout 是否存在且与 Domain Stable ID 对齐。

存在活动引用时，第一版默认拒绝删除并返回明确失败结果。不得自动删除 Connection、Terminal、ElectricalNode、CableSegment 或其他引用者来“帮助完成”删除。

删除成功后，工程应满足：没有悬空引用、没有孤立 Layout、没有仍指向已删除 Stable ID 的 SelectionTarget。引用检查是 Domain/Document 一致性职责；Rendering 只负责应用布局删除，不绕过 Domain 保护。

## 5. Selection 集成

删除发起前，Selection 提供目标 `SelectionTarget`。Command 成功执行后，如果当前选择仍指向被删除对象，交互层必须调用 `SelectionService.Clear()`。

如果引用保护导致删除失败，Selection 保持不变，便于用户继续查看和处理阻断原因。Undo 恢复对象后，是否自动恢复选择属于交互体验策略，不应改变 Domain Command 的语义；默认不把 Selection 变化写入 Undo 历史。

## 6. Persistence 影响

删除后的保存结果只包含当前工程状态，不保存删除操作历史、Before Snapshot、Undo 栈或 Redo 栈。

如果 V6 已能表达被删除对象及其当前 Layout，正常删除不需要升级工程格式。保存前必须完成引用校验，确保不会写出指向不存在 Stable ID 的 DTO。打开旧工程仍按既有迁移链恢复，不通过删除流程重建对象。

如果未来 Cable 删除需要表达拓扑断开、历史施工状态或中间节点生命周期，应先进行独立 Persistence Gap Analysis，再决定扩展现有格式或新增版本；本设计不临时改变 V6。

## 7. Rendering 与 Layout 边界

Renderer 不创建 Domain，也不决定对象是否可删除。删除成功后，Runtime Layout 移除对应条目，下一次 Scene 构建自然不再生成该对象图形。

删除 RingCabinet 或 Pole 时，相关附属图形应按 Domain/Attachment 所有关系一起刷新。Terminal、ElectricalNode 和 Connection 没有独立删除图形操作；它们是否仍存在由 Domain 生命周期和拓扑引用决定。

## 8. 明确不实现

本阶段不包含：

- 新建设备；
- 属性编辑；
- CableSegment 普通直接删除；
- Terminal、ElectricalNode、Connection 单独删除；
- Cable Split/Reconnect；
- 自动清理外部引用；
- 删除历史持久化。

## 9. 后续实施建议

建议先实现 `RingCabinet` 与 `Pole` 的引用检查和原子删除，再实现 `PoleAttachment` 的安装关系删除，最后单独评估 Cable 生命周期。每一步都应覆盖 Execute、引用失败、Undo、Redo、Selection 清理和保存后重新加载验证。
