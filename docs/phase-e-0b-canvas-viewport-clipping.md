# Phase E-0B — Canvas Viewport Clipping

## 1. 阶段目标

Phase E-0B 只解决中央绘图区的 WPF viewport 边界问题：Zoom、Pan 或 Preview 后，`DrawingVisual` 内容不得越过中央画布，覆盖工具箱、Inspector、Menu、StatusBar 或其他非画布控件。

本阶段不修改 Domain、RuntimeLayout、Persistence、Scene 工程坐标、Zoom 范围或后续 Phase E 图元规范。

## 2. 原问题与根因

`MainWindow` 使用三列 Grid 布局，中央列的 `Border` 内直接放置自定义 `DrawingVisualHost`。Grid 和 Border 负责测量、排列和绘制边框，但默认不裁剪子元素内容。

`DrawingVisualHost` 原先同样没有设置 `ClipToBounds` 或 `Clip`。正式 Scene 被渲染为其 Visual child，并通过 `MatrixTransform` 应用 Zoom/Pan。WPF 允许 Visual child 绘制到父元素布局边界之外，因此变换后的内容能够侵入左右 Grid 列，甚至覆盖其他 UI 区域。

这不是 ViewTransform、Scene 坐标或 RuntimeLayout 错误，而是 `DrawingVisualHost` 缺少 View 层裁剪边界。

## 3. Clipping 实现

裁剪合同放在 `DistributionDrawing.Rendering.Wpf.Canvas.DrawingVisualHost`：

- 构造时设置 `ClipToBounds = true`；
- 覆盖 `OnRenderSizeChanged`；
- 每次实际 `RenderSize` 变化时创建 `Rect(0, 0, RenderSize.Width, RenderSize.Height)`；
- 将该矩形设置为冻结的 `RectangleGeometry Clip`。

裁剪尺寸没有硬编码，始终使用 `DrawingVisualHost` 的当前 WPF 布局尺寸。把合同放在 Host 自身，也确保未来其他位置复用该 Host 时不会遗漏 viewport clipping。

没有建立新的 Canvas 架构，也没有为左、右 UI 增加覆盖层或输入过滤层。

## 4. View-only 边界

Clip 是 WPF `UIElement` 的显示属性，只影响最终像素是否显示。被 Pan 到 viewport 外的 SceneElement：

- 仍存在于 `DrawingScene`；
- Domain 和 RuntimeLayout 保持不变；
- Persistence 不保存 Clip 或 ViewTransform；
- 重新 Pan 回 viewport 后能够再次显示。

因此不需要修改 Domain 坐标、Scene geometry、TerminalAnchor 或线路拓扑。

## 5. Zoom / Pan / Fit

现有 Zoom/Pan/Fit 继续只更新 `CanvasViewTransform`：

- Zoom scale 范围未修改；
- MouseWheel 锚点缩放未修改；
- Pan translation 未修改；
- Fit 计算未修改；
- Document ↔ View 转换未修改。

Transform 继续应用在 DrawingVisual child 上，Clip 固定在 Host 的 viewport 坐标系，因此 Zoom/Pan 不会移动或缩放裁剪边界。

## 6. HitTest 与 Interaction

Mouse、MouseWheel、Drag 和 Picking 事件原本只绑定到 `DrawingSurface`，事件坐标通过 `e.GetPosition(DrawingSurface)` 获取。裁剪后的 Host 不会在左右 UI 区域形成可见或可输入的越界内容，因此不增加全局输入过滤。

中央 viewport 内的以下链路保持不变：

- Selection / HitTestIndex；
- Device drag；
- Switch 双击；
- Cable Terminal picking；
- Cable reconnect picking；
- OverheadLine picking；
- MouseWheel zoom；
- Middle-button pan。

## 7. Preview

`RenderCurrentScene` 先合并：

1. 正式 `_currentScene.Elements`；
2. `_drawingTools.CreateTransientElements()`；
3. `SelectionOverlayBuilder.CreateElements(...)`。

合并结果由 `DrawingSceneRenderer` 生成单个 DrawingVisual，再交给同一个 `DrawingVisualHost.Show(...)`。因此以下内容共享完全相同的 Host Clip：

- 正式 Scene；
- Cable preview；
- OverheadLine preview；
- Device drag preview；
- Selection highlight。

不存在“正式 Scene 被裁剪但 Preview 未裁剪”的平行视觉路径。

## 8. Resize 行为

WPF 在窗口缩小、放大、最大化或恢复时重新排列中央 Grid 列和 DrawingVisualHost。`OnRenderSizeChanged` 每次根据新的 `RenderSize` 替换 RectangleGeometry，因此不会保留旧 Clip 尺寸，也不依赖固定像素值。

## 9. 测试

新增 WPF 结构测试验证：

- DrawingVisualHost 启用 `ClipToBounds`；
- Arrange 后 Clip 等于实际 RenderSize；
- Resize 后 Clip 更新为新尺寸；
- Zoom/Pan 后 Clip 保持 viewport 尺寸。

补充架构合同测试，防止 `CanvasViewTransform` 暴露到：

- `DrawingDocument`；
- `RuntimeLayoutDocument`；
- `ProjectLayoutDto`；
- `ProjectLayoutSnapshot`。

现有 MouseWheel Zoom 和 Middle-button Pan 测试继续验证 Transform 语义。

当前 macOS 验证结果：

- Solution build：Domain 成功，进入 Windows/WPF 阶段后约 5 分钟没有编译诊断，最终以 0 warning / 0 error 的失败状态退出；
- Rendering.Wpf 与 Desktop 独立 build：进入相同 Windows/WPF 工具链等待，没有产生源码诊断，短时确认后终止；
- Rendering.Wpf.Tests 与 Desktop.Tests build：因本地缺少各自的 `obj/project.assets.json` 返回 `NETSDK1004`；受限环境中的 restore 未能完成；
- `git diff --check`：通过。

上述结果没有证明 WPF 编译或测试通过，也没有产生可归因到本次源码的编译错误。最终 WPF build、TestHost 和运行验证必须在 Windows 完成；不为 macOS 修改 `net10.0-windows` 或 WPF 项目结构。

## 10. Windows 待验证内容

需要在 Windows 实机验证：

- Zoom in/out 后内容不覆盖左侧工具箱和右侧 Inspector；
- 大范围 Pan 后内容不覆盖 Menu、StatusBar 或其他 UI；
- Fit 后 viewport 边界正常；
- Selection、Drag、Switch 双击正常；
- Cable 创建、Cable reconnect、OverheadLine 的 Terminal picking 正常；
- Cable/OverheadLine/Drag Preview 在左右边界被裁剪；
- 窗口缩小、放大、最大化、恢复后 Clip 与中央绘图区一致；
- Pan 出 viewport 的对象在 Pan 回来后重新显示。

## 11. 明确不包含内容

Phase E-0B 未实现：

- Word 图元或 RingCabinet 视觉重绘；
- Cable/OverheadLine 新线型；
- Orthogonal Routing；
- Snap / Alignment；
- Auto sizing / obstacle avoidance；
- Crossing detection / Line jump；
- Domain、Topology、Persistence 或 E-0A Template/PT 规则修改。
