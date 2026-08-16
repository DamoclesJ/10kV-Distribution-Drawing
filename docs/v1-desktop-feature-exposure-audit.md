# V1 Desktop Feature Exposure Audit

> Audit date: 2026-08-16
>
> Audited baseline: `9b856be` (`docs: audit v1 product gaps`)
>
> Scope: Desktop feature reachability audit. No production code or test changes.

## 1. Current Desktop Entry Points

This audit distinguishes a production capability from a user-reachable product workflow. A type, command, renderer, or test does not make a feature available in the EXE unless a discoverable Desktop path reaches it.

The classifications used throughout this document are:

- **A. Complete**: lower layers, Desktop entry, and the intended operation loop are present.
- **B. Hidden Capability**: production capability exists below Desktop, but no user entry reaches it.
- **C. Partial**: an entry or controller exists, but the intended workflow cannot be completed reliably.
- **D. Rendering Only**: an object can be shown, but the user cannot create, modify, or operate it through Desktop.
- **E. Not Implemented**: the production capability itself does not exist.

### 1.1 Verified entry types

| Entry type | Current implementation | Reachable capabilities |
| --- | --- | --- |
| Menu | Four top-level menus in `MainWindow.xaml` | Project lifecycle, basic device placement, CableTermination attachment, OverheadLine, delete, Undo/Redo, professional data, viewport actions, three development/demo actions |
| Toolbar | No WPF `ToolBar` exists | None |
| Left toolbox | Three ordinary Buttons, not a toolbar | Select, create RingCabinet, create Pole |
| Context menu | No `ContextMenu` was found | None |
| Inspector | Read-only property projection plus object-specific editor panels | Interval type/configuration, Pole number, attachment position/layout, CableTermination name, GroundingPoint and WorkScope editing |
| Dialog | New project, RingCabinet creation, CableTermination creation, file Open/Save dialogs | Metadata and creation input for those workflows only |
| Canvas direct interaction | Mouse hit test, selection, supported device drag, terminal picking, line preview, zoom and pan | Selection, Pole/RingCabinet move, OverheadLine endpoints, GroundingPoint/WorkScope terminal picks, wheel zoom, middle-button pan |
| Keyboard shortcut | Handled in `MainWindow.OnWindowKeyDown` | Ctrl+S, Ctrl+Z, Ctrl+Y, Delete, Esc |

There are no XAML `CommandBindings`, `InputBindings`, or `KeyBindings`. Keyboard behavior is implemented by the Window `KeyDown` handler. Ctrl+N and Ctrl+O are not implemented shortcuts even though New and Open exist in the menu.

### 1.2 Actual high-level call chains

Project lifecycle:

```text
Menu / ViewModel ICommand
  -> MainWindow delegate
  -> ProjectWorkspaceController
  -> project persistence session / dialogs
  -> replace or save ProjectRuntimeSession
  -> rebuild Scene
```

Pole and RingCabinet placement:

```text
Menu or left toolbox
  -> MainWindow
  -> DrawingToolCoordinator
  -> PlacementController
  -> DeviceCommandFactory
  -> Rendering ICommand + CommandStack
  -> DrawingDocument + RuntimeLayoutDocument
  -> ProjectRuntimeSession.RebuildScene()
```

OverheadLine connection:

```text
Device menu
  -> DrawingToolCoordinator.BeginOverheadLine()
  -> OverheadLineConnectionController
  -> TerminalAnchorIndex two-endpoint picking and preview
  -> OverheadLineCommandFactory
  -> AddOverheadLineCommand + CommandStack
  -> DrawingDocument Connection/OverheadLine + layout
  -> rebuild Scene and select Connection
```

Selection and property display:

```text
Canvas click
  -> DrawingScene.SelectionHitTestIndex
  -> SelectionReference with stable ObjectId
  -> SelectionManager
  -> SelectionObjectResolver
  -> PropertyProjector
  -> PropertyInspectorViewModel
```

## 2. Menu Structure

The following table contains only menu items present in `MainWindow.xaml`.

| Menu | Item | Actual call | Status | Problem |
| --- | --- | --- | --- | --- |
| 文件 | 新建工程 | `NewProjectCommand` -> `ProjectWorkspaceController.NewProject` | A. Complete | None found in the normal workflow |
| 文件 | 打开工程 | `OpenProjectCommand` -> `ProjectWorkspaceController.OpenProject` | A. Complete | Internal exception text can reach the user in English |
| 文件 | 保存工程 | `SaveProjectCommand` -> `ProjectWorkspaceController.SaveProject` | A. Complete | Internal exception text can reach the user in English |
| 文件 | 另存为 | `OnSaveProjectAs` -> `ProjectWorkspaceController.SaveProjectAs` | A. Complete | No keyboard shortcut; not a blocker |
| 文件 | 关闭工程 | `OnCloseProject` -> `ProjectWorkspaceController.CloseCurrentProject` | A. Complete | This is project close, not application Exit |
| 设备 | 添加杆塔 | `OnBeginPlacePole` -> coordinator/placement/factory/command | A. Complete | Creates only a plain Pole |
| 设备 | 添加环网柜 | creation dialog -> coordinator/placement/factory/command | C. Partial | Dialog exposes PT enum, but creation factory rejects PT; DTU absent |
| 设备 | 添加电缆终端 | dialog -> `CableTerminationAttachmentController` -> command | A. Complete | Requires a selected Pole; does not establish a Cable connection |
| 设备 | 绘制架空线 | coordinator -> `OverheadLineConnectionController` | A. Complete | Error text from lower layers remains English in some cases |
| 设备 | 取消放置 | `DrawingToolCoordinator.Cancel` | A. Complete | Cancels placement/OverheadLine only; professional pick state is handled separately |
| 设备 | 删除所选对象 | `DeleteCommand` -> `DrawingToolCoordinator.RemoveSelected` | C. Partial | Supports RingCabinet, Pole, CableTermination attachment and OverheadLine, not Cable/Joint or pole switch attachment |
| 编辑 | 撤销 | `UndoCommand` -> current `CommandStack.Undo` | A. Complete | Applies only to operations that entered this CommandStack |
| 编辑 | 重做 | `RedoCommand` -> current `CommandStack.Redo` | A. Complete | Applies only to operations that entered this CommandStack |
| 编辑 | 添加工作地线 | MainWindow terminal pick -> `ProfessionalCommandFactory` -> CommandStack | A. Complete | Uses Inspector panel to finish input |
| 编辑 | 添加工作范围 | MainWindow two-boundary terminal pick -> `ProfessionalCommandFactory` -> CommandStack | A. Complete | Side values are manually entered as designed |
| 视图 | 放大 | `CanvasViewportController.ZoomIn` | A. Complete | View-only state |
| 视图 | 缩小 | `CanvasViewportController.ZoomOut` | A. Complete | View-only state |
| 视图 | 适合图形 | `CanvasViewportController.Fit` | A. Complete | View-only state |
| 视图 | 显示网格 | toggles `DrawingVisualHost.ShowGrid` state | A. Complete | View-only state |
| 视图 | 绘制测试内容 | constructs an in-memory demo Scene directly | C. Partial | Development/demo entry; it is not the current project creation workflow |
| 视图 | 绘制环网柜组合场景 | uses demo factory and `ShowScene` | C. Partial | Development/demo entry; it is not an editable persisted project workflow |
| 视图 | 清空绘图区 | clears the displayed Scene and interaction state | C. Partial | Does not mean New/Reset project and can be misunderstood as a document operation |

There is no explicit Exit item, Reset Project item, JPG Export item, or Print item.

## 3. Toolbar Structure

No WPF toolbar exists. The left panel labeled `工具箱` exposes only:

| Button | Call | Classification | Limitation |
| --- | --- | --- | --- |
| 选择 | `Toolbox.SelectModeCommand` -> cancel active drawing tool | A. Complete | Selection is also the implicit default mode |
| 创建环网柜 | opens RingCabinet dialog, then enters placement | C. Partial | PT creation is not completed; DTU absent |
| 创建杆塔 | enters Pole placement | A. Complete | No pole attachment or pole switch choice |

High-frequency V1 operations absent from this toolbox include OverheadLine, Cable, CableTermination, switch operation, GroundingPoint, and WorkScope. Some are still available through menus; Cable and switch operation are unavailable anywhere.

## 4. Context Menu / Canvas Actions

### 4.1 Context menu

There is no context menu in the current Desktop implementation. Object-specific operations therefore depend on the Inspector or global menu.

### 4.2 Canvas actions

| Action | Current behavior | Classification |
| --- | --- | --- |
| Left click in Select mode | Hit test and select the highest-priority `SelectionReference`; may begin drag for supported device kinds | C. Partial |
| Left drag | `DeviceDragController` moves Pole/RingCabinet layouts; attachment layout has separate Inspector editing | C. Partial |
| Left click in Pole/RingCabinet mode | Places through `PlacementController` | A. Complete for supported objects |
| Left click in OverheadLine mode | Picks first/second legal terminal via `TerminalAnchorIndex` | A. Complete |
| Mouse move in OverheadLine mode | Renders transient preview line | A. Complete |
| Professional terminal picking | Picks GroundingPoint terminal or WorkScope boundaries | A. Complete |
| Mouse wheel | Cursor-anchored zoom through `CanvasViewportController` | A. Complete |
| Middle drag | Pan | A. Complete, but not discoverable in UI |
| Switch primitive click | Selects the concrete SwitchDevice only | B. Hidden Capability for state operation |
| Cable/Joint click | Hit-test reference exists, but Desktop object resolution fails | C. Partial |

The Canvas does not directly alter Domain switch state, create Cable connections, split Cable, create Joint, or create pole switch attachments.

## 5. Inspector Capabilities

### 5.1 Read-only projection

The WPF Inspector can resolve and display:

- RingCabinet identity, name, composition, bus and interval count.
- RingCabinetInterval identity, Sequence, BayIndex, `BusinessNumber`, type, grounding structure, external terminal and internal switch business numbers.
- Concrete RingCabinet SwitchDevice identity, name, kind, current `SwitchState`, dispatch number and terminal IDs.
- Pole identity, number, name, type and overhead anchor count.
- PoleAttachment, attached device, CableTermination terminal IDs and layout.
- OverheadLine and its Connection/layout.
- Terminal.
- GroundingPoint and WorkScope.

Business numbers are read from `RingCabinetInterval.BusinessNumber` and `GetSwitchBusinessNumber(Guid)`. Sequence, BayIndex, BusinessNumber, and stable internal IDs are read-only, which is the intended contract.

### 5.2 Editable panels

The Inspector exposes these actual edits:

- RingCabinetInterval type and IntegratedFeeder `GroundingStructureKind`.
- Pole number.
- PoleAttachment offset.
- PoleAttachment width, height, and label offset.
- CableTermination display name.
- GroundingPoint location, number, note, and deletion.
- WorkScope description, GroundingPoint references, creation, and deletion.

Interval changes use:

```text
Inspector Apply
  -> PropertyEditor.TryChangeIntervalType
  -> PropertyCommandFactory.TryCreateIntervalTypeChange
  -> ChangeIntervalTypeCommand
  -> CommandStack
  -> RingCabinet.ChangeIntervalType / state restore
  -> Scene rebuild
```

This is a real Desktop entry, but the workflow remains **C. Partial** because interval structure replacement and the existing interval layout can diverge, especially when PT-specific layout data is required.

### 5.3 Missing Inspector reachability

- SwitchState is displayed but cannot be changed.
- CableSegment and IntermediateTerminal have hit-test target kinds, but `SelectionObjectResolver` has no cases for them.
- Consequently Cable and Joint do not reach `PropertyProjector` and have no current Inspector panels.
- CableType and Length are supported by a lower-level generic edit command, but are not wired through the current Desktop `PropertyCommandFactory`/Inspector.
- RingCabinet name and SwitchDevice display-name edits exist in lower-level property code/tests but are not exposed in the current Inspector.

## 6. Capability -> UI Entry Matrix

Each row has exactly one exposure classification.

### 6.1 Project and editing

| Capability | Lower-layer fact | Current Desktop entry | Classification |
| --- | --- | --- | --- |
| New | Workspace/session creation | File menu | A. Complete |
| Open | V6 load and candidate-session replacement | File menu/dialog | A. Complete |
| Save | V6 persistence and save-point tracking | File menu, Ctrl+S | A. Complete |
| Save As | Workspace save-as | File menu/dialog | A. Complete |
| Close project | Dirty confirmation and session close | File menu | A. Complete |
| Reset project | No distinct production operation | None; `清空绘图区` only clears the shown Scene | E. Not Implemented |
| Selection | Stable scene hit references | Canvas click | C. Partial |
| Move | Pole/RingCabinet layout drag; attachment layout commands | Canvas drag and Inspector | C. Partial |
| Delete | Commands for a limited set | Menu and Delete key | C. Partial |
| Undo | Rendering `CommandStack` | Edit menu and Ctrl+Z | A. Complete |
| Redo | Rendering `CommandStack` | Edit menu and Ctrl+Y | A. Complete |
| Property edit | Several command paths exist | Object-specific Inspector panels | C. Partial |

### 6.2 Device creation and cabinet configuration

| Capability | Lower-layer fact | Current Desktop entry | Classification |
| --- | --- | --- | --- |
| RingCabinet | Domain/factory/rendering/persistence exist | Dialog + placement | C. Partial |
| Pole | Domain, layout and command exist | Menu/toolbox + placement | A. Complete |
| PoleAttachment | Domain and mixed rendering exist | CableTermination-only entry | C. Partial |
| CableTermination | Full Pole attachment creation command exists | Select Pole -> menu/dialog | A. Complete |
| IntermediateTerminal / Joint | Factory, command, split runtime and rendering exist | None | B. Hidden Capability |
| Pole switch attachment | `PoleCreationFactory.CreateWithAttachments` and renderer exist | None | B. Hidden Capability |
| LoadSwitch interval | Domain, numbering, rendering and creation exist | RingCabinet dialog / Inspector | C. Partial |
| IntegratedFeeder interval | Domain, three structures, rendering and creation exist | RingCabinet dialog / Inspector | C. Partial |
| PT interval | Domain, numbering and rendering exist | Enum is visible; initial creation fails; type change is unsafe at layout boundary | C. Partial |
| Interval type change | Domain snapshot/restore command exists | Interval Inspector | C. Partial |
| BusinessNumber | Domain API and labels exist | Scene and read-only Inspector | A. Complete |
| DTU | Capability metadata only; production builders reject it | None | E. Not Implemented |

### 6.3 Switches and interlock

| Capability | Lower-layer fact | Current Desktop entry | Classification |
| --- | --- | --- | --- |
| LoadSwitch state | Concrete SwitchDevice, hit target, state command | Select/read only | B. Hidden Capability |
| IsolationSwitch state | Concrete SwitchDevice, hit target, state command | Select/read only | B. Hidden Capability |
| CircuitBreaker state | Concrete SwitchDevice, hit target, state command | Select/read only | B. Hidden Capability |
| GroundSwitch state | Concrete SwitchDevice, hit target, state command | Select/read only | B. Hidden Capability |
| `ChangeSwitchStateCommand` | Execute/Undo/Redo in Application | No adapter/controller/menu/context/Inspector action | B. Hidden Capability |
| Interlock refusal | Domain rejects invalid switch transitions | No user-triggerable switch path | B. Hidden Capability |

### 6.4 Lines and Cable workflow

| Capability | Lower-layer fact | Current Desktop entry | Classification |
| --- | --- | --- | --- |
| OverheadLine creation | Complete terminal-based command and layout path | Menu, two picks, preview | A. Complete |
| CableSegment display | Domain, V6 and main Scene rendering exist | Loaded/test-built objects only | D. Rendering Only |
| Cable create command | `CableSegmentCreationFactory` + `CreateCableSegmentCommand` exist | None | B. Hidden Capability |
| First legal Cable terminal pick | Terminal policy/anchors exist | No Cable tool state | E. Not Implemented |
| Second legal Cable terminal pick | Terminal policy/anchors exist | No Cable tool state | E. Not Implemented |
| Cable preview | No Cable Desktop controller | None | E. Not Implemented |
| CableType | Domain, persistence, label and generic edit runtime exist | No dialog/Inspector | B. Hidden Capability |
| Cable Length | Domain, persistence, label and generic edit runtime exist | No dialog/Inspector | B. Hidden Capability |
| Cable endpoints | Stable Terminal IDs and Connection exist | No Desktop endpoint workflow/resolver | B. Hidden Capability |
| IntermediateTerminal connection | Multiple-Cable terminal semantics and split runtime exist | No create/split UI | B. Hidden Capability |
| CableTermination Cable-side connection | Legal Cable terminal exists | No Cable tool | B. Hidden Capability |
| Cable split / Joint creation | `SplitCableCommand` exists | None | B. Hidden Capability |
| Cable reconnect | `ReconnectCableCommand` exists | None | B. Hidden Capability |
| Cable select/inspect | Scene hit entry exists | Resolver/projector missing | C. Partial |
| Joint select/inspect | Scene hit entry exists | Resolver/projector missing | C. Partial |

### 6.5 Professional data, view and output

| Capability | Lower-layer fact | Current Desktop entry | Classification |
| --- | --- | --- | --- |
| WorkScope | Domain, commands, rendering, persistence | Edit menu + Canvas terminal picks + Inspector | A. Complete |
| BoundaryPoint | Embedded WorkScope value, not a standalone object | WorkScope creation flow | A. Complete |
| GroundingPoint | Domain, commands, rendering, persistence | Edit menu + Canvas terminal pick + Inspector | A. Complete |
| Zoom | View transform | Menu and wheel | A. Complete |
| Pan | View transform | Middle-button drag | A. Complete |
| Grid | Drawing host display state | View menu | A. Complete |
| Fit | View transform | View menu | A. Complete |
| Reset View | No explicit operation | None | E. Not Implemented |
| JPG export | Required by `requirements.md`, no production path found | None | E. Not Implemented |
| Windows print | Required by `requirements.md`, no production path found | None | E. Not Implemented |

## 7. Hidden Capabilities

The most important lower-level capabilities currently unavailable to EXE users are:

1. `ChangeSwitchStateCommand`, Domain state changes and interlock validation.
2. Cable create, split, reconnect and IntermediateTerminal creation commands.
3. CableType and Cable Length editing.
4. Pole switch attachment construction and rendering.
5. RingCabinet name and SwitchDevice display-name lower-level edits.
6. Application Inspector support for CableSegment/IntermediateTerminal, which is not used by the current WPF resolver/projector path.

These are not complete product features. They reduce implementation effort, but require Desktop coordination and current CommandStack integration before they are usable.

### 7.1 Direct answers to the exposure questions

1. **Is `ChangeSwitchStateCommand` hidden?** Yes. It is production Application code with Execute/Undo/Redo and Domain interlock integration, but no Desktop caller or current `ICommand` adapter exists.
2. **How far does SwitchDevice hit testing go?** To the concrete switch. The hit entry is `SelectionTargetKind.Device` with `ObjectId = SwitchDeviceId` and `ParentId = IntervalId`.
3. **Can the EXE hit a switch rather than only an Interval/Cabinet?** Yes. `SelectionObjectResolver.ResolveDevice` resolves that pair to the concrete `SwitchDevice`. The click currently selects and inspects it; it does not operate it.
4. **What is CableSegment's current product state?** **D. Rendering Only** overall. Domain, persistence and Scene rendering exist, while creation and editing are not user-reachable. Its individual create/edit commands are **B. Hidden Capability**.
5. **Why can OverheadLine connect but Cable cannot?** OverheadLine has a Desktop controller, coordinator mode, legal-terminal picks, preview, current CommandStack commands, selection and rebuild. Cable has none of those Desktop layers.
6. **Where is Interval Type Change exposed?** Select a RingCabinetInterval, then use the Inspector's interval type/grounding controls and `应用间隔配置` button.
7. **Does IntermediateTerminal have a creation entry?** No. Its factory/command and Cable split runtime are hidden below Desktop.
8. **How is CableTermination created?** Select a Pole, choose `设备 -> 添加电缆终端`, complete the dialog, then `CableTerminationAttachmentController` executes an add command.
9. **How is a pole switch attachment created?** It is not creatable in Desktop. `PoleCreationFactory.CreateWithAttachments` and rendering support exist only below the UI.
10. **Are Undo/Redo visible?** Yes, in the Edit menu and through Ctrl+Z/Ctrl+Y.
11. **Is Delete visible?** Yes, in the Device menu and through Delete, but its supported object set is incomplete.
12. **What can the Inspector do with BusinessNumber/labels?** It displays Interval BusinessNumber and internal switch business numbers from Domain APIs. Sequence, BayIndex and BusinessNumber are intentionally read-only; label geometry is not edited as business data.
13. **What Cable Inspector capability exists?** None in the current WPF Desktop resolver/projector, despite a separate Application resolver and generic Cable property command being present.
14. **What Joint Inspector capability exists?** None in the current WPF Desktop resolver/projector.
15. **Where are WorkScope and GroundingPoint exposed?** Both start from the Edit menu, use Canvas terminal picking, and finish/edit/delete through dedicated Inspector panels.

## 8. Partial Workflows

### 8.1 RingCabinet configuration

LoadSwitch and IntegratedFeeder creation are reachable. PT appears selectable in the dialog but is rejected by the active creation factory. Inspector type change is reachable, yet its replacement structure may not match the existing interval layout. DTU is absent.

### 8.2 Selection and object editing

Concrete RingCabinet switches are selectable and inspectable. CableSegment and IntermediateTerminal are hit-testable but not resolvable by the Desktop Inspector. Move and delete support only selected object families.

### 8.3 CableTermination

The user can add a CableTermination to a selected Pole and edit its name/layout. The Cable-side terminal is correct Domain data, but no Desktop Cable workflow can use it.

### 8.4 Development Scene commands

The three View menu actions `绘制测试内容`, `绘制环网柜组合场景`, and `清空绘图区` operate on displayed/demo Scene state rather than a complete project editing workflow. They should not be treated as V1 creation or reset capabilities.

## 9. Rendering-only Capabilities

The current EXE can rebuild and display persisted CableSegment and IntermediateTerminal/Joint objects. Tests also construct complete Cable/Joint topologies and verify Scene output. An ordinary user cannot create those topologies from an empty project in Desktop, so this remains Rendering-only product exposure.

Loaded pole switch attachments can also render, but the lower-level pole attachment factory is hidden and Desktop cannot create these attachments. Unlike Cable, this is classified as Hidden Capability because a production creation factory already exists; neither classification implies a complete user workflow.

## 10. Missing V1 Entry Points

The blocking missing entries are:

1. Concrete SwitchDevice operation through the current CommandStack and Domain interlock.
2. Cable tool with two legal terminal picks, metadata input, preview, commit, selection, Undo/Redo and Scene rebuild.
3. IntermediateTerminal creation or Cable Split action for the required Cable-Joint-Cable scenario.
4. Cable/Joint selection resolution and Inspector projection.
5. Pole switch attachment creation and operation for the required pole-device scenario.
6. Reliable PT creation/type-change layout synchronization and DTU composition.
7. JPG export and Windows print if `docs/requirements.md` remains authoritative.

## 11. Recommended UI Responsibility Split

| UI surface | Responsibility |
| --- | --- |
| Menu | Project lifecycle, export/print, lower-frequency object actions, Undo/Redo and professional operations |
| Left toolbox / future toolbar | Frequent placement and connection tools: select, RingCabinet, Pole, OverheadLine, Cable, CableTermination/Joint where appropriate |
| Canvas | Selection, drag, endpoint picking, connection preview, and fast object interaction with unambiguous gestures |
| Context menu | Explicit actions for the current object, such as `分闸`, `合闸`, Cable split/reconnect/delete; it should be an auxiliary discoverable path |
| Inspector | Read stable identity/business facts and explicitly edit supported properties/states |
| Shortcut | Acceleration only; no V1-critical function should be shortcut-only |

User-facing business rejection messages should be mapped to Chinese at the Desktop interaction boundary. Domain/Application exceptions should retain business invariants and technical detail; MainWindow/controllers should not expose raw `exception.Message` for normal invalid-operation and interlock cases.

## 12. Impact on Phase D-1

### 12.1 Current switch reachability

`ChangeSwitchStateCommand` is a **B. Hidden Capability**. The lower path is present:

```text
SwitchDevice.SwitchState
  -> DrawingDocument.ChangeSwitchState
  -> SwitchAssembly interlock
  -> Application.ChangeSwitchStateCommand Execute/Undo/Redo
```

The Rendering/selection path is also present:

```text
RingCabinet Renderer/layout
  -> switch hit bounds
  -> SelectionReference(Device, SwitchDeviceId, IntervalId)
  -> SelectionObjectResolver
  -> concrete SwitchDevice in Inspector
```

LoadSwitch and GroundSwitch in a LoadSwitch interval, IsolationSwitch/CircuitBreaker/GroundSwitch in an IntegratedFeeder interval, and IsolationSwitch/GroundSwitch in a PT interval are independently mapped to their own stable IDs. The current EXE can therefore hit a specific switch, not merely its Interval or RingCabinet.

What is missing is the middle Desktop operation path:

```text
specific switch hit/selection
  -> SwitchOperationController
  -> Rendering ICommand adapter around ChangeSwitchStateCommand
  -> current CommandStack
  -> success or Domain interlock refusal
  -> selection preserved
  -> Scene rebuild
  -> Chinese user feedback
```

The Application command does not implement `DistributionDrawing.Rendering.Wpf.Interaction.ICommand`; a small adapter or equivalent boundary command is required. `CommandStack.ExecuteCommand` calls `Execute` before altering history, so a thrown Domain rejection will not enter Undo history or truncate Redo history.

### 12.2 Recommended switch UI

Single left-click should continue to mean **select**, not immediately toggle state. It currently also participates in drag startup, and turning selection into an electrical operation would make accidental switching likely.

Recommended D-1 behavior:

1. Single-click the concrete switch primitive to select its stable SwitchDeviceId.
2. Inspector shows current state and explicit `分闸` / `合闸` actions; impossible/no-op actions can be disabled without duplicating interlock rules.
3. Add a switch context menu with the same explicit actions as a Canvas-local accelerator.
4. If a direct gesture is required, use double-click on the already identified switch as an optional toggle accelerator, never as the sole entry.
5. Every action runs the same controller/command path; Inspector, context menu, and any double-click must not contain interlock logic.
6. On success, preserve the SwitchDevice selection and rebuild the Scene so existing state geometry and `合/分` text update from Domain.
7. On refusal, leave Domain, Scene, selection, Dirty and command history unchanged, and show a mapped Chinese reason.

This keeps selection predictable while satisfying the requirement that a user can operate the switch directly from its drawn symbol.

## 13. Impact on Cable Connection Workflow

### 13.1 Why OverheadLine works and Cable does not

OverheadLine has every Desktop layer: menu, coordinator mode, two-pick controller, `TerminalAnchorIndex`, type/occupancy filtering, preview, command factory implementing the current Rendering command contract, CommandStack, selection, delete and Scene rebuild.

Cable has Domain objects, terminal rules, factory/Application commands, persistence and rendering, but has no menu/tool, controller, two-pick state, preview, metadata dialog, current CommandStack adapter, selection resolver or Inspector projector. Rendering a Cable proves that saved Domain state can be displayed; it does not provide a creation workflow.

### 13.2 Reusable OverheadLine components

The Cable workflow should reuse:

- `TerminalAnchorIndex` as TerminalId-to-document-position lookup.
- View-to-document coordinate conversion and pick tolerance.
- The two-stage `PickingStartTerminal` / `PickingEndTerminal` interaction shape.
- Transient preview rendering.
- `DrawingToolCoordinator` mutual exclusion/cancel behavior.
- Current CommandStack, selection transition, Scene rebuild and error boundary conventions.

Cable-specific logic must remain independent:

- filter `Terminal.Allows(ConnectionType.Cable)`, external/multiple-connection and occupancy rules;
- collect Cable name, CableType and Length in a Cable creation dialog;
- create a Cable `Connection` plus `CableSegment` through `CableSegmentCreationFactory`/Domain validation;
- select `SelectionTargetKind.CableSegment`, not OverheadLine `Connection`;
- support RingCabinet external Cable terminals, CableTermination CableSide terminals and IntermediateTerminal terminals;
- keep Cable split/Joint creation and reconnect as separate object actions;
- never use the picked screen position as topology data.

Recommended chain:

```text
Cable toolbox/menu action
  -> CableConnectionController
  -> first legal Cable TerminalId
  -> preview
  -> second legal Cable TerminalId
  -> Cable metadata dialog
  -> Cable command adapter + CommandStack
  -> CableSegment + Connection in DrawingDocument
  -> rebuild Scene
  -> select CableSegment
  -> Undo/Redo and Save/Open
```

The representative workflows remain impossible from Desktop until this path exists:

```text
RingCabinet -> Cable -> Pole + CableTermination

RingCabinet -> Cable -> IntermediateTerminal -> Cable -> Pole + CableTermination
```

## 14. Recommended Next Development Stage

Phase D-1 Interactive Switch Operation remains the recommended next stage.

Reasons:

- It is a Windows-user-confirmed P0 gap.
- Concrete switch hit testing and stable IDs already exist for all RingCabinet switch roles.
- Domain state and interlock logic already exist and must be reused.
- The missing scope is narrow: one interaction controller/command boundary, explicit Inspector/context actions, Scene rebuild, selection preservation, localized refusal feedback, and focused tests.
- Completing it validates the intended `UI -> CommandStack -> Domain interlock -> Rendering` architecture before the larger Cable connection workflow reuses the same error, selection and command conventions.

After D-1, the next stage should be Desktop Cable Connection Workflow, reusing the established OverheadLine picking architecture as described above.
