# WP-EA-01A Closure Report

**Status: CLOSED / ACCEPTED**

## Scope

WP-EA-01A establishes project-persisted energization scenarios and a domain-independent analyzer over the existing electrical topology. It includes Scenario and Seed facts, EA-specific Boundary resolution, node-level propagation results, source tracking, V9 persistence, and V8-to-V9 migration.

It does not include production EA UI, Scenario editing workspace, red/black drawing presentation, Grounding conflict or final GroundingTarget conflict handling, WorkScope UI or adoption, work-ticket linkage, Device Interlock changes, or WP-EA-01B. These remain outside this package and are not closure defects.

## Accepted architecture and contracts

- Domain owns `EnergizationScenario`, stable Scenario ID, `EnergizedSeed`, stable Seed ID, strongly typed Side, and `IsSourceSetComplete`.
- Adding, removing, or replacing a Seed clears the completeness confirmation. Changing `SwitchState` does not change that confirmation.
- Application owns the independent EA Boundary Policy and `EnergizationAnalyzer`. Boundary resolution maps supported cabinet and pole device sides to existing Terminals; unresolved pole-number direction produces a diagnostic.
- Propagation traverses existing topology and each conducting switch edge. Multiple Seeds propagate together and every reached Terminal / ElectricalNode retains all `EnergizedBy` Seed IDs.
- `Closed` conducts and `Open` blocks, including for DropoutFuse. These states do not imply fuse installation or removal.
- A closed Grounding Switch is excluded from energization propagation. Its grounding connection identity remains available in the result for later conflict analysis.
- Result validity distinguishes `NoSeeds`, `ForwardOnly`, `Complete`, and `Incomplete`. Reached objects can be positively proven Energized by resolved Seeds. Unreached objects are Deenergized only when the source set is confirmed complete, at least one Seed exists, every declared Seed resolves, and topology is valid. Otherwise they remain Unknown. Unknown is never mapped to Deenergized.
- `ElectricalNode.ElectricalState` is not used as an implicit source and is not modified by the analyzer.
- The existing generic electrical connectivity graph contract and the closed WP-WTA-01 Boundary and WorkTicketAnalyzer contracts were not changed.

## Persistence

The project format is V9. Project files persist the Scenario ID, Seeds, Boundary device references, typed Side values, and `IsSourceSetComplete`. Derived EnergizationResult states and `EnergizedBy` are not serialized and are recalculated from the current topology and device states.

V8 opens through the controlled V8-to-V9 migration. Existing project facts are retained, an empty Scenario is created with `IsSourceSetComplete = false`, and the next save writes V9. V7 and earlier, versions greater than V9, and malformed or unexpected V9 JSON remain rejected.

## Accepted revisions

- Implementation candidate: `3918c6d85d81748b8bd47092d5abe38265e8c04d` — `feat(ea): implement WP-EA-01A energization analysis foundation`.
- Final candidate: `5246c1f37bab871319f08449dcbd15012aa2ad71` — `test(ea): strengthen WP-EA-01A acceptance evidence`.
- The final candidate adds only two automated tests; it contains no `src/` production-code changes.

## Windows acceptance evidence

The Windows full regression on the implementation candidate passed:

| Test project | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Domain | 177 | 0 | 0 |
| Application | 209 | 0 | 0 |
| Infrastructure | 112 | 0 | 0 |
| Rendering.Wpf | 619 | 0 | 0 |
| Desktop | 342 | 0 | 0 |
| **Total** | **1459** | **0** | **0** |

The solution build succeeded with 0 errors and 22 warnings. The warnings were recorded; this closure did not expand scope to clean them up.

The Windows delta acceptance on the final candidate passed:

- Application: 210/210, 0 failed, 0 skipped. The run discovered and passed `CableTerminationPropagatesFromCableSideThroughItsExistingInternalPath`.
- Infrastructure: 113/113, 0 failed, 0 skipped. The run discovered and passed `V8NonEmptyTopologyMigrationPreservesElectricalAndLayoutFacts`.
- Solution build: 0 errors.
- `git diff --check`: passed; working tree clean.

The Windows TRX files were parsed and checked during acceptance under `TestResults/WP-EA-01A/{Domain,Application,Infrastructure,Rendering.Wpf,Desktop}/`. These generated evidence files were not added to the repository.

The earlier `175/186` Domain/Application figures were superseded by the parsed Windows TRX counts above. The verified full run on `3918c6d` matched Mac Domain and Application counts at 177 and 209 respectively.

## Known limitations and exclusions

This package provides the analyzer and persistence foundation only. UI for selecting or editing Scenarios, final electrical-state rendering, Grounding conflict decisions and interlocks, connected-component WorkScope workflows, and work-ticket refresh behavior require separately scoped work. Windows GUI acceptance was not part of WP-EA-01A.

## Closure decision

The implementation candidate passed full Windows regression, and the final test-only candidate passed Windows delta acceptance. WP-EA-01A is **CLOSED / ACCEPTED** at final candidate `5246c1f37bab871319f08449dcbd15012aa2ad71`. No subsequent Work Package is authorized by this closure.
