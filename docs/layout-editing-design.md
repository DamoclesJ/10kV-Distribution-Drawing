# M3-B-3-A 图元布局编辑架构设计

> 文档状态：设计稿，仅定义拖动与 Layout 修改机制，不实现代码或 UI<br>
> 编制日期：2026-08-11<br>
> 依据：`docs/editor-architecture.md`、当前 SelectionManager、只读 PropertyInspector、Layout 模型及 WPF Rendering 链路

## 1. 目标与范围

本阶段设计最小图元拖动架构，使用户可以通过已有选择机制发起位置调整，并确保修改只落入 Layout，不改变任何电气事实。

目标链路：

```text
Mouse Drag
    ↓
SelectionReference
    ↓
LayoutObjectResolver / LayoutEditor
    ↓
Move Layout Command
    ↓
替换目标 Layout 值
    ↓
DrawingSceneBuilder 重新生成场景和 HitTestIndex
    ↓
DrawingSceneRenderer 刷新 DrawingVisual
```

本阶段只设计，不修改 Domain、Layout、Interaction、Rendering 或 Desktop 代码，不实现拖动、Undo/Redo、自动布局或保存。

## 2. 核心边界

### 2.1 几何拖动的唯一持久修改目标

对于本阶段四类可移动对象，Layout 是拖动操作唯一允许修改的工程数据：

| 选择对象 | Layout 目标 | 修改内容 | 保持不变 |
| --- | --- | --- | --- |
| RingCabinet | RingCabinetLayout | 柜体绝对 Position | 柜体 ID、间隔、开关、Terminal、ElectricalNode、内部相对布局 |
| Pole | PoleLayout | 杆塔绝对 Position | PoleId、PoleAttachment、锚点 Terminal、SupportPoleIds |
| PoleAttachment | AttachmentLayout | 相对所属 Pole 的 Offset | PoleId、AttachedDeviceId、附属设备端子和状态 |
| OverheadLine / Connection | OverheadLineLayout | Start、End 或后续显示路线 | ConnectionId、两个 TerminalId、LineModel、SupportPoleIds、延续语义 |

这里的“唯一修改目标”限定于位置、偏移和显示路径等几何操作。名称、型号、SwitchState、Connection 端点、所属关系和环网柜结构仍是 Domain 操作，不能通过拖动修改。

SelectionState、拖动预览和高亮属于瞬时编辑状态，不是 Layout，也不进入工程文件。

### 2.2 当前 Layout 的值语义

当前 `RingCabinetLayout`、`PoleLayout`、`AttachmentLayout` 和 `OverheadLineLayout` 的坐标属性均为只读。后续实现不应通过反射或绕过封装修改字段，而应采用值替换：

1. 从 LayoutStore 读取当前 Layout 值。
2. 以原值为基础构造仅位置变化的新 Layout。
3. 保留原 ID、尺寸、标签偏移和子布局等未修改字段。
4. 由 LayoutStore 的显式 Replace 操作原子替换。

RingCabinetLayout 替换时必须保留全部 IntervalLayouts；移动柜体不得重建间隔或修改 IntervalLayout.RelativePosition。PoleLayout 替换时 AttachmentLayout 不变，因为附属设备位置由“杆塔绝对位置 + 附属偏移”组合得到。

### 2.3 Domain、Symbol 和 Rendering

- Domain 不保存画布坐标，也不响应 MouseMove。
- ElectricalNode、Terminal、Connection 和 SwitchAssembly 不参与位置修改。
- SymbolDefinition 保持只读无状态，只接收新 Domain + Layout 重新生成 SceneElement。
- Rendering 不执行 Command、不保存拖动状态、不修改 Layout，只绘制主场景和预览 Overlay。
- HitTestIndex 是场景派生结果；Layout 提交后必须重建，不能手工改旧命中框充当新场景。

## 3. 选择对象到 Layout 的解析

### 3.1 LayoutObjectResolver

建议新增编辑器级 `LayoutObjectResolver`，输入当前 `SelectionReference` 和 EditorSession 的 LayoutStore，返回短生命周期的 `ResolvedLayoutTarget`。

映射规则：

| SelectionTargetKind | 解析条件 | Resolved Layout |
| --- | --- | --- |
| RingCabinet | ObjectId 等于 CabinetId | RingCabinetLayout |
| Device | ObjectId 在当前 Domain 中解析为 Pole | PoleLayout |
| PoleAttachment | ObjectId 等于 AttachmentId，ParentId 校验 PoleId | AttachmentLayout |
| Connection | ObjectId 对应 OverheadLine.ConnectionId | OverheadLineLayout |

当前不把以下选中对象作为整图元拖动目标：

- RingCabinetInterval。
- 柜内 SwitchDevice。
- Terminal、端子热点或标签。

它们可以继续被选择和查看属性，但 M3-B-3 第一版拖动时返回 `IsMovable=false`。后续若允许间隔重排、开关图元微调或标签移动，应增加独立 Layout Command，不复用顶层设备移动规则。

`ResolvedLayoutTarget` 只在一次拖动会话中使用，至少包含 SelectionReference、LayoutTargetKind、稳定 LayoutKey、当前 LayoutRevision 和起始布局值。UI 不长期保存 Layout 对象引用。

### 3.2 解析失败

以下情况不能进入拖动状态：

- SelectionReference 已失效。
- ParentId 与当前所属关系不一致。
- 找不到对应 Layout。
- 选择对象不是本阶段允许的移动类型。
- 当前处于其他互斥编辑模式。

解析失败只取消本次拖动并显示提示，不由编辑器创建缺失 Layout，也不改变选择对象。

## 4. 拖动状态机

建议使用文档级 DragController，状态如下：

```text
Idle
  └─ MouseDown on movable selection
        ↓
Armed（记录起点，但尚未形成修改）
  ├─ 未超过阈值 → MouseUp → Idle
  └─ 超过屏幕拖动阈值
        ↓
Dragging（捕获鼠标，更新预览）
  ├─ Escape / CaptureLost → Cancel → Idle
  └─ MouseUp
        ↓
CommitPending
  ├─ Command 成功 → Scene Refresh → Idle
  └─ Command 失败 → 恢复原显示并提示 → Idle
```

### 4.1 MouseDown

1. 使用当前 HitTestIndex 命中对象。
2. 若命中对象不是当前选择，先更新 SelectionManager。
3. LayoutObjectResolver 判断该 SelectionReference 是否可移动。
4. 记录屏幕起点、文档起点、原 Layout 值和基础 LayoutRevision。
5. 进入 Armed，不立即修改 Layout，也不写 Undo 历史。

### 4.2 MouseMove

超过拖动阈值后捕获鼠标并进入 Dragging：

- 将当前屏幕点转换为文档点。
- 计算 `DocumentDelta = CurrentDocumentPoint - StartDocumentPoint`。
- 根据 Layout 类型计算 PreviewLayoutValue。
- 生成 DragPreviewOverlay 或使用临时 PreviewLayoutSnapshot 重绘。
- 不修改正式 LayoutStore，不增加 DocumentRevision，不刷新 PropertyInspector 的事实值。

拖动阈值以屏幕 DIP 表达，保证不同缩放级别下操作手感一致；移动距离以毫米文档坐标表达。

### 4.3 MouseUp

MouseUp 时：

1. 使用最后一个有效 PreviewLayoutValue 构造 Move Command。
2. 如果前后布局相同，不执行命令。
3. CommandDispatcher 校验 LayoutKey 和基础修订号。
4. 成功后原子替换 Layout，写入一条 Undo 历史并发布 LayoutChanged。
5. 清除预览，重建 DrawingScene、HitTestIndex、SelectionOverlay 和 PropertyInspector 快照。
6. 保持原 SelectionReference 选中。

不允许把每次 MouseMove 作为独立 Command。

### 4.4 取消

Escape、鼠标捕获丢失、窗口失焦或命令前校验失败时：

- 丢弃 PreviewLayoutValue。
- 清除 DragPreviewOverlay。
- 正式 Layout 和 DocumentRevision 保持不变。
- 不产生 Undo 记录。
- 选择可以保持不变。

## 5. 坐标体系

### 5.1 屏幕坐标

屏幕坐标来自 WPF 指针事件相对 `DrawingVisualHost` 的位置，单位是 DIP，不是物理像素。屏幕坐标只用于输入、拖动阈值和鼠标捕获，不保存到 Layout。

当前未实现缩放和平移，可由 `DocumentCoordinateSystem.DipToMillimeters` 完成基础换算。未来加入视图变换后，必须使用统一逆变换：

```text
ScreenPoint（DIP）
    ↓ inverse ViewTransform（去除平移、缩放）
DocumentPoint（mm）
```

不能在各 Drag Handler 中分别实现 DPI、缩放和平移换算。

### 5.2 文档坐标

DocumentPoint 使用毫米，是 DrawingScene、页面布局和所有绝对 Layout 的共同坐标。拖动增量计算必须在文档坐标中完成：

```text
DocumentDelta = CurrentDocumentPoint - StartDocumentPoint
```

如果未来加入网格或吸附，应在文档坐标中处理目标位置；吸附容差可以从屏幕 DIP 经当前缩放换算成毫米。M3-B-3-A 不设计自动布局或吸附算法。

### 5.3 Layout 坐标

不同 Layout 使用不同坐标语义：

| Layout | 坐标语义 | 拖动计算 |
| --- | --- | --- |
| RingCabinetLayout.Position | 页面绝对文档坐标 | OriginalPosition + DocumentDelta |
| PoleLayout.Position | 页面绝对文档坐标 | OriginalPosition + DocumentDelta |
| AttachmentLayout.Offset | 相对所属 Pole 的文档毫米偏移 | OriginalOffset + DocumentDelta |
| OverheadLineLayout.Start/End | 页面绝对显示坐标 | 原 Start/End 按具体手柄或整线规则更新 |

Attachment 拖动不需要先转换成新的绝对位置再保存；只把文档增量加到原 Offset。移动 Pole 时不修改 AttachmentLayout.Offset。

### 5.4 架空线路坐标边界

OverheadLineLayout 只表示线路显示几何，不能改变 Connection 的两个 TerminalId：

- 拖动整条直线时，Start 和 End 使用同一 DocumentDelta 平移。
- 后续拖动端点手柄时，只修改对应显示端点或路线锚点。
- 如果项目启用“端点必须吸附 TerminalAnchor”的规则，提交前必须验证显示端点仍绑定正确端子；失败则拒绝提交或回到有效锚点。
- SupportPoleIds、LineModel、ContinuationState 和电气状态不随几何拖动改变。

当前 MVP 仍为简单直线，不设计曲线、弧垂、三维线路和自动布线。整线平移只是显示布局操作，不表示改变线路接线或物理经过杆塔。

## 6. LayoutEditor

LayoutEditor 位于 Editor/Application 协调边界，职责是把拖动意图转换成合法的新 Layout 值：

- 接收 ResolvedLayoutTarget、DocumentDelta 和当前 LayoutRevision。
- 按 LayoutTargetKind 调用明确的移动策略。
- 保留所有非位置字段。
- 校验坐标为有限值，尺寸仍为正数，ID 未变化。
- 返回 PreviewLayoutValue 或 MoveCommand 参数。

LayoutEditor 不读取鼠标事件，不绘制 Overlay，不调用 DrawingSceneRenderer，也不修改 Domain。

建议使用显式策略：

- RingCabinetLayoutMoveStrategy。
- PoleLayoutMoveStrategy。
- AttachmentLayoutMoveStrategy。
- OverheadLineLayoutMoveStrategy。

不建议通过反射查找名为 Position、Offset、Start 的属性，也不使用一个 `object NewValue` 绕过类型校验。

## 7. Move Command 与 Undo/Redo 预留

### 7.1 Command 分类

可以由统一接口承载，但具体命令保持类型明确：

| Command | 目标键 | Before / After |
| --- | --- | --- |
| MoveRingCabinetLayoutCommand | CabinetId | Position |
| MovePoleLayoutCommand | PoleId | Position |
| MoveAttachmentLayoutCommand | AttachmentId | Offset |
| MoveOverheadLineLayoutCommand | ConnectionId | Start + End 或指定手柄值 |

每个命令至少记录：

- CommandId。
- SelectionReference 或稳定 LayoutKey。
- BaseLayoutRevision。
- Before 值。
- After 值。
- 操作时间仅用于 UI 日志，不参与业务判断。

命令不保存 MouseEventArgs、屏幕坐标、DrawingVisual、SceneElement、Symbol 或 Domain 对象引用。

### 7.2 Execute

执行时必须：

1. 按稳定键重新获取当前 Layout。
2. 校验当前 LayoutRevision 与命令基准一致。
3. 校验当前位置与 Before 值一致，避免覆盖其他修改。
4. 构造新 Layout，并通过 LayoutStore.Replace 原子替换。
5. 增加 LayoutRevision / DocumentRevision。
6. 发布包含 LayoutKey 的 LayoutChanged。

Move Command 不执行专业电气校验，因为它不改变电气事实，但仍需校验 Layout 引用、坐标和组合所有权。

### 7.3 Undo 与 Redo

- Undo 使用同一稳定键把 After 恢复为 Before。
- Redo 把 Before 再恢复为 After。
- 一次完整拖动只对应一条历史记录。
- 取消拖动和零位移不进入历史。
- Undo/Redo 后重建场景和命中索引，并保持可解析的选择。
- 新 Move Command 成功后清空 RedoStack。

Undo/Redo 历史不写入工程文件。保存工程只保存当前 Layout 结果。

### 7.4 父子布局恢复边界

- 移动 RingCabinet 只恢复 RingCabinetLayout.Position，IntervalLayout 和 SwitchLayout 不进入该命令快照。
- 移动 Pole 只恢复 PoleLayout.Position，AttachmentLayout.Offset 不进入该命令快照。
- 移动 Attachment 只恢复自己的 Offset。
- 移动 OverheadLine 只恢复该 ConnectionId 对应的显示布局。

这样可以避免为一次简单移动复制整个 DrawingDocument 或整个 LayoutStore。

## 8. 预览与 Rendering 刷新

### 8.1 拖动预览

拖动预览有两种可接受实现：

1. 使用 PreviewLayoutSnapshot 临时生成目标对象的新场景元素。
2. 使用 DragPreviewOverlay 对目标对象的已有几何施加临时文档位移。

第一版优先保证正确性，可使用 PreviewLayoutSnapshot 重建完整 DrawingScene；若性能不足，再局部生成预览。无论采用哪种方式，预览都不能写入正式 LayoutStore。

预览应包含：

- 被移动对象的新位置轮廓或半透明图元。
- 当前 SelectionOverlay。
- 可选坐标提示。

预览不进入 JPG、打印或工程保存。

### 8.2 提交后刷新

```text
Move Command 成功
    ↓
LayoutChanged(LayoutKey, Revision)
    ↓
DrawingSceneBuilder 读取最新 Domain + Layout
    ↓
重建 SceneElement + HitTestIndex
    ↓
SelectionManager 保留同一 SelectionReference
    ↓
SelectionOverlayBuilder 使用新 Bounds
    ↓
DrawingSceneRenderer 刷新 DrawingVisual
```

Rendering 不比较 Before/After，不更新 Layout，不决定是否允许移动。

## 9. 与 PropertyInspector 的关系

当前 PropertyInspector 是只读值快照：

- Armed 和 Dragging 阶段仍显示正式 Layout 值，避免把预览误认为已提交事实。
- 可选坐标提示属于 DragOverlay，不写入 PropertyInspectorViewModel。
- Move Command 成功后，PropertyInspector 根据原 SelectionReference 重新 Resolve 和 Project，显示新的 Layout 值。
- 取消或命令失败时 PropertyInspector 无需回填，因为正式 Layout 从未改变。
- PropertyInspector 不直接发起或应用鼠标拖动结果。

未来属性面板允许输入 X/Y 时，也必须生成相同类型的 Move Layout Command，使鼠标拖动和数值输入共享 Undo/Redo 与校验边界。

## 10. 约束与异常处理

### 10.1 必须保持

- Domain 设备、开关状态、Terminal、ElectricalNode、Connection 端点和 SwitchAssembly 完全不变。
- SymbolDefinition 无状态、只读，不保存对象位置。
- Rendering 只根据新的 Domain + Layout 刷新。
- SelectionReference 使用稳定 ID，不保存 Visual 引用。
- Layout 修改使用文档毫米坐标，不保存屏幕 DIP。
- 同一 EditorSession 串行执行 Layout Command。

### 10.2 异常处理

| 异常 | 处理 |
| --- | --- |
| 拖动期间对象被删除或 Layout 被替换 | 取消提交，刷新选择和场景 |
| BaseLayoutRevision 过期 | 拒绝 Command，不自动合并 |
| 坐标 NaN 或 Infinity | 拒绝 Preview/Command |
| 鼠标捕获丢失 | 丢弃预览，不产生历史 |
| Scene 重建失败 | 保留上一个有效 Visual，报告布局错误 |
| Selection 已失效 | 清除 Selection 和 PropertyInspector |

页面边界限制、网格吸附和对象碰撞策略尚未确认，本阶段不自行加入。

## 11. 后续扩展预留

- 多选批量移动：保存每个 LayoutKey 的 Before/After，作为一个原子 Command。
- 环网柜间隔重排：使用专用 IntervalLayout/Reorder Command，不修改 IntervalKind。
- 标签拖动：使用 UpdateLabelOffsetCommand。
- 线路折点编辑：扩展 ConnectionRoute/OverheadLineLayout 后使用专用 Handle Command。
- WorkScope 和 GroundingPoint：语义仍引用 TerminalId，图面标记位置通过独立 Layout Command 调整。
- 缩放和平移：统一加入 ViewTransform，不改变现有文档毫米坐标。

这些扩展不得把坐标写入 Domain，也不得让 Symbol 成为编辑状态容器。

## 12. 校验与测试建议

后续实现至少覆盖：

- RingCabinet 和 Pole 拖动只改变各自绝对 Position。
- Pole 移动后 AttachmentLayout.Offset 不变，画面组合位置随父对象变化。
- Attachment 拖动只改变自身相对 Offset。
- OverheadLine 拖动只改变显示 Start/End，不改变 Connection TerminalId 和 SupportPoleIds。
- MouseMove 只更新预览，MouseUp 只产生一条 Command。
- Escape、捕获丢失和零位移不修改 Layout、不产生历史。
- Command、Undo、Redo 后 DrawingScene、HitTestIndex、高亮和 PropertyInspector 一致刷新。
- 任何拖动都不修改 Domain、SymbolDefinition 或 Rendering 状态。
- 屏幕到文档坐标在不同 DPI 及未来缩放下转换一致。

## 13. 本阶段不实现

- LayoutStore.Replace、LayoutEditor、DragController 或 Move Command 代码。
- WPF MouseMove、鼠标捕获、拖动 Overlay 或位置手柄。
- Undo/Redo 运行时代码。
- 属性编辑、保存、JPG 或打印。
- 自动布局、吸附、碰撞检测、页面边界限制。
- 环网柜间隔/开关内部拖动、线路折点和曲线编辑。
- 任何 Domain、电气拓扑或 Symbol 修改。
