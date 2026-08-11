# M3-A 绘图编辑器基础架构设计

> 文档状态：设计稿，仅定义编辑器架构，不实现代码或 UI<br>
> 编制日期：2026-08-11<br>
> 依据：当前 Domain、Layout、SymbolLibrary、DrawingScene、WPF Rendering 链路及环网柜、杆塔、架空线路模型

## 1. 目标与范围

本阶段为 10kV 配电工作票附图软件定义最小编辑器架构，使后续拖放、移动、属性修改、状态修改、撤销重做和工程保存能够沿同一条受控链路实现。

本设计保持现有分层方向：

```text
Desktop / ViewModel
        ↓ 用户意图
Editor Command（Application）
        ↓
Domain + Layout
        ↓
DrawingSceneBuilder
        ↓
DrawingScene
        ↓
DrawingSceneRenderer
        ↓
DrawingVisualHost
```

本阶段不修改 Domain，不实现 WPF 编辑逻辑，不新增 UI 控件，也不实现拖放、属性面板、工程保存、WorkScope 或 GroundingPoint。

## 2. 编辑对象边界

### 2.1 Domain 对象

Domain 是设备语义和电气事实的唯一来源，包括：

- `DrawingDocument`、`Device`、`Terminal`、`Connection` 和 `ElectricalNode`。
- `RingCabinet`、`RingCabinetInterval`、`SwitchAssembly` 和 `SwitchDevice`。
- `Pole`、`PoleAttachment`、`CableTermination` 和 `OverheadLine`。
- 开关机械状态、设备名称、线路型号、端子引用和聚合结构。

Domain 对象不应由 View、鼠标事件或 Rendering 直接修改。“不可直接编辑”指 UI 不能绕过应用命令和领域校验写入对象，并不表示业务属性永远不可变。后续名称、开关状态或结构变更必须由 Editor Command 调用公开的 Domain 行为或受控的聚合替换入口完成。

以下操作属于 Domain 修改：

- 修改设备或间隔名称。
- 修改单台开关的 `SwitchState`。
- 创建、删除或重新连接设备和 Connection。
- 修改架空线路型号、延续信息等业务属性。
- 后续创建或修改 WorkScope、GroundingPoint。

Domain 不保存选中、高亮、鼠标位置、拖动预览、画布坐标或 WPF 对象。

### 2.2 Layout 对象

Layout 是几何编辑的主要目标，负责保存可重建图面的实例布局数据：

- 杆塔绝对位置。
- 杆塔附属设备相对偏移。
- 环网柜位置、间隔相对位置和内部开关相对位置。
- 架空线路的显示起点、终点和后续可扩展的人工路线。
- 标签偏移、图元尺寸和端子显示锚点。

移动对象只修改 Layout，不改变 TerminalId、Connection 端点、PoleAttachment 关系、SupportPoleIds、SwitchState 或 ElectricalNode 拓扑。

Layout 不拥有设备语义，不根据坐标相交创建电气连接，也不保存运行状态或有效接地结论。Layout 的每条记录必须以稳定的 Domain ID 或关系 ID 为键。

### 2.3 Symbol、Scene 与 WPF Visual

`SymbolLibrary` 和各 SymbolDefinition 保持无状态、只读和可复用：

- Symbol 读取 Domain、Layout 和一次性的 RenderContext，生成 SceneElement。
- Symbol 不接收编辑命令，不缓存选择状态，不反写 Domain 或 Layout。
- `DrawingScene` 是每次刷新生成的临时场景，不作为工程数据。
- `DrawingVisual` 是 WPF 输出对象，不作为选择对象、撤销记录或保存内容。

选中框、高亮、端子热点和拖动预览属于编辑器覆盖层。它们可以与主场景一起显示，但不能写入专业 SymbolDefinition，也不能进入 JPG 或打印场景。

### 2.4 EditorSession

建议由 Application 层定义一个文档级 `EditorSession` 概念，统一持有或引用：

- 当前 `DrawingDocument` 或等价领域聚合集合。
- 当前 `DrawingLayout` 及环网柜等布局集合。
- 当前 SelectionState。
- Undo/Redo 历史。
- 文档修订号和保存点状态。
- 最近一次场景生成或校验错误，但不持有 WPF Visual。

一个窗口打开一个工程时对应一个 EditorSession。Desktop 只负责把输入转换为命令，并订阅会话变化以触发重绘。

## 3. 用户操作与刷新流程

### 3.1 统一流程

```text
鼠标、键盘或属性面板操作
        ↓
Desktop 将输入转换为 Editor Intent
        ↓
Application 创建并执行 Editor Command
        ↓
命令校验目标 ID 和当前版本
        ↓
修改 Domain 或 Layout
        ↓
命令成功后写入 Undo History，并增加 DocumentRevision
        ↓
DrawingSceneBuilder 读取最新 Domain + Layout
        ↓
生成主场景和编辑覆盖层
        ↓
DrawingSceneRenderer 重绘 DrawingVisual
```

命令失败时不得留下部分修改，不写入 Undo History，也不触发“成功修改”事件。界面可以显示校验错误，但 Rendering 不负责修复数据。

### 3.2 Layout 修改路径

移动杆塔、环网柜或附属设备时使用布局命令：

```text
Pointer Drag
  → MoveLayoutCommand(TargetId, From, To)
  → 更新对应 Layout
  → 重建 DrawingScene
  → 重绘主场景和选择覆盖层
```

拖动期间可以使用不保存的 PreviewLayout 或位移变换显示预览。指针释放后只提交一个最终命令，不能把每一个 MouseMove 都记录为一条撤销操作。

如果移动父对象，子对象继续使用既有相对偏移；例如移动 Pole 只更新 PoleLayout，AttachmentLayout 不变。移动 RingCabinet 时，IntervalLayout 和 SwitchLayout 的相对位置不变。

### 3.3 Domain 修改路径

属性和状态修改不应伪装成 Layout 修改：

```text
属性面板或状态操作
  → RenameDeviceCommand / SetSwitchStateCommand / UpdateLinePropertiesCommand
  → 调用 Domain 行为并执行聚合校验
  → 保持 Layout 不变
  → 重建 DrawingScene
  → SymbolRenderContext 读取新的状态并重绘
```

状态命令只修改用户明确选择的单台 SwitchDevice。Rendering 不计算 `OperationalState`、有效接地或联锁，不自动改变其他开关状态；需要校验时由 Domain 返回结果，编辑器决定是拒绝命令还是展示问题，具体策略由后续状态编辑设计确认。

### 3.4 结构修改路径

后续拖放创建、删除设备或改变环网柜间隔配置时，一条命令可能同时修改 Domain 和 Layout。此类命令必须作为单个原子事务：

1. 先构造并校验完整 Domain 对象或聚合定义。
2. 创建与其 ID 对应的初始 Layout。
3. 两部分均成功后一次性提交。
4. 任一部分失败时恢复到命令执行前状态。

不得先把无完整 Domain 的图元放入画布，再等待用户补齐电气对象。Symbol 也不能作为待创建业务对象的临时替代品。

## 4. 选择机制

### 4.1 选择身份

选择状态必须引用稳定业务身份，不能引用 `DrawingVisual`、SceneElement 对象地址或数组下标。建议使用类型化的 SelectionKey：

| 选择类型 | 主标识 | 可选上下文 |
| --- | --- | --- |
| Device | DeviceId | ParentId |
| RingCabinetInterval | IntervalId | CabinetId |
| SwitchDevice | SwitchDeviceId | IntervalId 或 PoleId |
| PoleAttachment | AttachmentId | PoleId |
| Connection / OverheadLine | ConnectionId | 无 |
| 后续 WorkScope | WorkScopeId | 无 |
| 后续 GroundingPoint | GroundingPointId | TerminalId |

SelectionState 至少包含 PrimarySelection，并预留有序的 SelectedKeys 集合。M3-A 首版可以只执行单选，但数据结构和命令目标不应假定永远只有一个对象。

### 4.2 命中测试

场景生成阶段应同步建立只读 HitTestIndex，将可命中几何映射到 SelectionKey。命中区域可以比实际细线稍宽，但不得改变打印或导出几何。

命中优先级建议为：

1. 端子热点或编辑手柄，仅在相应编辑模式显示。
2. SwitchDevice、PoleAttachment 等叶子设备。
3. RingCabinetInterval。
4. RingCabinet、Pole 等容器或主体。
5. Connection / OverheadLine。

重叠对象的具体循环选择或候选列表留到交互细化阶段；不得通过改变 Domain 顺序解决视觉重叠。

### 4.3 高亮显示

选中高亮由独立 SelectionOverlay 生成：

- 使用 Layout 和 HitTestIndex 计算选中边界或轮廓。
- 不修改 Symbol 的颜色事实，不覆盖设备的带电/停电专业颜色。
- 不写入 Domain、Layout 或工程文件。
- 不进入 JPG 和打印。

复合对象选中时只高亮对应层级。选中整个 RingCabinet 不等于同时把全部 Interval 和 SwitchDevice 写入多选集合；需要选择内部设备时，由新的命中结果替换 PrimarySelection。

### 4.4 属性查看

属性面板通过 SelectionKey 查询只读 PropertyViewModel：

- Domain 属性来自领域对象。
- 坐标、偏移和尺寸来自 Layout。
- 派生评估结果可以只读显示，但不能作为已保存字段回写。
- 多选时只显示共同可编辑字段，首版可以仅显示“已选择 N 个对象”。

属性面板提交修改时仍创建 Editor Command，不能直接绑定到 Domain 私有字段或 Layout 集合。

## 5. Editor Command 设计

### 5.1 基础合同

每个命令应具备以下语义：

- 稳定的命令类型和本次操作 ID。
- 目标对象 ID，不持有 WPF 控件或 SceneElement。
- 执行所需的新值，以及撤销所需的原值或受影响对象快照。
- `Execute`、`Undo`，必要时提供 `Redo` 或使用确定性 Execute 重放。
- 执行前校验和原子提交。
- 可选的合并键，用于将一次连续拖动或连续文本输入压缩成一个历史项。

命令是进程内编辑行为，不直接等同于工程文件 DTO，也不要求作为永久事件日志保存。

### 5.2 命令分类

| 分类 | 示例 | 修改边界 |
| --- | --- | --- |
| Layout 命令 | MoveLayout、MoveAttachment、UpdateLabelOffset | 仅 Layout |
| Domain 属性命令 | RenameDevice、UpdateOverheadLineProperties | 仅 Domain |
| Domain 状态命令 | SetSwitchState | 仅 Domain |
| 结构命令 | AddDevice、RemoveDevice、ConnectTerminals | Domain + Layout，原子执行 |
| 后续安全措施命令 | SetWorkScope、AddGroundingPoint | Domain + 对应 Layout |

Selection、缩放、平移和当前工具模式默认不进入文档 Undo History，因为它们不改变工程内容。

### 5.3 命令执行协调

建议由 CommandDispatcher 或 EditorCommandService 串行执行文档命令：

1. 校验命令目标仍存在，命令基准修订号可接受。
2. 捕获最小撤销数据。
3. 执行 Domain 和/或 Layout 修改。
4. 运行聚合结构校验及引用校验。
5. 成功后增加 DocumentRevision、写入历史并发布 DocumentChanged。
6. 由订阅者重建场景，不由命令直接调用 WPF Renderer。

同一个 EditorSession 内不允许并发写命令。MVP 不设计多用户合并或分布式冲突解决。

## 6. Undo/Redo 方案

### 6.1 历史结构

每个 EditorSession 维护两个栈：UndoStack 和 RedoStack。

- 新命令成功后进入 UndoStack，并清空 RedoStack。
- Undo 从 UndoStack 取出命令，恢复到执行前状态，再放入 RedoStack。
- Redo 重放同一已验证变更，成功后返回 UndoStack。
- 执行、撤销或重做失败时保持两个栈和当前文档一致，并报告错误。

### 6.2 恢复边界

不同命令采用不同粒度的恢复数据：

- 移动、标签偏移等简单 Layout 命令保存前后值。
- 名称、状态和线路属性命令保存明确的前后业务值。
- 创建、删除、连接和环网柜结构变更保存受影响聚合及其 Layout 子集的版本化内存快照。

不建议每次移动都复制整个 DrawingDocument；也不允许通过 WPF Visual 快照恢复业务对象。

撤销结构命令时必须同时恢复：

- Domain 对象和所有权关系。
- Terminal、Connection、ElectricalNode 等受影响引用。
- 对应 Layout 记录。
- 当前 SelectionState 中已失效的引用。

恢复后仍通过正常场景生成链路重绘，不保存或恢复旧 DrawingVisual。

### 6.3 命令合并与保存点

- 一次拖动从按下到释放合并为一条 Move 命令。
- 属性文本连续输入可在编辑提交时形成一条命令，不为每个字符建历史。
- 保存工程时记录 SavedRevision，不强制清空 Undo/Redo。
- 当前 DocumentRevision 与 SavedRevision 不同时标记为未保存。
- 关闭文档后历史可以丢弃；MVP 不把 Undo/Redo 历史写入工程文件。

## 7. DrawingScene 刷新策略

MVP 先采用文档级场景重建，保证正确性：任何成功命令都以最新 Domain + Layout 重新生成 DrawingScene，再由 DrawingSceneRenderer 输出新的 DrawingVisual。

```text
DocumentChanged
    ├── DomainRevision
    ├── LayoutRevision
    └── ChangedObjectIds
             ↓
DrawingSceneBuilder.Build(Domain, Layout, RenderContext)
             ↓
主场景 + SelectionOverlay + 交互覆盖层
             ↓
DrawingVisualHost.Show(newVisual)
```

`ChangedObjectIds` 可为后续局部重建预留，但首版不必建立复杂的增量渲染缓存。刷新期间出现缺失 Layout 或无效引用时，应保留上一次有效画面或显示明确错误，不由 Rendering 静默创建业务数据。

选中变化只需重建 SelectionOverlay；实现简单时也可重绘整个场景，但不能因此修改文档修订号或未保存状态。

## 8. 工程文件保存边界

### 8.1 必须保存

工程文件由 Infrastructure 负责序列化，Application 负责协调保存用例。至少保存：

- 文件格式版本、工程 ID、标题、创建时间和修改时间等文档元数据。
- 当前 MVP 的 Domain 对象、稳定 ID、所有权、端子、节点、连接和人工业务状态。
- RingCabinet、Interval、SwitchAssembly、PoleAttachment、OverheadLine 等聚合或关系数据。
- 所有可编辑 Layout：设备位置、相对偏移、尺寸、标签位置、线路路线及页面设置。
- 后续 WorkScope、BoundaryPoint、GroundingPoint 及其 TerminalId 引用。
- 使用的图元包或规则集版本引用；不复制运行时 Symbol 实例。

Domain 与 Layout 应序列化为版本化 DTO，再分别重建并做交叉引用校验。工程文件不是 Domain 对象的直接反射序列化结果。

### 8.2 不保存

- SymbolDefinition、SymbolRenderContext、DrawingScene、SceneElement、DrawingVisual。
- HitTestIndex、SelectionState、高亮、悬停、框选框和拖动预览。
- UndoStack、RedoStack、未提交命令和 UI 焦点。
- 可重新计算的 OperationalState、IsEffectivelyGrounded、联锁违规结果。
- 屏幕 DPI、窗口尺寸、临时缩放和平移位置；若未来需要恢复视图，应作为独立用户偏好，而不是电气工程事实。

### 8.3 保存流程预留

```text
EditorSession 当前一致状态
        ↓
Application SaveProject 用例
        ↓
Domain DTO + Layout DTO + Metadata
        ↓
跨引用与版本校验
        ↓
Infrastructure 写入临时文件
        ↓
重新读取校验
        ↓
原子替换正式工程文件
        ↓
SavedRevision = DocumentRevision
```

保存失败不得改变当前会话内容或 SavedRevision。JPG 和打印只消费同一 Domain + Layout 生成的场景，不替代工程文件。

## 9. 后续能力预留

### 9.1 拖放

设备库拖放应产生 AddDeviceCommand 或 AddTemplateCommand。落点转换为毫米文档坐标，并生成明确 Layout；RingCabinet 等聚合对象必须通过现有工厂创建完整 Domain 结构。拖动已有对象使用 MoveLayoutCommand，两者不共享“先画图、后补模型”的路径。

### 9.2 属性面板

属性面板以 SelectionKey 取得类型化属性描述。可编辑字段由当前 Domain 设计白名单决定；图元颜色、固定端子数量、ElectricalNode 固定拓扑和派生运行状态不是自由属性。

### 9.3 WorkScope

未来 WorkScope 通过两个 BoundaryPoint 引用明确的 DeviceId、TerminalId 和侧别。边界选择可以复用端子命中测试，但不能保存画布矩形或坐标来替代电气边界。编辑 WorkScope 不自动推导停电范围或改变 ElectricalState。

### 9.4 GroundingPoint

未来 GroundingPoint 由用户在有效 Terminal 上人工创建并编号。图面位置可以通过 TerminalId 和对应 Layout 计算，必要的标签偏移另存 Layout。工作地线不与环网柜内部接地刀闸或固定 GroundingLine 图元混用，也不由开关状态自动生成。

### 9.5 多对象选择

SelectionState 从一开始使用集合表达，但首版命令可以只接受单个目标。后续批量移动必须保存每个对象的前后 Layout 值并作为一个原子命令；批量属性修改只允许共同且语义一致的字段。

## 10. 校验与测试建议

后续实现至少覆盖：

- 移动 Pole 或 RingCabinet 只改变 Layout，电气端点和内部结构不变。
- 修改单台 SwitchDevice 状态后场景更新，其他开关状态不被 Rendering 改写。
- 选中与高亮不改变文档修订号，且不进入保存、JPG 或打印。
- Undo/Redo 可恢复简单 Layout、Domain 属性及 Domain + Layout 原子结构命令。
- 新命令执行后 RedoStack 清空；保存点能正确反映未保存状态。
- 保存重开后 Domain 引用和 Layout 一致，且不包含选择、历史或 WPF 对象。
- 混合环网柜、PoleAttachment 和 OverheadLine 的 SelectionKey 始终引用稳定 ID。
- 无效命令不留下部分对象、孤立 Layout 或悬空引用。

## 11. 本阶段不实现

- 任何 Domain、Application、Rendering.Wpf、Desktop 或 Infrastructure 代码。
- WPF 命中测试、选择覆盖层和属性面板控件。
- 拖放、移动、连接编辑或自动布局。
- Undo/Redo 运行时代码。
- 工程文件 DTO、保存、打开和迁移代码。
- WorkScope、GroundingPoint、PTInterval 或 DTU 实现。
- JPG、打印和多用户协作。
