# M6-B Drawing Core Capability Review

> 文档状态：基于当前仓库真实实现的能力审查，不是实施完成声明<br>
> 审查日期：2026-08-12<br>
> 审查基线：`main@55a34ed17b295e697e310d6cd206e6fdbfd9fcd1`，`phase-5-professional-core@ef376e969e2252cc6ae9c1d7140950af01873cb1`<br>
> 范围：只审查 Drawing Core、Electrical Topology 及其直接依赖；暂停 WorkTicketData 代码实施

## 1. 结论

**当前版本不能交给普通用户从空白工程完成一张真实的 10kV 配电工作票图。**

仓库已经具备以下基础：

- 环网柜、杆塔、电缆终端、架空线路、Terminal、Connection、ElectricalNode、WorkScope 和 GroundingPoint 等部分结构化模型；
- 环网柜内部固定拓扑、三种一二次融合接地结构、开关组合评估与部分联锁；
- Pole、PoleAttachment、OverheadLine、环网柜和 Professional 对象的毫米 Layout/Scene/Symbol 基础；
- 单选、高亮、Pole 拖动、少量属性编辑、Professional 编辑及 CommandStack 原型；
- `.kvdrawing` FormatVersion 2 的 Domain、Topology、Professional 和 Layout DTO 保存/恢复基础。

但是 Desktop 当前只提供两个硬编码演示入口：“绘制测试内容”和“绘制环网柜组合场景”。“文件”菜单为空，没有新建、打开、保存；没有设备库、设备创建命令、真实连线工具、普通设备删除、缩放/平移、JPG 或打印入口。`ProjectRuntimeSession` 和 `ProjectService` 也没有接入 `MainWindow`。

因此当前更准确的产品状态是：

> **结构化 Domain + Persistence + Rendering/Interaction 原型集合，尚未形成 Drawing Core 用户工作流。**

第一个阻断真实用户流程的 Gap 是：**启动后无法通过 Desktop UI 新建或打开一个由 `DrawingDocument + RuntimeLayout + ProjectSession` 支撑的可编辑工程。** 即使把启动时的空白视觉区域视作“空白画布”，用户下一步仍无法添加第一个设备。

## 2. Review 方法与判定口径

### 2.1 实际检查范围

本次实际检查了：

- Desktop：`MainWindow.xaml`、`MainWindow.xaml.cs`、`ProjectRuntimeSession.cs`、应用入口及项目引用；
- Domain：`DrawingDocument`、Device、Pole、PoleAttachment、CableTermination、RingCabinet、RingCabinetInterval、SwitchAssembly、SwitchDevice、Terminal、Connection、OverheadLine、Professional 对象；
- Layout：DrawingLayout、RuntimeLayoutDocument、Pole/Attachment/OverheadLine/RingCabinet/Interval/Switch Layout；
- Rendering：DrawingSceneBuilder、DrawingSceneRenderer、DrawingVisualHost、SymbolLibrary、Pole/Attachment/RingCabinet/Interval Symbol；
- Interaction：Selection、HitTest、Overlay、PoleLayoutEditor、MoveCommand、CommandStack、PropertyEditor 及 Professional Commands；
- Professional Rendering：TerminalAnchorIndex、ProfessionalSceneBuilder；
- Persistence：ProjectService、ProjectSession、ProjectFileContainer、Domain/Topology/Layout/Professional DTO 和恢复映射；
- Tests：当前唯一的 Domain 测试项目及其测试范围；
- Docs：MVP requirements、architecture、implementation plan、Professional 与 WorkTicketData 相关设计。

### 2.2 分层定义

| 层级 | 判定标准 |
| --- | --- |
| A. Domain Supported | 存在正式领域对象/API，并能表达该专业事实或规则 |
| B. Topology Supported | 可形成并校验真实 Terminal、Connection、ElectricalNode 等关系 |
| C. Layout Supported | 存在可持久化的毫米工程坐标或相对布局合同 |
| D. Rendering Supported | 能从 Domain + Layout 生成 Scene/Symbol/Visual |
| E. Interaction Supported | 存在选择、HitTest、拖动、属性或命令基础 |
| F. User Workflow Complete | 普通用户能从 Desktop UI 完成完整操作，无需硬编码或直接调用 API |

只有 F 成立，本文才描述为“当前用户可直接使用”。

### 2.3 Status 取值

- `Complete`：该能力的目标用户闭环已完整存在；
- `Partial`：多层已有实现，但用户闭环缺少关键步骤；
- `Infrastructure Only`：只有底层模型、DTO、Symbol 或通用框架；
- `Not Implemented`：没有对应实现；
- `Needs Verification`：代码路径存在，但当前环境或自动化验证不足，不能确认交付质量。

### 2.4 验证限制

当前执行环境没有 `dotnet` 命令，无法在本次 Review 中编译或运行测试。仓库存在 Domain xUnit 测试，但没有 Persistence、Rendering、Interaction、Desktop 或端到端自动化测试。因此本文对代码存在性和调用链的判断来自静态检查；Windows 实机交付质量仍需单独验证。

## 3. Drawing Core 能力矩阵

矩阵中的“有/部分/无”按当前代码而非设计目标填写。`Persistence` 表示数据合同是否覆盖，不代表 Desktop 已能保存该编辑结果。

| Capability | Domain | Topology | Layout | Rendering | Interaction | Desktop UI | Persistence | Undo/Redo | Status | Priority |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 新建/打开/保存工程 | 有 `ProjectService` | 可恢复 | 空 Layout DTO | 可从恢复对象建 Scene | 独立 RuntimeSession 原型 | 无文件入口 | 有容器与 DTO | 未与保存点集成 | Infrastructure Only | P0 |
| 工程基本信息 | DrawingDocument.Title、Metadata | 不适用 | 不适用 | 无专门显示 | 无编辑命令 | 无 UI | 可保存 | 无 | Infrastructure Only | P0 |
| Pole 创建与放置 | 有 | 可创建架空锚点 Terminal | 有 | 有 | 可单选、拖动、改杆号 | 仅硬编码演示，不能新增 | 有 | Move、杆号 | Partial | P0 |
| RingCabinet 创建与放置 | 有工厂与混合间隔 | 有内部 Node/Terminal | 有 | 有 | 可 HitTest/只读查看 | 仅硬编码演示，不能新增 | 有 | 无 | Partial | P0 |
| RingCabinet Interval 配置 | 有 LoadSwitch/IntegratedFeeder | 有内部拓扑 | 有 | 有 | 可单选/只读 | 无新增、增删或重配入口 | 有 | 无 | Infrastructure Only | P0 |
| RingCabinet Switch 状态 | SwitchAssembly 可受控改状态 | 内部拓扑存在 | Switch Layout 有 | 开/合图元有 | 无状态编辑器 | 无 | 有 | 无 | Infrastructure Only | P0 |
| PoleAttachment | 有加入校验 | 安装关系不等于导通 | 有相对偏移 | 有 | 可选中关系、只读 | 无新增/删除/移动入口 | 有 | 无 | Partial | P0 |
| 柱上开关设备 | 模型可表达，但无公共创建工厂 | Terminal 可表达，但生产创建入口缺失 | AttachmentLayout 可承载 | 四类开关 Symbol 有 | 只有 Attachment 级选择 | 无 | 顶层 SwitchDevice DTO 明确不支持 | 无 | Infrastructure Only | P0 |
| CableTermination | 有设备、双 Terminal、内部 Node | 有内部固定导通与端子策略 | 仅作为 AttachmentLayout | 有附属图元 | 命中结果是 PoleAttachment，设备属性未单独解析 | 无新增入口 | 有 | 无 | Partial | P0 |
| Connection 通用模型 | 有 AddConnection 与端子策略 | 有端点、类型、电压和占用校验 | 仅 OverheadLine 有布局 | 通用 Connection 无渲染 | 无连线交互 | 无 | 有 | 无 | Infrastructure Only | P0 |
| OverheadLine | 有线路明细 | 与同 ID Connection 一对一校验 | 有简单直线 | 有 | 可选择、只读 | 仅硬编码演示，不能连线 | 有 | 无 | Partial | P0 |
| Cable | 只有 `ConnectionType.Cable` | 电缆端子策略与 Connection 可表达 | 无 CableLayout | SymbolLibrary 有未接入 helper | 无 | 无 | Connection 可存，但无 Cable 明细/布局 | 无 | Infrastructure Only | P0 |
| 真实 Terminal 连线 | Terminal 与 Connection 规则有 | AddConnection 可校验部分非法情况 | 无通用 ConnectionRoute | 仅架空直线 | Terminal Pick 仅供 Professional | 无连线状态机/吸附 | 底层可存 | 无 | Not Implemented | P0 |
| 普通设备删除 | 无 RemoveDevice | 无依赖删除事务 | 无 Remove Layout | 不适用 | 无命令 | 无 | 不适用 | 无 | Not Implemented | P0 |
| Attachment/Connection/Line 删除 | 无对应 Domain API | 无引用保护删除流程 | 无 Remove | 不适用 | 无命令 | 无 | 不适用 | 无 | Not Implemented | P0 |
| GroundingPoint 创建/编辑/删除 | 有且有引用保护 | 引用 Terminal | 锚点派生，无独立 Layout | 有 | 有选择、命令、编辑 | 仅在 Document-backed 场景可用 | 有 | 有 | Partial | P0 |
| WorkScope 创建/编辑/删除 | 有 | 双 Boundary 引用 Terminal | 无人工范围路径 | 仅双边界标记 | 有双 Pick 与命令 | 仅在 Document-backed 场景可用 | 有 | 有 | Partial | P0 |
| 单选/点击空白取消 | 稳定 ID 引用 | 不适用 | 命中基于毫米 bounds | Overlay 有 | 有单选与清空 | 有 | 不保存 | 不需要 | Complete | P1 |
| 多选/框选 | 不适用 | 不适用 | 无 | 无 | 无 | 无 | 无 | 无 | Not Implemented | P1 |
| Pole 移动 | 不改变 Domain | 不改变 Topology | 可 Replace | 可刷新 | 拖动预览 + MoveCommand | 演示场景可用 | Runtime 改动未回写 DTO | 有 | Partial | P0 |
| RingCabinet/Attachment 移动 | 不适用 | 不适用 | 有位置/偏移但无 Replace API | 可按布局刷新 | 无 Editor | 无 | DTO 有 | 无 | Infrastructure Only | P0/P1 |
| 线路随设备移动 | Topology 保持 | Connection 保持 | OverheadLine 起终点是独立固定坐标 | 按旧坐标绘制 | 无联动 | 无 | 可存旧坐标 | 无 | Not Implemented | P0 |
| Zoom/Pan/大图导航 | 不适用 | 不适用 | 不保存视口合理 | 坐标只做 mm↔DIP | 无变换/视口状态 | 无 | 不适用 | 不适用 | Not Implemented | P0 |
| 属性只读查看 | 多对象数据存在 | 可显示连接/端子部分属性 | 可显示布局字段 | 可显示 Symbol 信息 | Resolver/Projector 有 | 有属性面板 | 不保存 Inspector | 不需要 | Partial | P1 |
| 属性编辑 | Pole/Professional 有 API | 不改拓扑 | 只支持 Pole Move | 可刷新 | 仅杆号、GroundingPoint、WorkScope | 有少量专用控件 | 业务层可存，UI 未接文件保存 | 有 | Partial | P0 |
| 普通文字/Annotation | 无 Annotation 对象 | 不适用 | 无 Text/Label Layout | 自动标签可画 | 无 | 无 | 无 | 无 | Not Implemented | P0/P1 |
| Copy/Paste | 无复制服务 | 无拓扑复制策略 | 无 | 无 | 无 | 无 | 无 | 无 | Not Implemented | P1 |
| Undo/Redo 框架 | 不适用 | 不适用 | Pole Move 已接 | 刷新可用 | CommandStack 有 | 菜单有 | 历史不保存合理 | 覆盖少量操作 | Partial | P0 |
| Runtime Layout 保存闭环 | 不适用 | 不适用 | Runtime 与 DTO 都有 | 加载后可建 Scene | Runtime 编辑存在 | 无保存入口 | 只有 DTO→Runtime；无 Runtime→Snapshot 接线 | 保存点未接 | Not Implemented | P0 |
| Domain/Topology DTO 往返 | 有 | 有 | 不适用 | 不适用 | 无 | 无 | 有实现 | 不适用 | Needs Verification | P0 |
| JPG/PNG/PDF 导出 | 不适用 | 不适用 | 无页面合同 | Scene 可渲染 Visual | 无离屏输出适配器 | 无 | 不适用 | 不适用 | Not Implemented | P0 |
| Windows 打印/预览 | 不适用 | 不适用 | 无页面合同 | 无打印页面构建 | 无 | 无 | 不适用 | 不适用 | Not Implemented | P0 |
| 常用模板 | 环网柜工厂有部分纯柜模板 | 可生成内部拓扑 | 无用户模板布局 | 可显示 | 无模板命令 | 仅硬编码演示 | 无模板库 | 无 | Infrastructure Only | P1 |
| PTInterval / DTU | 无当前实现 | 无 | 无 | 无 | 无 | 无 | 无 | 无 | Not Implemented | P0（按现有 MVP 需求） |

### 3.1 关于 `Complete` 单选能力的限定

单选、空白取消和 Overlay 本身已形成可运行机制，因此矩阵标为 `Complete`。这不代表所有对象都能正确被解析或编辑：当前 Resolver 对环网柜仍依赖单个 `_source.RingCabinet`，加载多个环网柜时只初始化第一台，其他柜及其间隔/开关的属性解析存在扩展缺口。

## 4. 真实绘图工作流盘点

### 4.1 新建工程

检查结果：

- `ProjectService.CreateProject()` 可以创建空 `.kvdrawing`；
- `ProjectService.LoadProject()` 和 `ProjectRuntimeSession.Load()` 存在；
- `MainWindow` 没有 `ProjectService`、`ProjectRuntimeSession` 字段或调用；
- “文件”菜单是空菜单项，没有新建、打开、保存、另存为；
- 没有工程标题/说明编辑 UI，也没有文件选择对话框；
- 启动后的白色区域只是 `DrawingVisualHost`，不是已建立的 DrawingDocument 工程。

结论：**Infrastructure Only，用户不可直接使用。**

### 4.2 添加设备

#### Pole

- Domain：`new Pole(...)`、杆号、PoleType、架空锚点 Terminal 均存在；
- Topology：可以显式创建并向 DrawingDocument 加入 Terminal；
- Layout/Rendering：PoleLayout、PoleSymbol 和 HitTest 存在；
- Interaction：现有 Pole 可拖动、编辑杆号；
- Desktop：仅 `OnDrawTestContent` 直接 `new Pole` 创建两根固定演示杆；用户没有设备库或新增命令。

#### RingCabinet

- Domain：支持 LoadSwitch 和 IntegratedFeeder 混合间隔，固定构建内部开关、Node、Terminal；
- Layout/Rendering：支持组合柜、间隔、开关和状态图元；
- Desktop：`OnDrawRingCabinetComposition` 每次构造固定四间隔演示柜；没有用户配置入口。

#### RingCabinet Interval / Switch

- Interval 不是 Device，聚合边界正确；
- 初始创建时可指定类型、接地结构和开关状态；
- SwitchAssembly 提供 `ChangeSwitchState()` 并执行已确认联锁；
- Interval 创建后结构和名称没有面向编辑器的增删改入口；
- Desktop 无开关状态编辑入口，PropertyInspector 只读显示状态。

#### PoleAttachment / CableTermination

- DrawingDocument 可以校验 PoleAttachment 只能关联柱上 SwitchDevice 或 CableTermination；
- CableTermination 有双端子与内部 ElectricalNode；
- AttachmentLayout 和 AttachmentSymbol 可以显示；
- Desktop 没有创建、选择设备类型、挂载、删除或相对移动入口；
- 命中 Attachment 后只投影安装关系，没有解析并编辑附属设备自身属性。

#### 柱上开关

- SwitchDevice 能表达柱上安装类型和四类图元需要的 SwitchKind；
- 但 SwitchDevice 构造器是 `internal`，没有公共柱上开关工厂；现有测试通过反射创建柱上开关；
- Persistence 明确拒绝非环网柜内部的顶层 SwitchDevice；
- 因此柱上开关目前不是可由生产代码正常创建并保存恢复的完整能力。

#### 其他 Device

- DeviceType 包含 PT，但没有 PTInterval 实现；
- DTUCabinet 不存在；
- 当前没有新增其他 MVP 设备类型的生产实现。

### 4.3 RingCabinet 实际绘制能力

| 操作 | 当前结果 |
| --- | --- |
| 新增 RingCabinet | ✗ 只有硬编码演示工厂调用 |
| 设置名称/编号 | △ 创建定义可传 DisplayName；无 Desktop 编辑 |
| 配置间隔数量 | △ Domain 工厂可配置；无 UI |
| 配置间隔类型 | △ Domain Definition 可混合配置；无 UI |
| 配置内部开关 | △ 初始状态可配置；无 UI |
| 显示外部 Terminal | △ IntegratedFeeder 显示“外部端子”图形；LoadSwitch 只有导体末端，Terminal 没有统一可见/可选图元 |
| 后续修改间隔 | ✗ 无聚合修改 API/Command/UI |
| 删除 RingCabinet | ✗ 无 RemoveDevice 或 UI |
| 移动 RingCabinet | ✗ Layout 有 Position，但无 Replace/Move Command/UI |
| 保存/恢复 | △ DTO 和恢复实现存在；Desktop 未接文件工作流 |

目前“能显示混合柜”是 SceneBuilder + 硬编码 Layout 的演示能力，不是用户绘制能力。

### 4.4 Pole / PoleAttachment

| 操作 | Pole | PoleAttachment |
| --- | --- | --- |
| 新增 | ✗ UI 无入口 | ✗ UI 无入口 |
| 基础属性 | △ 杆号可编辑 | △ 关系只读 |
| 删除 | ✗ | ✗ |
| 移动 | △ 演示 Pole 可拖动 | △ 随 Pole 平移显示，但自身偏移不可编辑 |
| 保存恢复 | △ DTO 有，编辑闭环未接 | △ DTO 有，编辑闭环未接 |

Pole 移动时 AttachmentSymbol 以 `PoleLayout.Position + AttachmentLayout.Offset` 重建，因此附属图元会跟随杆塔；这不代表外部线路端点也会跟随。

### 4.5 Connection / Cable / OverheadLine

目标流程：

```text
选择 Terminal A
→ 开始连线
→ 选择 Terminal B
→ 创建 Connection
→ 创建线路明细和 Layout
→ Scene 显示
→ Topology 与 Undo/Redo/保存同步
```

当前没有这条流程。

#### 已有基础

- DrawingDocument.AddConnection 校验 ID、端子存在、允许的 ConnectionType、电压等级和单连接端子占用；
- RingCabinet 外部端子允许 Cable/OverheadLine；
- CableTermination 两侧端子分别限制 Cable 和 OverheadLine；
- OverheadLine 与 Connection 使用同一 ID，并校验支撑杆及物理端点；
- OverheadLineLayout 和简单直线渲染存在；
- TerminalAnchorIndex 可以为 Professional Pick 计算部分端子坐标。

#### 缺失能力

- 没有普通连线工具或状态机；
- 没有 Terminal 热点 Scene/HitTest 条目；Professional Pick 是每次临时建立 AnchorIndex 后在固定 8 mm 方框内搜索；
- 没有端点吸附、连线预览、取消或 Commit；
- 没有 AddConnection/AddOverheadLine Editor Command；
- 没有删除 Connection/Line、Undo/Redo；
- 没有移动设备后重算线路端点；
- Cable 没有领域明细对象、CableLayout 或 DrawingSceneBuilder 接入；`SymbolLibrary.CreateCableLine()` 只是未接入 helper；
- CableTermination 的两个 Terminal 当前映射到同一个 Attachment 中心，不足以支持可靠的两侧连线选择；
- Pole 的多个 Anchor Terminal 映射到同一点，不能区分具体端口；
- OverheadLineLayout 会覆盖端点 Terminal 的锚点位置，设备移动后可能继续使用旧线路坐标。

### 4.6 对象删除

DrawingDocument 只有以下 Professional 删除 API：

- RemoveWorkScope；
- RemoveGroundingPoint，并拒绝删除仍被 WorkScope 引用的 GroundingPoint。

DrawingDocument 没有：

- RemoveDevice；
- RemoveTerminal / RemoveElectricalNode；
- RemovePoleAttachment；
- RemoveConnection；
- RemoveOverheadLine。

因此当前无法评价“删除设备时 Domain 能否完整保护拓扑一致性”，因为生产删除入口本身不存在。已有 Add API 能阻止部分非法新增，但不能替代删除事务设计。

Desktop 只提供 GroundingPoint 和 WorkScope 删除按钮；普通设备与连接均不可删除。

### 4.7 对象复制

以下均未实现：

- 单对象 Copy/Paste；
- Pole + Attachment 聚合复制；
- RingCabinet 深复制；
- 多对象复制；
- 新稳定 ID 分配和内部引用重写；
- 是否复制外部 Topology 的规则；
- Layout 复制；
- Copy/Paste Command 和 Undo/Redo。

### 4.8 Selection

已实现：

- 单个 SelectionReference；
- 点击最高优先级 HitTest bounds 单选；
- 点击空白将 target 设为 null；
- Overlay 不修改原 SceneElement；
- Pole、RingCabinet、Interval、柜内 Switch、PoleAttachment、OverheadLine Connection、GroundingPoint、WorkScope、Terminal 等目标类型基础；
- WorkScope 两个边界共享同一 SelectionReference，可共同高亮。

未实现或有缺口：

- 多选；
- 框选；
- Ctrl/Shift 增减选择；
- 多对象 Overlay/属性面板；
- 多对象移动；
- 多 RingCabinet 的 Resolver：当前 Source 只保存一个主 `RingCabinet` 实例，加载时选择第一台，其他柜可能 HitTest 成功但无法解析属性；
- CableTermination/柱上 Switch 的设备级选择：当前 Attachment 命中只解析 PoleAttachment。

### 4.9 Move / Layout

#### 当前可移动对象

只有 PoleLayout。

流程为：MouseDown → PoleLayoutEditor → preview 直接 Replace → MouseUp 创建 MoveCommand → Execute 再次 Replace → Scene 刷新。

#### 缺口

- RingCabinet、Attachment、线路端点和 Professional 标记不可拖动；
- 无多对象移动、吸附、网格、对齐、分布和坐标输入；
- 无画布边界约束；
- OverheadLineLayout.Start/End 是独立坐标，不从 TerminalAnchorIndex 自动更新，因此移动 Pole 后线路保持原位置；
- Runtime `DrawingLayout.Replace()` 只修改内存字典；没有把 RuntimeLayout 转回 `ProjectLayoutSnapshot` 并调用 `ProjectService.SetLayout()`；
- MainWindow 的 `_commandStack` 与 `ProjectRuntimeSession.CommandStack` 是两套未整合实例；
- MainWindow 演示场景本身也没有 PersistenceSession。

结论：Pole 拖动是演示级 Interaction，不是可保存工程中的完整布局编辑。

### 4.10 Zoom / Pan / Canvas

已实现：

- DocumentPoint/DocumentRect 使用毫米；
- `DocumentCoordinateSystem` 提供固定 96 DIP/inch 的 mm↔DIP 转换；
- DrawingVisualHost 可显示单个 DrawingVisual。

未实现：

- Zoom In/Out；
- 鼠标滚轮缩放；
- Zoom to Fit；
- Pan；
- 视口变换与逆变换后的 HitTest；
- 大图导航/小地图；
- 页面尺寸、方向、边距和画布边界；
- 当前缩放倍率显示；
- 缩放下保持固定像素命中/吸附距离。

当前鼠标坐标直接按未变换 DIP 转毫米，只适用于 100% 无平移视图。

### 4.11 Property Editing

#### 当前真正可编辑

- Pole.PoleNumber；
- GroundingPoint.Location；
- GroundingPoint.Number；
- GroundingPoint.Note；
- WorkScope.Description；
- WorkScope.GroundingPointIds（用户直接输入 Guid 列表）。

#### 只读投影

- RingCabinet 基本信息；
- RingCabinetInterval 类型、名称、序号、接地结构和外部 Terminal；
- 柜内 Switch 类型、状态、调度编号和 Terminal；
- Pole 的 DisplayName、PoleType 等；
- PoleAttachment；
- OverheadLine 型号、长度、支撑杆、延续状态；
- Terminal；
- Layout 和 Rendering 信息。

#### 未实现编辑

- RingCabinet 名称/编号、间隔结构；
- SwitchState、DispatchNumber；
- Pole DisplayName/PoleType；
- Attachment 归属和偏移；
- CableTermination 属性；
- Connection 名称、电压等级；
- OverheadLine 型号、长度、支撑杆和延续信息；
- Layout 坐标/尺寸/标签偏移；
- WorkScope Boundary 重绑和 Side 修改；
- GroundingPoint TerminalId 修改。

### 4.12 Text / Annotation

当前只有由 Symbol 自动生成的标签：杆号、环网柜名称、间隔序号/名称、部分开关名称、线路型号或延续表达、Professional 标记文字。

不存在：

- Annotation/Text Domain 对象；
- 用户新增自由说明；
- 专业标注对象；
- 电缆信息标签；
- 标签拖动、避让和独立 Layout；
- 字体、字号、对齐和旋转编辑；
- 保存用户自定义文字。

自动标签不等于用户可创建 Annotation。

### 4.13 Undo / Redo

当前 CommandStack 覆盖：

- Pole Move；
- PoleNumber 修改；
- GroundingPoint Add/Remove/Change；
- WorkScope Add/Remove/Change。

未覆盖：

- Add/Delete Device；
- RingCabinet 配置或状态；
- Add/Delete Attachment；
- Add/Delete Connection/OverheadLine/Cable；
- RingCabinet/Attachment/Line Move；
- Copy/Paste；
- Annotation；
- 工程 Metadata；
- 任何尚未实现的编辑操作。

CommandStack 的 `ExecuteCommand()` 先执行命令，成功后才写历史，适合拒绝失败命令。但当前没有 CompositeCommand、跨 Domain+Layout 原子命令、命令合并或未来复杂对象删除快照机制。

### 4.14 Persistence

#### Persistence 层单独支持

- ZIP `.kvdrawing`、Manifest、Metadata、FormatVersion 2；
- Domain：Pole、CableTermination、RingCabinet、ElectricalNode、Terminal、Connection、OverheadLine、PoleAttachment；
- Professional：GroundingPoint、WorkScope；
- Layout：Pole、Attachment、OverheadLine、RingCabinet、Interval、Switch；
- v1 → v2 空 Professional 迁移；
- 候选加载和写临时文件后替换。

#### 限制

- 顶层/柱上 SwitchDevice DTO 不支持；
- Cable 只有 Connection，没有 Cable 专用明细和 Layout；
- PT/DTU 不支持；
- 当前测试项目没有 Persistence 往返测试；
- `ProjectRuntimeSession.Load()` 只完成 DTO→Runtime→Scene；
- 没有 RuntimeLayout→ProjectLayoutSnapshot 映射；
- `ProjectService.SaveProject()` 保存的是 `ProjectSession.Layout` 快照，不会自动读取 MainWindow 中被拖动的 DrawingLayout；
- MainWindow 没有 Create/Load/Save Project 调用；
- MainWindow 的业务编辑源是硬编码演示对象，不是 ProjectService.Current；
- CommandStack SavePoint 与实际文件保存没有 Desktop 集成。

因此“Persistence DTO 能保存”不能描述为“用户编辑后可保存并重新打开继续编辑”。

### 4.15 Export / Print

代码中没有以下实现或入口：

- PNG/JPG 编码；
- RenderTargetBitmap 或离屏位图输出；
- DPI、像素尺寸、JPEG 质量；
- PDF；
- PrintDialog、打印预览或分页；
- 页面尺寸、方向、边距和缩放适配；
- 白色背景输出合同；
- 只导出 base DrawingScene 并排除 Selection Overlay、HitTest 热点和编辑器 UI 的输出适配器。

DrawingSceneRenderer 能生成 DrawingVisual，是未来统一导出语义的基础，但不等于已有导出或打印。

### 4.16 Templates

已有：

- RingCabinet Domain 的纯负荷开关柜和纯一二次融合柜工厂；
- MainWindow 中一个固定混合柜演示布局；
- 一个固定两杆、线路和电缆终端演示场景。

未实现：

- 用户可见设备库；
- 常用 RingCabinet 配置模板；
- Pole + 柱上设备组合模板；
- 常用线路结构；
- 工程模板；
- 模板保存、版本、参数输入和实例化 Command。

硬编码演示不能作为模板功能验收。

### 4.17 Professional Drawing

#### 已有能力

- DrawingDocument 管理 WorkScope/GroundingPoint；
- ProjectService 可以保存/恢复 Professional DTO；
- ProfessionalSceneBuilder 可以在可解析 TerminalAnchor 时显示工作地线和双边界；
- GroundingPoint、WorkScope 可单选、高亮、只读查看和部分编辑；
- Professional 修改走 CommandStack。

#### 实际限制

- 只有 Document-backed Scene 才能启动 Professional 创建；两个演示入口均未把完整 DrawingDocument 作为编辑源；
- 用户目前无法从 Desktop 打开 ProjectRuntimeSession，因此正常入口不可达；
- GroundingPoint 没有独立 Layout，始终依附计算锚点；
- WorkScope 只有两个边界标记，没有人工范围路径；
- 设备移动后 Anchor 是否更新取决于 Layout；Pole 端点若被固定 OverheadLineLayout 覆盖，可能仍停留在旧位置；
- CableTermination 两侧端子同锚点，实际选择不明确；
- RingCabinet 端子锚点由 Symbol 几何常量重复计算，布局变化时存在漂移风险；
- 保存恢复的 Persistence 代码存在，但 Desktop 端到端闭环没有接通；
- 没有 JPG/打印，无法确认最终图纸适用性。

结论：Professional Core 是较完整的基础模块，但尚不能加入真实用户生产图流程。

## 5. “从空白工程绘制一张工作票图”实际流程

| 步骤 | 用户目标 | 当前结果 | 说明 |
| --- | --- | --- | --- |
| 1 | 启动程序 | ✓ | MainWindow 可作为 WPF 启动窗口；本次环境未做运行验证 |
| 2 | 新建空白工程 | ✗ | 文件菜单无入口，MainWindow 未接 ProjectService |
| 3 | 设置工程标题/说明 | ✗ | Metadata 有模型，无 UI/Command |
| 4 | 放置第一个 Pole 或 RingCabinet | ✗ | 只有硬编码演示，无设备库/创建命令 |
| 5 | 配置环网柜间隔和开关 | ✗ | Domain 创建能力存在，用户无入口 |
| 6 | 放置 CableTermination/柱上设备并挂杆 | ✗ | 无创建/挂载工作流，柱上 Switch 生产工厂及持久化也缺失 |
| 7 | 选择两端 Terminal 创建 Cable | ✗ | 无连线状态机、CableLayout/Scene 接入 |
| 8 | 创建 OverheadLine | ✗ | 无连线 UI/Command，只有演示对象 |
| 9 | 移动排版并让线路跟随 | ✗ | 只有 Pole 可拖动，线路端点不联动 |
| 10 | 编辑名称、状态和线路参数 | ✗ | 仅杆号和 Professional 少量字段可编辑 |
| 11 | 添加 WorkScope/GroundingPoint | △ | 模块存在，但当前 Desktop 无可达 Document-backed 工程入口 |
| 12 | 保存工程 | ✗ | Persistence Service 有，Desktop 无入口；Runtime Layout 不回写 |
| 13 | 关闭并重新打开继续编辑 | ✗ | Load Runtime 原型存在但未接 MainWindow |
| 14 | 导出 JPG | ✗ | 未实现 |
| 15 | 打印/预览 | ✗ | 未实现 |

**第一个真正阻断点是步骤 2：无法新建空白工程。**

如果开发者绕过步骤 2，直接点击演示场景，步骤 4 仍立即阻断：用户无法新增自己的第一个设备。

## 6. 初步优先级建议

以下只是 Review 建议，不修改 implementation-plan 的正式路线。

### 6.1 P0：Drawing Core Production Readiness

没有这些能力，用户无法完成真实图纸：

1. **统一工程会话与文件工作流**
   - MainWindow 接入单一 ProjectRuntimeSession/ProjectService；
   - 新建、打开、保存、另存为、关闭确认；
   - Metadata 基础编辑；
   - CommandStack、Dirty、SavePoint 与当前工程统一。

2. **设备创建与删除**
   - 设备库/明确放置模式；
   - Pole、RingCabinet、CableTermination、柱上设备和 Attachment 创建；
   - 安全 Remove API、依赖报告、Domain+Layout 原子 Command；
   - PTInterval/DTU 的 MVP 范围决策与实现。

3. **真实 Terminal Connection 编辑**
   - 可见/可命中的端子端口；
   - Terminal A→B 连线状态机、预览、吸附、取消和校验；
   - Cable 与 OverheadLine 的 Domain/Layout/Rendering/Command；
   - 删除连接和 Undo/Redo。

4. **基本布局编辑闭环**
   - Pole、RingCabinet 和必要 Attachment 移动；
   - 线路端点从 Terminal Anchor 更新；
   - RuntimeLayout→Persistence Snapshot 回写；
   - 保存恢复端到端验收。

5. **画布导航**
   - Zoom、Pan、Zoom to Fit；
   - 坐标逆变换与缩放无关 HitTest；
   - 页面/画布尺寸基本合同。

6. **必要属性与状态**
   - 设备名称/编号、线路参数；
   - 环网柜开关状态与柱上开关状态；
   - Domain 联锁错误反馈和 Undo/Redo；
   - 必要自动标签及基本标签位置。

7. **输出**
   - 基于 base DrawingScene 的 JPG；
   - 固定页面、DPI、白底和 Overlay 排除；
   - Windows 打印预览/打印；
   - 至少一个真实代表性场景验收。

8. **测试与 Windows 验收**
   - Domain 删除/连接测试；
   - Persistence 编辑往返测试；
   - Rendering/Interaction 测试；
   - Windows 实机新建→绘制→保存→加载→导出→打印验收。

### 6.2 P1：可用效率

- Copy/Paste，包含聚合 ID 重建规则；
- 多选、框选和多对象移动；
- Snap、Grid、对齐、分布；
- RingCabinet 快速配置器和常用柜模板；
- Pole + Attachment 常用组合；
- GroundingPoint/WorkScope 的选择器，替代手工输入 Guid；
- 标签偏移编辑、重叠处理；
- 线路折点和基本人工路由；
- 对象树/图层或快速导航；
- 更完整的 PropertyEditor。

### 6.3 P2：增强能力

- Auto Layout；
- Smart Numbering；
- 可维护的 Template Library；
- 大图小地图和高级导航；
- History/Version Compare；
- 高级线路路由和批量排版；
- 专业规则检查和问题导航；
- 在 Drawing Core 稳定之后再继续 WorkTicketData、SafetyMeasure 和 OperationStep。

## 7. 架构风险

本节只报告，不在本阶段重构。

### 7.1 Desktop MainWindow 承担过多职责——高风险

`MainWindow.xaml.cs` 当前约 1019 行，同时承担：

- 演示 Domain 对象和 Layout 的构造；
- Professional Pick 状态；
- Terminal 所有权解析；
- Guid 文本解析；
- Command 创建与错误处理；
- 拖动；
- Scene 重建；
- Selection/Overlay/PropertyInspector 刷新；
- RingCabinet 演示布局策略。

继续在此加入 Add/Delete/Connect/Copy/Save/Export 会迅速形成不可测试的集中控制器。建议在下一阶段先确定单一 EditorSession 和 Application Service 边界，再接 UI 命令。

### 7.2 RuntimeLayout 与 Persistence Layout 双状态——高风险

当前存在：

- `ProjectLayoutSnapshot`：Persistence 状态；
- `RuntimeLayoutDocument` / `DrawingLayout`：编辑和渲染状态。

只有 Snapshot→Runtime 映射，没有 Runtime→Snapshot 的集成路径。Pole 拖动修改 DrawingLayout，但 ProjectService.SaveProject 保存旧 Snapshot。MainWindow 与 ProjectRuntimeSession 还各自持有 CommandStack/Selection 状态。

建议确立一个权威 Runtime Layout Store，并在保存时从同一编辑会话生成不可变 DTO Snapshot；不得让两个可变状态分别演进。

### 7.3 线路 Layout 与 Terminal Anchor 双坐标源——高风险

OverheadLineLayout 独立保存 Start/End，TerminalAnchorIndex 又从设备 Layout 计算端子位置，随后用线路 Start/End 覆盖端子锚点。设备移动后无法确定哪一份坐标是事实，造成线路和 Professional 标记可能不跟随。

建议明确：连接端点坐标必须由 Terminal Anchor 派生；Layout 只保存中间路由点或端点相对偏移，不重复保存可由设备端子确定的绝对事实。

### 7.4 DrawingSceneBuilder 集中增长——中高风险

DrawingSceneBuilder 当前约 307 行，已经直接编排 Pole、Attachment、OverheadLine、RingCabinet、Interval HitTest 和 ProfessionalSceneBuilder。新增 Cable、Annotation、页面、工作票 Overlay 后会继续膨胀。

建议按对象类型使用 Scene Contributor/Builder，并由工程级 orchestrator 合并元素与 HitTest；保持 Symbol 只负责局部图形。

### 7.5 Rendering 业务边界——当前大体正确，但有漂移风险

当前没有发现 Rendering 自动生成 WorkScope、GroundingPoint、OperationalState 或修改 Domain。IntegratedFeederIntervalSymbol 只根据 Domain 提供的 GroundingStructureKind 和 SwitchState 表达图形，边界基本正确。

风险在于：

- MainWindow 的演示工厂和布局策略混入 Desktop；
- TerminalAnchorIndex 重复编码 Symbol 几何常量；
- Attachment label 对柱上开关使用固定类型文字，可能忽略用户 DisplayName；
- 后续不要把端口所有权、连线合法性或状态联锁放进 Symbol/Rendering。

### 7.6 Selection / Resolver 扩展性——中高风险

- SelectionReference 的稳定 ID 方向正确；
- HitTestIndex 和 Overlay 可扩展；
- 但 SelectionObjectResolver 使用一个可选 `RingCabinet`，不是按 ID 查询全部 RingCabinet；
- Attachment 命中只解析关系，不解析附属 Device；
- Resolver 由多组列表和特殊字段组成，新增类型需要持续修改中心 switch 和快照模型。

建议建立项目级只读 ObjectResolver/Index，以“目标类型 + ID + ParentId”解析当前 Session 对象和 Layout。

### 7.7 CommandStack 支撑复杂操作不足——中高风险

基础状态索引、失败命令不入历史和 SavePoint 思路可复用，但未来 Add/Delete/Connect/Copy 需要：

- Domain + Layout 原子修改；
- CompositeCommand 或事务命令；
- 删除前完整引用快照；
- Undo 时恢复同一 ID 和全部关系；
- 命令失败后的原子回滚；
- 与 ProjectSession Dirty/SavePoint 唯一集成。

当前命令直接持有运行时对象引用，适合单会话简单编辑；工程被原子替换后旧命令不得继续作用于旧对象图。

### 7.8 TerminalAnchorIndex 不足以直接作为 Connection Editor——高风险

它适合 Professional 标记的第一阶段定位，但还不具备连线端口模型：

- 不生成可见 Terminal SceneElement；
- 没有端口独立 HitTest entry 和交互状态；
- 多个 Pole Terminal 共点；
- CableTermination 两侧 Terminal 共点；
- 环网柜端口位置由渲染常量重复计算；
- 固定 8 mm 命中范围不随 Zoom 换算为屏幕手感；
- 无连接占用、允许类型和电压的实时提示；
- 无端点吸附与预览。

建议在其基础上提炼由 Symbol/Layout 提供的 `TerminalPortDescriptor`，统一服务 Rendering、HitTest、Connection Editor 和 Professional Anchor。

### 7.9 Domain 删除和拓扑修改 API——高风险

DrawingDocument 当前擅长 Add 时校验，但没有普通设备、端子、节点、Attachment、Connection 和 Line 的 Remove/Update API。无法安全实现：

- 删除有连接设备；
- 删除 Pole 及其 Attachment/支撑线路；
- 拆除 CableTermination；
- 删除/改绑 Connection；
- 修改 RingCabinet 间隔；
- 复制聚合并重建引用。

建议先定义依赖查询和显式删除策略，再实现 Editor Command。不要让 UI 直接操作内部集合或通过重建整个 DrawingDocument 绕过校验。

### 7.10 Application 层为空——中风险

Application 项目当前没有业务服务代码。设备创建、连线、删除、复制、项目会话和导出等用例如果继续落到 Desktop/Rendering，将削弱分层。下一阶段应让 Application 层承载用例编排和事务边界，但不需要一次引入复杂框架。

## 8. 建议的下一阶段结构

建议暂停 WorkTicketData 实现，将下一阶段命名为：

> **Drawing Core Production Readiness**

建议分成可独立验收的工作包，不在此修改正式 milestone 编号：

1. **Project Workspace Vertical Slice**
   - Desktop 新建/打开/保存；
   - 单一 EditorSession；
   - 空工程可达；
   - Runtime Layout 保存回写。

2. **Device Lifecycle Vertical Slice**
   - 先实现 Pole 和 RingCabinet 的 Add/Move/Delete/Property/Undo/Save/Load；
   - 用真实 UI 取代演示按钮。

3. **Electrical Connection Vertical Slice**
   - TerminalPort、Cable/OverheadLine Connect/Delete、联动布局、Undo/Save/Load；
   - 以“环网柜→电缆→电缆终端→架空线路→杆塔”作为代表场景。

4. **Canvas and Output Vertical Slice**
   - Zoom/Pan/Page；
   - JPG；
   - Print Preview/Print；
   - 排除 Selection Overlay。

5. **Professional End-to-End Revalidation**
   - 在真实设备/连接编辑器中验证 WorkScope/GroundingPoint Anchor、移动跟随、保存恢复和输出。

每个工作包都应以 Desktop 用户流程验收，而不是以新增类或 SceneBuilder 能构造对象作为完成条件。

## 9. Review 最终判断

回答本阶段核心问题：

> 如果现在把软件交给实际用户，能否从空白工程开始，方便地完成一张真实的 10kV 配电工作票图？

**不能。**

不是因为底层模型完全缺失，而是因为 Drawing Core 的关键纵向切片尚未闭环：

```text
Desktop UI
→ Editor Command
→ Domain + Topology + Layout
→ Scene
→ Save/Load
→ JPG/Print
```

当前各层已经有不少横向基础，但 UI→Command→模型→布局→渲染→持久化→输出的用户路径在多个位置断开。下一阶段应优先完成 Drawing Core Production Readiness，再继续 WorkTicketData 代码实现。
