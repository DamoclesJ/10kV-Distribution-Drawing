# Phase E-1A — Rendering Primitives & Drawing Metrics

> 状态：**Implemented / Pending Windows Validation**<br>
> 实施日期：2026-08-19<br>
> 基线：`6056064`<br>
> 依据：`docs/phase-e-1-real-symbol-baseline-audit.md`

## 1. 目标与范围

Phase E-1A 为后续真实专业图元建立统一的 Scene primitive、线型语义和默认 Drawing Metrics。它只提供 Rendering 基础能力，不直接重画 RingCabinet、Interval、Switch、PT、Pole、CableTermination、Cable 或 OverheadLine。

本阶段新增：

- `SceneEllipse`；
- `ScenePolyline`；
- `SceneStrokeStyle.Solid/Dashed`；
- `DrawingMetrics` 统一入口；
- `DrawingSceneRenderer` 对新 primitive 和线型的 WPF 映射；
- `DrawingSceneBoundsCalculator` 对新 primitive 的范围计算；
- Primitive、HitTest、Renderer、Metrics 和架构边界测试。

本阶段没有修改 Domain、Persistence、RuntimeLayout DTO 或任何业务图元定义。

## 2. 现有 Primitive 审查

实施前 Scene 只有：

- `SceneLine`；
- `SceneRectangle`；
- `SceneText`。

这些 primitive 可以表达当前占位图，但不能直接、稳定地表达杆塔圆圈、PT 双圆线圈、电缆终端三角形和专业开关折线。`SceneLine` 也只有颜色与线宽，不能携带电缆虚线语义。

现有 HitTest 不执行 WPF 像素级 Geometry hit testing，而是使用 `SceneElement.HitTestBounds` 和 `SelectionTargetKind/TargetId`。该合同适合继续复用，新 primitive 不需要把 WPF Geometry 引入 Scene API。

## 3. Scene primitive 最终设计

### 3.1 SceneEllipse

`SceneEllipse` 使用 `DocumentRect Bounds` 表达外接矩形，并保存：

- Stroke；
- optional Fill；
- ThicknessMillimeters；
- SceneStrokeStyle；
- 从 `SceneElement` 继承的 TargetKind、TargetId、HitTestBounds。

选择 Bounds 而不是 Center + Radius 的原因是：同一 primitive 可以表达圆和椭圆，并与现有 `SceneRectangle`、`DocumentCoordinateSystem.ToRect(...)` 和 bounds calculator 保持一致。所有几何仍使用毫米文档坐标；WPF `EllipseGeometry` 只在 `DrawingSceneRenderer` 内部创建。

### 3.2 ScenePolyline

E-1 当前只需要线段序列，不需要 SVG/Bezier 完整路径系统。因此选择简单的 `ScenePolyline`：

- `IReadOnlyList<DocumentPoint> Points`；
- `IsClosed = false` 表达 open polyline；
- `IsClosed = true` 表达 closed polygon；
- closed polygon 可设置 Fill；
- Stroke、ThicknessMillimeters 和 SceneStrokeStyle；
- 自动计算整体 Bounds 和 HitTestBounds。

Open polyline 至少需要 2 个点，closed polygon 至少需要 3 个点。Scene API 不暴露 `PathGeometry`、`StreamGeometry` 或其他 WPF 类型。

该 primitive 足以支持后续三角形、刀闸、接地符号和多段线路。它不负责生成正交折点；Orthogonal Routing 仍是后续独立阶段。

### 3.3 兼容现有 primitive

`SceneLine` 和 `SceneRectangle` 仅新增尾部可选参数 `StrokeStyle`，默认值是 `Solid`。全部既有调用继续得到原来的实线视觉，不需要机械修改现有 SymbolDefinition。

## 4. StrokeStyle 设计

`SceneStrokeStyle` 当前只有：

- `Solid`；
- `Dashed`。

Scene primitive 保存业务无关的线型语义，不保存 WPF `DashStyle` 或 `DoubleCollection`。`DrawingSceneRenderer.CreatePen(...)` 是统一 WPF 映射入口：

- Solid 使用 WPF Pen 默认实线；
- Dashed 使用 `DrawingMetrics.Line` 中的固定 dash length 与 gap；
- dash 的工程毫米长度在映射时换算为 WPF Pen 所需的线宽倍数。

本阶段不提供自定义 dash pattern UI，也不把 Cable 改成 Dashed。E-1E 只需让 Cable 产生 `SceneStrokeStyle.Dashed`，无需直接操作 WPF DashArray。

## 5. Drawing Metrics 结构

统一入口为 `DistributionDrawing.Rendering.Wpf.Metrics.DrawingMetrics.Default`：

```text
DrawingMetrics
├── General
│   ├── StandardStrokeThickness
│   ├── ThinStrokeThickness
│   ├── StandardFontSize
│   └── SmallFontSize
├── RingCabinet
│   ├── StandardIntervalWidth / Height
│   ├── BusbarOffset / Height
│   ├── IntervalSpacing
│   └── CabinetNameOffset
├── Switch
│   ├── StandardSwitchLength
│   ├── GroundSwitchLength
│   └── ContactRadius
├── PT
│   ├── CoilRadius
│   └── CoilSpacing
├── Pole
│   └── PoleRadius
├── CableTermination
│   ├── TriangleWidth
│   └── TriangleHeight
├── Line
│   ├── ConnectionThickness
│   ├── CableDashLength
│   └── CableDashGap
└── LineJump
    └── Radius（仅预留）
```

### 5.1 数值来源

Word 没有给出绝对尺寸。本阶段默认值主要从当前项目已经使用的视觉比例提取，例如：

- 标准／细线宽来自现有 `0.8 / 0.6`；
- 标准／小字号来自现有 `4 / 3.5`；
- 标准间隔宽高来自现有 `60 / 125`；
- Switch length 来自现有 16；
- PT coil radius 从现有 14 宽 PT 占位尺度提取；
- CableTermination triangle 从现有 10 × 8 外部端子尺度提取。

这些数值是：**第一版工程绘图比例基线，后续可根据 Windows 截图和用户验收调整。**

它们不是 Word 给出的尺寸，也不得描述为行业标准。

### 5.2 Metrics 与 RuntimeLayout 的边界

- Metrics 是标准图元内部几何和新建默认布局的 Rendering 输入；
- RuntimeLayout 仍保存工程中实际采用的位置、尺寸和用户可编辑布局；
- 本阶段没有用 Metrics 覆盖或删除任何 RuntimeLayout 字段；
- 后续 LayoutFactory 可逐项引用 Metrics，但必须保持已保存布局和用户编辑能力。

## 6. Magic number 分类决定

本阶段统计到常用尺寸主要分布在：

- `RingCabinetLayoutFactory`；
- `IntegratedFeederIntervalSymbol`、`PTIntervalSymbol`；
- `TerminalAnchorIndex`；
- `PoleLayout`、`AttachmentLayout`、RingCabinet 各 Layout 默认值；
- `SwitchSymbolDefinition`、`PoleSymbolDefinition`、`CableTerminationSymbolDefinition`；
- `LabelLayoutEngine`；
- Selection overlay、Professional overlay 和 Canvas grid。

本阶段没有全局替换这些数字。分类原则是：

- 跨多个专业图元共享的标准线宽、字号、基础设备尺度、默认间隔尺度和线型参数进入 Drawing Metrics；
- Label 碰撞候选偏移、Selection margin、Canvas grid spacing 等特定算法参数暂时留在所属组件；
- RuntimeLayout 中已保存或可编辑的实际坐标／尺寸继续作为运行时事实；
- 某个具体 Symbol 内部、且不会跨符号复用的局部构造比例留给该 Symbol，避免把 Metrics 变成无边界常量仓库。

实际迁移在 E-1B～E-1E 随对应图元校准逐项进行。

## 7. Renderer 与 Bounds

`DrawingSceneRenderer` 现在统一支持：

- Line；
- Rectangle；
- Text；
- Ellipse；
- Polyline/closed polygon；
- Solid；
- Dashed。

Line、Rectangle、Ellipse 和 Polyline 共用同一个 `CreatePen(...)`；Fill 共用 `CreateOptionalBrush(...)`。Renderer 负责把毫米坐标转换为 DIP，并在内部创建／冻结 WPF Geometry、Brush 和 Pen。

`DrawingSceneBoundsCalculator` 已加入 Ellipse 和 Polyline，范围包含半个 stroke thickness，与现有 Line/Rectangle 规则一致。

## 8. HitTest 策略

E-1A 保持现有粗粒度 bounds hit testing：

- Ellipse 的默认 HitTestBounds 是外接 Bounds 向外扩展半个线宽；
- Polyline 的默认 HitTestBounds 是所有点的整体 Bounds 向外扩展半个线宽；
- TargetKind 和 TargetId 继续由业务 Renderer 设置或通过对象初始化器保留；
- `HitTestService` 无需知道 WPF Geometry，也不改变现有 Device、Switch、Cable 或 OverheadLine Selection。

该策略允许后续按业务需要用更宽的显式 HitTestBounds 覆盖默认值。本阶段不做 CAD 级逐段／逐像素命中测试。

## 9. 架构边界

新 primitive 和 Metrics 全部位于 `DistributionDrawing.Rendering.Wpf`：

- 不进入 Domain；
- 不进入 Persistence DTO 或文件格式；
- 不进入 RuntimeLayout 合同；
- 不进入 ElectricalNode、Terminal 或 Connection；
- 不影响 Stable ID；
- 不把 WPF Geometry 当作业务事实。

已增加反射边界测试，检查 Domain、RuntimeLayout 和 Persistence contract 不暴露 `DrawingMetrics`、`SceneStrokeStyle`、`SceneEllipse` 或 `ScenePolyline`。

## 10. 既有业务视觉兼容

本阶段没有修改任何现有 SymbolDefinition，也没有改变 SymbolLibrary 的业务分派：

- RingCabinet 外框仍保留；
- Interval、Switch、PT、Pole、CableTermination 仍使用原图形；
- Cable 当前仍是 Solid；
- OverheadLine 当前仍是 Solid；
- SceneBuilder、TerminalAnchor 和 Layout 行为未改变。

因此 Windows 截图理论上应与 E-1A 前基本一致。新 primitive 只有在 E-1B～E-1E 被业务 Symbol 使用后才会改变专业图元视觉。

## 11. 测试覆盖

新增／调整测试覆盖：

1. Ellipse 构造、Bounds、Fill 和 HitTest metadata；
2. Ellipse 默认 HitTestBounds 可用于选择；
3. open Polyline 构造、点集、Bounds 和粗粒度 HitTest；
4. closed polygon 构造、Fill 和 Bounds；
5. Scene bounds calculator 纳入 Ellipse/Polyline 线宽；
6. Renderer 生成 EllipseGeometry 和 StreamGeometry；
7. closed polygon Fill；
8. Solid/Dashed Pen 映射；
9. SceneLine 默认 Solid；
10. Cable 和 OverheadLine 当前继续 Solid；
11. Drawing Metrics 默认值稳定；
12. Metrics 不依赖 Domain，也不进入 RuntimeLayout；
13. Primitive/Metrics 不进入 Domain 或 Persistence contract。

WPF Renderer 和 Desktop 架构测试需要在 Windows TestHost 最终运行。macOS 不应通过修改 Windows TargetFramework 绕过。

## 12. 后续使用方式

- E-1B 使用 Ellipse/Polyline 和 Metrics 校准普通 RingCabinet，但不需再扩展 Scene 基础协议；
- E-1C 使用 Ellipse 表达 PT coil，使用 Polyline 表达三工位刀闸和接地结构；
- E-1D 使用 Ellipse 表达 Pole，使用 closed Polyline 表达 CableTermination 三角形；
- E-1E 将 Cable 的 Scene stroke 设为 Dashed，OverheadLine 保持 Solid；
- Orthogonal Routing、Snap、Alignment、避让、Crossing Detection 和 Line Jump 继续留在后续独立阶段。

## 13. 验证结果与 Windows 待验证

macOS 当前完成：

- `DistributionDrawing.sln` build：成功，0 error；
- `DistributionDrawing.Rendering.Wpf` build：成功；首次显示 26 个既有 nullable warning，后续 solution build 为 0 warning / 0 error；
- `DistributionDrawing.Desktop` build：成功，0 error；
- `DistributionDrawing.Rendering.Wpf.Tests` build：成功，0 warning / 0 error；
- `DistributionDrawing.Desktop.Tests` build：成功，0 error，3 个既有 xUnit analyzer warning。

两个 WPF 测试项目均已生成测试程序集，但 macOS 运行 TestHost 时缺少 `Microsoft.WindowsDesktop.App 10.0.0`，因此测试执行中止；这不是测试断言失败，也不构成 Windows 运行验证。

Windows 仍需验证：

1. Rendering.Wpf 和 Desktop 完整 build；
2. Rendering.Wpf.Tests、Desktop.Tests 全量运行；
3. Solid/Dashed WPF Pen 映射；
4. Ellipse、open Polyline 和 closed polygon 的实际 DrawingVisual；
5. 新 primitive 的选择 bounds；
6. 既有 RingCabinet、Pole、Cable、OverheadLine 截图无视觉回归；
7. Zoom/Pan/Fit 和 E-0B viewport clipping 无回归。

Phase E-1A 完成后停止，不自动进入 E-1B。
