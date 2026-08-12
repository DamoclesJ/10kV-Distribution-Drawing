# Drawing Core P0-3：Terminal-based Connection Editor 设计

> 文档状态：实现前设计，不修改生产代码、工程格式或既有设计文档。
> 审查基线：`main@fb0477c`。

## 1. 结论与阶段建议

当前拓扑模型足以实现一个受限但真实的架空线路闭环：用户显式选择两个已有外部 Terminal，创建同一稳定 ID 的 `Connection + OverheadLine`，并从当前 TerminalAnchor 派生直线端点。外部连接不创建、不合并 ElectricalNode。

当前 Cable 只由 `ConnectionType.Cable` 表达连接类别，没有独立 Cable 实体、CableLayout、线路明细属性或正式 Rendering 链路。因此建议：

- P0-3-B：先实现 OverheadLine 的 Terminal Pick、创建、删除、Undo/Redo、保存恢复；
- P0-3-C：专业确认 Cable 实体/明细和图元边界后再实现 Cable；
- 不为赶进度临时发明 Cable Domain 模型。

P0-3-B 第一版以 Pole 外部架空锚点之间的直线为主；RingCabinet 外部 Terminal 到 Pole/CableTermination 架空侧 Terminal 可在支持杆塔顺序能够明确时接入。RingCabinet 到 RingCabinet 的纯架空连接无法满足当前 `OverheadLine.SupportPoleIds` 至少一个杆塔的约束，第一版拒绝。

## 2. 当前 Topology 模型审查

### 2.1 Connection 的真实语义

`Connection` 是工程设备之间的一条外部电气连接事实，保存：

- 稳定 `Connection.Id`；
- `ConnectionType`；
- `StartTerminalId` / `EndTerminalId`；
- 名称和电压等级。

它直接引用两个 Terminal。构造器拒绝相同端点，`DrawingDocument.AddConnection` 解析两端 Terminal，并校验允许的连接类型、电压等级和单连接占用限制。

### 2.2 ElectricalNode 的职责

`ElectricalNode` 表达设备或内部聚合内的固定拓扑：环网柜母线、回路、接地节点，以及 CableTermination 内部固定导通。Terminal 可以引用一个内部 ElectricalNode；`AddTerminal` 会把 Terminal 挂到该 Node。

当前 `AddConnection` 不创建 ElectricalNode、不修改 Terminal 的 `ElectricalNodeId`、不合并 Node。由此可得：

```text
设备内部固定关系 = ElectricalNode
设备之间外部线路 = Connection(Terminal A, Terminal B)
```

P0-3 不引入“线路公共 Node”，也不做 Node 推理或合并。

### 2.3 OverheadLine 与 Connection

`OverheadLine.ConnectionId` 必须等于对应 `Connection.Id`，且连接类型必须为 `OverheadLine`。`DrawingDocument.AddOverheadLine` 要求 Connection 已存在，再校验：

- 一对一明细关系；
- 支撑杆塔至少一个、顺序稳定且不重复；
- 支撑杆塔存在；
- Pole、柱上设备或 CableTermination 端点与首末支撑杆的物理归属一致；
- 延续线路数据符合现有不变量。

因此创建顺序必须是 `Connection → OverheadLine`，删除顺序必须反向。

### 2.4 当前 Cable 表达

当前没有 `Cable` Domain 类。电缆只表现为：

- `Connection.Type = Cable`；
- Terminal 对 Cable 类型的允许策略；
- DTO 可以保存 Cable 类型 Connection；
- CableTermination 的电缆侧 Terminal。

它缺少线路型号/长度等独立明细、CableLayout、Scene/HitTest 和正式电缆渲染。现有 FormatVersion 2 能保存 Cable Connection，但不能形成与 OverheadLine 等价的绘图闭环。

### 2.5 CableTermination 的职责

CableTermination 是架空与电缆的转换设备，拥有：

- 电缆侧外部 Terminal，只允许 Cable；
- 架空侧外部 Terminal，只允许 OverheadLine；
- 一个内部 Intermediate ElectricalNode；
- 两个 Terminal 通过该 Node 表达固定内部导通；
- 通过 PoleAttachment 绑定物理杆塔位置。

它不是线路，也不替代 Connection。

## 3. 第一版可 Pick Terminal 范围

可 Pick 的必要条件必须同时满足：

1. Terminal 实际存在于当前 DrawingDocument；
2. `IsExternal = true`；
3. `Allows(当前线路类型) = true`；
4. TerminalAnchorIndex 能从当前 RuntimeLayout 解析毫米坐标；
5. Terminal 所属设备/内部聚合仍存在；
6. 当前连接占用策略允许继续连接。

当前 Anchor 能覆盖：

- Pole 的架空锚点 Terminal；
- RingCabinetInterval 的 `ExternalTerminalId`；
- 已通过 PoleAttachment 布局的 CableTermination 两侧 Terminal；
- 已通过 PoleAttachment 布局、且 Domain Terminal 为外部端点的柱上 SwitchDevice。

环网柜内部开关 Terminal 不是外部 Terminal，禁止暴露给连接工具。PoleAttachment 本身没有 Terminal；Pick 的是被安装设备的真实 Terminal。

现有 TerminalAnchorIndex 对 CableTermination 两个 Terminal 使用同一附件中心点，这在拓扑上可区分、图面上会重叠。P0-3-B 若只做 OverheadLine，应只暴露架空侧 Terminal；Cable 侧的视觉分离留 P0-3-C。

## 4. Connection Tool 状态机

新增小型 `ConnectionToolController`，不把状态塞入 MainWindow：

```text
Idle
  → PickingStartTerminal
  → Previewing(startTerminalId, pointerPosition)
  → PickingEndTerminal
  → Commit
  → Idle

任意活动状态 → Cancel → Idle
非法目标       → 保持当前 Pick 状态并反馈原因
Commit 失败    → 保留起点或取消，由实现采用一致策略
```

选择线路工具进入 `PickingStartTerminal`。选中合法起点后进入 Previewing；鼠标移动只更新临时终点；点击第二个合法 Terminal 后创建 Command。Esc 清除起点和预览。

### 与 PlacementController 的互斥

当前 PlacementController 已有活动 Mode。P0-3-B 增加最小 `DrawingToolCoordinator` 或等价协调入口，只保存当前活动工具类别：

- 启动 Connection Tool 时调用 PlacementController.Cancel；
- 启动 Pole/RingCabinet Placement 时调用 ConnectionToolController.Cancel；
- 工程 New/Open/Close 时两者都 Cancel；
- MainWindow 只转发工具选择、鼠标和 Esc。

这不是通用插件式工具框架，不设计工具注册表或复杂路由。

## 5. Terminal Pick 与坐标边界

### 5.1 Pick 流程

```text
鼠标 DIP
→ 当前 DocumentCoordinateSystem
→ 毫米文档坐标
→ TerminalAnchorIndex 最近邻命中
→ TerminalId
→ DrawingDocument 中重新解析 Terminal
→ Domain 可连接性校验
```

TerminalAnchor 只保存 `TerminalId + DocumentPoint`，不保存 Domain 对象引用。所属 Device/Interval、外部性、允许类型和占用情况必须使用 TerminalId 回到 DrawingDocument 解析，不能缓存为第二份业务事实。

### 5.2 Pick tolerance

第一版使用固定毫米容差，并由当前坐标转换边界换算鼠标位置；在尚无 Zoom/Pan 时可采用与现有端子 Pick 一致的小范围容差。后续引入缩放后应以屏幕可用性定义 DIP 容差，再转换成毫米，不能把 DIP 写入 Layout。

多个 Anchor 重叠时：

- 先过滤当前线路类型不允许的 Terminal；
- 按距离排序；
- 若最近候选仍有多个同位置 Terminal，不静默猜测，显示最小候选选择或拒绝并提示；
- CableTermination 架空/电缆侧可按当前工具类型确定唯一候选，这属于 Terminal 的允许策略，不是图形猜测。

## 6. TerminalAnchor 统一端点方案

当前存在双状态：

- TerminalAnchorIndex 能从设备 Layout 派生端点；
- OverheadLineLayout 又保存 `Start` / `End`；
- TerminalAnchorIndex 最后还用 OverheadLineLayout 端点反向覆盖设备 Anchor。

这会使设备移动后线路端点和专业标记仍停在旧坐标。P0-3-B 必须反转依赖：

```text
Domain TerminalId + 设备 RuntimeLayout
→ TerminalAnchorIndex
→ 正式 Line endpoint
```

原则：

- TerminalAnchor 是线路端点唯一坐标来源；
- OverheadLineLayout 的 Start/End 在当前 FormatVersion 2 中暂时保留为兼容字段，但运行时视为派生缓存，不再覆盖 Anchor；
- 创建/保存快照时用当前 Anchor 回填 Start/End，避免升级格式；
- 用户路径控制点目前不存在；ContinuationOffset 仍是布局事实；
- 加载旧文件后，优先按当前设备 Layout 重建 Anchor，旧 Start/End 只用于无法解析 Anchor 时的明确兼容诊断，不得悄悄成为事实源。

后续格式升级可移除冗余端点或改为显式路径控制点，但不属于 P0-3。

WorkScope 和 GroundingPoint 已依赖 TerminalAnchorIndex；统一后它们与线路会同时随设备移动，避免三套锚点算法。

## 7. ElectricalNode 创建与复用规则

P0-3-B 的明确规则是：**创建外部 Connection 不创建、复用或合并 ElectricalNode。**

- Terminal 没有 ElectricalNode：仍允许按自身连接策略建立外部 Connection；
- Terminal 已属于 Node：保留原 Node，只新增外部 Connection；
- 两端属于同一 Node：当前 Domain 没有专门禁止，但可能形成无意义外部回路，P0-3-B 应在 Domain 增加明确拒绝或至少拒绝同聚合内连接；
- 两端属于不同 Node：允许 Connection 连接两个设备的外部端点，但不合并 Node；
- Node 归属异常、缺失或 Terminal 悬空：由 DrawingDocument 校验拒绝；
- 不提供自动 Node 合并 API。

如果未来需要网络节点归并或潮流图模型，应另行设计，不能塞进 Connection Editor。

## 8. Add OverheadLine 原子事务

建议新增 `AddOverheadLineCommand`，保存：

- Connection 的全部稳定字段；
- OverheadLine 的全部稳定字段；
- 两端 TerminalId；
- SupportPoleIds；
- Continuation 数据；
- Runtime Layout 中非派生布局字段和创建时 Anchor 快照。

Execute 顺序：

1. 使用当前 DrawingDocument 重新校验两个 Terminal；
2. 创建同一稳定 ID 的 Connection；
3. `DrawingDocument.AddConnection`；
4. 创建同 ID OverheadLine；
5. `DrawingDocument.AddOverheadLine`；
6. 从 TerminalAnchorIndex 解析端点；
7. 添加 Runtime OverheadLineLayout；
8. 任一步失败，按 `Layout → OverheadLine → Connection` 反向回滚。

当前 DrawingDocument 缺少 RemoveConnection/RemoveOverheadLine，P0-3-B 需要增加最小安全 API。由于 P0-3-B 不创建 ElectricalNode，事务中没有 Node 创建和孤立 Node 回滚。

CommandStack 已能保证 Execute 失败的 Command 不进入历史；多对象原子性由 Command 和 Domain API负责，不新建第二套事务框架。

## 9. 非法连接规则

第一版必须在 Domain/Topology 层拒绝：

- 起点和终点 TerminalId 相同；
- 任一 Terminal 不存在、已删除或所有者不存在；
- 非外部 Terminal；
- Terminal 不允许当前 ConnectionType；
- 电压等级不兼容；
- 不允许多连接的 Terminal 已被占用；
- 同一无序 Terminal 对已有同类型 Connection；
- 会造成当前模型无法解释的同聚合内部外部回接；
- OverheadLine 缺少合法 SupportPoleIds；
- 端点柱上设备与首末支撑杆不一致；
- RingCabinet 内部开关 Terminal 被误选；
- Anchor 无法解析或 Anchor 对应对象已删除；
- 需要 ElectricalNode 合并、拆分或自动推理才能成立的复杂连接；
- 当前阶段的 RingCabinet-to-RingCabinet 纯架空连接。

UI 只显示 Domain 返回的错误，不复制业务校验。

## 10. Preview 边界

Preview 是 Interaction/Rendering 临时 Overlay：

- 起点来自已选 TerminalAnchor；
- 临时终点来自当前鼠标毫米坐标；
- 可显示合法/非法目标反馈；
- 不写 Domain、RuntimeLayout、ProjectLayoutSnapshot；
- 不进入 CommandStack、Dirty 或 Save；
- Esc、工具切换、工程切换和 Commit 后完全清除；
- 正式 Scene 重建不依赖 Preview。

保存前 ProjectWorkspaceController 的临时编辑检查应扩展到 Connection Preview：只能明确 Commit 或 Cancel，不能把 Preview 当正式线保存。

## 11. Remove Connection 原子事务

新增 `RemoveOverheadLineCommand`。执行前按 ConnectionId 解析完整三元组：Connection、OverheadLine、Runtime Layout。

Execute 顺序：

1. 校验没有其他对象引用该线路的稳定 ID或延续端点；
2. 删除 Runtime Layout；
3. 删除 OverheadLine 明细；
4. 删除 Connection；
5. 不删除 ElectricalNode。

如果后续步骤失败，使用快照按相反顺序恢复。Undo 恢复原 ConnectionId、TerminalId、OverheadLine 明细和 Layout；Redo 再执行安全删除。任何失败不改变历史索引和 Dirty。

## 12. Undo/Redo 与 Stable ID

```text
Add Line → Dirty
Undo     → Layout、OverheadLine、Connection 全部消失
Redo     → 相同 ConnectionId 和 TerminalId 恢复

Remove   → Dirty
Undo     → 原完整对象与布局恢复
Redo     → 再次安全删除

Save     → CommandStack.MarkSaved() → clean
```

所有 GUID 在 Command 创建时一次生成并保存；Redo 禁止重新生成 ID。Command 保存标量/值快照，不长期保存 UI Selection 或 WPF Visual。

## 13. 设备移动后的线路更新

设备拖动只修改 RuntimeLayout，不修改 Connection 或 TerminalId。统一刷新顺序：

```text
设备 RuntimeLayout 改变
→ TerminalAnchorIndex 重建
→ 按 Connection 两端 TerminalId 解析新端点
→ OverheadLine Scene/HitTest 重建
→ WorkScope/GroundingPoint 同步使用新 Anchor
```

正式线路绘制和 HitTest 必须使用解析后的端点，而不是旧 OverheadLineLayout.Start/End。保存时再把派生端点写入 v2 兼容 DTO。

## 14. Selection 与 PropertyInspector

现有线路 Selection 使用 `SelectionTargetKind.Connection + ConnectionId`，Resolver 能关联 Connection、OverheadLine 和 OverheadLineLayout。P0-3-B 保持这一稳定引用：

- 正式线段建立按派生端点计算的 HitTest bounds；
- 点击线路选择 Connection；
- Overlay 不修改原始 Scene；
- PropertyInspector 继续显示现有 Connection/OverheadLine 属性；
- Delete 根据 ConnectionId 找到完整事务对象；
- Undo/Redo 后重新验证 Selection，禁止悬空引用。

本阶段不扩张完整线路属性编辑器。

## 15. Persistence 闭环

FormatVersion 2 已能保存和恢复：

- ElectricalNode；
- Terminal；
- Connection；
- OverheadLine 明细；
- OverheadLineLayout 的端点兼容字段和 ContinuationOffset。

因此 P0-3 不升级格式。目标恢复流程：

```text
Add
→ Runtime Anchor 派生端点
→ Save 时生成 v2 Snapshot
→ Close/Open
→ Domain/Topology 恢复
→ RuntimeLayout 恢复
→ TerminalAnchor 重建
→ Scene 使用 Anchor 绘线
→ 新 CommandStack、Selection 空、Dirty=false
```

需补自动化验收：保存前后 ConnectionId、两端 TerminalId、OverheadLine 明细、SupportPoleIds 和派生几何一致。

## 16. P0-3-B 最小实现范围

建议包含：

- `ConnectionToolController` 和最小工具互斥协调；
- OverheadLine 工具入口、Terminal Pick、Preview、Esc；
- `AddOverheadLineCommand` / `RemoveOverheadLineCommand`；
- DrawingDocument 的安全 RemoveOverheadLine/RemoveConnection API及必要重复连接校验；
- DrawingLayout 的 OverheadLine Remove/Restore；
- TerminalAnchorIndex 去除 LineLayout 对设备 Anchor 的反向覆盖；
- DrawingSceneBuilder 由 TerminalAnchor 解析正式端点和 HitTest；
- 保存快照用当前 Anchor 回填 v2 Start/End；
- Selection、PropertyInspector 和统一 Scene 刷新接线；
- Domain 与端到端测试，覆盖非法连接、引用保护、Undo/Redo、移动和 Save/Reload。

第一版输入可以固定最小线路属性：10kV、明确的默认显示名和一个现有合法线型值；完整属性编辑留后续。SupportPoleIds 必须从端点的物理 Pole/PoleAttachment 关系显式形成，不从几何位置猜测。

## 17. 主要架构风险

| 风险 | P0-3 最小处理 |
| --- | --- |
| TerminalAnchor 反向受 LineLayout 覆盖 | 改为设备 Layout 派生 Anchor，线路只消费 Anchor |
| ElectricalNode API 不支持合并/解绑 | P0-3 不创建或合并 Node，复杂情况拒绝 |
| RuntimeLayout Start/End 双状态 | v2 中降级为派生兼容缓存，保存时回填 |
| DrawingDocument 无连接删除 | 增加明确、受引用保护的最小删除 API |
| Command 涉及多个对象 | 类型化事务 Command + 反向回滚，继续使用现有 CommandStack |
| DrawingSceneBuilder 继续集中膨胀 | P0-3 可提取小型 ConnectionSceneBuilder，但不做无关重构 |
| MainWindow 鼠标逻辑继续膨胀 | ConnectionToolController 管状态，MainWindow 只转发 |
| Placement 与 Connection 抢占鼠标 | 最小 ActiveTool 协调，切换时显式 Cancel |
| Cable Domain 不完整 | 推迟到 P0-3-C，不临时发明模型 |

## 18. 明确不做

- Cable 创建闭环；
- 自动布线、折线路径编辑；
- 自动 ElectricalNode 推理、合并或潮流计算；
- Copy/Paste、多选、Snap/Align、Zoom/Pan；
- 完整线路属性编辑；
- WorkTicketData、停电分析和自动安全措施；
- FormatVersion 升级。
