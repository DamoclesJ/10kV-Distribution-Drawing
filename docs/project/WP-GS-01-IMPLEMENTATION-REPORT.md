# WP-GS-01 — Implementation and Acceptance Status

> Requirements and architecture remain governed by the frozen contracts and implementation plan. This report records the current implementation candidate; it does not close or accept WP-GS-01.

## Current state

- Branch: `wp-gs-01`
- Implementation candidate: `2805057` (includes the final Seed lifecycle regression expectation)
- Requirements Freeze: FROZEN
- Architecture Freeze: FROZEN
- FormatVersion: V9
- Slices 1–11: implementation committed
- Slice 12: partially verified on macOS; Windows-only test execution remains pending
- Slice 13: Windows GUI checklist prepared; execution pending
- Governance: OPEN / IMPLEMENTED / AUTOMATED ACCEPTANCE PARTIAL / WINDOWS ACCEPTANCE PENDING

## Slice status and implementation commits

| Slice | Status | Commit |
| --- | --- | --- |
| 1. GAP Identity Integrity | Implemented | `221e031` — `fix(domain): reject provable grounding access side conflicts` |
| 2. Effective Grounding Domain API | Implemented | `9a1fe98` — `refactor(domain): expose read-only effective grounding evaluation` |
| 3. Candidate Electrical State View | Implemented | `e4e6110` — `feat(ea): add read-only candidate electrical state view` |
| 4. Candidate EA Support | Implemented | `e91c1c2` — `feat(ea): analyze candidate switch states without mutation` |
| 5. GroundingTarget Resolver | Implemented | `026a8a6` — `feat(gs): resolve grounding targets to EA identities` |
| 6. Grounding Safety Core Guard | Implemented | `1ae8d33` — `feat(gs): add application grounding safety guard` |
| 7. GroundingPoint Creation Guard | Implemented | `afaed91` — `feat(gs): guard grounding point creation commands` |
| 8. Switch Operation Guard | Implemented | `c6477f5` — `feat(gs): preflight switch state operations` |
| 9. Seed / Analysis Guard | Implemented | `656b72c` — `feat(gs): validate seed scenarios before EA publication` |
| 10. Undo / Redo Safety Integration | Implemented | `a054d41` — `fix(gs): enforce grounding safety before undo and redo` |
| 11. Minimal Desktop Messaging | Implemented | `541bcb7` — `fix(desktop): unify grounding safety blocking messages` |
| 12. Full Automated Acceptance | Partial | `2805057` — `test(gs): align stale EA lifecycle expectations`; complete Windows run pending |
| 13. Windows GUI Acceptance | Prepared, not executed | Windows acceptance pending |

## Implemented architecture

Domain now exposes a read-only switch-state view. `SwitchAssembly` and `RingCabinet` evaluate effective grounding through the existing assembly rules using that view; the result is derived runtime information and is not persisted. GAP validation rejects only a provable `LineSide` contradiction. When side derivation is unavailable, the stored manual value is retained and excluded from Grounding Safety identity decisions.

Application exposes immutable candidate switch/seed state, candidate-aware EA analysis, a resolver for Terminal and Connection identities, the Grounding Safety guard, and analysis submission preparation. Candidate EA and effective grounding consume the same view. No operation mutates the real Domain to calculate a candidate.

GroundingPoint creation checks a live valid result before mutation, including the prospective GAP in composite GAP-plus-GP creation. Switch Execute/Undo/Redo checks the destination state before Domain mutation. Explicit analysis checks the complete candidate Seed Set before publishing a successful result. Seed and switch edits do not establish a valid EA when no valid result exists; an existing valid EA continues its post-mutation refresh lifecycle. GroundingPoint restoration checks before inverse mutation, including at the root of selection-based composite deletion.

Blocking messages use the existing Desktop error/text flows and include the affected location. No safety panel, conflict list, canvas alarm, locator, or new persistence model was added.

## Automated validation

| Project | Result |
| --- | --- |
| Domain | 183 passed, 0 failed, 0 skipped |
| Application | 243 passed, 0 failed, 0 skipped |
| Infrastructure | 115 passed, 0 failed, 0 skipped |
| Rendering.Wpf | Test assembly compiled; testhost could not start on macOS because `Microsoft.WindowsDesktop.App 10.0` is unavailable |
| Desktop | Test assembly compiled; testhost could not start on macOS because `Microsoft.WindowsDesktop.App 10.0` is unavailable |
| Full solution Release build | Passed, 0 errors, 26 warnings |

The executable TRX files are under `artifacts/wp-gs-01/macos/`. The Rendering.Wpf and Desktop TRX runs were attempted and aborted before test execution; they are not reported as passed. The build confirms cross-target compilation only, not Windows runtime behavior.

`git diff --check` passed at each committed implementation boundary. Review of `ProjectFileFormat` confirms `CurrentVersion = Version9`. No DTO contract, persisted business fact, or persistence migration changed. V9 persistence regression tests passed in the Infrastructure suite. Rendering behavior and the newly added Windows-targeted command tests still require Windows execution.

## Windows GUI checklist

Run against the committed implementation candidate after Windows automated regression passes:

1. With no valid EA, create a GroundingPoint and edit Seed / switch state; confirm these edits do not establish a valid EA until explicit analysis.
2. With valid EA, reject GroundingPoint creation on an energized Terminal and GAP without changing Domain, history, savepoint, dirty state, or current result; allow deenergized targets.
3. Verify both PoleSwitch sides and multiple supports on one OHL Connection, including GAP inheritance from the Connection.
4. Verify ordinary LoadSwitch, UpperLowerGrounding, LowerLowerGrounding, and UpperIsolationGrounding effective grounding, including `GroundSwitch Closed + Breaker Open` and the later energized Breaker-close rejection.
5. Verify upstream energization is rejected when it would energize an existing work grounding point or effective grounding location.
6. Verify complete candidate Seed analysis is rejected atomically when any grounded location would become energized; verify an accepted analysis publishes its result.
7. Verify Undo / Redo and composite delete restoration cannot bypass checks and rejected actions preserve facts, history cursor, and dirty state.
8. Verify deenergizing and grounding-removal directions remain operable, Save / reopen remains V9, the existing red EA rendering remains correct, and no new safety panel or locator appears.

Do not mark WP-GS-01 CLOSED / ACCEPTED until full Windows automated acceptance and this GUI checklist pass on the final candidate SHA.
