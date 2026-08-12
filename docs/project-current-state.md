# Project Current State

> 本文创建于 checkpoint commit `3c20457`（`Add project current state checkpoint`）。实际当前 HEAD 以 `git rev-parse HEAD` 为准。
>
> 本文是新会话交接用的当前状态摘要。仓库代码和最新设计文档始终是实现事实源，聊天历史不是事实源。

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

- RingCabinet 不能由用户配置间隔数量和类型；
- Switch 图形操作和完整现场联锁尚未实现；
- Cable 只有 ConnectionType，没有完整 Cable Domain/Layout/Rendering/Editor；
- JPG 导出、打印和打印预览尚未完成真实验收；
- Windows 编译、运行和端到端保存验收尚未在当前环境完成。

## 6. Current Technical Debt / Validation Gap

当前开发环境没有可用的 `dotnet` 命令，因此最近阶段只能做静态检查和代码路径审查，不能把设计或代码存在描述为编译通过。

仍需在 Windows/.NET 环境验证：

- WPF 解决方案编译；
- Domain 和已有测试执行；
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

当前 P0-6-C 的 Git 实现基线为：

- `94d676e`：CableTermination + PoleAttachment Domain/Command 基础；
- `04f86d8`：Anchor / Selection / Inspector；
- `b1e4f25`：Desktop CableTermination 创建闭环；
- `2d211bf`：Desktop PoleAttachment 删除闭环。

当前限制：

- 尚未完成 Windows/.NET 环境下的实际运行和端到端验收；
- 当前开发环境没有可用的 `dotnet` 命令。

## 7. Confirmed P0-6 Professional Decisions

以下决策已经确认：

- RingCabinet 允许普通负荷开关间隔与断路器/融合类间隔混合存在。
- 用户决定间隔数量，并逐个选择当前 Domain 支持的合法间隔类型。
- 六间隔“前四个负荷开关、后两个断路器”是常见实例，不是固定业务规则。
- 常用组合将来可以成为模板/快捷配置，但模板不能限制 Domain。
- 创建柜体时不要求用户一次性配置最终开关状态。
- 长期希望在图上点击具体开关，并经过 Domain/联锁规则校验后改变状态。
- 开关操作顺序和完整联锁不能由实现自行假设，应独立设计。
- 第一版不建立 Incoming、Outgoing、Tie、Transformer、Spare 等 Interval Usage 枚举。
- DTU 柜术语已确认，但其具体模型仍未设计。
- PT 间隔具体模型仍未设计。
- “负1～负7”、上刀/下刀/上接地/下接地等命名规则尚未冻结。

当前 `RingCabinetDefinition.Create` 已能组合现有 `LoadSwitchInterval` 和 `IntegratedFeederInterval`；缺口主要在 Desktop 配置入口、按实际结构生成 Layout 和原子 Command。

## 8. Professional Decisions Still Required

- PT 间隔的 Domain、Layout、Rendering 表达。
- DTU 柜的 Domain、Layout、Rendering、Persistence 表达。
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

不包含 Switch Interlock、PT/DTU 新模型、Interval Usage、自动命名、Cable 或模板系统。创建时 Domain 工厂要求的开关初始值只能作为合法技术初始化值，不代表用户最终运行状态。

### P0-6-C：CableTermination + PoleAttachment 完整闭环

代码实现已完成。Windows/.NET 实际运行验收待执行。

### P0-6-D

待规划。下一阶段暂不提前实现，具体范围需在后续审查和专业决策确认后确定。

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
