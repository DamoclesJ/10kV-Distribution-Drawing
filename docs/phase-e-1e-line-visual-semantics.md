# Phase E-1E — OverheadLine / Cable Line Visual Semantics

> 状态：**Implemented / Pending Windows Validation**
> 基线：`eb5d645`
> 视觉依据：`配电专业附图图元.docx`

## 1. 范围

本阶段只冻结线路的视觉线型语义：

- `OverheadLine` 使用实线；
- `Cable` 使用虚线。

Cable/OverheadLine 的 Domain topology、TerminalId、Connection、Stable ID、CableType、Length、CommandStack、Runtime editing workflow 和 Persistence 均未修改。当前 Route 仍是 `Start → End` 直线路径。

本阶段没有实现 Orthogonal Routing、折点、Waypoint、Path Editing、Snap、Alignment、Obstacle Avoidance、Crossing Detection、Line Jump 或其他线路路径算法。

## 2. SceneStrokeStyle 与 Line Metrics

`LineSymbolDefinition` 根据 SymbolKind 选择 Scene 投影：

| 业务对象 | SymbolKind | StrokeStyle |
| --- | --- | --- |
| OverheadLine | `OverheadLine` | `Solid` |
| CableSegment | `CableLine` | `Dashed` |
| GroundingLine | `GroundingLine` | `Solid` |

`SceneLine` 仍只保存抽象 `SceneStrokeStyle`，不暴露 WPF `Pen` 或 `DashArray`。`DrawingSceneRenderer.CreatePen(...)` 统一把 Dashed 映射为 WPF Pen，并从 `DrawingMetrics.Line.CableDashLength` / `CableDashGap` 读取虚线节距。

Cable 与 OverheadLine 共用 `DrawingMetrics.Line.ConnectionThickness`。虚线样式、实线样式和 Dash 参数都是 Rendering projection，不写入 Domain 或 Persistence。

## 3. 正式 Scene 与 Preview

正式 Scene 路径保持：

```text
DrawingSceneBuilder
→ CableRenderer / OverheadLineSegment
→ SymbolLibrary
→ LineSymbolDefinition
→ SceneLine
→ DrawingSceneRenderer
```

Cable 创建 Preview 使用 Dashed，OverheadLine 创建 Preview 使用 Solid；Preview 仍是临时 SceneElement，不建立第二套业务线路模型。两种 Preview 继续使用现有 TerminalAnchor 和当前直线 Start/End 坐标。

## 4. Anchor、Selection 与拓扑回归

本阶段没有修改 `TerminalAnchorIndex`。E-1D 已建立的投影继续生效：

- Pole overhead anchor 对齐圆形 Pole 的线路轴；
- PoleAttachment Switch 的两端 anchor 分离；
- CableTermination CableSide 对齐三角形电缆侧，OverheadSide 保持架空线路侧；
- RingCabinet anchor 保持 E-1B 合同。

虚线不会改变 Cable 的逻辑 selection bounds，也不会要求用户点中某一段实际 dash。OverheadLine selection、Cable selection 和 Stable ID 映射保持原合同。

## 5. 回归边界

以下行为仍由已有测试和 Windows 验收覆盖：

- Cable 创建、属性修改、重连、删除、Undo/Redo；
- Cable Save/Open 后 CableType、Length、TerminalId 和 Connection 不变；
- OverheadLine 创建、Undo/Redo、Save/Open；
- 移动 Pole 或 CableTermination 后线路端点跟随新的 TerminalAnchor；
- CableSide 可建立 Cable，OverheadSide 不接受 Cable；
- RingCabinet、Pole、CableTermination 和 Joint 的原有 Scene/HitTest 不回归。

## 6. 当前验证状态

macOS 当前已确认：

- `DistributionDrawing.Rendering.Wpf.Tests` 项目编译成功，0 errors；
- `DistributionDrawing.sln` 应继续以 0 errors 为目标；
- WPF TestHost 实际运行仍需 Windows `Microsoft.WindowsDesktop.App 10.0.0`；
- Application.Tests 中既有 `RingCabinetTemplateDomainBuilderTests.cs` 的两个 `SwitchKind` 编译问题不属于本阶段；
- `git diff --check` 必须通过。

阶段状态为：**Implemented / Pending Windows Validation**。

## 7. Windows 验收清单

1. 新建 Cable 后立即显示虚线；
2. CableType/Length 修改后仍为虚线；
3. Cable 重连、删除、Undo/Redo 后线型正确；
4. Cable Save/Open 后仍由 Rendering 投影为虚线；
5. 新建 OverheadLine 后显示实线；
6. OverheadLine Undo/Redo、Save/Open 后仍为实线；
7. 移动 Pole 后 Cable 和 OverheadLine 端点跟随 E-1D anchor；
8. CableTermination CableSide / OverheadSide 语义不变；
9. 虚线不会破坏 Cable Selection；
10. Cable 和 OverheadLine Preview 分别保持 Dashed / Solid；
11. 场景仍为直线 Start → End，没有隐式 Routing、折点或避让。
