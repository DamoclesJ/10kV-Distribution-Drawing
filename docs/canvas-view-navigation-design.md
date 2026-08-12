# Drawing Core P0-5-A：Canvas ViewTransform、Zoom/Pan/Fit 与 RingCabinet 移动设计

> 设计基线：`1b39472d6951e9c04303d9818f7857a37ff7e43a`
>
> 范围：统一画布视图坐标、基本导航、Pole/RingCabinet 移动；不修改 Domain、工程格式或其他 P0 能力

## 1. 结论

当前 Scene、Layout 和 TerminalAnchor 都使用毫米文档坐标，基础方向正确；缺少的是位于文档坐标与 WPF View DIP 之间的统一运行时变换。现有代码实际上只有固定的物理单位换算，没有 Zoom 或 Pan：

```text
Document mm
→ 96 DIP/inch 固定换算
→ DrawingVisual
```

鼠标路径则在 MainWindow 中直接反向换算：

```text
Mouse DIP
→ 固定 DIP/mm 换算
→ DocumentPoint
```

一旦加入缩放或平移，Placement、Drag、Terminal Pick、Preview 和普通 HitTest 都会得到错误文档坐标。因此 P0-5 的首要设计原则是：

> 所有“文档毫米 ↔ View DIP”转换必须由同一个 `CanvasViewTransform` 完成；Scene、RuntimeLayout、TerminalAnchor 和 Persistence 始终保持毫米语义。

RingCabinet 移动应在该变换落地后接入一个只支持 Pole/RingCabinet 的小型 `DeviceDragController`。这两部分属于同一 P0 里程碑，但建议分两个可独立验证的实现切片，避免同时调试视口数学、输入路由和布局命令。

## 2. 当前坐标体系审查

### 2.1 已有坐标对象

- `DocumentPoint`、`DocumentRect`：毫米工程坐标；用于 Layout、Scene、HitTest 和 TerminalAnchor。
- `DocumentCoordinateSystem`：仅提供毫米与 DIP 的固定物理单位换算，比例为 `96 DIP / 25.4 mm`。
- `DrawingSceneRenderer`：把每个 SceneElement 从毫米转换为 DIP；线宽、字体也按同一固定比例转换。
- `DrawingVisualHost`：只保存并显示一个 DrawingVisual，没有 Scale/Translate 或视口状态。
- WPF Mouse Position：相对于 DrawingSurface 的 DIP 坐标。

`DocumentCoordinateSystem` 是单位换算器，不是 ViewTransform。它没有 Scale、Translation、ViewportSize，也无法表达鼠标中心缩放。

### 2.2 当前转换入口

| 路径 | 当前实现 | Zoom/Pan 后的问题 |
| --- | --- | --- |
| Scene Rendering | Renderer 内 `mm → DIP` | 不知道缩放和平移 |
| Placement | MainWindow MouseDown 直接 `DIP → mm` | 平移后放置位置错误，缩放后比例错误 |
| Pole Drag Start | MainWindow MouseDown 直接换算 | StartPointer 错误 |
| Pole Drag Preview | MainWindow MouseMove 再次直接换算 | 位移量随 Scale 错误 |
| Selection HitTest | 上述 DocumentPoint 传给文档 HitTest | 命中坐标错误 |
| OverheadLine Pick | MainWindow 传 DocumentPoint | 端子 Pick 错误 |
| Pick tolerance | `DipToMillimeters(8)` | 未除以当前 Scale |
| Connection Preview | MouseMove 直接 `DIP → mm` | Preview 终点错误 |
| GroundingPoint/WorkScope Pick | MainWindow 的 `HitTestTerminal` | 固定 8 mm 方框，视觉容差随缩放变化 |
| Selection Overlay | Overlay 生成毫米 SceneElement，再由 Renderer 显示 | 若统一变换放在 Visual 层即可自然保持 |
| Save/Reload | RuntimeLayout mm ↔ DTO mm | 当前正确，不应引入 View 状态 |

### 2.3 重复与隐含假设

当前 MainWindow 至少在 MouseDown、OverheadLine MouseMove、Pole Drag MouseMove 三处重复构造 DocumentPoint。普通专业端子 Pick 使用固定 8 mm 方框，而 OverheadLine Pick 使用由 8 DIP 固定换算得到的圆形容差，两者规则并不一致。

所有逻辑都隐含：

```text
Scale = 1
Translation = (0 DIP, 0 DIP)
```

渲染使用 WPF DIP，不直接使用物理像素；`PixelsPerDip` 当前只参与文字格式化，不应被误当成 Zoom。

## 3. CanvasViewTransform

### 3.1 定位

新增运行时值对象 `CanvasViewTransform`，建议放在 `DistributionDrawing.Rendering.Wpf/Canvas`。它是 WPF 视口坐标边界的一部分，不属于 Domain、Layout 或 Persistence。

状态：

- `Scale`：相对于当前毫米到 DIP 基准比例的无量纲缩放；
- `TranslationXDip`、`TranslationYDip`：View DIP 平移；
- 基础 `DocumentCoordinateSystem`：只负责 `mm ↔ base DIP`。

统一公式：

```text
ViewDip = BaseDip(DocumentMm) × Scale + TranslationDip

DocumentMm = BaseMm((ViewDip - TranslationDip) / Scale)
```

最小 API：

```text
DocumentToView(DocumentPoint) → Point(DIP)
ViewToDocument(Point DIP) → DocumentPoint(mm)
DocumentToView(DocumentRect) → Rect(DIP)
ViewDipToDocumentLength(double DIP) → double mm
ZoomAt(Point anchorDip, double targetScale)
PanBy(Vector deltaDip)
SetView(double scale, Vector translationDip)
Reset()
```

对象必须校验 Scale 为有限正数，Translation 为有限 DIP 值。建议初始 Scale 为 `1.0`，第一版视图范围可取 `0.2～8.0`；这些是编辑体验常量，不是专业业务规则。

### 3.2 状态边界

`CanvasViewTransform` 只属于当前 Desktop/Runtime View：

- Domain 不知道它；
- RuntimeLayout 仍保存毫米坐标；
- DrawingScene 仍由毫米 SceneElement 组成；
- TerminalAnchorIndex 仍输出毫米位置；
- ProjectSession 和 `.kvdrawing` 不保存它；
- CommandStack 不记录它；
- Zoom/Pan 不改变 Dirty。

工程打开后可使用默认视图或执行一次 Fit Drawing。第一版不承诺恢复上次视口。

## 4. 单一转换入口与迁移

新增 `CanvasViewportController`，持有一个 `CanvasViewTransform`、当前 Viewport DIP 尺寸和导航状态。MainWindow 只把 WPF Point、滚轮增量和 Viewport Size 转发给它。

迁移顺序控制为：

1. 引入 ViewTransform，并让视觉输出统一应用变换；默认 Scale=1、Translation=0 时显示保持不变。
2. MainWindow 的所有 Mouse DIP 先调用 `ViewToDocument`，再传给现有 Controller。
3. 所有“视觉容差”先以 DIP 定义，再调用 `ViewDipToDocumentLength`。
4. PlacementController、OverheadLineConnectionController、SelectionHitTestIndex、TerminalAnchorIndex 继续只接收毫米值，不直接依赖 WPF 或 ViewTransform。
5. 移除 MainWindow 对 `DocumentCoordinateSystem.DipToMillimeters` 的直接调用。

这样不需要修改 Domain，也不需要把视口对象注入每个业务/交互 Command。

## 5. Rendering 边界

SceneBuilder 和 Symbol 不应参与 Zoom/Pan。每个 Symbol 继续生成毫米 SceneElement，不自行乘 Scale。

建议由 DrawingVisualHost 或紧邻它的 Viewport Render Adapter 对整棵 DrawingVisual 应用一个 WPF MatrixTransform：

```text
Document mm Scene
→ DrawingSceneRenderer 使用基准 mm→DIP
→ 单一 Scale + Translate Matrix
→ Viewport
```

这一层同时变换正式 Scene、Connection Preview 和 Selection Overlay，因此三者不会产生坐标漂移。`DrawingSceneBuilder` 不需要因 Zoom/Pan 改造；只需继续构建文档空间 Scene/HitTest。

如果实现选择在 Renderer 的 DrawingContext 上 `PushTransform`，也必须由同一 CanvasViewTransform 生成 Matrix，且不得让 MainWindow 重算另一份矩阵。VisualHost 方案更有利于把 View 状态集中在 Canvas 层。

线宽和文字随 Zoom 一起缩放，符合第一版“整体图纸缩放”的预期。未来若需要屏幕恒定大小的控制点，应作为编辑器 Overlay 单独设计，不在 P0-5 扩展 Symbol 语义。

## 6. Zoom 设计

### 6.1 操作

- 鼠标滚轮：以鼠标当前位置为中心缩放；
- 菜单/按钮 Zoom In、Zoom Out：以 Viewport 中心缩放；
- 每次使用小比例因子，例如 `1.2` 和 `1/1.2`；
- 将结果夹在 MinScale/MaxScale 内。

### 6.2 鼠标中心不漂移

滚轮前先解析鼠标下的文档点，更新 Scale 后重算 Translation，使该文档点仍映射到同一 DIP：

```text
documentAnchor = ViewToDocument(mouseDip, oldTransform)
newTranslation = mouseDip - BaseDip(documentAnchor) × newScale
```

Zoom 只触发 Visual 刷新，不重建 Domain、RuntimeLayout 或 Scene。若 Preview/Overlay 与正式 Scene 被组合后统一变换，同一次刷新即可保持一致。

## 7. Pan 设计

第一版采用 **中键拖动**：与左键 Placement、Terminal Pick 和设备 Drag 的冲突最少，也不需要引入键盘 Space 状态。

状态：

```text
Idle
→ MiddleButtonDown(viewPoint)
→ Panning(lastViewPoint)
→ MiddleButtonUp / LostCapture / Cancel
→ Idle
```

每次 MouseMove 计算 DIP delta，并调用 `PanBy(deltaDip)`。Pan 不经过 CommandStack、不影响 Dirty，也不修改毫米布局。

输入优先级：

1. 活动中的 Device Drag 不允许启动 Pan；先完成或取消 Drag。
2. 中键 Pan 不触发左键 Placement 或 Connection Commit。
3. Placement/OverheadLine 工具可保持活动，Pan 结束后继续原工具。
4. 丢失鼠标捕获时结束 Pan，不能留下半活动状态。

## 8. Fit Drawing

新增文档空间 `DrawingSceneBoundsCalculator` 或等价 Rendering helper，计算正式 DrawingScene 的可见 Bounds。它应覆盖：

- SceneLine 两端；
- SceneRectangle Bounds；
- SceneText 的实际或保守文字范围；
- Professional 标记，因为它们已在正式 Scene 中；
- 不包含 Selection Overlay 和 Connection Preview。

Fit 输入为正式 Scene Bounds、Viewport DIP Size 和最小 View margin（建议 24 DIP）。分别计算 X/Y 可用比例，取较小值并夹入 Min/Max Scale，再计算居中 Translation。

空工程或无有效 Bounds 时安全 `Reset()`，不抛异常。Viewport 宽高无效时不改变当前 View。

Fit 不修改 Scene、Domain、Layout、Selection、Dirty 或 Persistence。

## 9. RingCabinet Move

### 9.1 Layout API

为 `RingCabinetLayout` 增加保持全部内部布局不变的 `MoveTo(DocumentPoint)`，返回同一 CabinetId 的新 Layout。为 `RuntimeLayoutDocument` 增加按 CabinetId 的 `ReplaceRingCabinet`。

RingCabinet Position 是唯一被修改的布局事实；Interval 和 Switch 仍保存相对位置。

### 9.2 Command

新增 `MoveRingCabinetCommand`：

- Before/After 都是完整 RingCabinetLayout 值快照；
- Execute/Redo Replace After；
- Undo Replace Before；
- 不保存或修改 RingCabinet Domain 对象；
- 不改变任何 TerminalId、ConnectionId 或 Professional 引用。

Pole 继续使用现有 MoveCommand；不为两个类型强行建立任意图元通用 Command。

### 9.3 跟线和 Professional 同步

```text
Replace RingCabinetLayout.Position
→ Rebuild DrawingScene
→ TerminalAnchorIndex 按新柜体位置重建
→ OverheadLine 从相同 TerminalId 解析新端点
→ WorkScope / GroundingPoint 使用同一新 Anchor
```

移动仅改变几何。Connection、ElectricalNode、Topology、WorkScope Boundary 和 GroundingPoint TerminalId 保持原值。FormatVersion 2 的 OverheadLine Start/End 仍只在 Save 时从 Anchor 回填。

## 10. 统一 Device Drag Preview

现有 Pole 流程由 MainWindow 分别处理 Begin、Update、Commit，并在 MouseMove 时直接 Replace RuntimeLayout。主要风险是 Cancel 路径必须显式恢复 Before；当前 Esc 和保存提示的取消分支没有统一控制这一职责。

新增最小 `DeviceDragController`，只支持：

- `SelectionTargetKind.Device + PoleLayout`；
- `SelectionTargetKind.RingCabinet + RingCabinetLayout`。

状态：

```text
Idle
→ Armed(target, startPointerDocument, beforeLayout)
→ Dragging(currentLayout)
→ Commit / Cancel
→ Idle
```

Controller 负责：

- 根据 SelectionReference 解析两类 Layout；
- 用统一 ViewToDocument 后的指针计算毫米 delta；
- Preview 时 Replace 当前 RuntimeLayout 并请求 Scene 重建；
- Commit 时创建对应 Move Command 并进入现有 CommandStack；
- Cancel、Esc、LostCapture、工程切换时恢复 Before 并重建 Scene；
- 释放鼠标捕获由 Desktop Adapter 完成。

Preview 可以暂时改变编辑期 RuntimeLayout，以便线路和 Professional Anchor 实时跟随，但它不是已提交事实：

- 不进入 CommandStack；
- 不产生 Dirty；
- 保存前必须 Commit 或 Cancel；
- Cancel 必须恢复 Before；
- 只有 Commit 后才允许生成 ProjectLayoutSnapshot。

## 11. Tool Interaction

### 11.1 允许的导航

- OverheadLine 选完起点后允许滚轮 Zoom 和中键 Pan；起点 TerminalId 保持不变。
- Zoom 后 Preview 的 start 由 TerminalAnchor 重算；current pointer 重新通过 ViewToDocument 更新。
- Pan MouseMove 不提交终点；Pan 结束后用当前鼠标 DIP 更新一次 Preview 文档终点。
- Placement 模式允许 Zoom/Pan，中键事件必须被 ViewportController 消耗，不能落下设备。
- Selection 在 Zoom/Pan 前后保持同一稳定 SelectionReference。

### 11.2 禁止的并发

- Device Drag 活动时禁止 Zoom、Pan、Placement 和 Connection Commit；用户先 MouseUp 或 Esc。
- 开始 Device Drag 前，Placement/Connection 工具必须处于 Idle。
- Fit Drawing 可在 Placement 或 Connection Pick 状态使用，但不可在 Device Drag/Pan 过程中执行。
- Esc 优先取消 Device Drag，其次取消 Pan/当前 Drawing Tool，避免只取消某一个 Controller。

`DrawingToolCoordinator` 继续协调 Placement 与 Connection；ViewportController 不成为 Drawing Tool，因为 Zoom/Pan 不创建工程对象。建议增加一个很小的 Desktop `EditorInputCoordinator`，只决定 Drag、Pan、Drawing Tool 的输入优先级，不建设通用 CAD 工具框架。

## 12. HitTest、Selection 与容差

统一流程：

```text
Mouse DIP
→ CanvasViewTransform.ViewToDocument
→ DrawingScene.HitTestIndex.HitTest(documentPoint)
→ stable SelectionReference
```

Zoom/Pan 不重建 Selection，也不改变 HitTestIndex 中的文档 Bounds。Overlay 仍由 SelectionReference 找到文档 Bounds，再与 Scene 一起经过统一视觉变换。

Terminal Pick tolerance 必须从视觉 DIP 转为毫米：

```text
toleranceMm = ViewDipToDocumentLength(8 DIP)
```

普通 GroundingPoint/WorkScope Pick 和 OverheadLine Pick 应共享同一 tolerance 转换入口。可以保留不同业务过滤规则，但不能一个用固定 mm、另一个用视觉 DIP。

P0-5 可顺带把线路 HitTest 从“大包围矩形包含”修正为文档空间点到线段距离，距离阈值同样来自视觉 DIP；这属于 Zoom 后保证正确选择的必要修复，不是高级选择功能。

## 13. MainWindow 减负

MainWindow 当前约 1275 行。P0-5 不应新增 ViewScale、Translation、Pan Start、Drag Before/After 和 Fit 数学字段。

新增职责对象：

- `CanvasViewTransform`：纯变换状态和双向转换；
- `CanvasViewportController`：Zoom/Pan/Fit 与 Viewport Size；
- `DeviceDragController`：Pole/RingCabinet Drag 状态和 Move Command；
- 可选 `EditorInputCoordinator`：少量输入优先级和 Esc 取消顺序。

MainWindow 只保留：

- WPF Mouse/Key/Wheel/SizeChanged 事件转发；
- Zoom In/Out/Fit 菜单调用；
- VisualHost 刷新；
- 捕获/释放鼠标和最小错误提示。

现有 Professional UI 和演示入口不在本阶段重构，但 P0-5 新逻辑不得继续复制它们的模式。

## 14. Persistence 边界

保存内容保持：

```text
RuntimeLayout(mm)
→ ProjectLayoutSnapshot(mm)
```

不保存：Scale、Translation、Viewport Size、Selection、Pan 状态、Drag Preview。Zoom/Pan 不进入 Undo/Redo，不产生 Dirty。

RingCabinet Commit 后的新毫米 Position 会正常进入现有 RingCabinetLayout DTO；无需修改 DTO 或 FormatVersion。Save/Reload 后工程几何保持，视图使用默认值或 Fit。

## 15. 验收场景

| 场景 | 验收结果 |
| --- | --- |
| A. New → Place → Zoom/Pan → 再 Place | 新对象毫米位置等于点击 DIP 经 ViewToDocument 的结果 |
| B. Zoom/Pan → Pick A → Preview → Pick B | 命中真实 Terminal，Preview 贴合鼠标，正式端点贴合 Anchor |
| C. Move Pole | OverheadLine、GroundingPoint、WorkScope 同步，ConnectionId/TerminalId 不变 |
| D. Move RingCabinet | 柜侧 OverheadLine 端点和 Professional 标记同步 |
| E. Move RingCabinet → Undo → Redo | Position 往返，Domain、Topology、稳定 ID 不变 |
| F. Move → Save → Close → Open | Pole/RingCabinet 毫米坐标和线路端点一致，新 Session clean |
| G. Fit Drawing | Pole、柜、线路和 Professional 标记均位于 margin 内 |
| H. 空工程 Fit | 不报错，回到默认 View |
| I. 连续 Zoom/Pan/Fit | CommandStack、Dirty、RuntimeLayout、ProjectSnapshot 均不改变 |
| J. Drag → Esc | 恢复 Before，线路/标记复位，不新增 Command、不产生 Dirty |

## 16. P0-5-B 推荐实现范围

RingCabinet Move、Zoom、Pan、Fit 在架构上相互关联，但一次实现会同时改动渲染矩阵、全部鼠标坐标入口、输入互斥和两类 Drag。为降低回归风险，建议作为一个 P0-5 里程碑的两个连续切片：

### P0-5-B1：统一 ViewTransform 与导航

新增：

- `CanvasViewTransform`；
- `CanvasViewportController`；
- `DrawingSceneBoundsCalculator`；
- VisualHost/Render Adapter 的统一 Matrix 应用。

修改：

- MainWindow 增加 MouseWheel、中键、Zoom In/Out/Fit 的薄事件接线；
- 所有 Mouse DIP、Placement、Terminal Pick、Preview、HitTest 容差改走 ViewTransform；
- DrawingTool 在 Zoom/Pan 后保持状态；
- 修正 OverheadLine 线段距离 HitTest。

验收：A、B、G、H、I，以及 Zoom/Pan 后普通 Selection/Overlay 正确。

### P0-5-B2：统一 Device Drag 与 RingCabinet Move

新增/修改：

- `RingCabinetLayout.MoveTo`；
- `RuntimeLayoutDocument.ReplaceRingCabinet`；
- `MoveRingCabinetCommand`；
- `DeviceDragController`；
- 最小输入协调和保存前 Commit/Cancel 接线；
- 用新 Controller 替换 MainWindow 中 Pole 专用 Drag 状态编排。

验收：C、D、E、F、J。

两个切片应连续完成，B1 建立的 ViewTransform API 是 B2 的唯一坐标入口。若团队更偏好单次交付，也可在一个分支完成后统一验收，但不建议在未验证 B1 坐标数学前同时调试 RingCabinet Move。

## 17. 明确不实现

P0-5 不包含 Cable、RingCabinet 配置器、PoleAttachment/CableTermination 创建、Copy/Paste、多选、Snap/Align、Export/Print、WorkTicketData、Mini-map、无限画布高级导航或 FormatVersion 升级。

## 18. 实现完成判定

P0-5 完成不以“画面能放大”判断，而以以下闭环同时成立判断：所有鼠标输入只经 CanvasViewTransform；Scene/Layout/Anchor 保持毫米语义；Zoom/Pan/Fit 不影响工程数据和 Dirty；Pole/RingCabinet 均可 Preview、Commit、Cancel、Undo/Redo；线路与 Professional Anchor 自动跟随；Save/Reload 保持毫米位置；MainWindow 不保存变换数学或 Drag 状态机。
