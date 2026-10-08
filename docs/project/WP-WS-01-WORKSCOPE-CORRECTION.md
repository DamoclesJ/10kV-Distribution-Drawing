# WP-WS-01 — WorkScope inverse-selection correction

## Current status and baseline

- WP-WS-01: OPEN / IN PROGRESS.
- Phase 6: CORRECTION / CANDIDATE; implementation prepared, candidate commit/push pending the Windows test gate or explicit candidate authorization.
- Starting branch: `wp-ws-01`; HEAD, origin tracking ref and live remote: `2419eb4beae317552180553d25812b281983cd4b`; starting working tree clean.
- FormatVersion: V10. No DTO, enum-value or serialized schema change; V8/V9 migration remains unsupported.
- Windows automated verification: PENDING. Windows GUI acceptance: PENDING / NOT RUN for this correction.

The product owner's inverse-selection decision supersedes conflicting Phase 2–5 and original Phase 6 contracts. Prior accepted commits remain unchanged historical evidence. All-graph E/D transitions, reviewed Region/Boundary lists, legacy ElectricalRange rejection, and mandatory deterministic materialization before scope confirmation are no longer the current requirements.

## Revised product contract

WorkScope membership is exactly the non-Earth Deenergized Terminal/Node membership of current valid `EnergizationAnalysisState.CurrentResult`. Existing conducting topology only groups those facts into internal Regions; it does not recompute electrical state. Isolated Deenergized nodes are retained.

User-selected devices come only from `EnergizationScenario.Seeds`, supporting 1..N selections. Candidate captures those immutable Seeds and restricts E/D transition facts to their devices. Freshness, result provenance and the existing EA boundary policy validate the input without changing EA algorithms or selection rules. Seed.Side is an EA direction; actual topology and EA facts determine the work side. There is no cast or guessed reversal.

Work Range shows EA status, readonly selected devices, work content/location and 代入/取消. There is no Region/Boundary review or second selection. `PrepareFromAnalysis` reads the latest valid EA and selection at apply time, prepares confirmation and the existing Phase 5 handoff, then one `ConfirmWorkScopeCommand` creates one history entry. Navigation does not commit another ticket edit. Stable WorkScopeId/TicketId and complete Before/After snapshots are replayed on Undo/Redo without rerunning EA or Analyzer.

Only selected-device facts reach the existing WTA resolver/whole-set validation. Complete handoff calls the existing Analyzer exactly once. An unavailable conversion preserves the selected device as `WorkScopeBoundary.Side = Unknown`, using the already defined enum and unchanged V10 fields. This expressly revises the Domain constructor's former rejection of Unknown. It produces C-class `UnresolvedSelectedWorkSide`; no fabricated boundary, partial handoff, substituted graph transition or incomplete Analyzer execution is allowed. Confirmed scope/linkage and independent UserFacts, GroundingPointIds and WorkScopeItems remain intact; derived state follows existing stale semantics. WorkTicket limitation remains visible after Save/Open without EA CurrentResult, and incomplete manual analysis is blocked.

EA propagation, EA Seed eligibility, GS authority/safety, WTA core rules, and ticket 6.x rules are unchanged.

## Original behavior and diagnostic evidence

Original `WorkScopeCandidateProjector.FindBoundaries` traversed every SwitchDevice in the drawing. A production RingCabinet topology with 24 open feeders and two Bus-side Seeds yields 24 E/D transitions on the original baseline, because both Seeds energize the common bus while all feeder sides remain Deenergized. The former projector treated those transitions as selection, adding 22 devices the user had not selected.

An executable reproduction against the archived exact baseline `2419eb4` also established:

```text
EA = Complete
WorkScopeHandoffStatus = ConfirmationRejected
WorkScopeConfirmationFailureCode = BoundaryMaterializationAmbiguous
Message = Pole Boundary 的 Deenergized side 没有唯一 Connection。
```

The accepted EA policy can resolve a selected Pole switch's energized side while its isolated Deenergized terminal has no unique downstream Connection. Original confirmation rejected that topology before allowing the scope to persist. The correction confirms it with an explicit C-class handoff limitation. The original GUI also collapsed several typed failures into generic text; the revised UI records status, confirmation code and candidate/projection diagnostics and displays specific messages.

The original user project/log has not been supplied. This is a verified original-code reproduction, not an assertion that the user's particular GUI failure has the same code. That project's exact diagnostic remains to be checked.

## Verification evidence

| Check | Result |
|---|---|
| Domain full Release suite | 236/236 passed, 0 failed, 0 skipped |
| Application full Release suite | 313/313 passed, 0 failed, 0 skipped |
| Infrastructure full Release suite | 153/153 passed, 0 failed, 0 skipped |
| Portable actual CommandStack/ConfirmWorkScopeCommand and GUI source checks | 13/13 passed, 0 failed, 0 skipped |
| Rendering.Wpf full suite | Compiled; test host aborted before execution: missing Microsoft.WindowsDesktop.App on macOS |
| Desktop full suite | Compiled; test host aborted before execution: missing Microsoft.WindowsDesktop.App on macOS |
| Full solution Release build | 0 errors; 3 NU1900 NuGet cache-access warnings |

TRX evidence is in `/tmp/wp-ws-01-correction-tests/` with `wp-ws-01-correction-{domain,application,infrastructure,command-ui,rendering,desktop}.trx` filenames. Supplemental execution uses linked actual production command/history code and the new tests; it does not count as Desktop full execution or native GUI acceptance. The available Windows remote device was offline; no Windows result is claimed.

Coverage includes two selected devices versus 24 transitions (also 1/5 selections), exact Deenergized membership, Seed Bus to actual work-side Line conversion, isolated node membership, single apply/linkage/history, stale EA rejection and latest-result use after reanalysis, stable-ID Undo/Redo without analysis rerun, preserved independent ticket facts, unavailable selected sides without substitution/Analyzer, original Pole rejection reproduction, and V10 Save/Open with complete and Unknown boundaries.

## Windows acceptance gate

The requested all-suite PASS gate is not yet satisfied; do not represent unavailable Windows execution as PASS. A committed/pushed fixed candidate is required for Windows validation under the repository workflow, so publishing before Windows tests requires explicit candidate authorization under this request. No previous accepted commit is amended and no merge to main is authorized.

First run only the real user-judged Windows scene: EA selects two switches → EA analysis → Work Range → one 代入 → correct WorkTicket. Until the user accepts that scene, the remaining 14 GUI scenarios stay suspended and WP-WS-01 remains OPEN.
