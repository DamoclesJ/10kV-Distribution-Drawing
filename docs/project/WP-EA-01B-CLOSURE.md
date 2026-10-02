# WP-EA-01B Closure — Authoritative Seed Analysis and Energization UI

## Final status

**CLOSED / ACCEPTED.** The final accepted candidate is commit `45125261600a60897e8a7367bd51c1f665557ecc` on `wp-ea-01b`, titled `fix(ea): reconcile scenario after interval topology changes`.

This report updates the prior WP-EA-01B closure that recorded the earlier Scenario UI / visualization candidate `d5ad046b515c7d90c9ac1511d0a71ba694771b6a`. The work package remained open through its authorized authoritative-Seed binary-model amendment and interval-mutation defect fix. The earlier closure remains historical evidence for that candidate; this final accepted closure supersedes its completeness/Unknown product contract and candidate SHA.

## Scope and final product behavior

WP-EA-01B delivers the Energization Scenario UI, precise energization analysis and native visualization, live switch-state reanalysis, and integrity across supported RingCabinet interval-type changes. The final accepted behavior uses the user's configured Seed list as the authoritative source set for each analysis.

### Authoritative Seed binary-analysis contract

```text
configured Seeds = authoritative electrical source set
reachable through current conductive topology => Energized
not reachable => Deenergized
successful analysis => no Unknown state
```

One or more Seeds are supported. `EnergizedBy` preserves all contributing Seed IDs. Open switches block propagation; closed conductive switches allow propagation, including the existing DropoutFuse behavior. A Seed's typed Boundary and Side must resolve. Unresolved Seeds, invalid sides, or inconsistent required topology produce an explicit failed analysis with diagnostics and no partial point-state result.

The previous `IsSourceSetComplete` confirmation flow and normal `ForwardOnly` / `Unknown` business semantics are removed. `Unknown` and the old completeness field remain only as compatibility values where needed; the analyzer does not emit Unknown for a successful run, and the Desktop no longer requires source-set confirmation. With no Seeds, analysis reports `NoSeeds`, shows no energization overlay, and explains that no source is configured.

A GroundSwitch/Earth branch remains excluded from ordinary energization propagation. The accepted 01A boundary policy and typed-side resolution remain unchanged.

### Visualization and Desktop workflow

- Energized objects retain the accepted red EA emphasis and update after switch changes.
- Deenergized objects retain their original drawing colors, fills, and line patterns.
- Unknown gray/pattern styling, question marks, and Unknown legend/status wording are removed from normal analysis.
- Exact visual-identity mapping failures are reported as visualization diagnostics. An unmapped visual retains its original appearance and is not presented as an electrical Deenergized result.
- The existing `[属性] [带电分析]` right-panel behavior, selection coexistence, Candidate/Seed UI, show/hide control, and native drawing geometry are retained.
- Candidate selection, typed boundary Side, multiple Seed configuration, and aggregate Hazard presentation remain available under the accepted precise-identity rules.
- Switch Open/Closed changes and their Undo/Redo trigger fresh analysis when an analysis is active.
- Adding, removing, or replacing Seeds after analysis updates the authoritative source set through the existing CommandStack flow.

### Interval mutation integrity

The final Windows GUI acceptance found and verified the fix for a supported interval-type edit that had prevented later EA analysis.

The defect had **both** causes:

1. `RingCabinet.ChangeIntervalType` rebuilt switches and their terminals/nodes with new identities. The Scenario retained the Seed's old `BoundaryDeviceId`; manually restoring the previous interval type created another switch identity, so the Seed did not recover.
2. `DrawingDocument.SynchronizeRingCabinetAggregate` did not replace the cabinet-owned main bus node when it replaced interval topology. The retained node could still list retired terminals, while the document contained replacement terminals. The analyzer correctly rejected this inconsistent node/terminal membership as invalid topology. Changing interval type back or reconnecting a cable did not repair either condition.

The final fix synchronizes the main bus node's membership with the replacement aggregate and, in the same interval-edit CommandStack transaction, removes only Seeds whose boundary switches that edit retires. It does not infer a successor from screen position or switch kind. The UI tells the user to select the source boundary again. Other Seeds remain. Undo restores the captured original switch/topology identities and Scenario; Redo reapplies the captured replacement and Seed reconciliation. Cable reconnection remains analyzable after the topology is valid and any retired source is reselected.

The Analyzer remains strict: an unrelated invalid declared Seed continues to fail with diagnostics. It does not mutate or repair the Scenario as a side effect.

## Persistence and frozen boundaries

- `FormatVersion` remains V9. No persistence format upgrade or new serialized fact was introduced.
- Scenario inputs continue through the existing V9 path. Derived analysis, visualization diagnostics, and removal notices remain runtime-only.
- WTA business semantics and WorkTicket/WorkScope persistence remain unchanged. No writes to `IsolationBoundaries`, `WorkScopeItems`, ticket sections, or WorkTicket data were added.
- WP-EA-01A remains CLOSED / ACCEPTED and was not reopened. Its Boundary Policy and grounding propagation contract remain in force.

## Acceptance evidence

### Windows automated acceptance

User-reported Windows acceptance on final candidate `45125261600a60897e8a7367bd51c1f665557ecc`:

| Test suite | Passed | Failed | Skipped | Total |
| --- | ---: | ---: | ---: | ---: |
| Domain | 180 | 0 | 0 | 180 |
| Application | 222 | 0 | 0 | 222 |
| Infrastructure | 114 | 0 | 0 | 114 |
| Rendering.Wpf | 666 | 0 | 0 | 666 |
| Desktop | 360 | 0 | 0 | 360 |
| **Total** | **1542** | **0** | **0** | **1542** |

The solution build reported 0 errors / 0 warnings. `git diff --check` passed.

### Windows GUI acceptance

The user manually completed GUI validation on the real scenarios and confirmed **PASSED**. Accepted checks included:

- authoritative Seed binary analysis and no normal Unknown state;
- energized red emphasis and original appearance for non-energized regions;
- automatic reanalysis on Open/Closed switch operation;
- mixed multi-cabinet topology;
- interval-type replacement and reversal without permanently invalidating EA;
- removal and clear notification for a retired Seed boundary, followed by successful source reselection;
- cable reconnection followed by successful analysis;
- no recurrence of the permanent analysis failure.

## Exclusions and future work

The following remain outside this closure and require separate planning/authorization:

- EA-aware image/export output;
- EA → WorkScope Draft generation and any WorkTicket integration;
- changes to WTA, WorkScope, or WorkTicket semantics;
- GroundingTarget conflict analysis;
- Device Interlock and prevention of otherwise representable switch combinations;
- new source-path visualization or inference about unconfigured/off-drawing sources.

This closure does not start WP-EA-01C and does not define a release version.

## Final disposition

**WP-EA-01B = CLOSED / ACCEPTED** at `45125261600a60897e8a7367bd51c1f665557ecc`.
