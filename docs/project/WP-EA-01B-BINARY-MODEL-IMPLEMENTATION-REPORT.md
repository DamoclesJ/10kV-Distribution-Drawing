# WP-EA-01B Binary Model Implementation Report

Date: 2026-10-02. State: Implemented / Mac Verified / Candidate for Windows Acceptance. No closure.

## 1. Baseline and authorization

- Initial checkout observed before branch preparation: `wp-ea-01c-a`, HEAD `e00bf8438944d5495067ee22c3bbc26bc910d69a`, clean.
- Prepared branch: `wp-ea-01b`; fetched origin and performed a fast-forward-only pull.
- Actual implementation baseline, explicitly confirmed by the user: `8ddace96206dcb45f0a2fb1b06f93e1057fd36a2`.
- Before editing: HEAD = `origin/wp-ea-01b` = the above SHA; working tree clean. Observed `origin/main`: `074d2bc48620f7678fe41549bc481c28a1556304`.
- `00fe81e8b0bc3b8c99b9287e85ea6b0c453fc0b7` is the historical Right Panel Fix candidate and an ancestor of the confirmed baseline. Returning to it would discard subsequently accepted live analysis, native rendering, workflow, hazard/selection, and closure commits. No reset, rebase, or checkout back to that historical candidate was performed.
- This amendment changes the EA source-completeness/result product contract under explicit user authorization. It preserves the accepted Boundary policy, typed Side, electrical identities, conductivity, grounding propagation exclusion, right-panel behavior, and WTA contracts. Historical closure documents are unchanged.

## 2. Read-only audit: former Unknown paths

Three independent code paths explained the former gray/Unknown behavior:

1. Application Analyzer used `IsSourceSetComplete == false` to produce `ForwardOnly`; unreached terminals/nodes became `Unknown`. Empty scenarios also produced Unknown point states. Earth/empty nodes had separate Unknown fallback behavior.
2. Seed resolution failures did not withdraw the whole analysis: other Seeds could still propagate while unreached objects received Unknown, leaving a partial result.
3. Rendering independently produced Unknown for missing Terminal/Node identity, mixed exact Association members, and unmatched Edge type/source/endpoints. Gray color, special stroke patterns, text underline, and `?` markers then made these electrical and visual issues appear alike.

These are confirmed code paths. The user's actual Windows pressure-test project file was not available, so the precise contribution of each path to that particular drawing cannot be established here.

## 3. New analyzer contract

```text
configured Seeds = authoritative electrical source set
reachable through current conductive topology => Energized
unreached => Deenergized
successful analysis => Complete, never Unknown
unresolved declared source or inconsistent required topology => Failed + diagnostics
```

The existing conductive graph and multi-Seed traversal are reused. Closed ordinary switches conduct; Open switches block. Every contributing Seed ID remains in `EnergizedBy`. No source outside the configured list is inferred.

Before propagation, the Analyzer validates node/terminal membership in both directions and the required CableTermination internal path. The existing graph builder validates Connection/switch endpoint existence. All declared Seeds must resolve; one failure withdraws the entire point result. Failed results contain diagnostics and empty terminal/node/edge maps, not partial Deenergized conclusions.

`CurrentResult` exposes only a fresh successful result. `LatestResult` retains failure diagnostics for the UI. Reanalysis failure removes the former successful overlay.

Zero Seeds uses option B: `NoSeeds`, an empty result, no overlay, and “当前未配置电源点”. The software does not publish an all-Deenergized drawing for an empty source list.

`Unknown`, `ForwardOnly`, `Incomplete`, and the legacy source-confirmation diagnostic enum value remain only for source compatibility. The normal Analyzer no longer emits them; the Desktop no longer presents their business semantics.

## 4. Mixed topology and preserved electrical behavior

The new `FourCabinetsUpperLowerGroundCableTerminationOhlAndPoleHaveNoTopologyBreak` regression constructs:

- Four RingCabinets, including UpperIsolationGrounding, UpperLowerGrounding, and LowerLowerGrounding integrated feeders;
- Cable connections between cabinets;
- A real CableTermination with CableSide/InternalNode/OverheadSide topology;
- Two OHL spans, numbered poles, and a pole IsolationSwitch;
- Closed ordinary conductive paths, an open internal breaker, an open pole switch, and a legally closed grounding branch behind an open ordinary switch.

The fixture verifies downstream Deenergized state at the open breaker, propagation after closing it through the CableTermination and first OHL, blocking at the open pole switch, and propagation to the final pole after closing that switch. Pole side resolution and exact contributing Seed identity are checked. All successful terminal/node states are binary; Earth remains unreached with no source contribution.

No genuine topology break was found in the audited graph construction paths or this executable mixed fixture. The existing CableTermination passive/internal path was retained without graph or geometry approximation. Missing/inconsistent required topology now fails explicitly. This evidence does not replace acceptance on the user's actual Windows drawing.

DropoutFuse retains Closed-conducts/Open-blocks semantics; no Installed/Removed inference was introduced. GroundSwitch is still rejected as an EA Boundary and cannot propagate to Earth. No grounding-conflict or new interlock engine was added.

## 5. Rendering contract and diagnostics

- Energized retains the current red emphasis and native geometry, thickness, fill rules, and stroke pattern.
- Deenergized retains original color, fill, and line style; it receives no gray or black replacement styling.
- Unknown gray/pattern styling, underline, and question-mark rendering are removed.
- Missing or inconsistent visual mapping is `ElectricalVisualState.Normal` plus an `EnergizationVisualizationDiagnostic`. Normal means no resolved visual state; it is not an electrical Deenergized conclusion.
- Diagnostics identify the visual identity kind/ID and the missing or inconsistent mapping. The text explicitly states that keeping the original appearance does not mean the object was proven Deenergized.
- An exact Association with mixed states is diagnosed rather than treated as an electrical state. Hazard associations retain red when a known member is Energized and also report unmapped members. Hazard aggregation does not replace precise electrical result data.
- Unmatched Edges must match type, source ID, and both terminal IDs; no geometry proximity fallback is used.

The render call forwards mapping diagnostics to the EA panel's “显示映射诊断” list. Failed/stale/hidden results clear that list and withdraw the overlay. Mapping diagnostics do not change the Application analysis result.

## 6. Desktop workflow and history

The source-set confirmation button, confirmation status, and confirmation preconditions were removed. The Seed list is the source configuration. Success text states “分析完成；红色表示带电，其余保持原图外观”; failure keeps the panel open with analysis diagnostics.

After analysis has been attempted, Add/Remove/Replace Seed commands and their Undo/Redo reanalyze the current authoritative list. Before the first analysis, Seed editing retains NotAnalyzed behavior. Removing the last Seed withdraws the result; restoring it through Undo can analyze again.

Switch commands and Undo/Redo preserve automatic live reanalysis and the user's overlay visibility preference. Structural commands retain the existing stale-result behavior. Analysis is runtime calculation and adds no separate history command; Seed/switch edits continue using the existing CommandStack.

The accepted `[属性] [带电分析]` structure, selection behavior, and right-panel routing were retained. `MainWindow` changed only to forward render diagnostics. The former independent “工作范围” path was not changed.

## 7. Persistence and scope impact

- FormatVersion remains V9. Infrastructure production code/schema and WorkTicketData are unchanged.
- `IsSourceSetComplete` remains serialized as a deprecated compatibility field. Seed edits preserve its legacy value. Analyzer and Desktop UI ignore it; both false and true produce the same binary analysis.
- Scenario ID, Seed IDs, Boundary IDs, typed Sides, and Seed order remain stable through Save/Open and Undo/Redo.
- Analysis results and rendering diagnostics remain runtime-only and are not persisted.
- Domain WorkScope, WTA business semantics, IsolationBoundaries, WorkScopeItems, ticket sections, and WorkTicket writes were not changed.
- No WorkScope generation, 01C-B integration, new source-path visualization, release tag, or closure is included.

## 8. Files changed by responsibility

| Area | Changes |
| --- | --- |
| Domain | `EnergizationScenario`: document/preserve legacy V9 completeness field; remove Seed-edit confirmation invalidation. |
| Application | Analyzer binary propagation/failure validation; result success predicate; successful CurrentResult guard; Scenario command compatibility; UI diagnostic/source-confirmation removal. |
| Infrastructure | No production changes. Persistence tests cover false/true legacy V9 field and identical binary analysis. |
| Rendering.Wpf | `EnergizationSceneStyler`, visual style resolver, native text renderer, and compatibility enum comment; mapping diagnostics and original appearance preservation. |
| Desktop | EA panel XAML/code, runtime live reanalysis, and MainWindow render-diagnostic forwarding. |
| Tests | Application analyzer/UI/new mixed fixture; Domain Scenario; Infrastructure V9; Rendering overlay/native/CableTermination; Desktop panel/runtime/right-panel regression. |
| Governance | This implementation report and the current target/state lines in STATUS.md. Historical closure documents unchanged. |

## 9. Mac validation

Executed on macOS with .NET 10. WPF/Desktop runtime tests were not executed on Mac.

| Gate | Passed | Failed | Skipped | Total | Outcome |
| --- | ---: | ---: | ---: | ---: | --- |
| Domain.Tests | 177 | 0 | 0 | 177 | PASS |
| Application.Tests | 220 | 0 | 0 | 220 | PASS; parsed TRX counters agree |
| Infrastructure.Tests | 114 | 0 | 0 | 114 | PASS |
| Solution build | — | — | — | — | PASS, 0 errors |
| Rendering.Wpf.Tests | — | — | — | — | Compile PASS, 0 errors; runtime not executed |
| Desktop.Tests | — | — | — | — | Compile PASS, 0 errors; runtime not executed |
| `git diff --check` | — | — | — | — | PASS |

Commands:

```sh
dotnet build src/DistributionDrawing.sln --no-restore -v quiet
dotnet test tests/DistributionDrawing.Domain.Tests/DistributionDrawing.Domain.Tests.csproj --no-restore -v quiet
dotnet test tests/DistributionDrawing.Application.Tests/DistributionDrawing.Application.Tests.csproj --no-restore -v quiet --logger 'trx;LogFileName=application-binary.trx' --results-directory /private/tmp/wp-ea-01b-binary-validation
dotnet test tests/DistributionDrawing.Infrastructure.Tests/DistributionDrawing.Infrastructure.Tests.csproj --no-restore -v quiet
dotnet build tests/DistributionDrawing.Rendering.Wpf.Tests/DistributionDrawing.Rendering.Wpf.Tests.csproj --no-restore -v quiet
dotnet build tests/DistributionDrawing.Desktop.Tests/DistributionDrawing.Desktop.Tests.csproj --no-restore -v quiet
git diff --check
```

Application TRX: `/private/tmp/wp-ea-01b-binary-validation/application-binary.trx` (local evidence; not committed). The final incremental solution build reported four NU1900 warnings, Rendering test compile one NU1900 warning, and Desktop test compile six warnings (NU1900 and existing xUnit2031). Earlier full compilation also reported existing nullable warnings. No warning is represented as a Windows runtime validation result.

Key coverage includes one/multiple Seeds and source attribution, Open-switch downstream Deenergized, invalid Seed/Side and corrupt node membership failure, failed reanalysis withdrawing previous facts, DropoutFuse Open/Closed, Earth exclusion, CableTermination real internal path, the four-cabinet mixed fixture, V9 false/true round-trip, and compiled rendering/Desktop contracts for original appearance, missing/mixed identities, no question mark, confirmation removal, live switch/Seed history, and right-panel routing.

## 10. Candidate and remaining acceptance

Commit message: `refactor(ea): simplify energization to authoritative seed model`.

Target: one candidate commit directly on the confirmed `wp-ea-01b` baseline, pushed to `origin/wp-ea-01b`. The resulting SHA, remote equality, and final clean working-tree evidence are reported after commit/push; a report inside that commit cannot contain its own SHA.

Windows automated execution of Rendering.Wpf.Tests and Desktop.Tests, full Windows regression, actual right-panel/Seed/switch Undo/Redo GUI acceptance, mapping-diagnostic visibility, and the real mixed pressure drawing remain required. No Windows runtime or GUI pass is claimed here.

**WP-EA-01B Binary Model Candidate Ready for Windows Acceptance**
