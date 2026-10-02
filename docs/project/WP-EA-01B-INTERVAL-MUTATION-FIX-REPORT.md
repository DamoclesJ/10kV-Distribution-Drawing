# WP-EA-01B Interval Mutation Fix Report

Date: 2026-10-02. Status: Implemented / Mac Verified / Awaiting Windows Acceptance. No closure.

## Baseline

- Branch: `wp-ea-01b`.
- Before editing: HEAD = `origin/wp-ea-01b` = `2874a0bbdd2fda83112283c88c00e155cc2949fa`; working tree clean.
- Observed `origin/main`: `074d2bc48620f7678fe41549bc481c28a1556304`.
- The authoritative Seed binary analysis contract and all accepted fixes in this baseline are retained.

## Root cause: both stale Seed references and corrupted document topology

The reproduction uses the supported `ChangeIntervalTypeCommand` and `CommandStack`, starting with a valid integrated feeder, an EA Seed, and a cable. It changes UpperIsolationGrounding to UpperLowerGrounding, changes back, explicitly selects a current source again, and reconnects the cable to another live interval. Production code was not changed before the reproduction ran.

Two regression tests failed on the baseline:

1. `ReplacementRevertAndReconnectRemainAnalyzableAfterReselectingSource`: `InvalidTopology`, reporting a terminal with inconsistent electrical-node membership. The test deliberately reselects a live boundary after each change, separating topology failure from the old Seed reference.
2. `ReplacementDoesNotRetainDeletedSourceBoundary`: the Scenario still contained the retired switch reference.

Local baseline evidence is `/private/tmp/wp-ea-interval-validation/before.trx` (0 passed / 2 failed / 0 skipped / 2 total). One reproduced stale reference was Seed `2ee7f9b6-08c5-4f7a-8db0-9f00fd47de4f` -> retired Boundary `54e88bd7-e2b8-4cff-803c-b1543c284671`. One topology diagnostic named terminal `6a6aac75-d41a-4ec1-bbd2-c2216fa2a958`. Fixture GUIDs are generated per run; these are representative test evidence, not IDs from the user's Windows drawing.

### Production edit path

```text
MainWindow.OnApplyIntervalConfiguration
  -> PropertyEditor.TryChangeIntervalType
  -> PropertyCommandFactory.TryCreateIntervalTypeChange
  -> CommandStack.ExecuteCommand(ChangeIntervalTypeCommand)
  -> RingCabinet.ChangeIntervalType
  -> DrawingDocument.SynchronizeRingCabinetAggregate
  -> layout rebuild and scene refresh
```

`RingCabinet.CreateTypeChangeIntervalDefinition` creates replacement switch/device, switch-terminal, node, and assembly IDs. It does not mutate the previous switch in place or provide an EA semantic-successor mapping.

| Fact | Actual replacement semantics |
| --- | --- |
| Cabinet identity / bus ID | Cabinet object and GUID, MainBusNodeId stay stable. The aggregate rebuilds the bus node object and its terminal membership. |
| Interval | IntervalId, parent, slot/order/business placement are preserved; the interval object is rebuilt. |
| Internal switches | Isolation, breaker, and ground switches receive new GUIDs in the changed interval. Unchanged intervals retain their IDs. |
| Switch terminals | Recreated with new GUIDs and node bindings matching the new structure. |
| Nodes | Circuit/Earth/intermediate nodes are recreated; the bus keeps its GUID but needs its newly derived membership synchronized. |
| Cable endpoint | Optional CableTerminalId is preserved for LoadSwitch/IntegratedFeeder transitions. Existing CableSegment/Connection IDs and endpoint references remain valid. Changes that retire a referenced cable endpoint are rejected and rolled back. |
| Selection | Interval selection can remain by stable IntervalId. Scene rebuild uses existing selection retention to remove selections of deleted internal devices. No positional reference remapping is added. |
| Scenario | Previously unchanged: Seed.Id and Side stayed stable, but BoundaryDeviceId still pointed at the deleted switch. |

The old synchronization selected interval-owned and switch-owned nodes but omitted the cabinet-owned main bus. It therefore installed new terminals while the document's retained bus still listed deleted terminals. The new Analyzer correctly rejected this inconsistency. Reverting the type generated a third set of switch/terminal GUIDs rather than restoring the original IDs, and did not repair the retained bus.

Symbolically: original boundary A -> replacement B -> manually restored type C, while the old Seed still referenced A. Undo differs from manual type reversal: the existing command snapshots can restore A exactly.

## Fix

### 1. Synchronize the whole affected bus fact

`DrawingDocument.SynchronizeRingCabinetAggregate` now includes MainBusNodeId in both previous and replacement node sets. The same atomic synchronization installs the new bus membership alongside interval terminals/nodes/devices. Bus GUID, connections, and the existing dependency guards remain intact.

No orphan device owner, terminal/node membership, or Connection endpoint remains in the tested supported replacements, manual reversals, Undo/Redo, or rejected edits. Aggregate structure and the existing conductive graph are validated by regression tests.

### 2. Reconcile retired boundaries in the edit transaction

The Application policy is `EnergizationScenarioCommand.RemoveDeletedBoundaries`. It creates a reversible Scenario edit that removes only Seeds referencing devices retired by this edit, preserving unaffected Seeds, IDs, Side, order, and the legacy V9 completeness field. An unrelated invalid Seed is not silently removed or concealed.

The existing interval command adapter captures retired switch IDs from its before/after aggregate identities and applies that Application command after topology/layout replacement succeeds. Undo/Redo use the captured Scenario states with the existing cabinet/layout snapshots. No Analyzer side-effect, rendering-time repair, extra history entry, or separate persistence cleanup is introduced.

The Desktop property-command factory receives the project-owned Scenario. Preview/non-project command uses can continue without Scenario ownership. The Scenario removal policy belongs to Application; the command under `Rendering.Wpf/Interaction` orchestrates the existing edit transaction, and no renderer or scene styler reconciles electrical facts.

No automatic successor migration is implemented. Templates do not define an authoritative EA-boundary inheritance rule; same switch kind or screen position is not used to infer one. Retired boundary Seeds are explicitly removed and require user reselection.

### 3. Notify the user

The normal Apply action displays “原电源边界已不存在，请在带电分析中重新选择”. Runtime reference diagnostics also remain visible in the EA panel while the result is stale or there are no Seeds. Undo clears the removal notice when the original Seed is restored; Redo reissues it. Seed editing clears the notice. These notices are runtime-only.

Removing the sole Seed gives the existing NoSeeds message, never an all-Deenergized result. Other valid Seeds remain analyzable after a new analysis. Reselecting a legal source restores normal binary EA. Structural edits retain the existing stale-result behavior; switch live reanalysis is unchanged.

## Undo/Redo and rejected edits

- One history entry contains the interval/device/node/layout replacement and Scenario reconciliation.
- Undo restores original switch/terminal IDs, original topology, exact Seed list/order, and layout.
- Redo restores the same captured replacement IDs and the reconciled Seed list.
- Scene-validation rejection uses the existing CommandStack rollback to restore both topology and Scenario.
- An attempted PT replacement that would retire a connected cable endpoint is rejected before source removal; the original Seed, layout, graph, and history remain intact.

## Cable reconnection

Reconnection could not repair either original defect: it edits CableSegment/Connection endpoints, not the cabinet bus's terminal membership or Scenario BoundaryDeviceId.

The fixed regression proves that replacement -> reverse type -> reselect current Seed -> supported cable reconnection analyzes successfully. A separate test deliberately omits Scenario wiring at the low-level command boundary: its topology is valid after the bus fix, but its stale Seed still produces explicit MissingBoundaryDevice before and after cable reconnection. Removing that Seed and adding a current boundary then succeeds. Analyzer remains strict and does not repair or ignore invalid sources.

Already-invalid references outside the current edit's retirement set remain explicit diagnostics and can be removed/reselected through the existing EA UI; this change does not globally discard declared sources on analysis or load.

## Preserved scope

- Successful EA remains reachable => Energized / unreached => Deenergized; failures remain diagnostics with no published point states.
- No Unknown, gray overlay, question mark, ForwardOnly business semantics, or source-confirmation workflow is reintroduced.
- Multi-Seed/EnergizedBy, Open/Closed propagation, OHL/pole resolution, CableTermination internal path, and grounding-switch Earth exclusion remain green in Application regression.
- V9 and all persistence DTOs are unchanged. Reconciled Seeds are existing Scenario inputs saved through the existing V9 path; no new serialized fact is added.
- WTA, Domain WorkScope, WorkTicketData, ticket sections, GroundingTarget rules, and Device Interlock are unchanged.
- No new closure or follow-up package.

## Mac validation

All test counts below were parsed from TRX counters under `/private/tmp/wp-ea-interval-validation`.

| Gate | Passed | Failed | Skipped | Total | Result |
| --- | ---: | ---: | ---: | ---: | --- |
| Baseline reproduction, before production changes | 0 | 2 | 0 | 2 | Expected failures confirming defect |
| Domain.Tests | 180 | 0 | 0 | 180 | PASS |
| Application.Tests | 222 | 0 | 0 | 222 | PASS |
| Infrastructure.Tests | 114 | 0 | 0 | 114 | PASS |
| Portable production-command regression | 7 | 0 | 0 | 7 | PASS; not Windows WPF runtime acceptance |
| Solution build | — | — | — | — | PASS, 0 errors |
| Rendering.Wpf.Tests | — | — | — | — | Compile PASS; full Windows runtime not executed |
| Desktop.Tests | — | — | — | — | Compile PASS; Windows runtime not executed |
| `git diff --check` | — | — | — | — | PASS |

Required commands:

```sh
dotnet build src/DistributionDrawing.sln --no-restore -v quiet
dotnet test tests/DistributionDrawing.Domain.Tests/DistributionDrawing.Domain.Tests.csproj --no-restore -v quiet --logger 'trx;LogFileName=domain.trx' --results-directory /private/tmp/wp-ea-interval-validation
dotnet test tests/DistributionDrawing.Application.Tests/DistributionDrawing.Application.Tests.csproj --no-restore -v quiet --logger 'trx;LogFileName=application.trx' --results-directory /private/tmp/wp-ea-interval-validation
dotnet test tests/DistributionDrawing.Infrastructure.Tests/DistributionDrawing.Infrastructure.Tests.csproj --no-restore -v quiet --logger 'trx;LogFileName=infrastructure.trx' --results-directory /private/tmp/wp-ea-interval-validation
dotnet build tests/DistributionDrawing.Rendering.Wpf.Tests/DistributionDrawing.Rendering.Wpf.Tests.csproj --no-restore -v quiet
dotnet build tests/DistributionDrawing.Desktop.Tests/DistributionDrawing.Desktop.Tests.csproj --no-restore -v quiet
git diff --check
```

The temporary `net10.0` harness `/private/tmp/wp-ea-interval-harness/Harness.csproj` directly compiles the production ICommand, CommandStack, ChangeIntervalTypeCommand, Layout/Metrics source files and the committed `EnergizationIntervalMutationTests.cs`, referencing the real Application/Domain projects. Only the two coordinate value records are supplied locally to omit the unused WPF coordinate converter. It runs the command/topology tests without substituting their behavior. It does not load WPF visuals, MainWindow, or the Desktop session. Harness/TRX/bin/obj are not committed. The seven tests also compile in the normal Windows Rendering test project.

Solution build reported 26 warnings and 0 errors, including existing nullable warnings and NU1900 from the restricted NuGet vulnerability cache. These warnings are not represented as Windows acceptance evidence.

New coverage includes three Domain grounding arrangements and reverse/restore membership integrity, Application removal-policy Undo/Redo and preservation of unrelated invalid sources, production interval CommandStack Undo/Redo with unaffected Seed, command rejection rollback, cable reconnect recovery, strict failure for an unreconciled Seed, and a compiled Desktop property-edit/panel-notice/reselection regression.

## Candidate handoff

Commit message: `fix(ea): reconcile scenario after interval topology changes`.

The fix is a single candidate commit on `wp-ea-01b`, pushed to `origin/wp-ea-01b`. Final SHA, remote equality, and clean working-tree evidence are reported after commit/push. This in-commit report cannot contain its own commit SHA.

Windows must still execute Rendering.Wpf/Desktop and full regression, reproduce the actual UpperIsolationGrounding -> UpperLowerGrounding -> reverse edit, check notice visibility, reselect a source, reconnect the cable, and verify Undo/Redo. No Windows runtime/GUI PASS is claimed from Mac.

**WP-EA-01B Interval Mutation Fix Candidate Ready for Windows Acceptance**
