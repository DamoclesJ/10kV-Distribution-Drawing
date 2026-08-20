# Project Current State

> 本文创建于 checkpoint commit `3c20457`（`Add project current state checkpoint`）。实际当前 HEAD 以 `git rev-parse HEAD` 为准。
>
> 本文是新会话交接用的当前状态摘要。仓库代码和最新设计文档始终是实现事实源，聊天历史不是事实源。

## 0. Windows V1 Product Calibration Baseline (2026-08-15)

以下事实已经由 Windows 环境实际验证：

- `DistributionDrawing.Rendering.Wpf.Tests`：110/110 PASS；
- `DistributionDrawing.Desktop.Tests`：20/20 PASS；
- `win-x64` publish：PASS；
- `artifacts/publish/desktop/win-x64/DistributionDrawing.Desktop.exe` 已成功生成；
- EXE 可以正常启动；
- Desktop 主窗口和 Canvas 可以正常显示。

Executable smoke test passed, but V1 product/visual accuracy is not yet accepted.

当前已进入 V1 产品校准阶段。上述结果只证明 Windows 构建、发布、启动和现有自动测试基线成立，不代表 Desktop 功能闭环、真实工作票图元准确性或 V1 产品验收已经完成。详细差距见 `docs/v1-product-gap-audit.md`。

本文后续章节包含较早 checkpoint 的历史状态；如与本节、当前代码或最新专项审计冲突，以当前代码和最新专项审计为准。

## 0.1 Phase E-0A — RingCabinet Template Creation & PT Closure (2026-08-19)

状态：**Implemented / Pending Windows Validation**。

Phase E 已进入 E-0A，当前实现事实如下：

- Desktop 环网柜主要创建入口已由逐行手工配置改为“柜名 + 柜型 + 业务间隔数量”的 Template 创建流程；
- 普通负荷开关柜支持 3/4/5/6 个业务间隔；
- 一二次融合柜支持 4/6 个业务间隔；
- 业务间隔按 Template 顺序自动命名为 `负1`～`负N`，并可通过 Property/CommandStack 修改；
- PT 已通过正式 `PTConfiguration`、`RingCabinetTemplateDomainBuilder` 和 `RingCabinetIntervalDefinition.CreatePT(...)` 接入正常 Desktop 创建链路；
- PT 创建入口不创建 DTU，也不冻结最终 PT/DTU 组合与位置规则；
- RingCabinet 创建仍由单个 `AddRingCabinetCommand` 原子加入 Domain + RuntimeLayout，Undo/Redo 保持相同 Stable IDs；
- Interval Type Change 已同步维护 Domain + RuntimeLayout，失败、Undo、Redo 均恢复一致状态；
- `PTSymbolPosition` 被确认是标准布局的确定性派生值，不扩展 Persistence 格式；加载时由统一 `RingCabinetLayoutFactory` 规则重建。

详细设计与范围见 `docs/phase-e-0a-ring-cabinet-template-and-pt-closure.md`。

Phase E-0A 不包含真实 Word 图元重绘、DTU、正交 Routing、Snap、Alignment、自动避让、Crossing Detection、Line Jump 或 viewport clipping。上述内容仍属于后续 Phase E 子阶段。

本轮 macOS 最终 solution/test/WPF 构建受环境异常阻断：构建进程约等待 5 分钟后以“0 个错误”退出失败，没有产生可归因到源码的编译诊断。未修改 Windows WPF TargetFramework 规避该问题；Phase E-0A 的最终编译、自动测试和运行验收仍需在 Windows 实机完成。静态 `git diff --check` 已通过。

## 0.2 Phase E-0B — Canvas Viewport Clipping (2026-08-19)

状态：**Implemented / Pending Windows Validation**。

中央 `DrawingVisualHost` 已建立严格 WPF viewport：启用 `ClipToBounds`，并在每次 `RenderSize` 变化时把 `RectangleGeometry Clip` 更新为实际中央绘图区尺寸。

该修改只属于 View 层：Zoom/Pan/Fit、Document ↔ View 坐标、Domain、RuntimeLayout、Persistence、TerminalAnchor、Cable/OverheadLine topology 均未改变。正式 Scene、Selection overlay、Device drag preview、Cable/OverheadLine transient preview 继续合成为同一个 DrawingVisual，因此统一受 Host Clip 约束；Cable reconnect picking 行为未改变。

已增加 ClipToBounds、RenderSize/Resize、Zoom/Pan 裁剪合同和 ViewTransform 架构边界测试。Windows 实机仍需验证左右 UI、Menu/StatusBar 不被覆盖，以及窗口缩放和全部既有交互回归。详细记录见 `docs/phase-e-0b-canvas-viewport-clipping.md`。

当前 macOS 验证中，Solution 仅完成 Domain 后即在 Windows/WPF 阶段无诊断等待并失败；Rendering.Wpf/Desktop 独立 build 同样无法完成，两个 WPF 测试项目则因缺少本地 restore 资产返回 `NETSDK1004`。这些结果不代表源码编译失败或通过；`git diff --check` 已通过，最终结论等待 Windows。

Phase E-0B 不包含图元重绘、Routing、Snap、Alignment、自动避让、Crossing Detection 或 Line Jump；Phase E-1 图元实现尚未开始。

## 0.3 Phase E-1 — Real Symbol Baseline Audit (2026-08-19)

状态：**Audit Completed**。图元实施状态见后续 E-1A／E-1B 小节。

已完整检查《配电专业附图图元.docx》的 3 个页面、图元表、38 个嵌入图片和两张组合图，并完成 Word 视觉合同、当前 Rendering/Layout/Domain 差异、Drawing Metrics 建议以及 E-1A～E-1E 实施顺序与验收标准。

审计确认：已有 MVP 对象的主要缺口位于 Rendering Geometry、Layout 和 Scene primitive；普通开关与现有三工位组合所需的 Domain 状态基本足够。架空变压器、运行／检修位置、在运／拆除及当前 MVP 外独立站内设备仍是 Domain／产品范围缺口，不在本轮实现。

本轮只新增审计文档，没有实施任何真实图元重绘。详细结论见 `docs/phase-e-1-real-symbol-baseline-audit.md`。

## 0.4 Phase E-1A — Rendering Primitives & Drawing Metrics (2026-08-19)

状态：**Implemented / Pending Windows Validation**。

已新增毫米文档坐标的 `SceneEllipse`、open/closed `ScenePolyline`、`SceneStrokeStyle.Solid/Dashed` 和集中式 `DrawingMetrics.Default`。`DrawingSceneRenderer` 已统一映射 Line、Rectangle、Text、Ellipse、Polyline 及 Solid/Dashed Pen，`DrawingSceneBoundsCalculator` 和现有 bounds-based HitTest 已覆盖新 primitive。

Drawing Metrics 是第一版工程绘图比例基线，数值来自当前项目已有视觉比例，不是 Word 或行业绝对尺寸；后续需根据 Windows 截图和用户验收调整。Metrics 和新 primitive 不进入 Domain、RuntimeLayout 或 Persistence。

E-1A 本身没有重画 RingCabinet、Interval、Switch、PT、Pole、CableTermination、Cable 或 OverheadLine；Cable 和 OverheadLine 当前仍保持 Solid。后续环网柜实施状态见 E-1B 小节。

当前 macOS 已完成 Solution、Rendering.Wpf、Desktop、Rendering.Wpf.Tests build 和 Desktop.Tests build。两个 WPF 测试项目因 macOS 缺少 `Microsoft.WindowsDesktop.App 10.0.0` 无法运行 TestHost；自动测试实际执行和既有视觉无回归结论仍等待 Windows 验证。详细记录见 `docs/phase-e-1a-rendering-primitives-and-metrics.md`。

## 0.5 Phase E-1B — Ring Cabinet Professional Symbol System (2026-08-19)

状态：**Implemented / Pending Windows Validation**。

原计划 E-1B 与 E-1C 已合并实施。普通负荷开关柜、一二次融合柜、三种 `GroundingStructureKind` 和 PT Interval 已改用专业电气几何；所有状态继续来自现有 `SwitchDevice.SwitchState` 和 Domain interlock，没有增加视觉专用 Domain 状态。

RingCabinet 与 Interval 不再绘制可见外围矩形。连续母线、等宽间隔、居中柜名、刀闸／断路器／接地支路、PT 双圆线圈和向下电缆终端三角形统一使用 E-1A Scene primitive 与 Drawing Metrics。新增非渲染 `SceneLogicalBounds` 保持 Fit／Scene extent，Selection 继续使用现有 cabinet/interval/switch hit-test index。

TerminalId、Connection、ElectricalNode 和 Persistence 格式没有变化；ExternalTerminal anchor 只移动到新三角形视觉端点。Phase D-1 的 SwitchOperationController → CommandStack → Domain interlock → RebuildScene 操作路径保持不变。

当前实现没有涉及 Pole、PoleAttachment、独立 CableTermination 专项、Cable 虚线、OverheadLine 改造、Routing、Snap、Alignment、Avoidance、Crossing Detection 或 Line Jump。详细设计、测试与 Windows 验收清单见 `docs/phase-e-1b-ring-cabinet-professional-symbol-system.md`。

macOS 已完成 solution、Rendering.Wpf、Rendering.Wpf.Tests、Desktop 和 Desktop.Tests 构建；solution 为 0 warning / 0 error。Domain 55/55、Infrastructure 50/50 通过。WPF TestHost 因缺少 `Microsoft.WindowsDesktop.App 10.0.0` 未运行测试断言，Windows 自动测试、截图与交互验收仍待完成。

## 0.6 Phase E-1D — Pole / PoleAttachment / CableTermination Professional Symbols (2026-08-19)

状态：**Implemented / Pending Windows Validation**。

水泥杆已由旧“竖线 + 顶部横线”替换为空心圆；柱上 CircuitBreaker、LoadSwitch、IsolationSwitch 和 DropoutFuse 已按《配电专业附图图元.docx》分别实现独立 Open/Closed 专业几何，不复用 E-1B 环网柜内部 Switch 图元。CableTermination 已由矩形占位符替换为闭合三角形，Pole + CableTermination 形成相互独立身份的“圆圈 + 三角形”组合。

DrawingMetrics 已补充 PoleAttachment 和 CableTermination 所需工程比例，PoleLayout/AttachmentLayout 默认值由 Metrics 派生。TerminalAnchorIndex 继续是唯一锚点索引：Pole overhead 对齐圆心，柱上 Switch 两端分离到线路入口/出口，CableTermination CableSide/OverheadSide 分别对齐三角形外侧和 Pole 侧。Selection/HitTest、Label Runtime、Stable ID、Domain、CommandStack 和 Persistence 格式保持。

本阶段没有修改 Cable/OverheadLine 线型，没有开始 E-1E、Routing、Snap、Alignment、Avoidance、Crossing Detection、Line Jump 或 DTU。Word 中的架空变压器 R45304 等对象仍缺少当前 Domain 模型，不在 E-1D 范围。绝对尺寸仍是项目工程比例基线，不是行业标准。

详细实现和 Windows 验收清单见 `docs/phase-e-1d-pole-and-cable-termination-professional-symbols.md`。

## 1. Project Identity

本项目是面向 10kV 配电专业场景的 Windows 桌面绘图软件。

核心优先级为：

1. Drawing Core；
2. Electrical Domain / Topology correctness；
3. Professional semantics；
4. 在上述基础上扩展工作票草稿、拓扑维护等能力。

工程不是图片容器。文件中维护真实的 Device、Terminal、Connection、ElectricalNode、Professional 对象和 Layout；Rendering 只是这些事实的图形表达。

工作票自动或辅助生成是后续能力，不替代 Drawing Core，也不能反向推导或修改电气事实。

## 2. Stable Architecture Boundaries

### Domain / Topology

负责设备、端子、内部 ElectricalNode、外部 Connection、开关组合和领域校验。

不保存 WPF 类型、屏幕坐标、选择状态、视口状态或渲染临时对象。

当前主要对象包括 `DrawingDocument`、`RingCabinet`、`RingCabinetInterval`、`SwitchDevice`、`Pole`、`PoleAttachment`、`CableTermination`、`Terminal`、`Connection` 和 `OverheadLine`。

### Professional

负责已冻结的 `WorkScope`、`BoundaryPoint` 和 `GroundingPoint` 专业事实。

通过稳定 ID 引用 Domain/Topology；不从图形、开关状态或拓扑自动推导工作范围或接地点。

### Persistence

负责 `.kvdrawing` ZIP 容器、Manifest、Metadata、FormatVersion 2，以及 Domain、Topology、Professional 和 Layout DTO 的保存/恢复。

不保存 DrawingVisual、Selection、Overlay、Undo 历史、拖动 Preview、Zoom/Pan 或屏幕坐标。

### RuntimeLayout

`RuntimeLayoutDocument` 是编辑期间唯一的布局事实源，保存毫米工程坐标和图面布局对象。

不属于 Domain，不保存 ViewTransform，也不与 Persistence Snapshot 并行作为可编辑状态。

### Rendering / DrawingScene

`DrawingSceneBuilder`、SymbolLibrary、SceneElement 和 DrawingVisual 将 Domain/Professional + RuntimeLayout 转换为显示场景。

Rendering 不创建、保存或推导业务事实，不负责联锁、潮流、停电分析或工作票规则。

### TerminalAnchorIndex

以稳定 `TerminalId` 解析毫米文档坐标锚点，供 OverheadLine、Professional 标记、Selection 和 HitTest 使用。

正式架空线端点来自 TerminalAnchor；设备移动只改变几何，不改变拓扑 ID。

### Interaction / CommandStack

负责 Placement、Connection Tool、Device Drag、Selection、PropertyInspector、Command、Undo/Redo 和 Dirty 协调。

Command 失败不得进入历史，也不得留下 Domain/Layout 半状态。

### Desktop / Workspace

负责 WPF 输入、文件菜单、工程会话、Candidate Session 替换、对话框和最小错误反馈。

工程生命周期由 Workspace 协调对象处理；MainWindow 只做事件转发和显示接线。

### ViewTransform

`CanvasViewTransform` 负责 document millimeter ↔ viewport DIP 的转换、Zoom、Pan、Fit 和视觉容差换算。

View 状态不进入 Domain、RuntimeLayout、CommandStack、Dirty 或工程文件。

### Future WorkTicketData

WorkTicketData 是未来独立业务区，引用现有稳定 ID，不复制 Device、Terminal、WorkScope 或 GroundingPoint，也不直接修改 Domain/Topology。

## 3. Non-Negotiable Architecture Principles

- 普通编辑保持 Stable ID，不删除重建对象来模拟修改。
- Domain/Topology 保存专业事实和电气连接关系。
- Professional 保存 WorkScope/GroundingPoint 等专业事实。
- Layout 只保存图面几何和工程坐标。
- RuntimeLayout 是编辑期唯一布局事实源；Persistence Layout 是保存快照。
- Rendering 只表现事实，不保存或反推事实。
- TerminalAnchorIndex 是 Terminal → document mm anchor 的统一解析基础。
- OverheadLine 正式端点由 TerminalAnchor 解析。
- FormatVersion 2 的 OverheadLine `Start/End` 仅是兼容缓存，保存时由当前 Anchor 回填。
- ViewTransform 只处理 document mm ↔ viewport DIP。
- Zoom/Pan/Fit 不进入 Domain、CommandStack、Dirty 或 Persistence。
- Selection、Preview 和 Undo 历史不持久化。
- Professional 通过稳定 ID 引用 Domain/Topology。
- 不从 Rendering、开关状态或图形猜测 Professional 数据。
- 未确认的专业规则不得由实现自行定义。
- WorkTicketData 与 DrawingDocument 保持独立业务边界。

## 4. Completed Milestones

### Persistence Core

已完成 FormatVersion 2 的工程容器、Manifest/Metadata、Domain/Topology/Professional/Layout DTO、Candidate Session 加载、Runtime Layout 恢复和 Scene 重建基础。

正式 Tag：`phase-4-persistence-core`。

### Professional Core

已完成 BoundaryPoint、WorkScope、GroundingPoint、DrawingDocument 工程级校验、Professional 持久化、TerminalAnchor 显示、WorkScope 双边界显示、选择/高亮、只读属性查看，以及 GroundingPoint/WorkScope 的创建、删除、编辑、Undo/Redo、Dirty 和刷新闭环。

正式 Tag：`phase-5-professional-core`。

### WorkTicketData Architecture

已完成 WorkTicketData、SafetyMeasure、OperationStep 的独立业务区设计，尚未实现代码或工程格式接入。

### Drawing Core P0-1

已完成 Desktop 工程会话基础：新建、打开、保存、另存为、关闭/切换、Dirty 确认、Candidate Session 原子替换和真正空工程 Scene。

### Drawing Core P0-2

已完成 Pole 与最小 RingCabinet 的放置、选择、移动、删除、Undo/Redo、RuntimeLayout 同步和保存恢复基础。

RingCabinet Desktop 创建已支持当前 Domain 范围内的可配置和混合间隔组合。

### Drawing Core P0-3

已完成基于真实 TerminalAnchor 的 OverheadLine Terminal 连线入口、Preview、Connection + OverheadLine + Layout Command、选择、删除、Undo/Redo 和保存恢复基础。

当前 Cable 仍未实现。

### Drawing Core P0-5

已完成 CanvasViewTransform、Zoom In/Out、鼠标中心缩放、中键 Pan、Fit、统一 DIP/mm 入口、Terminal Pick 容差迁移，以及 Pole/RingCabinet 共用 DeviceDragController 的设计/实现基础。

RingCabinet Move 可保持 Domain/Topology/Stable ID 不变，并通过 TerminalAnchor 使线路和 Professional 图形跟随。

### Drawing Core P0-6-A

已完成真实设备配置能力审查与设计，提交文档为 `docs/real-device-configuration-design.md`。

该阶段没有生产代码实现。

## 5. Current User-Capable Workflow

当前代码已形成一条受限但结构化的绘图链路：

```text
New/Open
→ Place Pole / fixed RingCabinet
→ Move / Zoom / Pan / Fit
→ Pick external Terminal
→ Create OverheadLine
→ Select supported objects
→ Edit Pole number and Professional fields
→ Undo / Redo
→ Save / Reload
```

Professional 对象可以通过已有编辑入口加入工程并显示；其数据不由图形自动生成。

这仍不能称为完整可交付 Drawing Core MVP，原因是：

- Switch 图形操作和完整现场联锁尚未实现；
- Cable 只有 ConnectionType，没有完整 Cable Domain/Layout/Rendering/Editor；
- JPG 导出、打印和打印预览尚未完成真实验收；
- Windows 编译、运行和端到端保存验收尚未在当前环境完成。

## 6. Current Technical Debt / Validation Gap

当前已在 MacBook 环境使用 .NET SDK `10.0.400` 完成解决方案编译和 Domain 测试验证。

仍需在 Windows/WPF 实机环境验证：

- WPF 应用实际启动和运行；
- Desktop 创建、删除、Undo/Redo、Dirty、Save/Reload 端到端行为；
- Windows 实机显示和交互。

- 新建/打开/保存/另存为；
- P0-2 设备放置、移动、删除和恢复；
- P0-3 OverheadLine 连线、设备移动跟线和恢复；
- P0-5 Zoom/Pan/Fit、Terminal Pick、Drag；
- Professional 显示、选择和编辑；
- 后续 JPG、打印预览和实际打印机输出。

静态 `git diff --check` 不等于 Windows 实机验收。

### Drawing Core P0-6-C

P0-6-C 的 CableTermination + PoleAttachment 闭环代码已经完成，包含：

- CableTermination 创建及完整 Domain aggregate 注册；
- PoleAttachment 注册；
- CableTermination 的 CableSide/OverheadSide TerminalAnchor；
- PoleAttachment → AttachedDevice → CableTermination 的 Selection 解析；
- CableTermination Attachment 的只读 PropertyInspector 投影；
- Desktop CableTermination 创建入口；
- 通过统一“删除所选对象”入口删除 PoleAttachment；
- Add/Remove Command 的 Undo/Redo 基础支持及 Stable ID 保持；
- RuntimeLayout AttachmentLayout 的加入、删除和恢复；
- Dirty、Save/Reload 的代码路径和静态验证。

当前 P0-6-C 的 Git 实现和验证基线为：

- `94d676e`：CableTermination + PoleAttachment Domain/Command 基础；
- `04f86d8`：Anchor / Selection / Inspector；
- `b1e4f25`：Desktop CableTermination 创建闭环；
- `2d211bf`：Desktop PoleAttachment 删除闭环。
- `55f6586`：修复 Domain 编译错误；
- `42f0dcb`：修复 Rendering.Wpf 编译错误；
- `9b23a16`：修复 Desktop 编译错误；
- `c1e5f03`：修正 CableTermination terminal set 测试断言。

当前验证结果：

- 验证环境：MacBook；
- .NET SDK：`10.0.400`；
- `dotnet build src/DistributionDrawing.sln`：成功；
- Domain Tests：`55/55` 成功。

当前限制：

- Windows/WPF 实机运行验收仍待完成。

### Drawing Core P0-6-D-2

P0-6-D-2 已完成，包含：

- SelectionTransition Infrastructure；
- Move Selection Preserve；
- Undo/Redo Selection Restore；
- Add Attachment Selection Restore；
- Remove Attachment Selection Restore。

当前架构已将 Command 状态变化与 Selection UI 状态解耦：

- Command 负责 Domain/Layout 状态变化；
- SelectionTransition 负责编辑器 Selection 历史。

当前验证状态：

- Add/Move/Remove Selection 行为已接入；
- Undo/Redo Selection 恢复已接入。

## 7. Confirmed P0-6 Professional Decisions

以下决策已经确认：

- RingCabinet 允许普通负荷开关间隔与断路器/融合类间隔混合存在。
- Desktop 主要创建入口由用户选择柜型和业务间隔数量，并通过 Template 一次性生成合法间隔；创建后可再使用属性系统修改名称和支持的间隔类型。
- 六间隔“前四个负荷开关、后两个断路器”是常见实例，不是固定业务规则。
- 常用组合已进入参数化 Template 创建入口；Template 只约束当前产品入口，不限制 Domain 表达能力。
- 创建柜体时不要求用户一次性配置最终开关状态。
- 长期希望在图上点击具体开关，并经过 Domain/联锁规则校验后改变状态。
- 开关操作顺序和完整联锁不能由实现自行假设，应独立设计。
- 第一版不建立 Incoming、Outgoing、Tie、Transformer、Spare 等 Interval Usage 枚举。
- DTU 柜术语已确认，但其具体模型仍未实现。
- PTInterval 已完成 Domain、Layout、Rendering、Persistence 重建和 Desktop Template 创建闭环；最终 PT/DTU 组合位置仍未冻结。
- 新建 Template 的业务间隔默认名称已冻结为 `负1`～`负N`；上刀/下刀/上接地/下接地等更细命名规则仍未冻结。

当前 `RingCabinetDefinition.Create` 已能组合现有 `LoadSwitchInterval` 和 `IntegratedFeederInterval`；缺口主要在 Desktop 配置入口、按实际结构生成 Layout 和原子 Command。

## 8. Professional Decisions Still Required

- DTU 柜的 Domain、Layout、Rendering、Persistence 表达。
- PT/DTU 最终组合与左右位置规则。
- Switch 操作顺序、完整 Interlock 和图形操作边界。
- 设备编号/命名建议规则及人工确认方式。
- 首批 PoleAttachment 设备范围。
- CableTermination 的现场使用边界：柜到杆是否必须经过终端、柜到柜是否可直连。
- Cable 第一版必要业务属性、是否需要独立 Cable 明细对象。
- 哪些 Interval 可以直接连接 OverheadLine。

PT/DTU 的术语不再是待确认项；待确认的是具体软件表达。

## 9. Current Roadmap

### P0-4：Build/Smoke Validation

在可用 Windows/.NET 环境补齐解决方案编译、Domain 测试、WPF 启动和基础端到端验收。

### P0-6-B：Minimal Configurable RingCabinet

已完成。详细实现状态以代码和本节 P0-6-C 状态为准。

```text
Add RingCabinet
→ basic information
→ interval count
→ per-interval supported type selection
→ legal Domain aggregate
→ RuntimeLayout
→ Rendering/Selection
→ Undo/Redo
→ Save/Reload
```

该历史阶段当时不包含 Switch Interlock、PT/DTU 新模型、Interval Usage、自动命名、Cable 或模板系统；其中 Switch、Cable、参数化 Template、自动命名和 PTInterval 已由后续阶段完成。创建时 Domain 工厂要求的开关初始值仍只代表合法技术初始化值，不代表用户最终运行状态。

### P0-6-C：CableTermination + PoleAttachment 完整闭环

已完成代码实现并通过 MacBook 编译/测试验证。Windows/WPF 实机运行验收仍待完成。

### P0-6-D

P0-6-D-2 已完成。当前已完成 SelectionTransition 基础设施、Attachment Move 的 Selection Preserve，以及 Add/Remove Attachment 的 Selection Restore。

### P0-7：Cable Editor

在 Cable endpoint 边界和 Cable 业务属性确认后，实现 Cable Domain/Layout/Rendering/Terminal Editor。

### P0-8：JPG/Print 与代表性真实图

补齐 JPG、打印预览、Windows 打印及第一张代表性真实工作票图的实机验收。

### P1 及远期

P1 关注多选、复制、吸附、对齐、模板和绘图效率；更远期再实现 WorkTicketData/工作票草稿和台区拓扑维护等扩展。远期方向不是当前交付承诺。

## 10. New Session Recovery

新的开发会话按以下顺序恢复：

1. 阅读 `README.md`；
2. 阅读本文 `docs/project-current-state.md`；
3. 阅读 `docs/implementation-plan.md`；
4. 阅读当前阶段的专项设计文档；
5. 检查关键代码结构、`git status` 和当前 HEAD；
6. 以代码与最新文档交叉核对后再修改。

当前文档不是详细设计替代品，也不替代代码审查。尤其不要把 README 中较早的里程碑文字或历史聊天结论当作最新实现状态。
