# Drawing Core P0-1-A Desktop 工程会话集成设计

> 文档状态：实现前设计，仅定义 P0-1 Desktop 单工程生命周期，不修改代码<br>
> 编制日期：2026-08-12<br>
> 依据：`docs/drawing-core-capability-review.md`、当前 `ProjectService`、`ProjectSession`、`ProjectRuntimeSession`、`MainWindow` 和 Persistence/Runtime Restore 实现

## 1. 结论与设计范围

P0-1 采用**单窗口、单工程、创建时即选择保存路径**的最小方案。用户可以通过 Desktop 完成：

```text
新建 / 打开
→ 候选 ProjectSession
→ 候选 ProjectRuntimeSession
→ 空白或恢复后的 DrawingScene
→ 原子替换 MainWindow 当前工程
→ 保存 / 另存为 / 关闭
```

本阶段不引入多文档标签、未命名临时工程、自动保存、最近文件、模板或设备编辑功能。

核心决策：

1. 新增 Desktop 级 `ProjectWorkspaceController`，统一管理 `ProjectService`、`ProjectSession` 和 `ProjectRuntimeSession` 生命周期；
2. `MainWindow` 只转发菜单事件、显示当前会话和响应协调器事件，不直接执行文件或 Session 业务流程；
3. `ProjectRuntimeSession` 是工程打开期间 Domain、Runtime Layout、Scene 和 Editor 临时状态的唯一编辑会话；
4. `ProjectSession` 是文件合同、路径、Manifest/Metadata 和保存快照的 Persistence 表达，不作为第二份可编辑 Layout；
5. 保存前必须把当前 Runtime Layout 显式映射为 `ProjectLayoutSnapshot`；
6. 加载或创建必须先构建完整候选 RuntimeSession，成功后才替换当前工程；
7. 保存后保留当前运行时 Domain/Layout 对象图并调用 `CommandStack.MarkSaved()`，不能让已有 Command 指向被重新恢复出的另一套对象；
8. 新工程使用真正空的 DrawingDocument、RuntimeLayout 和 DrawingScene，不调用任何演示入口。

## 2. 当前实现约束

### 2.1 已有可复用能力

- `ProjectService.CreateProject(filePath, title, description)` 会创建有效空 `.kvdrawing`；
- `ProjectService.LoadProject(filePath)` 会完成容器、版本、Domain、Topology、Professional 和 Layout 校验；
- `ProjectService.SaveProject()` 能从当前 ProjectSession 生成 DTO 并原子写入文件；
- `ProjectLayoutRuntimeMapper.ToRuntime()` 能把 `ProjectLayoutSnapshot` 恢复为毫米 Runtime Layout；
- `DrawingSceneBuilder.Build(document, runtimeLayout)` 对空 DrawingDocument 可生成空 Scene；
- `ProjectRuntimeSession.Load()` 能把加载后的工程映射为 Scene、Selection、PropertyInspector 和 CommandStack；
- `CommandStack` 已有 SavePoint 和 `IsDirty`。

### 2.2 必须处理的缺口

1. MainWindow 没有 ProjectService/RuntimeSession 集成，“文件”菜单为空；
2. ProjectRuntimeSession 只有 Load，没有从已创建 ProjectSession 构建空运行时会话的入口；
3. 只有 Layout Snapshot→Runtime 映射，没有 Runtime→Snapshot；
4. MainWindow 和 ProjectRuntimeSession 各自拥有 SelectionManager、CommandStack、Scene 等状态；
5. MainWindow 的 `_currentScene`/`_activeSource` 与 RuntimeSession.Scene/InspectionSource 形成重复状态；
6. `ProjectService.SaveProject()` 重新恢复一套 Domain 对象并把它设为 Current，现有 Command 仍持有保存前对象引用；
7. ProjectService 没有 Save As；
8. `ProjectService.LoadProject()` 成功后立即替换该 service 的 Current，但完整 Runtime Scene 可能仍构建失败。

P0-1-B 必须最小修正这些问题，否则“保存后 clean”和“打开失败不破坏当前工程”不能可靠成立。

## 3. Application / Desktop 职责边界

### 3.1 现阶段落点

建议新增：

```text
DistributionDrawing.Desktop
└── Workspace
    ├── ProjectWorkspaceController
    ├── IProjectWorkspaceDialogs
    └── WpfProjectWorkspaceDialogs
```

协调器暂时放在 Desktop，而不是 Application，原因是：

- 当前 Application 只引用 Domain；
- Infrastructure 已引用 Application；
- ProjectRuntimeSession 和 WPF 对话框位于 Desktop/Rendering；
- 若 Application 直接引用 Infrastructure 或 Rendering，会形成错误依赖或循环依赖；
- P0-1 目标是最小接通现有模块，不在本阶段重构项目依赖图。

协调器虽位于 Desktop，但不直接操作 WPF 控件。对话框和确认通过 `IProjectWorkspaceDialogs` 抽象，以便测试生命周期逻辑。后续若建立 Application ports，可将协调用例下沉而不改变 MainWindow 的调用方式。

### 3.2 MainWindow 职责

MainWindow 只负责：

- 文件菜单 Click 事件调用协调器；
- 订阅 `CurrentSessionChanged`、`DirtyStateChanged` 或等价通知；
- 把当前 Scene 交给 DrawingVisualHost；
- 更新窗口标题、菜单启用状态和属性面板绑定；
- 在 Window Closing 时调用协调器的关闭确认入口；
- 显示由 Dialog Service 提供的 WPF 窗口。

MainWindow 不负责：

- 直接调用 ProjectFileContainer；
- 维护 ProjectService.Current；
- 拼装 ProjectSession；
- Layout DTO 映射；
- 选择文件路径；
- 判断是否应替换当前工程；
- 保存失败后的状态修复；
- 复制 New/Open/Save/SaveAs 的 Dirty 判断。

### 3.3 ProjectWorkspaceController 职责

```text
ProjectWorkspaceController
├── CurrentService : ProjectService?
├── CurrentSession : ProjectRuntimeSession?
├── NewProject()
├── OpenProject()
├── SaveProject()
├── SaveProjectAs()
├── CloseProject()
└── CanCloseApplication()
```

协调器负责：

- 单一当前工程；
- 创建候选 ProjectService/ProjectRuntimeSession；
- Dirty 询问和 Save/Discard/Cancel 分支；
- Runtime Layout 快照同步；
- 保存、另存为和保存点；
- 候选 Session 原子替换；
- 错误分类和用户可读错误通知；
- 当前工程关闭后的空 Workspace 状态。

协调器不负责设备创建、连线、Zoom/Pan、Export 或 WorkTicketData。

## 4. Session 模型与唯一事实源

### 4.1 ProjectSession

ProjectSession 表示 Persistence 生命周期状态：

- FilePath；
- ProjectFileDocument / Manifest / Metadata；
- 当前运行时 DrawingDocument 引用；
- 保存时生成的 ProjectLayoutSnapshot；
- ProjectProfessionalSnapshot；
- 文件层 IsDirty 状态。

它不拥有 Scene、DrawingVisual、Selection 或 Undo 历史，也不应在打开工程期间维护另一份可独立变化的 Runtime Layout。

### 4.2 ProjectRuntimeSession

ProjectRuntimeSession 是当前编辑期唯一会话，持有：

- 当前 DrawingDocument；
- 当前 RuntimeLayoutDocument；
- 当前 DrawingScene；
- PropertyInspectionSource；
- SelectionManager / SelectionObjectResolver；
- PropertyInspector；
- 唯一 CommandStack；
- 当前关联的 ProjectSession 文件状态。

建议提供以下最小入口：

```text
Create(ProjectSession, DrawingSceneBuilder?)
Load(ProjectService, filePath, DrawingSceneBuilder?)  // 可保留为便利入口
RebuildScene()
AcceptSavedSession(ProjectSession)
```

`Create` 名称表示“从已完成 Persistence 恢复或新建的 ProjectSession 创建 Runtime”，不创建 Domain 业务对象。

### 4.3 MainWindow 不再持有重复 Editor 状态

工程已打开时，MainWindow 应使用：

```text
workspace.CurrentSession.CommandStack
workspace.CurrentSession.SelectionManager
workspace.CurrentSession.Scene
workspace.CurrentSession.InspectionSource
```

不再另建窗口级 `_commandStack`、`_selectionManager`、`_currentScene` 和 `_activeSource` 作为第二套工程状态。

无工程时：

- CurrentSession 为 null；
- DrawingSurface.Clear()；
- PropertyInspector 清空；
- Save/SaveAs/Close/Undo/Redo 等工程命令禁用。

## 5. Runtime Layout 与 Persistence Layout 双状态处理

### 5.1 权威状态

工程打开期间：

```text
RuntimeLayoutDocument = 可编辑权威状态
ProjectLayoutSnapshot = 最近一次文件快照或保存时生成物
```

编辑命令只能修改 RuntimeLayoutDocument。ProjectLayoutSnapshot 不与每次鼠标移动同步，也不允许成为另一份可编辑状态。

### 5.2 增加反向映射

在现有 `ProjectLayoutRuntimeMapper` 增加：

```text
ToSnapshot(DrawingDocument domain, RuntimeLayoutDocument runtime)
    → ProjectLayoutSnapshot
```

必须覆盖：

- PoleLayout；
- AttachmentLayout；
- OverheadLineLayout；
- RingCabinetLayout；
- RingCabinetIntervalLayout；
- RingCabinetSwitchLayout；
- 坐标单位固定为 `mm`；
- 使用稳定 Domain ID；
- 调用现有 Persistence 校验入口验证完整覆盖、重复与孤立 Layout。

映射不保存 Scene、DIP、Selection 或 Undo。

### 5.3 保存同步顺序

```text
Current ProjectRuntimeSession
  ↓
取消或提交正在进行的临时交互
  ↓
RuntimeLayout → ProjectLayoutSnapshot
  ↓
ProjectService.SetLayout(snapshot)
  ↓
ProjectService.SaveProject / SaveProjectAs
  ↓
重新打开并校验已写文件
  ↓
保留当前运行时 Domain/Layout 对象图
  ↓
RuntimeSession.AcceptSavedSession(savedSession)
  ↓
CommandStack.MarkSaved()
  ↓
IsDirty = false
```

### 5.4 保存后对象身份

当前 Command 直接持有 Pole、DrawingLayout、DrawingDocument 等运行时对象引用。若 SaveProject 后直接用重新加载出的 Domain/RuntimeLayout 替换当前对象图，已有 Undo/Redo 会修改旧对象，形成严重错误。

因此 P0-1-B 应调整 ProjectService 保存发布行为：

- 写入后仍执行完整 reopen/restore 作为验证；
- 验证通过后，发布的 ProjectSession 保留当前运行时 DrawingDocument 引用和刚生成的 Layout Snapshot；
- 只更新 Persisted Document、Manifest 时间、FilePath、Professional/Layout 快照和 IsDirty；
- 不用验证过程中临时恢复的 Domain 替换当前运行时 Domain；
- ProjectRuntimeSession 接受新的文件会话并调用现有 CommandStack.MarkSaved()；
- Undo/Redo 历史保留，Undo 回保存点时可恢复 clean，之后再 Undo 则 dirty。

如果实现不愿修改 ProjectService 此行为，则必须在保存时清空 CommandStack 并彻底替换 RuntimeSession；该退化方案丢失保存前 Undo 历史，不建议作为 P0-1-B 默认实现。

### 5.5 Preview 状态

Pole 拖动等 Preview 已经直接修改 Runtime Layout。执行 New/Open/Close/Save 前，应由 MainWindow 的交互宿主统一结束当前手势：

- 已 Commit 的修改进入 CommandStack；
- 未 Commit 的 Preview 恢复 Before；
- 不允许把半完成 Preview 写入工程文件。

P0-1-B 只需覆盖当前 Pole 拖动，不扩展新的编辑工具。

## 6. 新建工程流程

### 6.1 最小策略

第一版选择：**新建时即输入工程信息并选择保存路径。**

原因：

- 当前 ProjectService.CreateProject 立即创建文件；
- 避免引入“未命名、未落盘工程”的临时路径和生命周期；
- Save 行为始终有明确路径；
- Dirty 提示无需区分“从未保存”和“已保存”。

### 6.2 流程

```text
文件 → 新建
  ↓
检查当前工程 Dirty
  ├── Save → 保存成功后继续
  ├── Discard → 继续，不删除原文件
  └── Cancel → 停止，新建对话框不打开
  ↓
NewProjectDialog：Title（必填）+ Description（可选）
  ↓
SaveFileDialog：*.kvdrawing
  ↓
创建 candidate ProjectService
  ↓
candidateService.CreateProject(path, title, description)
  ↓
ProjectRuntimeSession.Create(candidate ProjectSession)
  ↓
空 RuntimeLayout + DrawingSceneBuilder.Build(empty document)
  ↓
候选完整成功
  ↓
原子替换 CurrentService + CurrentSession
  ↓
MainWindow 显示真正空白 Scene
```

用户取消工程信息或路径选择时，不改变当前工程。

若文件创建成功但异常导致空 RuntimeSession 构建失败：

- 当前工程不替换；
- 报告“文件已创建但未能打开”的实际路径；
- 不自动删除用户刚选择的文件。

### 6.3 空 Scene 标准

新工程必须满足：

- DrawingDocument.Devices/Connections/Professional 集合为空；
- ProjectLayoutSnapshot 与 RuntimeLayoutDocument 为空且 DocumentId 一致；
- DrawingScene.Elements 和 HitTestIndex.Entries 为空；
- Selection 为空；
- CommandStack 已 MarkSaved，IsDirty=false；
- 不调用 `OnDrawTestContent`、`CreateMixedRingCabinet` 或任何演示工厂。

演示菜单可以在 P0-1-B 保留为开发入口，但不得自动写入当前工程，也不得作为验收路径；建议后续单独移除或转入 Debug-only 场景。

## 7. 打开工程流程

```text
文件 → 打开
  ↓
当前 Dirty 确认（Save / Discard / Cancel）
  ↓
OpenFileDialog：选择 *.kvdrawing
  ↓
创建独立 candidate ProjectService
  ↓
candidateService.LoadProject(path)
  ↓
ProjectRuntimeSession.Create(candidate ProjectSession)
  ├── Runtime Layout restore
  ├── DrawingScene build
  ├── PropertyInspectionSource build
  └── Editor transient state initialize
  ↓
候选完整成功后原子替换 CurrentService + CurrentSession
  ↓
通知 MainWindow 刷新 Scene/属性/标题
```

必须使用独立 candidate ProjectService，不能先调用当前 service 的 LoadProject。这样即使 ProjectService 加载成功但 Runtime Layout 或 Scene 构建失败，当前 service 和当前 RuntimeSession 都不变。

成功切换后：

- 旧 Selection、CommandStack、Preview 和 PropertyInspector 不进入新工程；
- 新 Session CommandStack 为空并 MarkSaved；
- 新工程 IsDirty=false；
- 旧 RuntimeSession 可释放。

## 8. 保存工程流程

### 8.1 Save

```text
文件 → 保存
  ↓
要求 CurrentSession 存在
  ↓
结束当前 Preview
  ↓
Runtime Layout → Snapshot
  ↓
ProjectService.SetLayout(snapshot)
  ↓
ProjectService.SaveProject()
  ↓
完整文件 reopen/restore 验证
  ↓
AcceptSavedSession
  ↓
CommandStack.MarkSaved
  ↓
窗口标题移除 *，IsDirty=false
```

Domain 和 Professional 修改已经发生在 CurrentSession.DrawingDocument 上，保存时 ProjectService 从同一对象生成 DTO；不再从 PropertyInspector 或 Rendering 反向收集数据。

### 8.2 保存失败

- 当前 RuntimeSession、CommandStack、Selection 和 Scene 保持不变；
- IsDirty 继续为 true；
- 不调用 MarkSaved；
- 不发布失败保存产生的候选 ProjectSession；
- ProjectFileContainer 的临时文件清理由现有原子保存实现负责；
- 错误信息包含目标路径和根因，不宣称保存成功。

## 9. 另存为

### 9.1 最小语义

P0-1 将 Save As 定义为：**把当前同一工程保存到新路径，并让当前工作区随后指向新路径。**

- 保持 ProjectId 不变；
- 保持所有 Domain/Professional/Layout 稳定 ID；
- 原路径文件不删除、不继续自动更新；
- 新路径保存成功后 CurrentSession.FilePath 切换到新路径；
- 保存后 clean；
- 保存失败或用户取消时仍指向原路径且保持 Dirty 状态。

这不是“复制工程”功能，也不定义新 ProjectId。未来若业务需要“从当前工程创建独立副本”，应作为单独命令确认其 ID 重建规则，不能借用 Save As 隐式实现。

### 9.2 所需服务入口

建议 ProjectService 增加：

```text
SaveProjectAs(string filePath)
```

它复用 SaveProject 的快照、校验和原子写入，只改变目标路径；成功前不得发布新路径。

## 10. Dirty、保存点与切换提示

### 10.1 Dirty 来源

当前工程 Dirty 统一由 ProjectRuntimeSession 暴露：

```text
IsDirty = PersistenceSession.IsDirty || CommandStack.IsDirty
```

P0-1 接通后，所有当前已支持的编辑继续通过同一个 RuntimeSession.CommandStack。Layout 快照同步本身不是新的用户编辑命令，不应额外产生历史项。

### 10.2 保存点

- 新建成功：空历史，MarkSaved；
- 打开成功：空历史，MarkSaved；
- 编辑成功：CurrentStateId 改变，Dirty=true；
- 保存/另存成功：MarkSaved，Dirty=false；
- 保存后 Undo：若回到保存前状态，当前状态与 SavePoint 不同，Dirty=true；
- Redo 回保存点：Dirty=false；
- 切换工程：新 Session 使用独立空历史，不保存旧工程 Undo 历史。

### 10.3 最小确认对话框

当 New/Open/Close/Window Close 将离开 Dirty 工程时，显示：

```text
是否保存对“{工程标题}”的更改？
[保存] [不保存] [取消]
```

语义：

- 保存：先执行 Save；成功才继续原操作；
- 不保存：丢弃内存中未保存更改，不删除已有文件；
- 取消：原操作终止，当前工程、画布和选择保持不变。

保存失败等价于无法继续，不自动转为“不保存”。

## 11. 关闭与切换工程

### 11.1 关闭当前工程

```text
文件 → 关闭工程
→ Dirty 确认
→ 允许关闭
→ 取消当前手势
→ CurrentSession = null
→ 清空 DrawingSurface / Selection / PropertyInspector
→ 禁用工程相关菜单
```

关闭工程不退出应用，也不删除工程文件。

### 11.2 关闭应用窗口

MainWindow Closing 事件调用 `workspace.CanCloseApplication()`：

- true：允许窗口关闭；
- false：设置 Cancel=true；
- 不在 App.Exit 或析构器中静默丢弃 Dirty 工程。

### 11.3 新建/打开时切换

New/Open 都复用同一 `PrepareToReplaceCurrent()` 流程，避免三份 Dirty 逻辑。只有候选 RuntimeSession 完整成功后才释放旧 Session。

## 12. 文件对话框

### 12.1 OpenFileDialog

最小设置：

```text
Filter = "10kV 配电绘图工程 (*.kvdrawing)|*.kvdrawing|所有文件 (*.*)|*.*"
DefaultExt = ".kvdrawing"
CheckFileExists = true
Multiselect = false
```

### 12.2 SaveFileDialog

最小设置：

```text
Filter = "10kV 配电绘图工程 (*.kvdrawing)|*.kvdrawing"
DefaultExt = ".kvdrawing"
AddExtension = true
OverwritePrompt = true
```

新建和另存为共用 SaveFileDialog。路径标准化继续交给 ProjectService/ProjectFileContainer。

### 12.3 NewProjectDialog

最小字段：

- Title：必填；
- Description：可选。

ProjectId 自动生成，不向用户展示。第一版不增加票号、文档类型、页面设置或模板选择。

## 13. 错误处理

### 13.1 错误分类

| 场景 | 典型异常 | UI 行为 | 当前工程 |
| --- | --- | --- | --- |
| 文件不存在 | FileNotFoundException | 显示文件路径和“文件不存在” | 保持 |
| ZIP/JSON/结构错误 | InvalidDataException / JsonException | 显示“工程文件损坏或格式错误”及详情 | 保持 |
| Version 不兼容 | InvalidDataException | 显示当前支持版本和文件版本 | 保持 |
| Domain/Topology 恢复失败 | InvalidDataException / InvalidOperationException | 显示对象 ID/字段诊断 | 保持 |
| Layout/Scene 恢复失败 | InvalidDataException / InvalidOperationException / NotSupportedException | 显示布局或图元不支持 | 保持 |
| 保存失败 | IOException / UnauthorizedAccessException / InvalidDataException | 显示目标路径和失败原因 | 保持 Dirty |
| Dirty 提示取消 | 用户选择 Cancel | 不视为错误，不提示失败 | 保持 |

### 13.2 原子状态规则

- Candidate 创建、加载、Runtime Restore 或 Scene Build 任一步失败，不触发 SessionChanged；
- Save/SaveAs 只有文件完成原子写入并通过 reopen 验证后，才更新 FilePath、Manifest 和 SavePoint；
- UI 对话框关闭或取消不改变当前工程；
- 不吞掉格式或校验异常后显示空工程；
- 不自动修复 Domain、Topology 或 Layout 数据；
- 不因加载失败调用 ClearDrawing。

## 14. MainWindow 最小集成

### 14.1 文件菜单

```text
文件
├── 新建...
├── 打开...
├── 保存
├── 另存为...
├── 关闭工程
└── 退出
```

建议快捷键：

- Ctrl+N：新建；
- Ctrl+O：打开；
- Ctrl+S：保存；
- Ctrl+Shift+S：另存为。

### 14.2 窗口标题

```text
无工程：10kV Distribution Drawing
已打开且 clean：{Title} - 10kV Distribution Drawing
已打开且 dirty：*{Title} - 10kV Distribution Drawing
```

### 14.3 SessionChanged 刷新

协调器替换当前 Session 后，MainWindow 只执行：

1. 解绑旧 SelectionChanged；
2. 绑定新 Session.SelectionManager；
3. 显示新 Session.Scene；
4. 绑定新 PropertyInspector；
5. 更新命令启用状态和窗口标题；
6. 无 Session 时清空 UI。

不得调用测试场景构建器补充空 Scene。

## 15. P0-1-B 最小实现范围

### 15.1 建议新增类

```text
src/DistributionDrawing.Desktop/Workspace/
├── ProjectWorkspaceController.cs
├── IProjectWorkspaceDialogs.cs
└── WpfProjectWorkspaceDialogs.cs

src/DistributionDrawing.Desktop/Dialogs/
├── NewProjectDialog.xaml
└── NewProjectDialog.xaml.cs
```

职责：

- ProjectWorkspaceController：单工程生命周期、Candidate Session、Dirty、Save/SaveAs、原子替换；
- IProjectWorkspaceDialogs：路径选择、新工程信息、Dirty 决策和错误显示接口；
- WpfProjectWorkspaceDialogs：OpenFileDialog、SaveFileDialog、MessageBox 和 NewProjectDialog 实现；
- NewProjectDialog：只收集 Title/Description。

### 15.2 建议修改文件

```text
src/DistributionDrawing.Desktop/MainWindow.xaml
src/DistributionDrawing.Desktop/MainWindow.xaml.cs
src/DistributionDrawing.Desktop/ProjectRuntimeSession.cs
src/DistributionDrawing.Infrastructure/Persistence/ProjectService.cs
```

必要修改：

- MainWindow.xaml：文件菜单；
- MainWindow.xaml.cs：协调器接线、SessionChanged、Closing 和重复窗口状态收敛；
- ProjectRuntimeSession.cs：从 ProjectSession 创建、Scene 重建、保存 Session 接受、Runtime→Snapshot 映射；
- ProjectService.cs：SaveProjectAs，以及保存验证后保留当前运行时 Domain 引用的发布逻辑。

原则上不修改：

- Domain；
- ProjectFileFormat/DTO 合同；
- Rendering Symbol；
- Professional 模型；
- implementation-plan 或其他文档。

若实现时发现 ProjectSession 需要一个只更新文件路径/Manifest 的明确构造入口，可以对 `ProjectSession.cs` 做最小修改，但不得增加第二个编辑状态容器。

### 15.3 不纳入 P0-1-B

- Add/Delete Device；
- Connection Editor；
- RingCabinet 配置器；
- Zoom/Pan；
- JPG/Print；
- WorkTicketData；
- 模板；
- 多文档标签；
- 最近文件、自动保存、恢复草稿；
- 清理全部 MainWindow 既有演示代码的无关重构。

## 16. P0-1-B 验收标准

### A. 新建空工程

```text
启动
→ 文件/新建
→ 输入 Title
→ 选择 .kvdrawing 路径
→ 显示空白 DrawingScene
```

检查：

- 文件立即存在；
- 当前 ProjectId/DocumentId 一致；
- Domain、Layout、Professional 为空；
- Scene 无演示元素；
- Save 可用；
- Dirty=false。

### B. 保存文件

```text
打开工程
→ 执行一个现有 Command（可使用 Pole Move 的集成测试 fixture）
→ Dirty=true
→ 保存
→ 生成/更新 .kvdrawing
→ Dirty=false
```

检查：

- 保存的是当前 Runtime Layout，而不是旧 Snapshot；
- CommandStack.SavePoint 更新；
- 保存后 Undo/Redo 仍作用于当前对象图；
- 保存失败不清 Dirty。

### C. 关闭并重新打开

```text
保存
→ 关闭工程
→ 文件/打开
→ 恢复 ProjectRuntimeSession
→ Scene 正常显示
```

检查：

- Domain/Topology/Professional/Layout 稳定 ID 保持；
- 新 Session Selection 为空；
- Undo 历史为空；
- Dirty=false。

### D. 打开失败不丢当前工程

```text
当前工程保持打开
→ 打开损坏/不兼容工程
→ 加载或 Scene Build 失败
```

检查：

- CurrentService、CurrentSession、Scene、Selection、CommandStack 和 FilePath 均保持；
- 不显示空画布替代当前工程；
- 显示明确错误。

### E. Dirty 切换确认

分别验证 New、Open、Close Project 和 Window Close：

- Save：保存成功后继续；
- Discard：继续且不覆盖旧文件；
- Cancel：当前工程完全保留；
- Save 失败：停止切换并保持 Dirty。

### F. Save As

```text
当前工程
→ 另存为新路径
→ 新文件可打开
→ Current FilePath 指向新路径
```

检查：

- ProjectId 和对象稳定 ID 不变；
- 原文件保留；
- 成功后 clean；
- 取消/失败后路径和 Dirty 不变。

## 17. 后续紧邻阶段

P0-1-B 完成后，应先做一次 Windows 实机端到端验证，再进入 Drawing Core 设备生命周期。建议紧邻顺序：

1. P0-1-B：Desktop Project Workspace；
2. P0-1-C：工程会话、Layout 保存和失败原子性的自动化测试/Windows 验收；
3. P0-2：Pole/RingCabinet Add-Move-Delete-Property-Undo-Save-Load 纵向切片；
4. P0-3：Terminal Connection 与 Cable/OverheadLine 纵向切片。

本设计不修改现有正式里程碑编号，等待人工确认后再同步 implementation-plan。

## 18. 本阶段不实现

- 不修改任何代码；
- 不修改 `docs/implementation-plan.md` 或其他设计文档；
- 不实现设备、连接、缩放、输出、模板或 WorkTicketData；
- 不创建 Git Tag；
- 不提交变更。
