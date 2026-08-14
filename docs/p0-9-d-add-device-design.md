# P0-9-D-1 Add Device Design

## 1. 新增范围

第一阶段支持从画布创建两类主体设备：

- `RingCabinet`；
- `Pole`。

暂不支持通过通用新增入口创建：

- `CableSegment`；
- `Terminal`；
- `SwitchAttachment`。

Terminal 是设备或拓扑元素拥有的端点，不能作为独立图形设备新增。开关附属能力应通过后续 Pole Attachment 专用流程创建，不能在 Pole 新增时隐式塞入未经确认的设备组合。

## 2. 创建来源与边界

创建入口属于 Application/Rendering Interaction 层。用户输入模板、设备参数和画布位置后，由 Creation Factory 构造合法 Domain 对象和对应 Layout，再交给 Add Command 执行。

UI 或 Renderer 不直接把用户输入写入 Domain。Factory 负责生成初始 Stable ID 和最小合法结构；Command 负责将已经构造好的对象注册进 `DrawingDocument` 与 `RuntimeLayoutDocument`。

## 3. AddCommand

未来可继续使用按对象类型分工的 Command：

- `AddRingCabinetCommand`；
- `AddPoleCommand`。

每个 Command 在构造时保存：

- 创建的 Domain 对象；
- 其必要的内部 Terminal/结构对象；
- 对应 Layout；
- Stable ID。

`Execute` 原子地注册 Domain 对象和 Layout。任一注册失败时回滚已完成的注册，不留下半个设备或孤立布局。

`Undo` 移除本次创建的 Domain 对象及 Layout；`Redo` 使用第一次创建时保存的同一对象和同一 Layout，不重新生成 Stable ID，不重新调用 Creation Factory。

Add Command 进入现有 `CommandStack`，因此创建、Undo、Redo 和 Dirty 状态保持统一。

## 4. Layout 创建

新增对象必须同时拥有对应 Layout：

- RingCabinet → `RingCabinetLayout`；
- Pole → `PoleLayout`。

Layout 的位置来自用户插入点，尺寸与内部排列由现有 Layout Factory/模板规则生成。Layout 只包含图形坐标、尺寸和相对布局，不写入 Domain 的电气属性。

RingCabinet 的间隔布局随柜体模板生成；Pole 的 Attachment 布局在后续附属能力创建时追加。新增主体设备本身不自动创建 Cable、Connection 或额外拓扑。

## 5. Selection 集成

创建 Command 成功执行后，交互层自动创建并选中对应的 `SelectionTarget`：

- RingCabinet → `SelectionTargetKind.RingCabinet`；
- Pole → `SelectionTargetKind.Pole`。

Selection 只保存稳定 ID 和目标类型，不持有 Domain 引用。Selection 变化不是 Domain Command，但创建 Command 本身进入 Undo/Redo；Undo 删除对象后应清除指向该对象的 Selection，Redo 成功后可重新选中同一 Stable ID。

如果创建失败，当前 Selection 不应被清除或替换。

## 6. Persistence 影响

新增 RingCabinet/Pole 使用现有 Domain 与 Layout 持久化边界，不引入新的工程对象种类，因此无需格式版本升级。

保存的是创建成功后的当前工程状态，不保存 Creation Factory、Add Command、Undo 栈或 Redo 栈。V6 文件打开后应继续恢复相同 Domain Stable ID、内部 Terminal/Node 和对应 Layout。

如果未来新增对象需要新的 DTO 或拓扑关系，应先做独立 Persistence Gap Analysis；本阶段不借新增入口临时改变 V6 格式。

## 7. 引用与一致性保护

创建前应校验：

- Stable ID 不与现有工程对象冲突；
- Layout ID 与 Domain 对象 ID 一致；
- RingCabinet 模板和 Pole 基础参数合法；
- 必要的内部 Terminal/Node 归属关系完整。

创建后的 `DrawingDocument` 不应出现重复 ID、悬空 Layout 或未注册的内部结构。

## 8. 明确非目标

本阶段不实现：

- Cable 创建；
- Terminal 独立创建；
- SwitchAttachment 创建；
- 属性编辑；
- 自动拓扑连接；
- 自由 CAD 对象；
- 删除对象。

## 9. 后续实施建议

建议先验证 RingCabinet 与 Pole 的 Add Command 在 CommandStack 中完成 Execute/Undo/Redo，再接入 Selection 自动选中和创建失败回滚，最后补充 V6 round-trip 验证。Cable、Terminal 和 Attachment 继续沿各自领域生命周期设计。
