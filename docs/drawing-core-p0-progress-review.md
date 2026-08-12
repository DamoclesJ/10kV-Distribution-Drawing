# Drawing Core P0 Progress Review

> 审查基线：`b45f1469662acd342318d0dbc417c3b3158fad00`
>
> 审查范围：P0-1 工程会话、P0-2 设备放置/删除、P0-3-B 架空线 Terminal 连线后的 Drawing Core 真实可用性
>
> 判定口径：以当前生产代码和 Desktop 用户入口为准；不把 Domain、DTO、演示场景或设计文档单独视为用户能力

## 1. 总体判断

假设当前代码能在 Windows 上成功编译运行，用户已经可以从空白工程完成一条真实但很窄的绘图链路：

```text
新建工程
→ 放置 Pole / 固定三间隔 RingCabinet
→ 在真实 Terminal 间绘制 OverheadLine
→ 移动 Pole
→ 保存、关闭、重新打开
```

这比上一次 Review 的“基础设施和演示原型”前进了一整层。工程生命周期、首个设备创建、设备安全删除、真实架空线拓扑和 RuntimeLayout 保存入口都已接到 Desktop。

但它仍不足以完成多数实际 10kV 工作票图。当前可画的是“杆塔 + 固定最小环网柜 + 简单架空直线”的受限子集；用户还不能形成典型的“环网柜电缆出线 → 电缆终端 → 杆上/架空线路”结构，也不能配置真实柜型、移动环网柜、浏览较大图纸，最终更不能导出 JPG 或打印。

因此当前成熟度应描述为：

> **Drawing Core 最小结构化编辑闭环已建立，但真实专业图的设备配置、完整布置、介质连接和输出闭环仍未完成。**

## 2. 已解决的旧 Review 阻断点

上一次 Review 中以下首要阻断已经由 P0-1～P0-3-B 实际解决：

1. Desktop 已有新建、打开、保存、另存为和关闭工程入口。
2. 新工程建立真实空 `DrawingDocument + RuntimeLayoutDocument + DrawingScene`，不依赖演示对象。
3. 打开工程采用 Candidate Session，恢复成功后才替换当前会话。
4. Dirty 工程在 New/Open/Close 前有 Save/Discard/Cancel 处理。
5. 用户可从空白画布放置 Pole 和最小 RingCabinet。
6. Add/Remove 通过 CommandStack，同时修改 Domain 与 RuntimeLayout。
7. Pole/RingCabinet 删除会拒绝仍被 Topology、PoleAttachment 或 Professional 数据引用的对象。
8. 多 RingCabinet 的 Selection Resolver 不再依赖单柜假设。
9. 用户可显式 Pick 两个真实外部 Terminal，创建 `Connection + OverheadLine + Layout`。
10. OverheadLine 创建、删除、Undo/Redo 保持稳定 ConnectionId。
11. Preview 线保持为临时 Scene 元素，不进入 Domain、Layout、Undo、Dirty 或 Persistence。
12. 正式架空线端点改由 TerminalAnchor 解析，设备移动不改变 Topology。
13. 保存前会把当前 RuntimeLayout 映射为 ProjectLayoutSnapshot。
14. 保存架空线时会用当前 TerminalAnchor 回填 FormatVersion 2 的 Start/End 兼容缓存。

## 3. 当前真实用户工作流

| 用户步骤 | 当前状态 | 代码事实与限制 |
| --- | --- | --- |
| New Project | 已完成 | 新建时选择路径并输入最小 Metadata，得到真实空工程和空白 Scene |
| Place devices | 部分完成 | 可放置 Pole、固定三间隔普通负荷开关柜；不能放置附件、电缆终端、柱上设备 |
| Configure devices | 阻断 | Pole 仅可改杆号；RingCabinet 结构、名称、间隔和开关状态无用户配置闭环 |
| Connect electrical system | 部分完成 | OverheadLine 已形成真实 Terminal/Connection 闭环；Cable 完全没有编辑与绘图闭环 |
| Arrange drawing | 阻断 | 只有 Pole 可拖动；RingCabinet、Attachment、标签均不可移动；无 Zoom/Pan/Fit |
| Save | 已完成 | RuntimeLayout 单向生成 Snapshot；保存成功后 MarkSaved |
| Reload | 部分完成 | 恢复链代码存在，但近期 Desktop/Interaction 改动没有 Windows 编译和端到端验证 |
| Export/Print | 阻断 | 没有 JPG/PNG 导出、打印、预览、页面或 DPI 入口 |

以“第一张真实可用工作票图”为验收标准，流程最早在 **Configure devices** 处被阻断；即使选用固定最小柜绕过配置，随后也会在完整布置、Cable 和最终输出处再次被阻断。

## 4. 分项复查

### 4.1 Project lifecycle

状态：**已完成，待 Windows 验证。**

`ProjectWorkspaceController` 已从 MainWindow 分离 New/Open/Save/SaveAs/Close、Dirty 判断和 Candidate Session 替换。Save As 保持当前 Domain 对象图和 ProjectId，只改变路径。加载或保存失败不会主动替换当前有效会话。

剩余风险不是架构缺口，而是缺少自动化和 Windows 实机验收：损坏文件、保存失败、Dirty 三分支和 Save As ID 保持尚未通过可重复测试证明。

### 4.2 Device placement and removal

状态：**Pole 与最小 RingCabinet 已完成。**

`PlacementController + DeviceCommandFactory` 能从真实工程创建 Domain 和 RuntimeLayout；Add/Remove Command 保持稳定 ID，并统一重建 Scene、HitTest、Selection 和 PropertyInspector。

删除保护已有明确边界：存在 Connection、OverheadLine 支撑引用、PoleAttachment、GroundingPoint 或 WorkScope 引用时拒绝删除，不做猜测式级联。

限制是设备种类过窄，且删除 UI 只覆盖当前能选中的 Pole、RingCabinet 和 OverheadLine；Attachment、CableTermination 尚无生命周期命令。

### 4.3 Device dragging

状态：**部分完成，是当前 P0 缺陷。**

只有 `PoleLayoutEditor + MoveCommand`。RingCabinetLayout、AttachmentLayout 虽有位置或偏移数据，但没有拖动编辑器、Replace API 和 Move Command 用户闭环。

Pole 拖动预览直接更新 RuntimeLayout，MouseUp 后提交一个 MoveCommand；保存前若仍有未提交拖动，会要求 Commit/Cancel。该流程方向正确，但 Esc 主要取消绘图工具，未形成统一的全部临时编辑取消机制，仍需 Windows 交互验证。

真实图中环网柜通常是主要构图锚点。创建后不能移动意味着用户必须第一次点击就放准位置，这不足以支撑实际排版。

### 4.4 RuntimeLayout → Persistence

状态：**核心闭环已建立，但覆盖面受当前编辑能力限制。**

编辑期唯一布局事实源是 `RuntimeLayoutDocument`。保存时 `ProjectLayoutRuntimeMapper.ToSnapshot` 重新生成柜体、杆塔、附件和架空线 Layout DTO，成功后 `AcceptSavedSession` 重建 Scene 并 `MarkSaved()`。

Pole 移动坐标会进入 Snapshot；加载后再转回 RuntimeLayout。当前没有发现 RuntimeLayout 与 ProjectLayoutSnapshot 同时可编辑的双事实源。

仍需补的 P0 验证包括：Move → Save → Reload 坐标一致；Undo 到保存点恢复 clean；Save 失败保留 Dirty；多次 Save As 不改变稳定 ID。

### 4.5 TerminalAnchor and OverheadLine following

状态：**代码闭环已完成，待运行验证。**

正式 Scene 使用 Connection 的 Start/End TerminalId 查询 TerminalAnchor；不再用 `OverheadLineLayout.Start/End` 反向覆盖 Anchor。Pole 移动后重建 Scene，线路几何随 Anchor 更新，Connection 和 ElectricalNode 均不改变。

FormatVersion 2 中 Start/End 仍存在，但只作为兼容缓存，并在保存时由当前 Anchor 回填。因此运行时没有第二个可编辑端点事实源。

同一 TerminalAnchorIndex 同时服务 OverheadLine、WorkScope 和 GroundingPoint，这是正确的收敛方向。

### 4.6 Selection and HitTest

状态：**当前对象范围内部分完成。**

Pole、RingCabinet、Interval、内部 Switch、PoleAttachment、OverheadLine、GroundingPoint、WorkScope 都有稳定 SelectionReference 和 Scene HitTest 条目；多条架空线、多台环网柜可按稳定 ID 解析。

当前仍是单选。没有框选、多选、选择循环和重叠对象候选 UI。线路 HitTest 使用包围矩形，而不是线段距离；斜线或长线的大矩形空白区域可能抢占点击，需要实机确认命中体验。

Terminal Pick 是 Connection Controller 按 Anchor 距离独立完成的，不是普通 Scene HitTest。该边界可接受，但未来 Zoom 后容差必须持续从 DIP 转为毫米。

### 4.7 Property editing

状态：**阻断真实设备配置。**

当前真正可编辑的 Drawing Core 设备属性只有 PoleNumber。GroundingPoint 和 WorkScope 有专用编辑闭环，但不替代设备配置。

以下仍为只读或无入口：RingCabinet 名称、间隔数量、间隔类型、接地结构、开关状态；OverheadLine 型号、长度、名称；PoleType；Attachment 属性；CableTermination 名称和端子侧信息。

第一张实际图不要求通用反射式属性表，但至少需要针对 RingCabinet、线路和附件的受控编辑入口，并全部进入 CommandStack。

### 4.8 PoleAttachment and CableTermination

状态：**Domain/Persistence/Rendering 支持，用户工作流阻断。**

Domain 能表达 PoleAttachment，CableTermination 有电缆侧/架空侧 Terminal 和内部固定 ElectricalNode；DTO、AttachmentLayout、AttachmentSymbol 和 TerminalAnchor 分支也存在。

Desktop 没有创建、挂载到指定 Pole、设置相对偏移、删除、Undo/Redo 或属性编辑入口。CableTermination 两个 Terminal 当前共享同一个附件中心 Anchor；OverheadLine 工具可按允许类型过滤到架空侧，但未来 Cable 工具仍需要清晰可选的电缆侧视觉锚点。

结论：**PoleAttachment/CableTermination 应先于或与 Cable 第一阶段一起完成。** 否则典型“电缆转架空”没有可落点的真实设备，Cable 编辑器只能覆盖较窄的柜间直连场景。

### 4.9 RingCabinet creation and configuration

状态：**固定最小柜可创建，真实配置能力已成为 P0 阻断。**

当前创建入口固定调用 `CreateNormalLoadSwitchCabinet`：3 个普通负荷开关间隔，负荷开关和接地刀均为 Open。用户不能选择 4/5/6 间隔、混合间隔、一二次融合结构或三种接地结构，也不能在创建后修改柜体。

Domain 已能表达混合 LoadSwitch/IntegratedFeeder 柜和三种 GroundingStructureKind，Rendering 也能组合显示；缺口主要是安全的创建配置 UI、Command 快照、布局生成和必要的状态编辑，而不是再造 Domain。

固定三间隔柜只能支持演示或恰好匹配的简单现场。它不足以代表“第一张真实图”的通用最小能力。建议优先实现一个受控创建对话框：名称、3～6 间隔、每间隔 LoadSwitch/IntegratedFeeder、融合间隔接地结构和初始开关状态。创建后结构重配可后置，但名称和开关状态编辑不宜后置太久。

### 4.10 Cable

状态：**阻断常见真实图，但不应孤立先做。**

当前只有 `ConnectionType.Cable` 和 Terminal 允许策略。没有 Cable 明细实体、CableLayout、Scene 构建、Symbol 接入、Selection Resolver、Command、Terminal 连线工具、删除或保存布局合同。

多数 10kV 工作票附图会出现环网柜电缆出线、电缆终端或柜间电缆，因此 Cable 已是“第一张具有代表性的真实图”的 P0 能力。如果首个验收样例被刻意限定为纯架空线路，它可以暂时绕过，但这不足以证明 MVP 的实际适用性。

实现顺序应是：先明确最小 Cable 明细与两端 Anchor 表达，并打通 CableTermination/PoleAttachment 创建，再复用 P0-3-B 的 Terminal Pick、原子 Command、Selection 和 Undo/Redo 模式。不要把 Cable 伪装成 OverheadLine，也不要为了 UI 临时复制端点坐标事实。

### 4.11 Zoom, Pan, Fit and navigation

状态：**未实现，属于 P0。**

DrawingSurface 位于固定 Border 内，没有画布 ScrollViewer、ViewTransform、滚轮缩放、平移、Zoom to Fit、页面范围或大图导航。当前坐标转换只按固定 mm↔DIP 比例工作。

Zoom/Pan 应进入下一轮 P0，但不建议成为下一次唯一工作。先修齐 RingCabinet 移动和 Windows 编译/端到端验证，再引入统一 ViewTransform；否则缩放只会让一个仍不能完整布置的编辑器更容易浏览。

最小导航范围应控制为：滚轮中心缩放、平移、Fit All、屏幕/DIP/文档毫米三层统一转换，并让 Placement、Drag、Terminal Pick、HitTest 和 Preview 全部走同一转换。Zoom/Pan 不属于 Layout，也不应进入当前工程格式。

### 4.12 Delete, Undo and Redo

状态：**当前已支持操作形成闭环，覆盖范围仍窄。**

Pole、RingCabinet、OverheadLine、Pole Move、PoleNumber、GroundingPoint 和 WorkScope 的正式修改进入同一 CommandStack。Add/Remove 保持稳定 ID，失败 Execute 不进入历史。

未覆盖 RingCabinet 移动/配置、Attachment/CableTermination 生命周期、Cable、线路属性和标签。Undo/Redo 后通过 Scene 重建和 Selection 校验避免保留悬空选择，但没有自动化测试覆盖多步交错事务和历史容量边界。

### 4.13 JPG/PNG Export and Print

状态：**完全阻断最终交付。**

当前没有 PNG/JPG/PDF、打印、打印预览、页面尺寸、DPI、边距或白色背景输出服务。`DrawingSceneRenderer` 只能生成 WPF DrawingVisual，Desktop 没有离屏位图或 PrintDialog 接线。

Export/Print 不应继续推迟到所有效率功能之后。它应在设备配置、Cable 和基本导航/布局达到首个验收样例后立即进入 P0 收口阶段。

优先顺序建议是先 JPG（满足明确 MVP 输出），再 Windows Print。两者都必须只消费正式 DrawingScene，排除 Selection Overlay、Preview、属性面板和其他编辑器 UI。输出前需要最小页面/范围策略，至少支持按图形 Bounds 加边距导出。

## 5. 架构风险

### 5.1 MainWindow.xaml.cs

当前文件约 1275 行，已经同时承担：菜单接线、鼠标路由、Pole Drag、Professional Pick 状态、属性面板专用控件同步、Scene 刷新、演示场景和临时编辑确认。

P0-1 的 ProjectWorkspaceController、P0-2 的 PlacementController、P0-3-B 的 Connection Controller 已避免继续把核心事务塞入窗口，但 MainWindow 仍是多个控制器之间的手工总线。继续加入 RingCabinet 配置、Attachment、Cable、Zoom/Pan 和 Export 会快速放大耦合。

下一阶段应做最小职责切分：统一 Editor/Tool 协调、Scene Refresh 管道和 Viewport 控制器；移除或隔离演示入口。不要在 P0 中进行全面 MVVM 重写。

### 5.2 DrawingSceneBuilder

当前约 336 行，同时组合 OverheadLine、Pole、Attachment、RingCabinet、Professional 元素，并构建各类 HitTest。它尚可维护，但新增 Cable、Terminal 热点、Annotation 和输出 Bounds 后会继续膨胀。

建议在新增 Cable 时提取按对象类别的 Scene contributor/builder，保留一个工程级编排入口。业务校验仍留在 Domain，SceneBuilder 不应决定连接合法性或设备配置规则。

### 5.3 Runtime and interaction risks

- Pole 是唯一可拖动对象，布局编辑抽象仍按具体类型编写。
- Tool 互斥只协调 Placement 与 OverheadLine；Professional Pick 和 Pole Drag 仍由 MainWindow 额外协调。
- OverheadLine HitTest 是线段包围矩形，可能产生大面积误命中。
- TerminalAnchor 对 CableTermination 两端使用同一点，Cable 工具前必须处理可辨识性。
- Command 的原子回滚主要靠手写 try/catch，缺少针对每个失败步骤的测试。
- 当前测试项目只覆盖部分 Domain；没有 Persistence、Rendering、Interaction、Desktop 或端到端测试。

### 5.4 Unable-to-compile risk

当前审查环境没有 `dotnet`，无法编译 net10.0-windows/WPF，也无法运行测试。P0-1～P0-3-B 涉及多个项目边界和大量 Desktop 接线，因此“静态代码存在”不能等价为“Windows 可运行”。

在继续扩展功能前，应把 Windows 构建和最小冒烟验收设为硬门槛：启动、新建、放置两设备、连线、移动、Undo/Redo、保存、关闭、重开。若该链路失败，应先修复，不应在未验证基线上继续堆 P0 功能。

## 6. 关键问题回答

### A. 当前最大的下一个用户阻断点是什么？

**真实设备配置与完整布局能力。** 用户能放置对象了，但只能得到固定三间隔柜，而且环网柜不能移动；这使“配置设备”和“安排图面”在最早阶段就失真。紧随其后的是 PoleAttachment/CableTermination/Cable 缺口。

在工程执行顺序上，Windows 编译与端到端冒烟验证应先作为门禁处理，因为当前甚至不能确认已完成链路可运行。

### B. Zoom/Pan 是否应该成为下一阶段？

**应该进入紧邻的 P0 阶段，但不建议单独抢在布局完整性之前。** 先支持 RingCabinet 移动并建立统一 ViewTransform 边界，再完成 Zoom/Pan/Fit，收益最大且不会重复改坐标交互代码。

### C. RuntimeLayout/拖动保存闭环是否足够？

Pole 的 RuntimeLayout → Command → Snapshot 路径已经足够作为基础，没有发现持久化双事实源。P0 缺陷是覆盖面和验证：RingCabinet/Attachment 不可移动，拖动取消/保存失败/Reload 坐标一致缺少 Windows 与自动化验证。

### D. 固定三间隔最小柜是否足够？

**不足。** 它能验证技术链路，不能覆盖实际常见的 4～6 间隔、混合负荷开关/融合间隔及接地结构。最小 RingCabinet 配置器已成为 P0，而完整的创建后结构重配可分阶段后置。

### E. Cable 是否是必要 P0？

**是，若目标是具有代表性的实际工作票图。** 纯架空验收样例可以绕过 Cable，但不能作为 MVP 完成标准。Cable 应在环网柜最小配置和终端设备创建之后进入。

### F. PoleAttachment/CableTermination 是否必须先于 Cable？

**对于典型电缆转架空流程，是。** 至少要先形成 CableTermination 的创建、挂载、布局、端子 Anchor、删除、Undo/Redo 和保存恢复闭环；否则 Cable 缺少可靠的现场转换端点。

### G. Export/Print 何时进入？

在首个代表性样例能完成设备配置、布置和 Cable/OverheadLine 连接后立即进入，不等待 Copy/Paste、Multi Select、Snap、模板等 P1 能力。先 JPG，后 Print；两者都只输出正式 Scene。

### H. MainWindow 是否出现新架构债务？

**是。** 控制器拆分方向正确，但窗口仍有 1275 行，并负责多个工具和刷新链协调。下一轮应做局部协调层提取，避免 RingCabinet/Cable/Viewport 继续直接增加窗口业务状态。

## 7. 重新排序后的最小 P0 路线

### P0-4：Windows 可运行门禁与当前闭环稳定化

- 在 Windows/.NET 10 上编译全部项目并运行现有测试；
- 冒烟验证 New → Place Pole/RingCabinet → Connect OverheadLine → Move Pole → Save → Reload；
- 修复编译、命中、Dirty、Undo/Redo、Anchor 跟随和恢复问题；
- 为 RuntimeLayout 映射与 Add/Remove OverheadLine 原子性补最小自动化测试；
- 不新增专业对象。

### P0-5：完整基本布局与画布导航

- 支持 RingCabinet 拖动、Undo/Redo 和保存恢复；
- 建立统一 ViewTransform；
- 实现 Zoom、Pan、Fit All；
- 统一 Placement、Drag、HitTest、Terminal Pick 和 Preview 坐标转换；
- 修正线路 HitTest 的明显误命中问题；
- 提取最小 Scene Refresh/Tool 协调职责，控制 MainWindow 增长。

### P0-6：最小真实设备配置

- RingCabinet 创建对话框：名称、3～6 间隔、LoadSwitch/IntegratedFeeder、GroundingStructureKind、初始开关状态；
- 保持创建命令原子性和稳定聚合 ID；
- 支持必要的柜名和开关状态编辑；
- 实现 CableTermination + PoleAttachment 创建、挂载、相对布局、删除、Undo/Redo、保存恢复；
- 明确并显示 CableTermination 两侧 Terminal Anchor。

### P0-7：Cable 绘图闭环

- 在既有专业设计边界内补齐最小 Cable 明细、Layout、Symbol 和 DTO；
- 复用 Terminal-based Pick、Preview、Domain 校验和原子 Command；
- 支持创建、选择、删除、Undo/Redo、设备移动跟线、保存恢复；
- 不实现自动布线或复杂路径编辑。

### P0-8：首张图输出与验收

- 实现按正式 DrawingScene Bounds 导出 JPG；
- 明确白色背景、边距和 DPI；
- 实现 Windows PrintDialog 和最小打印页面；
- 排除 Selection Overlay、Preview 和编辑器 UI；
- 完成“空白工程 → 代表性设备 → Cable/OverheadLine → 布置 → 保存重开 → JPG/打印”的 Windows 验收。

## 8. P0 与非 P0 边界

在第一张真实图完成前，以下能力不是下一批 P0：

- Copy/Paste；
- Multi Select、框选；
- Snap、Align、Distribution；
- 自动布局；
- 模板库和智能编号；
- 高级折线/曲线路由；
- 完整通用属性编辑器；
- 多文档编辑；
- WorkTicketData 代码实现。

这些能力会显著提高效率，但不应先于真实设备配置、Cable、基本导航和输出闭环。

## 9. 最终结论

P0-1～P0-3-B 已经证明当前架构可以把 Domain、Topology、RuntimeLayout、Rendering、Interaction 和 Persistence 串成一条真实编辑链，而不再只是演示代码。

下一步不应继续增加孤立底层模型，也不应只做界面美化。最小正确路线是：先在 Windows 证明现有链路可运行，再补齐环网柜可布置/可配置、CableTermination/PoleAttachment、Cable，最后立即完成 JPG 和打印输出。

完成上述 P0-4～P0-8 后，项目才具备“普通用户从空白工程完成第一张具有代表性的 10kV 工作票图”的可信交付基础。
