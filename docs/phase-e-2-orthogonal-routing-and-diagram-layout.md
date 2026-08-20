# Phase E-2 — Orthogonal Routing & Diagram Layout

状态：**Implemented / Pending Windows Validation**。

## 1. Architecture contract

电气事实仍唯一来自 `Terminal`、`ElectricalNode`、`Connection`、`CableSegment` 和 `OverheadLine`。线路显示链路为：

```text
Domain topology + current RuntimeLayout
→ directional TerminalAnchor
→ RoutingObstacleBuilder
→ OrthogonalRoutePlanner / OrthogonalRouter
→ transient OrthogonalRoute
→ RouteCrossingDetector
→ LineJumpDecorator
→ SceneLine / SceneArc
```

`OrthogonalRoute` 是每次 Scene build 重新派生的 Rendering 状态，不进入 Domain、`RuntimeLayoutDocument`、CommandStack 或 Persistence。工程格式保持 V6；没有 Route、Waypoint、Bend、Crossing 或 Jump DTO，也没有人工折点编辑。

## 2. Directional terminal anchors

`TerminalAnchor` 在既有稳定 `TerminalId + document-mm Position` 基础上增加 Rendering-only 的 `Auto / Left / Right / Up / Down` 方向：

- RingCabinet external cable terminal：Down；
- 水平 Pole Switch：Left / Right；
- DropoutFuse：Up / Down；
- CableTermination CableSide（外侧 apex）：Up；
- CableTermination OverheadSide（靠 Pole 的底边中心）：Down；
- Pole overhead 与 IntermediateTerminal：Auto。

位置和方向都由 `TerminalAnchorIndex` 通过现有专业几何取得；Domain `Terminal` 没有变化。

## 3. Routing and obstacles

Cable 与 OverheadLine 在同一个 planning context 中按 `ConnectionId` 升序规划。Router 使用固定候选集：aligned direct、HV/VH L route、水平/垂直 dogleg、obstacle edge channel 和 parallel offset channel。评分顺序固定为设备穿越、线路重合、无关交叉、折点数、路径长度、候选优先级及坐标键，不使用随机数或容器原始遍历顺序。

每条线路先按 terminal direction 形成 port stub。source/target 所属 obstacle 允许线路沿端口方向离开。V1 obstacle 来自当前 Layout／专业 logical geometry：RingCabinet overall bounds、Pole、PoleAttachment（含 CableTermination）和 IntermediateTerminal。Label、Preview、Selection overlay、Line Jump 与 RingCabinet 内部图元不作为 obstacle。

`DrawingMetrics` 集中维护第一版 document-mm 工程比例：port stub、obstacle clearance、parallel spacing、minimum dogleg、crossing tolerance、snap tolerance、jump radius 和 endpoint clearance。这些值不是行业绝对尺寸。

## 4. Formal scene, labels and hit testing

正式 Cable 与 OverheadLine 均由 `OrthogonalRoute` 投影，不再以 Start→End 斜线绘制：

- Cable 保持 Dashed；
- OverheadLine 保持 Solid；
- 每个正交 segment 使用原 CableSegment／Connection selection identity；
- Cable label anchor 使用 route arc-length midpoint，再进入现有 `LabelLayoutEngine`；
- Stable ID、TerminalId、ConnectionId 和 topology 均不改变。

兼容性的低层 DrawingSceneBuilder overload 在没有 Connection／TerminalAnchor 数据时使用传入的历史 `OverheadLineLayout` 端点生成确定的 direct/L 正交线；真实 `DrawingDocument` 主链路总是使用统一 Router。`SupportPoleIds` 不解释为 route waypoint，因为当前合同不定义其可靠几何顺序。

## 5. Crossing and line jump

`RouteCrossingDetector` 区分 independent perpendicular interior crossing、endpoint touch、collinear overlap 和 separated parallel segments。视觉交叉永远不会创建 `ElectricalNode`、`Terminal` 或 `Connection`；共享 topology terminal 的连接不画 jump，unrelated endpoint touch 也不解释为电气连接。

`LineJumpDecorator` 仅在 Scene projection 阶段工作，原始 Route 保持正交。独立内部交叉由较大的 `ConnectionId` 线路画半月形 `SceneArc`；Arc 继承所属线路的 Solid／Dashed 和统一线宽。交叉离 terminal、route endpoint 或 bend 小于安全距离时保留普通 crossing。Cable dashed arc 的 dash phase 连续性属于 Windows 视觉验收项。

## 6. Preview, snapping and move reroute

Cable／OverheadLine 的 transient picking preview 使用统一 Router 的轻量 direct/L 正交预览；Cable 为 Dashed，OverheadLine 为 Solid。选中真实终端后，正式 Scene 会重新运行完整 Router，不保存 mouse preview。

`DeviceDragController` 在现有 Layout preview 链路调用 `LayoutSnapService`。RingCabinet／Pole 的 logical center 在 X 或 Y 进入 tolerance 时吸附，可同时吸附两轴。Commit 写入的是 snapped Layout position；现有 Move Command 和 CommandStack 因此自然支持 Undo／Redo。

设备移动、Undo、Redo 或 Save/Open 后统一执行：

```text
RuntimeLayout change
→ RebuildScene
→ rebuild anchors and obstacles
→ deterministic reroute
→ rebuild crossing/jumps
```

没有 AutoRouteCommand、MoveCableRouteCommand 或任何 route history。

## 7. Persistence boundary

CurrentVersion 保持 V6。保存的是既有 Domain topology 与 device layout；Business route、bend、jump、crossing、preview 和 alignment candidate 均不保存。OverheadLine 历史 Start/End 继续只是兼容字段，正式 Scene endpoint 以恢复后的 TerminalAnchor 为准。

## 8. Automated verification scope

新增／扩展测试覆盖：

- horizontal／vertical direct、directional port exit、L/dogleg、正交约束；
- duplicate point cleanup、zero-length cleanup、collinear merge、arc-length midpoint；
- obstacle avoidance、parallel overlap penalty、稳定 ConnectionId 排序和输入顺序确定性；
- independent crossing、shared-terminal connection、unrelated endpoint touch、overlap、same-route adjacency；
- jump owner、endpoint clearance、Cable dashed／Overhead solid Arc 和 SceneArc rendering/bounds；
- Cable／OverheadLine 正交 Preview；
- Pole／RingCabinet X/Y snap、超出 tolerance、snapped Move Undo/Redo；
- Cable multi-segment Scene identity、label 和 hit-test。

Domain/Application/Infrastructure 回归用于确认 E-2 没有改变 topology、Command 或 persistence semantics。WPF TestHost 在 macOS 缺少 `Microsoft.WindowsDesktop.App 10.0.0` 时只能确认项目编译，实际断言必须在 Windows 运行。

## 9. Windows validation checklist

1. Cable 全程虚线、OverheadLine 全程实线，所有正式与 Preview segment 均水平／垂直；
2. RingCabinet、Pole、Attachment、CableTermination、Joint 不被明显穿越；
3. 多线路尽量分离，重建、Undo/Redo、Save/Open 后线路形状一致；
4. 移动 Pole／RingCabinet 后线路自动重路由，Undo/Redo 恢复确定线路；
5. 每个线路 segment 均可命中且保持原业务 Selection；
6. 独立 crossing 仅由较大 ConnectionId 线路画 jump，共享 Terminal 不画 jump；
7. Cable dashed jump、Overhead solid jump、靠近 endpoint/bend 的 crossing 视觉正确；
8. Cable label 位于线路累计长度中点附近并继续参与 LabelLayout；
9. X/Y alignment snap 在缩放、平移后仍按 document-mm 工作；
10. Cable create/delete/reconnect/property edit、OverheadLine create/delete、Switch operation、Fit、Zoom/Pan 和 clipping 无回归。
