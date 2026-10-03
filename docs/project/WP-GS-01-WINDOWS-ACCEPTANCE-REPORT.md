# WP-GS-01 — Windows Acceptance Report

## State

- Governance: **OPEN / IMPLEMENTED / AUTOMATED ACCEPTANCE PASSED / WINDOWS GUI ACCEPTANCE PENDING**. No CLOSED / ACCEPTED declaration.
- Branch: `wp-gs-01`.
- GS-AF-05 implementation and clean pushed evidence candidate: `0ca64ae4beba77e34614d6e1dc36c67ac57db3ab` — `fix(ea): preserve valid analysis for grounding-only mutations`.
- FormatVersion: V9. Requirements Freeze and existing GS safety semantics are unchanged.
- `TestResults/` remains untracked, preserved, and excluded from commits.

## GS-AF-05 reproduction, cause, and fix

User-confirmed reproduction: establish valid EA and red/normal overlay; add GAP on a deenergized OHL working area or an allowed GroundingPoint; EA unexpectedly becomes stale and the overlay disappears.

Cause: `ProjectRuntimeSession.OnCommandStackStateChanged` invalidated every command other than the existing Seed/switch refresh cases. Grounding-only commands fell into that broad fallback, although their facts do not participate in EA propagation.

Fix: `IEnergizationImpactCommand` explicitly classifies grounding add/remove, non-electrical GroundingPoint properties/layout, and their history operations as neutral. Professional and selection-delete composites are neutral only when every child is explicitly neutral. Runtime preserves the exact existing CurrentResult and overlay request; it performs no cached-result copy, republish, or visual workaround. Unclassified/electrical mutations retain the established lifecycle. GS guards are unchanged.

## Windows automated evidence

Targeted `GroundingEnergizationLifecycleTests`: **11 passed / 0 failed / 0 skipped**. Pre-commit full build and regression also passed. Formal validation below ran on clean, committed, pushed candidate `0ca64ae4beba77e34614d6e1dc36c67ac57db3ab`.

| Suite | Passed | Failed | Skipped | Result |
| --- | ---: | ---: | ---: | --- |
| Solution Release build | — | 0 errors | — | PASS (22 existing nullable warnings) |
| Domain | 183 | 0 | 0 | PASS |
| Application | 243 | 0 | 0 | PASS |
| Infrastructure, including ProjectPersistence | 115 | 0 | 0 | PASS |
| Rendering.Wpf | 669 | 0 | 0 | PASS |
| Desktop | 373 | 0 | 0 | PASS |
| **Total tests** | **1,583** | **0** | **0** | **PASS** |

Commands: `dotnet build src/DistributionDrawing.sln --configuration Release --no-restore`; each of the five formal test projects ran with `dotnet test <project> --configuration Release --no-restore --logger 'trx;LogFileName=<suite>.Candidate.trx' --results-directory TestResults/WP-GS-01-GS-AF-05`.

TRX: `TestResults/WP-GS-01-GS-AF-05/{Domain,Application,Infrastructure,Rendering.Wpf,Desktop}.Candidate.trx`; targeted evidence is `GS-AF-05.Targeted.trx`. Counters were parsed from TRX. No other formal test projects were found. Post-validation `git diff --exit-code` passed; the only untracked path was `TestResults/`; HEAD equalled `origin/wp-gs-01`; CurrentVersion remained Version9.

Regression covers same-result identity and freshness through GP/GAP add/remove/history, combined GAP+GP creation, GroundingPoint properties/layout, actual selection-delete wrappers, hidden-overlay/unavailable-EA preservation, upstream switch GS rejection after neutral grounding, release and successful electrical recompute, and mixed composites containing true topology changes remaining stale. Existing Seed and electrical tests also pass.

Historical evidence remains under `TestResults/WP-GS-01-Windows/`: previous candidate `9e16ffb3b998659eadf6a5710bf1d42997483728` passed 1,572 tests before these 11 additional regressions.

## GUI evidence and final-candidate rechecks

The user manually confirmed the following core behavior before the GS-AF-05 fix. These confirmations are recorded as prior evidence, not as final-candidate GUI PASS:

| Core case | Prior evidence | Final candidate |
| --- | --- | --- |
| Valid EA rejects GroundingPoint at Energized locations | USER-CONFIRMED PASS | RECHECK PENDING |
| Energized device Effective Grounding is blocked | USER-CONFIRMED PASS | RECHECK PENDING |
| Seed analysis reaching existing grounding is rejected | USER-CONFIRMED PASS | RECHECK PENDING |
| PoleSwitch sides are judged independently | USER-CONFIRMED PASS | RECHECK PENDING |
| A switch operation that reaches existing grounding is rejected | USER-CONFIRMED PASS | RECHECK PENDING |
| Simple blocking messages match the frozen requirements | USER-CONFIRMED PASS | RECHECK PENDING |

After candidate automation passed, Computer Use launch returned `Computer Use was not approved to use desktop`. No GUI actions or alternative UI automation route were used. The new lifecycle case failed before the fix; its final-candidate GUI result remains PENDING.

Required GS-AF-05 manual recheck on the newly built candidate:

1. Open a drawing with a real Seed and an open upstream switch. Explicitly analyze; verify both red energized and normal deenergized conductors are displayed.
2. Create GAP / 验电接地环 on an existing OHL Connection in the deenergized working area. **PASS criterion:** EA remains valid and red/normal overlay remains visible.
3. Create an allowed GroundingPoint on that GAP. **PASS criterion:** current EA is used for GS validation, creation succeeds, and EA/overlay remain valid and visible. Check corresponding Undo/Redo and allowed grounding removal too.
4. Attempt the upstream switch close that would energize the grounding location. **PASS criterion:** the existing GS blocking message rejects the operation; switch, grounding, history cursor, and overlay remain unchanged.

The remaining original checklist must also be completed/confirmed on the final candidate: no-valid-EA behavior; Energized Terminal/GAP rejection and Deenergized grounding; ordinary cabinet GroundSwitch; UpperLower/LowerLower; UpperIsolation GS Closed + Breaker Open and energized Breaker rejection; existing Effective Grounding upstream rejection; atomic Seed rejection; NoSeeds then new Seed requiring explicit analysis; Undo/Redo safety; release operations; simple messages/no panel/locator; Save/reopen; V9. Unspecified cases are **NOT RUN / PENDING**, not inferred from the user’s grouped confirmations.

## Separate issue discovered during regression construction

A mixed selection deleting a GAP together with its own OHL Connection has a pre-existing Undo defect: the line removal snapshot and explicit GAP removal both restore the same GAP, causing `GroundingAccessPoint ID ... is already in use`. Reproduction: select the GAP and its parent Connection, delete, then Undo. This is outside the grounding-only EA lifecycle change; it is recorded for separate resolution. Mixed topology classification is tested using independent GAP/Connection targets.

## Closure gate

WP-GS-01 remains OPEN. GUI rechecks and the remaining acceptance checklist are pending; the separate overlapping-delete Undo defect remains recorded. Closure Review conditions are **not met**.
