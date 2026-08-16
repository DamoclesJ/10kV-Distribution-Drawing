# V1 Drawing Accuracy / Product Gap Audit

> Audit date: 2026-08-15
>
> Audited baseline: `cb131e0` (`test: align desktop canvas interaction tests`)
>
> Scope: 10kV work-ticket drawing tool V1; code and test audit only

## 1. Current Verified Baseline

### 1.1 Windows EXE verification

The following facts were verified on Windows by the user:

- `DistributionDrawing.Rendering.Wpf.Tests`: 110/110 PASS.
- `DistributionDrawing.Desktop.Tests`: 20/20 PASS.
- `win-x64` publish: PASS.
- `artifacts/publish/desktop/win-x64/DistributionDrawing.Desktop.exe` was generated.
- The executable launches successfully.
- The main window and Canvas are visible.

Executable smoke test passed, but V1 product/visual accuracy is not yet accepted.

### 1.2 Windows EXE User Validation Findings

Confirmed working:

- EXE launches.
- MainWindow is visible.
- Canvas is visible.

Confirmed product gaps:

- User-facing errors and restrictions still include English messages.
- Switch symbols cannot currently be directly operated by clicking to open or close.
- Cable cannot currently be interactively connected through Desktop.
- Visual symbols and Desktop UI still require real-world accuracy refinement.

These four findings are user-observed product facts, not inferences from tests.

### 1.3 Evidence and status convention

This audit treats code and current tests as implementation facts. Older planning documents describe intent, but several of them predate PT, Cable V6, Label Runtime, and the current Desktop implementation.

- **Domain**: the business object or invariant exists in production Domain code.
- **Rendering**: the object enters the production DrawingScene path.
- **Desktop**: a user-facing path exists in the current EXE.
- **Tested only**: code is covered, but the user has not accepted the real visual or workflow result.
- **Lower-level only**: a factory, command, renderer, or resolver exists without a complete Desktop path.

## 2. V1 Implemented Capability Matrix

| Capability | Domain | Rendering | Desktop entry | Current factual status | V1 action |
| --- | --- | --- | --- | --- | --- |
| Load-switch RingCabinet | Yes: `LoadSwitchInterval` and two independent switches | Yes | Yes: creation dialog; pure cabinet constrained to 3–6 intervals | Structurally tested; visual accuracy not accepted | Calibrate symbol and complete switch operation |
| Integrated-feeder RingCabinet | Yes: three switches and three `GroundingStructureKind` values | Yes | Yes: creation dialog; pure cabinet constrained to 4 or 6 intervals | Creation/configuration exists; operation is read-only | Add switch operation and validate real layouts |
| PT interval | Yes; maximum one per cabinet | Yes in direct rendering | Partial and unreliable | Inspector offers type change, but initial creation factory rejects PT; type change does not rebuild interval/switch/PT layout | P0 closure required |
| DTU cabinet | No production DTU object | No | No | `DtuSecondaryConfiguration` is capability metadata; builders explicitly reject it | P0 V1 gap |
| Isolation switch | Yes | Yes | Select/read only; no open/close action | Lower-level state command exists | P0 interactive operation |
| Circuit breaker | Yes | Yes | Select/read only; no open/close action | Lower-level state command exists | P0 interactive operation |
| Ground switch | Yes, as a branch to Earth in Domain topology | Yes | Select/read only; no open/close action | Rendering branch quality varies by interval type | P0 operation; P1 symbol calibration |
| Load switch | Yes | Yes | Select/read only; no open/close action | Lower-level state command exists | P0 interactive operation |
| Independent SwitchState | Yes: each `SwitchDevice` stores Open/Closed | Yes: state drives symbol and “合/分” text | No state editing control | Tested below Desktop | P0 |
| Interlock | Partial confirmed rules | Rendering only reflects accepted Domain state | No operation/error path | Load/Ground mutual exclusion and Integrated Isolation/Ground mutual exclusion exist; PT has no rules; circuit breaker is not part of the current Integrated mutual-exclusion rule | P0 interaction; confirm rule completeness with user reference |
| Pole | Yes | Yes | Create, select, move, delete, edit pole number | Windows visual not accepted | P1/P2 calibration |
| PoleAttachment | Yes | Yes | CableTermination attachment can be added/moved/resized/deleted | Switch attachments are lower-level only; no Desktop creation entry | P0/P2 depending required pole equipment |
| CableTermination | Yes, with separate CableSide and OverheadSide terminals and internal node | Yes | Add to selected pole; edit name/layout | Cannot yet be used by a Desktop cable tool | P0 cable workflow |
| CableSegment | Yes; connection and stable endpoint IDs | Yes in main Scene | No create/connect/delete workflow; no Desktop property resolver | Lower-level create/split/reconnect commands exist | P0 |
| IntermediateTerminal / Joint | Yes | Yes in main Scene | No creation/split workflow; Desktop resolver does not resolve its selection | Lower-level split runtime exists | P0 for joint workflow |
| OverheadLine | Yes | Yes | Two-terminal picking, preview, create, select, delete, Undo/Redo | Strongest completed connection workflow | Retain as reference implementation |
| Cable model/length/endpoints | Yes and V6 persisted | CableType and Length label rendered | No Desktop create or property editor; endpoints not user-operable | Generic edit command is test-covered only | P0/P2 |
| Label / BusinessNumber | Domain numbering API exists | RingCabinet, Cable, Pole, Joint use Label Runtime | Displayed through Scene | OverheadLine main path has no LineModel label; fixed/status/professional text still uses direct `SceneText` by design or legacy | P1 consistency and visual calibration |
| Selection | Stable IDs and hit-test entries exist | RingCabinet, interval, switch, pole, attachment, cable, joint, overhead entries exist | Works for main editable devices | Cable/Joint entries are generated, but Desktop `SelectionObjectResolver` does not resolve them | P0/P2 closure |
| Inspector | Application resolver covers broad domain set | WPF projector covers cabinet/interval/switch/pole/attachment/overhead/terminal/professional | Read panels plus a few editors | No CableSegment or Joint projection; switch is read-only | P0/P2 closure |
| Move / Delete / Undo / Redo | Domain/layout operations exist where supported | Scene rebuilds after commands | Pole/RingCabinet move/delete; attachment move/delete; overhead delete; interval type Undo/Redo | Cable/Joint move/delete absent; switch-state command not integrated with current CommandStack | P0/P2 closure |
| Save / Open / Persistence | V6 preserves current domain facts | Scene rebuilt after load | New/Open/Save/Save As/dirty confirmation exist | Domain round trips are strong; PT and cable/joint layout gaps remain | P0 persistence acceptance |

### 2.1 Current completion indicators

These percentages are an audit score over explicit V1 acceptance capabilities, not a milestone estimate from planning documents.

| Module | Evidence-based completion | Basis |
| --- | ---: | --- |
| Domain / topology | 82% | Core devices, cable/joint/overhead topology, state, numbering and V6 facts exist; DTU and fully confirmed interlock contract do not |
| Rendering | 68% | All current primary topology objects enter Scene, but symbols are provisional/generic, DTU is absent, routing is basic, and output accuracy is unaccepted |
| Editor interaction | 48% | Pole/cabinet/attachment/overhead workflows exist; switch operation and cable/joint workflows are absent |
| Desktop workflow | 55% | Project lifecycle, Canvas, selection, basic inspector and commands work; major V1 operations remain inaccessible |
| Persistence | 78% | V6 domain round trip is covered; PT layout and editable cable/joint route persistence are incomplete |
| Windows release mechanics | 75% | Build/publish/launch pass; product acceptance, JPG/print, and full Windows scenario acceptance do not |

The aggregate indicates a runnable engineering baseline, not a releasable V1 product.

## 3. Desktop Functional Gaps

### 3.1 What the user can create now

The current MainWindow exposes these production creation paths:

- Pole placement.
- RingCabinet placement through a per-interval creation dialog.
- CableTermination attachment on a selected Pole.
- OverheadLine through two-terminal picking.
- GroundingPoint and WorkScope through their professional editing paths.

The user cannot currently create through Desktop:

- CableSegment.
- IntermediateTerminal / Joint or Cable Split.
- Pole switch attachments, despite lower-level Domain/Rendering support.
- DTU.
- A PT interval directly in the initial cabinet creation flow.

### 3.2 Properties and operations exposed now

Editable Desktop properties are limited to:

- Pole number.
- RingCabinet interval type and IntegratedFeeder grounding structure, although the current layout synchronization makes type changes unsafe in the real Scene.
- PoleAttachment offset, dimensions, and label offset.
- CableTermination display name.
- GroundingPoint and WorkScope fields.

Lower-level property commands exist for RingCabinet name, CableType, Cable length, and SwitchDevice display name, but no current Desktop editor wires them into the user workflow. OverheadLine data is displayed but not edited.

### 3.3 Interactive Switch Operation: confirmed P0 gap

Current chain:

```text
SwitchDevice.SwitchState
  → DrawingDocument.ChangeSwitchState
  → SwitchAssembly.ChangeSwitchState / interlock
  → Application.ChangeSwitchStateCommand (Execute/Undo/Redo)

RingCabinet rendering
  → switch layout has SwitchDeviceId
  → DrawingSceneBuilder hit entry uses Device + SwitchDeviceId + IntervalId
  → SelectionObjectResolver resolves the concrete SwitchDevice
  → Inspector displays kind/state
```

Missing chain:

```text
click or explicit state action
  → Desktop switch-operation controller
  → target Open/Closed decision
  → current Rendering ICommand / CommandStack adapter
  → ChangeSwitchStateCommand
  → success/failure result handling
  → selection preservation
  → RebuildScene
  → Chinese interlock feedback
```

Therefore SwitchState exists but cannot be changed in the EXE because the hit test currently ends at selection/inspection. MainWindow has no click, context-menu, toolbar, or Inspector action that invokes `ChangeSwitchStateCommand`. The Application command also does not implement the Rendering interaction `ICommand`, so a minimal adapter or boundary integration is required before it can enter the current CommandStack.

All cabinet switch layouts carry stable SwitchDevice IDs. IntegratedFeeder isolation switch, circuit breaker, and ground switch are individually mapped; LoadSwitchInterval load and ground switches are individually mapped; PT isolation and ground switches use the same mapping. Rendering already reads the updated Domain state, so a successful command only needs a Scene rebuild, not visual state mutation.

### 3.4 Desktop Cable Connection Workflow: confirmed P0 gap

Available lower layers:

- `CableSegmentCreationFactory` creates a CableSegment plus a Cable Connection using Terminal IDs.
- `CreateCableSegmentCommand`, `SplitCableCommand`, and `ReconnectCableCommand` exist.
- `DrawingDocument.AddCableSegment` validates Connection type, endpoint consistency, terminal policy, voltage and occupancy.
- RingCabinet external terminals allow Cable.
- CableTermination CableSide allows Cable.
- IntermediateTerminal supports the two-Cable joint scenario.
- CableSegment and Joint render in the main DrawingScene and persist in V6.

Missing Desktop layers:

- No Cable creation menu/tool/mode.
- No CableCreationController.
- No two-terminal Cable picking workflow or preview.
- No metadata dialog for name, cable type, and length.
- No integration of the cable command with the current Rendering CommandStack, selection transitions, error display, and Scene rebuild.
- No Desktop creation path for IntermediateTerminal/Split.
- Cable and Joint hit-test entries exist, but the current Desktop resolver only resolves `Connection` as OverheadLine and does not resolve CableSegment/IntermediateTerminal.

OverheadLine works because it has a complete `OverheadLineConnectionController` + `OverheadLineCommandFactory` + Add/Remove command + DrawingToolCoordinator + menu + preview + type-filtered Terminal picking chain. Cable has Domain/Application commands and Rendering, but none of those Desktop coordination layers.

The Cable tool can reuse the interaction shape of two-endpoint picking, but must apply independent Cable rules and data. Coordinates may choose a Terminal anchor; topology must be created as `TerminalId → Connection → CableSegment`. Coordinates must never become the connection fact.

Required V1 scenarios are not currently creatable from an empty Desktop project:

```text
RingCabinet → Cable → Pole + CableTermination

RingCabinet → Cable → IntermediateTerminal → Cable → Pole + CableTermination
```

### 3.5 RingCabinet, PT and DTU Desktop gaps

- The creation dialog lists all `IntervalKind` enum values, including PT.
- `RingCabinetCreationFactory.CreateIntervalDefinition` only handles LoadSwitch and IntegratedFeeder; selecting PT reaches an unsupported-kind exception.
- The Inspector can invoke `ChangeIntervalTypeCommand` to create PT in Domain.
- The command replaces internal switch IDs, but no matching interval layout replacement occurs. Existing SwitchLayouts still point to retired IDs, and a previous non-PT layout has no `PTSymbolPosition`; Scene rebuild can therefore fail.
- `ProjectRingCabinetIntervalLayoutDto` does not store `PTSymbolPosition`, so a PT layout cannot be round-tripped losslessly by the current layout mapper.
- DTU has capability metadata but production builders reject it; there is no DTU Domain/Layout/Rendering/Persistence/Desktop object.

### 3.6 User-visible message localization

Current English message sources include:

| Source | Examples / path | Classification |
| --- | --- | --- |
| Domain | `DrawingDocument`, `SwitchAssembly`, Terminal/Connection validation | Internal business/validation exceptions |
| Application | `ChangeSwitchStateCommand`, Cable factories/commands | Internal command validation |
| Rendering interaction | `PropertyEditor`, `PropertyCommandFactory`, device/connection factories | Many English `PropertyEditError` messages and exceptions |
| Desktop controllers | Placement, OverheadLine, CableTermination controllers | English operational exceptions |
| Desktop boundary | `MainWindow.ShowCommandError`, `ProjectWorkspaceController` | Frequently passes `exception.Message` or `result.ErrorMessage` directly to MessageBox |

Confirmed examples include `ShowCommandError(..., exception.Message)`, workspace `_dialogs.ShowError(..., exception.Message)`, and PropertyEditor returning English messages such as “The selected object no longer exists.” The current interlock exception would expose an English sentence and a technical rule code if connected directly.

Recommended minimal boundary:

```text
Domain/Application exception or typed error code
  → Desktop IUserMessageResolver (or equivalent small mapper)
  → Chinese title + Chinese user action/restriction message
  → technical exception retained for logging/debugging
```

Use stable error codes and known interlock rule codes where available. Do not translate by rewriting Domain exception text, and do not duplicate electrical rules in UI. Unknown exceptions should show a safe Chinese fallback while retaining technical detail outside the normal user message.

V1 acceptance: ordinary invalid connections, interlock rejection, invalid edits, delete restrictions, and open/save failures must not expose English implementation messages to the end user.

## 4. Symbol / Drawing Accuracy Gaps

### 4.1 Current geometry facts

| Symbol / layout | Current implementation facts | Accuracy conclusion |
| --- | --- | --- |
| RingCabinet | 10 mm padding, 5 mm interval gap, 60×125 mm intervals, 145 mm cabinet height, bus Y=25 mm | Initial layout strategy only; no real work-ticket dimension baseline in repository |
| Cabinet switch | Every kind uses a 16×10 mm initial layout and the same rectangle-based `SwitchSymbolDefinition` | Kind/state wiring is testable; professional symbol distinction is not demonstrated |
| Switch state | Open is a diagonal line; Closed is a horizontal line; “分/合” uses 3.5 mm text | State changes are visible; visual standard is unverified |
| LoadSwitch interval | Full vertical center line with LoadSwitch and GroundSwitch placed on fixed positions | Code does not visibly derive the ground branch from Terminal/ElectricalNode; requires professional review against the branch topology |
| IntegratedFeeder | Fixed upper/lower switch positions; ground branch selected from `GroundingStructureKind`; conductor 0.6 mm | Three configurations are distinct, but coordinates are hand-set and not validated against a real drawing |
| PT interval | 14×12 mm rectangle with fixed “PT” text; fixed switch positions | Functional placeholder; no DTU and no accepted PT/DTU composition |
| Pole | 4×42 mm vertical line plus 14 mm crossarm | Basic symbol only; no authoritative pole family proportions |
| PoleAttachment | Default 18×10 mm; connector line 0.7 mm | Relative layout is editable, but default professional placement is unverified |
| CableTermination | Rectangle plus a 4 mm stem | Topology anchor separation exists; symbol shape is provisional |
| Cable | Straight line between terminal anchors; 3.5 mm CableType/Length label at midpoint | Topology-driven, but no bends, route control, cable-specific line convention, or route persistence |
| Joint | 4 mm rectangle placed at midpoint of the two outer cable endpoints | Stable and deterministic; midpoint placement is an automatic rendering approximation, not an accepted work-ticket layout |
| OverheadLine | Straight line; optional continuation segment | Main Scene does not currently add the LineModel label through Label Runtime |
| Label engine | Default 3 mm, common 3.5/4 mm sizes; four 4 mm collision candidates | Deterministic basic avoidance only; no page/leader/complex typography standard |

### 4.2 Label-path facts

The primary Cabinet/Interval/Switch business numbers, Cable label, Pole/Attachment labels, and Joint label use `LabelRequest → LabelLayoutEngine → LabelLayoutResult → SceneText`.

This is not yet literally the only text path:

- Switch “合/分” remains direct state text, correctly separate from business labels.
- PT uses fixed literal “PT”.
- IntegratedFeeder draws a fixed “外部端子” label through the low-level symbol path.
- Professional boundary and grounding labels use direct SceneText/line-label paths.
- The main OverheadLine scene path currently creates geometry without a LabelRequest for LineModel.

These distinctions explain why unit tests for the Label Runtime do not establish complete drawing typography consistency.

### 4.3 Topology-driven rendering facts

- Cable endpoints come from CableSegment/Connection Terminal IDs and `TerminalAnchorIndex`.
- Joint validity requires exactly two current Cable connections and is derived from the IntermediateTerminal terminal.
- OverheadLine endpoints are rebuilt from Terminal anchors.
- Rendering does not create Domain connections.
- IntegratedFeeder branch placement currently uses `GroundingStructureKind`, not direct ElectricalNode inspection.
- LoadSwitch visual geometry does not currently demonstrate the Domain side-branch relation.

The electrical facts are topology-driven; several drawing positions are still fixed layout conventions rather than visually validated professional conventions.

## 5. Real-device Reference Gaps

The repository contains structural designs and textual topology diagrams, but it does not contain a complete, accepted set of real work-ticket symbol masters with measurable dimensions. Therefore code review can confirm implementation mechanics, IDs, state mapping, and topology source, but cannot confirm that the shapes, proportions, line widths, spacing, or typography match the required production drawings.

The following references are required from the user or a designated electrical professional:

1. One accepted ordinary load-switch RingCabinet work-ticket example.
2. One accepted IntegratedFeeder example for each of the three grounding structures.
3. Accepted PT + DTU composition examples, including allowed PT positions and DTU side/layout.
4. Open/closed symbol examples for LoadSwitch, IsolationSwitch, CircuitBreaker, and GroundSwitch.
5. Pole, pole switch, CableTermination, cable, overhead line, and joint symbol examples.
6. Required line weights, font family/sizes, label offsets, cabinet/interval proportions, and page scale.
7. At least one complete accepted work-ticket drawing covering cabinet → cable → joint → cable termination pole → overhead line.
8. The approved switch-operation/interlock table and user-facing Chinese rejection wording, especially the role of the CircuitBreaker and PT interval rules.
9. Confirmation whether JPG export and Windows printing remain mandatory V1 acceptance criteria as stated in `docs/requirements.md`.

No industry-standard geometry should be invented in code without these references.

## 6. V1 Blocking Issues

### P0-1 Interactive Switch Operation

The EXE cannot open/close a selected switch. Domain state, basic interlock, stable switch hit targets, and rendering state mapping exist, but the Desktop controller/command-stack/error/rebuild path is absent.

### P0-2 Desktop Cable Connection Workflow

Cable Domain, persistence, rendering, and lower-level commands exist, but there is no user workflow to pick terminals, enter cable facts, commit through CommandStack, select, undo, save, and reopen. This blocks the representative V1 topology from being created in the product.

### P0-3 PT/DTU and interval reconfiguration closure

PT creation and type-change layout synchronization are incomplete; PT layout persistence is lossy; DTU is absent. This blocks the defined IntegratedFeeder V1 scenario.

### P0-4 Complete Desktop topology editing closure

Joint creation/split, pole switch attachment creation, Cable/Joint Inspector resolution, and cable deletion/editing are absent. The complete scenario exists in tests because fixtures construct Domain objects directly, not because a user can build it from the EXE.

### P0-5 Project output acceptance

`docs/requirements.md` requires JPG export and Windows print. No production export/print path was found. If that requirements document remains authoritative, output is a V1 blocker. If product scope has intentionally changed, the requirements document must be explicitly updated rather than silently treating output as complete.

### P0-6 Save/open visual fidelity for all V1 objects

V6 preserves current Domain facts, but PT layout position is not represented and Cable/Joint paths are regenerated rather than saved as editable layout. A Windows save/open acceptance scenario must cover PT, Cable, Joint, PoleAttachment, state, labels, and connection identity.

## 7. Non-blocking Issues

### P1

- Chinese localization of normal user-facing errors and restrictions.
- Real-work-ticket symbol geometry, proportions, spacing, line weights, typography, and grounding branch calibration.
- Consistent Label Runtime coverage for OverheadLine and remaining business labels.
- Clear Chinese display names for enum values in dialogs and Inspector.

Although message localization does not change electrical correctness, the confirmed English prompts are not acceptable for a Chinese V1 end-user release.

### P2

- Replace the current basic Menu/left-toolbox/right-inspector shell with a clearer task-oriented workflow.
- Improve mode visibility, action availability, selection feedback, dialogs, and empty/error states.
- Complete property entry for current V1 objects without exposing Stable IDs as editable data.

### P3

- Existing warnings and unrelated code cleanup.
- Remove or reconcile older duplicate selection/inspector abstractions only when required by a V1 fix.
- Update stale historical documentation after product behavior is accepted.

## 8. Why All Tests Pass but the Product Is Not Yet Accurate

### 8.1 What the tests prove

- Domain construction and known invariants.
- Stable IDs, numbering, topology references, and persistence round trips.
- Scene element presence, counts, labels, hit-test identities, deterministic layout, and non-mutation.
- Desktop ViewModel/controller command invocation and project lifecycle behavior.
- WPF projects compile and the verified Windows suites pass.

### 8.2 What the tests do not prove

- Pixel/shape fidelity to an accepted electrical work-ticket reference.
- Correct proportions across real cabinet variants.
- End-user ability to create every tested fixture through Desktop.
- A real operator workflow for switch operation and interlock feedback.
- A real operator workflow for Cable/Joint connection.
- Chinese-only normal business/error feedback.
- A full saved project reopened and visually compared by a user.
- JPG/print output fidelity.

No golden-image, screenshot-comparison, UI automation, or professional visual acceptance suite was found. The complete-work-ticket test constructs topology directly in test code, which verifies model/persistence semantics but bypasses missing Desktop creation workflows.

## 9. Recommended Implementation Order

### Phase D-1 Interactive Switch Operation (P0)

- **Goal:** click/select a concrete switch and open/close it through existing Domain rules.
- **Current fact:** stable SwitchDevice hit targets, Domain state/interlock, Application command, Inspector display, and state-driven rendering already exist.
- **Modify:** Desktop controller/input, minimal CommandStack adapter, selection preservation, Scene rebuild, Chinese operation/interlock feedback, focused tests.
- **Do not modify:** topology model, interlock rule ownership, Renderer state directly, persistence format.
- **Acceptance:** all current cabinet switch kinds operate independently; valid action rebuilds Scene and supports Undo/Redo; invalid action leaves state/Scene/history unchanged and shows Chinese reason.
- **Windows validation:** required.

This is the recommended first phase because it is a confirmed P0 user gap and most lower layers already exist, making it the smallest high-value end-to-end closure. Its error mapping also establishes the pattern used by later workflows.

### Phase D-2 Desktop Cable Connection Workflow (P0)

- **Goal:** connect RingCabinet/Joint/CableTermination Cable terminals from Desktop.
- **Current fact:** Domain, factory, commands, persistence, anchors and rendering exist; Desktop coordination does not.
- **Modify:** Cable tool state/controller, cable command adapter/factory, metadata dialog, DrawingToolCoordinator/MainWindow, selection/inspector resolver, tests.
- **Do not modify:** coordinate-based topology, Domain connection policy, LabelLayoutEngine.
- **Acceptance:** both required cable scenarios can be built from an empty project, undone/redone, selected, saved and reopened with IDs/topology intact.
- **Windows validation:** required.

### Phase D-3 RingCabinet PT/DTU Configuration Closure (P0)

- **Goal:** make the approved IntegratedFeeder + PT + DTU combination creatable, editable, renderable and persistable.
- **Current fact:** PT Domain/numbering/rendering exists; creation/layout round trip is incomplete; DTU is absent.
- **Modify:** only the confirmed Domain/Layout/Rendering/Persistence/Desktop boundaries required by approved references.
- **Do not modify:** numbering rules, interval position identity, existing topology without evidence.
- **Acceptance:** valid PT at supported position, one DTU following the approved layout rule, type change Scene rebuild, Save/Open fidelity, maximum-one-PT invariant.
- **Windows validation:** required with user reference drawings.

### Phase D-4 User Message Localization Boundary (P1)

- **Goal:** ensure normal business restrictions and failures are presented in Chinese.
- **Current fact:** Desktop frequently displays internal English exception/result messages directly.
- **Modify:** Desktop message resolver/error boundary and stable error-code mapping; controllers only as needed to supply codes/context.
- **Do not modify:** Domain rules or exception text solely for translation; logging detail.
- **Acceptance:** operation, interlock, connection, property, delete, open/save and persistence errors show actionable Chinese messages; unknown errors use a Chinese fallback.
- **Windows validation:** required.

### Phase D-5 Drawing Accuracy Calibration (P1)

- **Goal:** calibrate the existing symbols against accepted work-ticket examples.
- **Current fact:** symbols are deterministic but rely on provisional fixed geometry and generic switch rectangles.
- **Modify:** Rendering symbols/layout constants and targeted visual regression tests after references are approved.
- **Do not modify:** Domain topology, numbering, state, persistence semantics.
- **Acceptance:** user/professional signs off ordinary cabinet, three IntegratedFeeder structures, PT/DTU, pole/attachments, cable/joint and overhead examples at target page scale.
- **Windows validation:** required; image/print comparison required.

### Phase D-6 V1 Desktop and Output Acceptance (P2/P0 if output remains required)

- **Goal:** finish task-oriented UI polish and execute the complete Windows release checklist.
- **Current fact:** shell/publish/launch work; complete workflow and output acceptance do not.
- **Modify:** Desktop presentation, current V1 property entry, status/help affordances, JPG/print only if requirements remain authoritative.
- **Do not modify:** add new device families, AI, cloud, collaboration, or unrelated architecture.
- **Acceptance:** two representative projects are created from the EXE, operated, saved/reopened, visually accepted, and exported/printed if required.
- **Windows validation:** mandatory.

## 10. Windows Validation Requirements

For every P0/P1 phase:

1. Build and run both Windows WPF test projects.
2. Publish `win-x64` using the existing profile.
3. Create the scenario through the EXE rather than a test fixture.
4. Verify selection, operation, error feedback, Undo/Redo, dirty state, Save/Open and Scene rebuild.
5. Compare the drawing to the user-provided accepted reference at the intended scale.
6. Record screenshots and the project file used for acceptance.
7. Verify no normal business restriction exposes English text.

## 11. Questions / References Needed From User

1. Please provide accepted work-ticket screenshots or redacted samples for ordinary, IntegratedFeeder, PT/DTU, pole, cable termination, cable, overhead line and joint symbols.
2. What page size, drawing scale, line weights, font family and minimum text sizes are required?
3. What is the exact DTU box shape, label, size and relation to a PT at each allowed position?
4. Please provide the approved operation/interlock table for LoadSwitch, IsolationSwitch, CircuitBreaker and GroundSwitch, including PT intervals and Chinese rejection wording.
5. Should a single click toggle a switch immediately, or should click select and a second explicit action confirm Open/Close?
6. What Cable fields are mandatory at creation, and is a straight segment sufficient for V1 or are editable bend points required?
7. How should an IntermediateTerminal be created: explicit tool, Split Cable action, or both?
8. Are JPG export and Windows printing still mandatory V1 acceptance criteria?
9. Which enum terms should be shown to operators for interval kind and grounding structure instead of current English enum names?
10. Please identify one final representative project that will be the V1 Windows acceptance drawing.
