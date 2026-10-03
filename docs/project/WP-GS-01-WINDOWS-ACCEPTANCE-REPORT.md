# WP-GS-01 — Windows Acceptance Report

## State

- Governance: **OPEN / IMPLEMENTED / AUTOMATED ACCEPTANCE PASSED / WINDOWS GUI ACCEPTANCE PASSED / READY FOR CLOSURE REVIEW**. No CLOSED / ACCEPTED declaration.
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

## Windows GUI acceptance — PASS

The product owner reports personally completing the following core Windows GUI acceptance, including the prior WP-GS-01 core cases and the three GS-AF-05 rechecks. These are recorded as user-performed results; Computer Use automation was not used for this report.

| Case | Result | Observed behavior |
| --- | --- | --- |
| Valid EA; create GAP / 验电接地环 on a Deenergized OHL | **PASS** | Creation succeeded; EA remained valid; red/normal overlay stayed visible. |
| Create an allowed GroundingPoint on that GAP | **PASS** | Creation succeeded; EA remained valid; overlay remained visible. |
| With GroundingPoint present, attempt switch operation that would energize it | **PASS** | GS rejected the operation with the established blocking message; no hazardous switch state was entered; overlay remained normal. |
| Prior core WP-GS-01 GUI acceptance | **PASS — previously completed by product owner** | Energized GroundingPoint/effective grounding prevention, Seed-analysis conflict rejection, independent PoleSwitch-side judgment, rejection of switch operation that would energize existing grounding, and the frozen blocking-message behavior. |

**Combined Windows GUI Acceptance: PASS.** Automated and GUI acceptance gates are passed. WP-GS-01 is OPEN / READY FOR CLOSURE REVIEW; this report does not close or accept the Work Package.

## Separate Known Issue — outside WP-GS-01

A mixed selection deleting a GAP together with its own OHL Connection has a pre-existing Undo integrity defect: the line removal snapshot and explicit GAP removal both restore the same GAP, causing `GroundingAccessPoint ID ... is already in use`. Reproduction: select the GAP and its parent Connection, delete, then Undo. This is a **Known Issue in composite deletion / Undo integrity**, outside WP-GS-01. It has not been observed to bypass Grounding Safety or violate a frozen WP-GS-01 rule. Mixed topology classification is tested using independent GAP/Connection targets.

## Closure gate

WP-GS-01 is **OPEN / READY FOR CLOSURE REVIEW**. Automated and Windows GUI Acceptance have passed. Closure Review may begin; this report does not mark WP-GS-01 CLOSED / ACCEPTED.
