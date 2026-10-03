# WP-GS-01 — Implementation and Acceptance Status

> Requirements and architecture remain governed by the frozen contracts and implementation plan. This report records the current implementation candidate; it does not close or accept WP-GS-01.

## Current state

- Branch: `wp-gs-01`
- Implementation fix commit: `fb0e0333edd38984f7679ca7dbd2ac33dbd66949`
- Clean pushed GS-AF-05 evidence candidate: `0ca64ae4beba77e34614d6e1dc36c67ac57db3ab`
- Requirements Freeze: FROZEN
- Architecture Freeze: FROZEN
- FormatVersion: V9
- Slices 1–11: implementation committed
- Slice 12: full Windows Release build and all five test suites passed on the clean pushed candidate
- Slice 13: user manually confirmed core GS behavior; GS-AF-05 GUI rechecks remain pending. Latest Computer Use launch was not approved.
- Governance: OPEN / IMPLEMENTED / AUTOMATED ACCEPTANCE PASSED / WINDOWS GUI ACCEPTANCE PENDING

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
| 12. Full Automated Acceptance | PASS, 1,583 tests on clean pushed GS-AF-05 candidate | `0ca64ae4beba77e34614d6e1dc36c67ac57db3ab` |
| 13. Windows GUI Acceptance | Partial prior manual evidence; final rechecks pending | Latest Computer Use launch not approved; see [Windows Acceptance Report](WP-GS-01-WINDOWS-ACCEPTANCE-REPORT.md) |

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
| Infrastructure (includes ProjectPersistence) | 115 passed, 0 failed, 0 skipped |
| Rendering.Wpf | 669 passed, 0 failed, 0 skipped |
| Desktop | 373 passed, 0 failed, 0 skipped |
| Total | 1,583 passed, 0 failed, 0 skipped |
| Full solution Release build | Passed, 0 errors, 22 warnings |

The latest clean-candidate Windows build and tests were run on `0ca64ae4beba77e34614d6e1dc36c67ac57db3ab`; TRX files are under `TestResults/WP-GS-01-GS-AF-05/`. `git diff --exit-code` passed after testing, so the run created no tracked file changes. The Release build completed with 0 errors and 22 nullable warnings.

The Windows run exposed one Desktop behavior gap and stale test fixtures. Explicit successful EA analysis now restores the EA overlay. Tests now reflect the frozen contracts: a provably contradictory GAP `LineSide` is rejected, a Seed edit after `NoSeeds` leaves EA stale until explicit analysis, and an invalid candidate Seed is rejected atomically. No Requirements/Architecture Freeze, FormatVersion, persisted fact, Transformer Seed, or terminal-seed boundary changed. `ProjectFileFormat.CurrentVersion` remains `Version9`.

Fix commit `fb0e0333edd38984f7679ca7dbd2ac33dbd66949` updates `ProjectRuntimeSession.ExecuteEnergizationAnalysis` to enable the overlay after successful explicit analysis and updates the corresponding WPF/Desktop regression tests. Documentation commit `9e16ffb3b998659eadf6a5710bf1d42997483728` records the test run and keeps WP-GS-01 OPEN.

## Windows GUI checklist

The user manually confirmed the core GS rejection, side judgment, and blocking-message behavior before this fix, and found the GS-AF-05 lifecycle defect. The latest post-fix Computer Use launch returned `Computer Use was not approved to use desktop`. Final-candidate GUI rechecks remain PENDING; prior manual evidence and the full checklist are recorded in the [Windows Acceptance Report](WP-GS-01-WINDOWS-ACCEPTANCE-REPORT.md).

Do not mark WP-GS-01 CLOSED / ACCEPTED until every GUI checklist case passes on the final candidate SHA.

## GS-AF-05 acceptance-driven lifecycle clarification

Fix commit `0ca64ae4beba77e34614d6e1dc36c67ac57db3ab` explicitly classifies GP/GAP add/remove and non-electrical GP properties/layout as EA-neutral. Both professional and selection-delete composites preserve EA only when all children are explicitly neutral. Runtime retains the exact CurrentResult and overlay request, with no cached copy, recompute, or visual workaround. True electrical mutations retain their established lifecycle; the GS guard is unchanged. Architecture Freeze has a minimal GS-AF-05 clarification; Requirements Freeze and V9 are unchanged.

Eleven added Desktop runtime regressions passed targeted validation, and the clean pushed candidate passed all 1,583 Windows tests. Recheck on the final candidate: overlay after GAP creation, overlay after allowed GroundingPoint creation, and GS rejection of a subsequent operation that energizes grounding. Keep WP-GS-01 OPEN.

A separate pre-existing mixed-selection deletion issue was found while constructing topology-control tests: deleting a GAP and its own OHL then Undo restores the GAP twice. Reproduction and follow-up are recorded in the Windows Acceptance Report; this lifecycle fix does not change cascade deletion.
