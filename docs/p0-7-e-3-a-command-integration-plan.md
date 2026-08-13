# P0-7-E-3-A Template Build Command Integration Plan

> 状态：Command Integration 实施前计划与就绪审查。
>
> 基线：commit `2fa3fa3`（`feat: add template build coordinator`）。
>
> 本阶段只新增本文档，不修改生产代码、测试、项目文件或保存格式。

## 1. 结论与当前就绪状态

当前架构已具备把 `RingCabinetTemplateBuildResult` 接入编辑事务的必要能力，可以进入 P0-7-E-3-B。

关键结论如下：

- 直接复用现有 `AddRingCabinetCommand`，不新增 `TemplateAddRingCabinetCommand`；
- Template Coordinator 每次创建只运行一次，Command 保存首次 Build 产生的同一 `RingCabinet` 与 `RingCabinetLayout`；
- `AddRingCabinetCommand` 已实现 Domain 与 RuntimeLayout 的原子加入、Undo 同步移除及 Redo 原对象恢复；
- `SelectionTargetKind.RingCabinet`、`SelectionReference`、Resolver 与 Inspector 已支持整柜选择；
- `SelectionTransition.ForAdd(before, after)` 已能表达模板 Add 的 Undo/Redo Selection；
- Dirty 继续完全由 `CommandStack` 推进；
- Scene 刷新复用 Desktop Controller 的 `RebuildScene -> Select -> SceneChanged` 链路，每次成功创建只进行一次 Scene rebuild；
- 推荐新增模板专用 Desktop Controller，保持现有手工创建链路不变；
- 不需要修改 Domain、Application Builder、Template Coordinator、Persistence、CommandStack、Selection 基础设施或 Inspector。

当前不存在阻断 E-3-B 的架构问题。唯一需要在实施中严格保持的边界是：只有 `ExecuteCommand(command)` 成功后，才能为执行过的同一个 Command 实例登记 SelectionTransition。

## 2. 当前真实代码位置

本次审查以以下实际文件为准。

### 2.1 Template Build

- `src/DistributionDrawing.Rendering.Wpf/Templates/RingCabinets/Building/RingCabinetTemplateBuildRequest.cs`
- `src/DistributionDrawing.Rendering.Wpf/Templates/RingCabinets/Building/RingCabinetTemplateBuildCoordinator.cs`
- `src/DistributionDrawing.Rendering.Wpf/Templates/RingCabinets/Building/RingCabinetTemplateBuildResult.cs`
- `src/DistributionDrawing.Rendering.Wpf/Templates/RingCabinets/Building/RingCabinetTemplateBuildOutcome.cs`
- `src/DistributionDrawing.Rendering.Wpf/Templates/RingCabinets/Building/RingCabinetTemplateBuildFailure.cs`

`RingCabinetTemplateBuildResult` 已提供同一次 Build 的：

- `Definition`；
- `Cabinet`；
- `Layout`；
- `RequiredCapabilities`。

其构造器已校验 `Cabinet.Id == Layout.CabinetId`。

### 2.2 Command 与 Layout

- `src/DistributionDrawing.Rendering.Wpf/Interaction/Devices/AddRingCabinetCommand.cs`
- `src/DistributionDrawing.Rendering.Wpf/Interaction/Devices/RemoveRingCabinetCommand.cs`
- `src/DistributionDrawing.Rendering.Wpf/Interaction/Devices/DeviceCommandFactory.cs`
- `src/DistributionDrawing.Rendering.Wpf/Interaction/CommandStack.cs`
- `src/DistributionDrawing.Rendering.Wpf/Interaction/ICommand.cs`
- `src/DistributionDrawing.Rendering.Wpf/Layout/RuntimeLayoutDocument.cs`

### 2.3 Desktop Session 与 Selection

- `src/DistributionDrawing.Desktop/ProjectRuntimeSession.cs`
- `src/DistributionDrawing.Desktop/Selection/SelectionTransition.cs`
- `src/DistributionDrawing.Desktop/Selection/SelectionTransitionCoordinator.cs`
- `src/DistributionDrawing.Rendering.Wpf/Interaction/SelectionReference.cs`
- `src/DistributionDrawing.Rendering.Wpf/Interaction/SelectionManager.cs`
- `src/DistributionDrawing.Rendering.Wpf/PropertyInspector/SelectionObjectResolver.cs`
- `src/DistributionDrawing.Rendering.Wpf/PropertyInspector/PropertyProjector.cs`

### 2.4 当前手工创建入口

- `src/DistributionDrawing.Desktop/MainWindow.xaml.cs`
- `src/DistributionDrawing.Desktop/DrawingTools/DrawingToolCoordinator.cs`
- `src/DistributionDrawing.Desktop/Placement/PlacementController.cs`
- `src/DistributionDrawing.Desktop/RingCabinetCreation/RingCabinetCreationDialog.xaml.cs`
- `src/DistributionDrawing.Desktop/RingCabinetCreation/RingCabinetCreationViewModel.cs`
- `src/DistributionDrawing.Rendering.Wpf/Interaction/Devices/RingCabinetCreationConfiguration.cs`
- `src/DistributionDrawing.Rendering.Wpf/Interaction/Devices/RingCabinetCreationFactory.cs`

## 3. 当前手工 RingCabinet 创建链路

当前手工链路为：

```text
MainWindow.OnBeginPlaceRingCabinet
    -> RingCabinetCreationDialog
    -> RingCabinetCreationViewModel.TryCreateConfiguration
    -> RingCabinetCreationConfiguration
    -> DrawingToolCoordinator.BeginRingCabinet
    -> PlacementController.BeginRingCabinet

canvas click
    -> MainWindow.OnDrawingSurfaceMouseLeftButtonDown
    -> DrawingToolCoordinator.HandleClick
    -> PlacementController.Place
    -> DeviceCommandFactory.CreateAddRingCabinet
    -> RingCabinetCreationFactory.Create
    -> RingCabinetLayoutFactory.Create
    -> AddRingCabinetCommand
    -> CommandStack.ExecuteCommand
    -> ProjectRuntimeSession.RebuildScene
    -> SelectionManager.Select(RingCabinet)
    -> PlacementController.SceneChanged
    -> MainWindow.OnDrawingToolVisualChanged
    -> Inspector projection and RenderCurrentScene
```

当前 `PlacementController.Place` 的成功顺序是：

```text
ExecuteCommand
-> clear pending placement state
-> RebuildScene
-> Select new object
-> SceneChanged
```

手工 RingCabinet 创建目前没有登记 `SelectionTransition`，也没有调用 `SelectionTransitions.Prune`。这不是 Template Add 的底层阻断，但意味着模板入口不能照抄手工 Placement 的 Selection 历史缺口。E-3-B 应按 P0-6 已冻结的 Transition 规则完成模板入口；是否随后统一手工 Placement 是独立后续任务。

## 4. AddRingCabinetCommand 复用审查

### 4.1 保存对象

现有构造器接收并保存：

```text
DrawingDocument
RuntimeLayoutDocument
RingCabinet
RingCabinetLayout
```

公开属性 `Cabinet` 与 `Layout` 是构造时传入的同一对象。构造器还校验 Cabinet/Layout ID 一致。

### 4.2 Execute 原子性

`Execute` 的实际顺序为：

```text
确认不存在同 CabinetId 的 RuntimeLayout
-> DrawingDocument.AddDevice(Cabinet)
-> RuntimeLayoutDocument.AddRingCabinet(Layout)
```

如果 Layout 添加失败，Command 会调用 `DrawingDocument.RemoveDevice(Cabinet.Id)` 回滚刚加入的 Domain aggregate，然后重新抛出异常。

`DrawingDocument.AddDevice(RingCabinet)` 在实际写入集合前完成结构和 Stable ID 冲突校验，再一次性注册 Cabinet、Intervals、Switches、Assemblies、Nodes 与 Terminals。因此当前 Add Command 已提供模板创建所需的 Domain/Layout 原子边界。

### 4.3 Undo 与 Redo

Undo 移除同一 Cabinet aggregate 与同一 Layout。Redo 直接调用 `Execute()`，重新添加 Command 保存的原对象，不调用 Factory 或 Builder。

因此：

- CabinetId 保持；
- IntervalId 保持；
- SwitchId 保持；
- TerminalId 保持；
- ElectricalNodeId 保持；
- SwitchAssemblyId 保持；
- Layout 与 Domain identity 保持。

### 4.4 复用结论

`AddRingCabinetCommand` 可以直接复用，不需要任何 Command 行为修改，也不需要模板专用 Command。

## 5. BuildResult 到 Command 的映射

唯一允许的映射是：

```text
RingCabinetTemplateBuildResult.Cabinet
    -> AddRingCabinetCommand.Cabinet

RingCabinetTemplateBuildResult.Layout
    -> AddRingCabinetCommand.Layout
```

禁止：

- 根据 `Definition` 再次创建 RingCabinet；
- 根据 Cabinet 再次创建 Layout；
- 再次运行 Coordinator；
- 再次运行 Domain Builder；
- 再次运行 Layout Builder；
- clone、restore 或重新分配 Stable ID。

为保持现有 `Controller -> DeviceCommandFactory -> Command` 风格，推荐给 `DeviceCommandFactory` 增加一个薄重载：

```text
CreateAddRingCabinet(
    DrawingDocument,
    RuntimeLayoutDocument,
    RingCabinet,
    RingCabinetLayout)
```

该重载只执行 `new AddRingCabinetCommand(...)`，不调用 `RingCabinetCreationFactory` 或 `RingCabinetLayoutFactory`。现有接收 `RingCabinetCreationConfiguration + Position` 的手工创建重载保持不变，并可在未来选择性复用这个对象重载。

此方案使 Desktop Controller 不直接持有 Command 构造细节，同时避免让 `DeviceCommandFactory` 依赖 Template 类型或 BuildResult 类型。

## 6. Build 时机与成功事务顺序

`Coordinator.Build()` 必须发生在 `CommandStack.ExecuteCommand` 之前。推荐完整顺序：

```text
读取 beforeSelection
-> Coordinator.Build(request)
-> 如果失败，返回 typed failure
-> 从 BuildResult 的同一 Cabinet/Layout 创建 AddRingCabinetCommand
-> CommandStack.ExecuteCommand(command)
-> 构造 afterSelection
-> SelectionTransition.ForAdd(beforeSelection, afterSelection)
-> SelectionTransitions.RecordExecuted(command, transition)
-> SelectionTransitions.Prune(CommandStack.History)
-> ProjectRuntimeSession.RebuildScene()
-> SelectionManager.Select(afterSelection)
-> SceneChanged
```

重要顺序约束：

- `beforeSelection` 在任何 Build、Execute 或 Selection 修改前读取；
- Transition 只能在 Execute 成功后登记；
- `RecordExecuted` 使用的必须是实际传入 `ExecuteCommand` 的同一个 Command 实例；
- `Prune` 必须在 `RecordExecuted` 后调用；
- Rebuild 必须发生在 Select 前，使 Resolver 已能解析新 Cabinet；
- SceneChanged 只通知 MainWindow 采用 Session 已重建的 Scene，不再调用 `RefreshDrawingScene`。

## 7. Build 失败边界

如果 `Coordinator.Build(request)` 失败：

- 不创建 `AddRingCabinetCommand`；
- 不调用 `CommandStack.ExecuteCommand`；
- 不进入 History；
- Dirty 不变化；
- 不登记 SelectionTransition；
- 不 Prune；
- 不 Rebuild Scene；
- 不改变 Selection；
- Build 创建的候选对象如果停在 Layout 失败路径，也没有进入 Project，无需 rollback。

Desktop 只返回或上报 Coordinator 的类型化失败，不解析消息来判断类型。

## 8. Command Execute 失败边界

`CommandStack.ExecuteCommand` 先调用 `command.Execute()`，只有成功后才截断 Redo 分支、加入 History、增加 CurrentIndex 和推进 StateId。

因此 Execute 失败时：

- Command 不进入 History；
- CommandStack Dirty 不变化；
- Add Command 在 Layout 添加失败时回滚 Domain；
- BuildResult 仍只是未提交候选；
- 不登记 SelectionTransition；
- 不 Prune；
- 不 Rebuild Scene；
- 不改变 Selection；
- 不重新 Build，也不自动重试。

Controller 中 `RecordExecuted`、`Prune`、Rebuild、Select 必须全部位于 `ExecuteCommand` 返回之后，不能放入 `finally`。

## 9. Selection Before 与 After

### 9.1 Before

创建前直接保存：

```text
SelectionReference? beforeSelection = session.SelectionManager.Selected;
```

允许 before 为：

- null；
- RingCabinet；
- Device；
- RingCabinetInterval；
- PoleAttachment；
- 其他现有 Selection kind。

Transition 应保留完整 `SelectionReference`，不能只保存 ObjectId，也不能在创建后重新解析或猜测 before。

### 9.2 After

当前 Selection 系统已原生支持 `SelectionTargetKind.RingCabinet`。新柜体选择引用应为：

```text
new SelectionReference(
    SelectionTargetKind.RingCabinet,
    buildResult.Cabinet.Id)
```

真实 `SelectionReference` 构造器的 `ParentId` 默认为 null，整柜选择不需要 ParentId。

after 的 `ObjectId` 必须来自实际 BuildResult 的 `Cabinet.Id`，不能来自 TemplateId、Definition 之外的重建对象或 Command 的等价副本。

## 10. SelectionTransition 登记位置

推荐新增 Desktop 层模板创建 Controller，并由它登记 Transition。

原因：它同时拥有：

- 当前 `ProjectRuntimeSession`；
- 当前 Selection；
- Template Build Coordinator；
- 当前 DrawingDocument 与 RuntimeLayoutDocument；
- 实际执行的 Add Command 实例；
- Scene rebuild 与 SceneChanged 边界。

不应登记在：

- `AddRingCabinetCommand`：Command 不应依赖 Desktop Selection；
- Application Domain Builder：Application 不应依赖 Rendering/Desktop；
- RuntimeLayout Builder 或 Template Coordinator：Builder 应保持工程无副作用；
- MainWindow：窗口只负责 UI 输入、错误展示和事件转发，避免继续扩大事务逻辑。

推荐路径：

```text
src/DistributionDrawing.Desktop/RingCabinetTemplateCreation/
    RingCabinetTemplateCreationController.cs
```

## 11. SelectionTransition 生命周期

成功 Execute 后：

```text
SelectionTransition transition =
    SelectionTransition.ForAdd(beforeSelection, afterSelection);

session.SelectionTransitions.RecordExecuted(command, transition);
session.SelectionTransitions.Prune(session.CommandStack.History);
```

`SelectionTransitionCoordinator` 使用 `ReferenceEqualityComparer`，因此：

- Transition key 必须是实际进入 CommandStack 的 Command 对象；
- 禁止创建新的等价 Add Command 作为 key；
- 禁止在 Execute 前登记；
- 禁止为 Redo 再登记一次 Transition。

## 12. Undo / Redo 行为

MainWindow 当前已统一消费 Transition：

```text
Undo:
    读取 History[CurrentIndex - 1] 的 undo selection
    -> CommandStack.Undo()
    -> RefreshDrawingScene()
    -> ApplySelectionTransition(before)

Redo:
    读取 History[CurrentIndex] 的 redo selection
    -> CommandStack.Redo()
    -> RefreshDrawingScene()
    -> ApplySelectionTransition(after)
```

Template Add 的预期行为为：

```text
Execute:
    before selection -> new RingCabinet selection

Undo:
    remove same Cabinet/Layout
    -> restore before selection

Redo:
    re-add same Cabinet/Layout
    -> restore same RingCabinet selection
```

不需要 MainWindow 增加模板专用 Undo/Redo 分支，也不需要 Redo 再调用 Coordinator。

## 13. Dirty 状态

`ProjectRuntimeSession.IsDirty` 当前组合：

```text
PersistenceSession.IsDirty || CommandStack.IsDirty
```

模板 Build 不修改 Project 或 CommandStack，因此 Build 成功但尚未 Execute 时 Dirty 不变化。

只有 `CommandStack.ExecuteCommand` 成功并推进 StateId 后 Dirty 才变化。Undo/Redo 与 SavePoint 继续由现有 CommandStack 规则控制。

禁止 Builder、Coordinator、Desktop Controller 或 MainWindow 直接设置 Dirty。

## 14. Scene Refresh 与 Inspector

### 14.1 推荐刷新链路

模板 Controller 成功后复用 Controller 模式：

```text
session.RebuildScene()
-> session.SelectionManager.Select(afterSelection)
-> SceneChanged
```

MainWindow 的 `OnDrawingToolVisualChanged` 只采用：

- `session.Scene`；
- `session.InspectionSource`；
- Session 的 Resolver source；

然后投影 Inspector 并 `RenderCurrentScene()`。它不会再次调用 `session.RebuildScene()`。

因此成功创建只有一次完整 Scene rebuild。不要在模板 Controller 的 `SceneChanged` 处理器里额外调用 `RefreshDrawingScene()`。

### 14.2 Inspector 支持

`SelectionObjectResolver` 已支持 `SelectionTargetKind.RingCabinet`，并通过 CabinetId 查找 Domain Cabinet 与对应 `RingCabinetLayout`。

`PropertyProjector` 已有整柜投影，显示 Id、DisplayName、CompositionKind、MainBusNodeId、IntervalCount、Layout 与 Rendering 信息。

模板创建成功后选择整柜即可自动显示 Inspector，不需要修改 Inspector、Resolver 或 SelectionReference。

### 14.3 已存在的非阻断刷新特征

`SelectionManager.Select` 会触发 MainWindow 的 `OnSelectionChanged` 并渲染；随后 Controller 的 `SceneChanged` 又会触发一次 UI render。当前 Attachment 与 Placement Controller 采用相同事件风格，虽然可能产生两次轻量 render，但只有一次 Scene rebuild。本阶段不为此重构 MainWindow 事件总线。

## 15. Failure UI 边界

Coordinator 已统一提供：

- `FailureStage`：Coordinator、Domain、Layout；
- `FailureKind`：InvalidTemplate、UnsupportedCapability、DomainCreationFailure、InvalidLayoutInput、MissingRequiredCapability、UnsupportedLayoutRule、LayoutCreationFailure；
- 类型化 UnsupportedCapabilities、MissingCapability、UnsupportedRuleId 与 Cause。

未来 Desktop UI 应按 `FailureKind + FailureStage` 选择标题和用户提示，并使用结构化字段补充上下文。禁止通过 `Message` 或 Exception 文本解析失败类型。

E-3-B 只需让 Controller 将失败 Outcome 返回给 UI；E-3-D 再实现 Template Picker/Dialog 和具体消息展示。

Command Execute 的 `ArgumentException` / `InvalidOperationException` 属于工程提交失败，与 Template Build Failure 分开处理。不要把 Command 异常伪装成 Coordinator Failure。

## 16. 现有手工创建兼容性

现有手工链路保持不变：

```text
RingCabinetCreationDialog
-> RingCabinetCreationConfiguration
-> PlacementController
-> DeviceCommandFactory existing overload
```

模板链路新增独立入口：

```text
Template UI/request
-> RingCabinetTemplateCreationController
-> RingCabinetTemplateBuildCoordinator
-> DeviceCommandFactory object overload
-> AddRingCabinetCommand
```

两个入口共享 Add Command 和 Session 基础设施，但不在 E-3 阶段统一配置模型、ViewModel、Factory 或 Placement 状态机。

手工 Placement 当前缺少 SelectionTransition 的问题可作为独立一致性改进，不应与 Template Add 首次接入绑定提交。

## 17. Desktop Controller 推荐设计

推荐新增 `RingCabinetTemplateCreationController`，职责为：

1. 取得当前 Session；
2. 保存 `beforeSelection`；
3. 调用 `RingCabinetTemplateBuildCoordinator.Build(request)`；
4. Build 失败时原样返回 typed outcome；
5. 从 BuildResult 的同一 Cabinet/Layout 创建 Add Command；
6. 调用 `CommandStack.ExecuteCommand(command)`；
7. 创建整柜 after selection；
8. 为同一 Command 登记 `SelectionTransition.ForAdd`；
9. Prune active History；
10. Rebuild Scene；
11. Select 新 Cabinet；
12. 触发 `SceneChanged`。

建议构造依赖：

```text
Func<ProjectRuntimeSession?> getSession
RingCabinetTemplateBuildCoordinator (optional injection)
DeviceCommandFactory (optional injection)
```

Controller 不保存 BuildResult，不持有 Template Library，不读取 UI 控件，也不处理 MessageBox。

### 17.1 是否复用 PlacementController

不建议把模板逻辑加入现有 `PlacementController`：

- PlacementController 当前保存 `RingCabinetCreationConfiguration` 作为两阶段鼠标放置状态；
- Template Request 已包含 Position，且 Build/Failure 模型不同；
- 混入模板会让 PlacementController 同时管理手工配置、模板 Build Outcome 与 UI failure mapping；
- Existing manual creation 必须保持不变。

如果 E-3-D 采用“先选模板、再点击画布”的交互，DrawingToolCoordinator 可以只负责把点击得到的 Position 转交模板专用 Controller，但 Build/Command/Transition 事务仍留在模板 Controller。

## 18. Full BuildResult 生命周期

`RingCabinetTemplateBuildResult` 是一次创建操作中的短生命周期候选：

```text
Coordinator.Build
-> Controller creates AddRingCabinetCommand
-> Command stores Cabinet/Layout
-> Execute succeeds
-> Controller no longer needs to retain BuildResult
```

Command 已持有 Undo/Redo 所需对象。Desktop 不需要把 BuildResult 保存在 Session、History metadata、SelectionTransition 或 UI 状态中。

Redo 只依赖 Command 保存的 Cabinet/Layout，不依赖 BuildResult、Template、Request 或 Coordinator。

## 19. Add Command 与 Layout 原子性

当前原子性结论：通过。

- Execute 在 Layout 已存在时先失败，不修改 Domain；
- Domain Add 完成后若 Layout Add 失败，会移除刚加入的 Cabinet aggregate；
- Undo 在移除前确认 Layout 存在，然后同步移除 Domain 与 Layout；
- Redo 复用 Execute；
- Cabinet/Layout ID 在 BuildResult 和 Command 构造器两处均有一致性保护。

在当前单线程 Desktop 编辑模型下，没有新增事务抽象的必要。

非阻断风险：Undo 在预检查后先移除 Domain、再移除 Layout；如果未来出现并发修改，第二步理论上可能失败并留下不一致。当前 UI/CommandStack 是串行模型，不构成本阶段阻断，也不应在 Template Integration 中重构 Command。

## 20. Transition 登记失败风险

如果 `ExecuteCommand` 已成功，但 `SelectionTransitions.RecordExecuted` 抛出异常，则 Command 已进入 History 且 Dirty 已推进。

沿用 P0-6 原则，这属于编辑器基础设施不变量失败：

- 不重新 Execute；
- 不自动 Undo；
- 不静默忽略；
- 让异常进入现有命令错误边界或顶层诊断；
- 不继续 Prune、Rebuild 或 Select。

当前 `RecordExecuted` 只有 null 参数或同一 Command 重复登记时失败。正确使用首次执行的实际 Command 实例即可避免正常路径触发。

## 21. Prune

模板 Add 成功登记 Transition 后必须调用：

```text
session.SelectionTransitions.Prune(session.CommandStack.History)
```

它清理：

- 新 Execute 截断的 Redo 分支对应 Transition；
- CommandStack 容量裁剪后不再处于 active History 的 Transition。

不得在 Build 失败、Command 创建失败或 Execute 失败路径调用 Prune。无需修改 CommandStack 或 SelectionTransitionCoordinator。

## 22. 是否需要新的 Command

结论：不需要。

模板专用 `TemplateAddRingCabinetCommand` 会重复现有 Command 的全部职责，并带来 Redo 误调用 Builder 的风险。

只有未来 Template BuildResult 包含现有 Add Command 无法原子提交的新工程对象时，才重新评估 Command 边界。当前 Full BuildResult 的工程提交事实只有 RingCabinet 与 RingCabinetLayout，完全匹配现有 Command。

## 23. 是否需要通用 Execution Coordinator

结论：本阶段不需要。

虽然多个 Desktop 操作都包含 Execute、Transition、Prune 与 Refresh，但当前目标只接入 Template Add。立即引入通用 `CommandExecutionCoordinator` 会扩大：

- 所有现有 Controller；
- Command 失败语义；
- Refresh 策略；
- Selection transition 类型；
- MainWindow 事件协调。

推荐继续使用模板专用 Desktop Controller 的最小事务。等手工 Placement、Remove、Property Editing 等入口确实要统一且拥有共同测试后，再单独设计通用执行协调器。

## 24. 测试计划

### 24.1 Controller 级必须覆盖

1. Build 成功后创建并执行 Add Command；
2. Build 失败时不创建 Command、不进入 History、不改变 Selection/Dirty/Scene；
3. Execute 失败时不登记 Transition、不 Prune、不刷新、不改 Selection；
4. Execute 成功后 Selection 指向 BuildResult CabinetId；
5. Transition key 与 `CommandStack.History` 中对象引用相同；
6. Undo 后恢复 before selection；
7. Redo 后恢复同一 Cabinet selection；
8. Undo/Redo 前后 Cabinet、Interval、Switch、Terminal、Node、Assembly ID 保持；
9. Dirty 只由 CommandStack 成功 Execute 推进；
10. 成功创建只调用一次 `ProjectRuntimeSession.RebuildScene` 语义路径；
11. PT、DTU、Unknown LayoutRule 失败不进入 History；
12. null before selection 的 Undo 返回 null，Redo 返回新 Cabinet。

### 24.2 Command 级回归

对现有 `AddRingCabinetCommand` 补充或确认：

- Execute 同时加入 Domain/Layout；
- Layout 冲突时 Domain 回滚；
- Undo 同时移除；
- Redo 恢复同一对象引用与 Stable ID。

这些测试可放在现有 `tests/DistributionDrawing.Rendering.Wpf.Tests/`，不需要 Desktop/WPF UI 自动化。

### 24.3 Desktop 测试基础设施判断

当前仓库没有 `DistributionDrawing.Desktop.Tests`。E-3-B 不应为了 Controller 建立窗口驱动或 UI automation 框架。

实现时优先把 Controller 保持为无 WPF Control 的普通类，并评估新增一个最小 `net10.0-windows` xUnit Desktop test project。若引用 WinExe/WPF 项目的成本明显超出本切片，则：

- 先用 Rendering.Wpf.Tests 覆盖 Add Command 原子性与 Stable ID；
- 用静态审查确认 Controller 顺序；
- 把 Desktop Controller 自动化测试项目作为 E-3-C 的独立验证切片。

不得为验证调用次数而提前引入大型 mock 框架或通用 Execution Coordinator。

## 25. Failure-path 可测试性

Build 的 PT、DTU、Unknown Rule、两间隔等失败可直接使用真实 Coordinator 验证，不需要 Builder interface。

Command Execute 失败可通过以下受控状态验证：

- 使用已构建 Result 创建 Add Command；
- 预先在目标 RuntimeLayout 或 DrawingDocument 注册冲突 ID；
- 执行并确认 CommandStack/SelectionTransition/Scene 状态未推进。

如果完整 Controller 方法不便在 Build 与 Execute 之间注入冲突，不应为单个测试公开半事务 API。可在 Command 级验证 rollback，在 Controller 级通过一个最小命令创建依赖或测试专用 session arrangement 验证后续语句不会执行。具体测试 seam 应在 E-3-B 以最小代码量决定。

## 26. 预计修改文件

### 26.1 E-3-B：Command Execution 核心

新增：

- `src/DistributionDrawing.Desktop/RingCabinetTemplateCreation/RingCabinetTemplateCreationController.cs`

修改：

- `src/DistributionDrawing.Rendering.Wpf/Interaction/Devices/DeviceCommandFactory.cs`
  - 增加接收现有 `RingCabinet + RingCabinetLayout` 的薄 Add overload；
  - 不修改现有手工创建 overload 的行为。

可能新增或修改测试：

- `tests/DistributionDrawing.Rendering.Wpf.Tests/AddRingCabinetCommandTests.cs`
- `tests/DistributionDrawing.Desktop.Tests/DistributionDrawing.Desktop.Tests.csproj`，仅在无需 UI automation 的最小引用可行时；
- `tests/DistributionDrawing.Desktop.Tests/RingCabinetTemplateCreationControllerTests.cs`，若采用上述最小测试项目。

### 26.2 E-3-C：Selection/Undo/Redo 验证

主要是测试与必要的小范围 Controller 调整，原则上不修改：

- `CommandStack.cs`；
- `SelectionTransition.cs`；
- `SelectionTransitionCoordinator.cs`；
- `ProjectRuntimeSession.cs`；
- MainWindow Undo/Redo。

### 26.3 E-3-D：Desktop Entry / Minimal UI

届时根据 Template Library/UI 设计，可能新增或修改：

- `src/DistributionDrawing.Desktop/MainWindow.xaml`
- `src/DistributionDrawing.Desktop/MainWindow.xaml.cs`
- `src/DistributionDrawing.Desktop/DrawingTools/DrawingToolCoordinator.cs`，仅当采用画布点击确定 Position；
- `src/DistributionDrawing.Desktop/RingCabinetTemplateCreation/` 下的 Dialog/ViewModel 或入口类型。

E-3-A/B 不应提前实现 Template Library 或复杂 UI。

## 27. 明确不修改文件与边界

E-3-B 原则上不修改：

- `src/DistributionDrawing.Domain/`
- `src/DistributionDrawing.Application/`
- `src/DistributionDrawing.Infrastructure/`
- Persistence DTO、FormatVersion、Migration；
- Template Runtime Model；
- Template Domain/Layout Builder 与 Thin Coordinator；
- `AddRingCabinetCommand.cs`，因为当前行为已满足；
- `CommandStack.cs`；
- `SelectionReference.cs`；
- `SelectionManager.cs`；
- `SelectionTransition.cs`；
- `SelectionTransitionCoordinator.cs`；
- `ProjectRuntimeSession.cs`；
- Inspector、Resolver、Rendering geometry。

## 28. E-3 实施拆分

推荐拆分如下：

### E-3-A：当前 Integration Plan

- 冻结 Command、Selection、Dirty、Refresh 与失败边界。

### E-3-B：Template Add Command Execution

- 新增模板专用 Desktop Controller；
- 增加 DeviceCommandFactory 对象 overload；
- Build 一次；
- 创建并执行现有 Add Command；
- 成功后登记 ForAdd、Prune、Rebuild、Select、SceneChanged；
- 不接 UI。

### E-3-C：SelectionTransition 与 Undo/Redo 验证

- 覆盖 before/after、实际 Command key、Undo/Redo Selection；
- 验证 Stable ID、Dirty 和失败路径；
- 根据可行性增加最小 Desktop controller test project。

### E-3-D：Desktop Template Entry / Minimal UI

- 接入模板选择和 DisplayName；
- 决定 Position 输入方式；
- 调用 E-3-B Controller；
- 按 FailureKind/Stage 显示错误；
- 不实现 JSON、厂家模板或复杂参数编辑器。

此拆分避免在一个提交中同时引入事务、完整 Template Library 和复杂 WPF UI。

## 29. 阻断问题检查

| 检查项 | 结论 | 依据 |
| --- | --- | --- |
| AddRingCabinetCommand 无法复用 | 无阻断 | 已保存 Cabinet/Layout，并在 Redo 复用同一对象 |
| Layout 原子性不足 | 无阻断 | Layout Add 失败时回滚 Domain；Undo 同步移除 |
| RingCabinet Selection 不支持 | 无阻断 | Kind、Scene、Resolver、Inspector 均已支持 |
| SelectionTransition 无法表达 Add | 无阻断 | `ForAdd(before, after)` 完整满足 |
| Undo/Redo 不兼容 | 无阻断 | MainWindow 已按实际 History Command 统一恢复 Transition |
| Dirty 不兼容 | 无阻断 | CommandStack 在成功 Execute 后推进 StateId |
| Scene Refresh 不兼容 | 无阻断 | Session Rebuild + SceneChanged 链路可直接复用 |
| Build Failure 会污染工程 | 无阻断 | Builder/Coordinator 只创建未提交候选 |
| Execute Failure 会进入 History | 无阻断 | CommandStack 仅在 Execute 返回后写 History |

结论：可以进入 E-3-B。

## 30. 非阻断风险与后续建议

1. 手工 Placement 尚未登记 SelectionTransition，导致手工 Add 与模板 Add 的 Undo Selection 体验可能暂时不同；应单独修复，不与本次模板事务混合。
2. Controller 的 `Select` 与 `SceneChanged` 可能触发两次 UI render，但只发生一次 Scene rebuild；后续可统一通知策略。
3. Transition 登记发生在 Command 成功之后，登记异常无法自然 rollback Command；沿用基础设施错误策略。
4. Desktop 当前没有专用测试项目；应优先保持 Controller 为普通非 UI 类，再决定是否增加最小测试项目。
5. DeviceCommandFactory 的对象 overload 必须保持无 Template 依赖，避免 Rendering interaction factory 知道 BuildResult。
6. 不应在 E-3-B 引入通用 CommandExecutionCoordinator；待多个事务入口统一时再设计。

## 31. E-3-B 成功标准

E-3-B 实现完成时必须满足：

- Coordinator 每次创建只 Build 一次；
- Command 使用 BuildResult 中原始 Cabinet/Layout；
- CommandStack 中的对象与 Transition key 引用相同；
- Build/Execute 失败不改变 History、Dirty、Selection 或 Scene；
- 成功后顺序为 Record、Prune、Rebuild、Select、SceneChanged；
- Undo 恢复 before selection；
- Redo 恢复同一 Cabinet selection；
- Redo 不调用 Coordinator；
- Stable ID 全部保持；
- 只进行一次 Scene rebuild；
- 不修改 Domain、Persistence、CommandStack、Selection 基础设施或现有手工创建行为。
