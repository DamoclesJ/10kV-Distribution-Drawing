# P0-9-A Move Design

## 1. 目标与边界

移动是绘图编辑行为。它改变图形布局，不改变设备身份、电气属性或拓扑事实。

Domain 保存稳定的业务事实：设备、间隔、开关、端子、节点、连接和开关状态。Rendering/Layout 保存图形事实：位置、尺寸、相对偏移、标签偏移和线路端点。移动流程只更新 Layout，并通过现有 Command 边界纳入文档的编辑生命周期。

Renderer 继续只读 Domain 和 Layout，不在移动过程中创建或重建 Domain 对象。

## 2. 可移动对象

第一版允许移动以下对象：

- `RingCabinet`：移动 `RingCabinetLayout.Position`；间隔保持相对排列。
- `Pole`：移动 `PoleLayout.Position`；杆上附属能力随主体布局计算。
- `PoleAttachment`：移动 `AttachmentLayout.Offset`，但不能脱离所属 Pole 的布局坐标系。
- `CableLayout`：移动或调整电缆图形路径/端点布局；不得改变 `CableSegment` 的端子引用或 `Connection`。

以下对象不可直接移动：

- `Terminal`：它是拓扑端点，不是独立图形设备。
- `ElectricalNode`：它表示固定内部导通区域。
- `Connection`：它表示电气连接事实；改接属于独立拓扑 Command，不是拖拽移动。

移动 RingCabinet 或 Pole 时，子图形使用相对布局重新投影。子对象不会因此产生独立 Domain 变更。

## 3. Layout 与坐标合同

移动的输入是屏幕坐标转换后的文档坐标位移：

```text
delta = currentDocumentPoint - mouseDownDocumentPoint
after = before + delta
```

所有坐标必须使用有限值，并由 Layout 类型保持自身尺寸、边界和相对坐标合同。移动不排序间隔、不修改 `BayIndex`，也不改变 Sequence。

对于 `PoleAttachment`，`after.Offset` 是相对于 Pole 的位置；Pole 移动时，Attachment 的绝对显示位置随 Pole 一起变化。

对于 `CableLayout`，端点应由当前 Layout/锚点解析得到。移动线段图形不等于 Reconnect；只有明确的 Reconnect Command 才能改变 CableSegment 的端子关系。

## 4. MoveCommand

未来提供按布局类型分工的 Command，或由一个明确的 Layout Move Command 统一承载：

- `MoveRingCabinetCommand`
- `MovePoleCommand`
- `MovePoleAttachmentCommand`
- `MoveCableLayoutCommand`

每个 Command 在构造时保存稳定对象 ID、`before` Layout 和 `after` Layout。构造阶段不修改 RuntimeLayoutDocument。

`Execute` 应用 `after` Layout；`Undo` 恢复 `before` Layout；`Redo` 再次应用同一个 `after` Layout。Redo 不生成 ID、不创建 Domain 对象、不重新计算电气拓扑。Command 只操作现有 RuntimeLayoutDocument，并拒绝目标不存在或布局归属不一致的输入。

一次用户拖拽产生一个原子移动 Command，而不是为每个 MouseMove 产生一个 Undo 项。

## 5. Drag 流程

```text
MouseDown
  -> HitTest
  -> SelectionTarget
  -> capture target layout snapshot

MouseMove*
  -> convert pointer to document coordinates
  -> preview layout only
  -> render current preview

MouseUp
  -> validate final layout
  -> create one MoveCommand(before, after)
  -> execute and push to CommandStack
```

MouseMove 期间的预览可以暂存于交互层或临时 Layout，不写入 Domain，也不进入 Undo 历史。MouseUp 取消、无位移或校验失败时，不产生 Command。

## 6. Selection 集成

Selection 提供被拖拽对象的 `SelectionTarget` 和稳定 ID。HitTest 只决定目标，不直接移动对象。

拖拽控制器根据 `TargetKind` 选择可移动策略：

- RingCabinet → Cabinet Layout
- Pole → Pole Layout
- PoleAttachment → Attachment Layout
- CableSegment → Cable Layout

选中 Terminal、ElectricalNode 或 Connection 时，不启动普通移动流程。Selection 变化本身不进入 Undo；只有 MouseUp 形成的布局 Command 才进入 Undo/Redo。

## 7. Persistence 影响

移动后的 Layout 是当前工程的可恢复图形状态，应由现有 Layout 持久化边界保存；Domain 电气对象不增加位置属性。保存的是 MouseUp 后的最终 Layout，不保存 MouseMove 预览、拖拽过程或 Command 历史。

如果当前 V6 工程格式已经保存对应 Layout，移动不需要升级格式版本。若某类 Layout 尚未持久化，应在实施前做独立 Persistence Gap Analysis，不得在 Move Runtime 中临时改变 V6 DTO 或拓扑格式。

加载后应保持：

- Domain Stable ID 不变；
- Terminal、ElectricalNode、Connection 不变；
- Layout 位置与保存前一致；
- Graph 查询结果不因纯移动而改变。

## 8. 明确不实现

本阶段设计不包含：

- 新建设备；
- 删除设备；
- 属性编辑；
- 改变 Terminal、ElectricalNode 或 Connection；
- Cable Split/Reconnect；
- 自动避让或拓扑重排；
- 自由 CAD 几何编辑。

## 9. 后续实施建议

建议按以下顺序实施：

1. RuntimeLayout 的 Move API 与不可变快照；
2. RingCabinet/Pole/Attachment Move Command；
3. CableLayout Move Command；
4. Drag Controller 与 Selection/HitTest 集成；
5. Layout Persistence round-trip 验证；
6. Windows/WPF 交互回归测试。
