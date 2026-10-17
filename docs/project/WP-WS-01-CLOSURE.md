# WP-WS-01 — Final Closure Report

## Final accepted state and authority

- **WP-WS-01:** CLOSED / ACCEPTED — 2026-10-17.
- **Phase 6:** VERIFIED / ACCEPTED.
- **Closure Gate:** PASS; no outstanding STOP.
- **Windows automated:** PASSED.
- **Windows GUI acceptance:** PASSED within approved scope.
- **FormatVersion:** V10.
- **Branch:** `wp-ws-01`.
- **Accepted code baseline:** `2c8fa8291ff805cf434dc72b11ae8230e5ce16bc`.
- **Windows automated production-code evidence SHA:** `4f1603ff16c182b69cc42bdf8edd0a26675f811f`.

The product owner confirmed the final three GUI checks, approved the No-Ticket / Shared Scope acceptance-method dispositions, reviewed Closure Gate PASS, and explicitly authorized an independent docs-only closure commit and push to `origin/wp-ws-01` in this package's Windows acceptance thread on 2026-10-17. This report makes those decisions and their evidence boundaries durable in Git. It is not dependent on the ignored preparation artifacts for its contract, acceptance conclusions or disposition matrix.

The starting closure gate verified branch `wp-ws-01`, HEAD and tracking ref at `2c8fa8291ff805cf434dc72b11ae8230e5ce16bc`, ahead/behind `0/0`, clean working tree and clean `git diff --check`. A live remote query also returned that SHA. The closure changes only the five designated governance Markdown files; it changes no production, test, schema, resource or project file. No merge to main, release definition or successor package is included.

## Final business and architecture contract

```text
EA CurrentResult.Deenergized + EA Scenario.Seeds
    -> one 代入
    -> Confirmed WorkScope + WorkTicket linkage
    -> existing Analyzer once only when the complete WTA handoff is valid
```

1. EA is the sole energized-state authority. The current valid EA result supplies exactly the non-Earth Deenergized Terminal / ElectricalNode membership of the WorkScope, including isolated Deenergized points.
2. The user has already selected switches and EA directions in `EnergizationScenario.Seeds`. Only those selected devices supply WorkScope selected-device facts. All-graph E/D transitions must not add devices the user never chose.
3. Existing conducting topology groups EA membership into internal, nonoverlapping Regions; it does not run a second energized-state analysis. The model retains 1..N Regions and 0..N structural Boundaries for computation and persistence.
4. Work Range displays the EA status, readonly selected devices and task/location, with one 代入 action. It requires no second switch, Region or Boundary selection/review. Old automatic Region/Boundary candidate-confirmation interaction requirements are superseded by the approved inverse-selection correction.
5. `PrepareFromAnalysis` reads and validates the current EA result and selection at apply time. Missing, failed or stale EA cannot be applied. One 代入 confirms the WorkScope and WorkTicket handoff as one atomic command/history entry.
6. Execute, Undo and Redo replay complete Before/After snapshots and stable IDs. They do not rerun EA or Analyzer. Reapplication preserves independent user facts, GroundingPointIds, legacy WorkScopeItems and manual draft content according to the accepted stale-derived-state semantics.
7. EA Seed.Side is an EA direction, not a WTA work-side cast. A selected device's work side is resolved through actual topology, EA state and the existing WTA resolver. No direction is guessed or substituted.
8. A valid WorkScope remains confirmable when the selected isolation set cannot be represented by WTA. An unresolved selected side persists as the existing `BoundarySide.Unknown`; the safe handoff is empty, no partial isolation set is emitted, and incomplete Analyzer execution is blocked. The GUI states that the scope is confirmed and explains the handoff limitation.
9. Complete handoff reuses the existing WTA resolver, whole-set validation and Analyzer. Prepare runs Analyzer exactly once; replay uses the captured result. Legal zero-boundary handoff may yield NeedsInput without inventing a boundary or weakening the legacy manual-range guard.
10. GroundingPoint authority remains with GS; WorkScope does not own GroundingPoint membership. Existing EA propagation, Seed eligibility, GS safety and WTA core/6.x rule contracts remain authoritative.
11. V10 persistence preserves confirmed scope membership, selected facts, ticket linkage and independent content. Transient EA CurrentResult/Candidate is not persisted or required to reopen a confirmed scope. V8/V9 migration remains unsupported under this development-format policy.

The [Implementation Plan](WP-WS-01-IMPLEMENTATION-PLAN.md) records Requirements Freeze and Architecture / Data Model Freeze as complete. The plan's current-contract preface and [Correction](WP-WS-01-WORKSCOPE-CORRECTION.md) are the repository's available durable contracts; no separate WP-WS-01 requirement/architecture freeze file is asserted here. Historical phase acceptance facts remain valid evidence without restoring their superseded interaction requirements.

## Phase 1–6 implementation and correction history

| Phase | Accepted historical SHA / checkpoint | Windows evidence and disposition |
|---|---|---|
| 1 — Domain + V10 | `47349b5c369c7cefb9e9f5a9b233b6f36ea5f6a3` | 1678/1678; immutable multi-Region snapshots, structural Boundaries, reference integrity and V10 persistence accepted |
| 2 — EA Candidate | `4f50bc134ab1f556340ecf0225594edaa2117e5e` | 1697/1697; EA-derived membership/projector accepted; later selection/review behavior superseded by the correction |
| 3 — Confirmation + CommandStack | `2444fdf8a7e4e19440a3054fcc3fa0bcfae9c4ab` | 1717/1717; stable identity, single history, preservation, atomic Undo/Redo and shared-scope isolation accepted |
| 4 — WTA projection | `3f4a5fea1827f22abee39c18dda980357368dd91` | 1738/1738; A/B/C mapping, whole-set validation, no fabricated side/partial safe handoff accepted |
| 5 — WorkTicket handoff | `10cb9187c37abe7dab55bed012b008030c952823` | 1755/1755; Analyzer once, snapshot replay, independent-content preservation and V10 handoff persistence accepted |
| 6 — Windows GUI and correction | Correction started from `2419eb4beae317552180553d25812b281983cd4b`; selected-side/overlay follow-up at candidate `33f66d9c87612599f9b30c949694f8efc8ebed89`; import fix `4f1603ff16c182b69cc42bdf8edd0a26675f811f`; governance baseline `2c8fa8291ff805cf434dc72b11ae8230e5ce16bc` | Final fixed-code Windows 1779/1779; current inverse-selection contract and owner-confirmed GUI accepted; Phase 6 VERIFIED / ACCEPTED |

All historical Windows totals above have 0 failed and 0 skipped. The historical macOS correction checks and unavailable WPF/Desktop runtime execution remain labeled as such in the correction document; they are not substituted for the later full Windows evidence.

The first correction removed the former all-switch transition scan as selection authority: a 24-transition drawing with two EA Seeds must not silently add 22 devices. It uses exact EA Deenergized membership and selected Seeds, confirms even when a selected side is unrepresentable, and commits through one command. The selected-side follow-up uses resolver terminal matching for integrated FirstTerminal/SecondTerminal devices and actual Pole connections. Work Range suppresses only the work-ticket overlay; accepted EA energized-state styling remains controlled by its original display preference.

### Actual project import failure and fix

At baseline `921641011e0318772fab32e683ec8292af62a9b7`, a copy of the owner's actual V10 project reproduced `ConfirmationRejected / InvalidMaterializedWorkScope` with complete EA for both its two-cabinet-Seed case and saved three-Seed scenario. `WorkScope.Create` succeeded; the earliest failure was Domain `ValidateBoundaryConnection`, before isolation projection.

The materializer had copied the WTA interval-external cable ConnectionId into a WorkScope Boundary whose terminal belonged to an internal cabinet switch. That cable was not directly incident to the selected switch terminal, so Domain reference validation correctly rejected the pair. Fix `4f1603ff16c182b69cc42bdf8edd0a26675f811f` retains the cabinet selected switch/work terminal in WorkScope and lets the existing WTA adapter resolve its external cable. Direct Pole connections remain captured. Reference safety checks, EA/GS/WTA algorithms and FormatVersion were not weakened.

Four new Application cases reproduced the pre-fix failure and passed after the fix; additional connected load-switch/integrated-feeder tests cover membership, selected-only devices, safe limitation handling, snapshots, diagnostics and V10 persistence. Programmatic preparation on the actual project copy then returned `ConfirmedHandoffNeedsInput / Projection Complete` without confirmation failure for both Seed sets. Separately, the product owner confirmed actual Windows GUI EA → Work Range → 代入 PASS on 2026-10-11; the prior inconsistency message did not recur. The historical standalone Pole reproduction was not asserted to be the cause of this actual-project failure.

## Final Windows build and full automated evidence

The evidence belongs to production-code SHA **`4f1603ff16c182b69cc42bdf8edd0a26675f811f`**:

| Suite | Passed | Total | Failed | Skipped |
|---|---:|---:|---:|---:|
| Domain | 236 | 236 | 0 | 0 |
| Application | 321 | 321 | 0 | 0 |
| Infrastructure | 154 | 154 | 0 | 0 |
| Rendering.Wpf | 674 | 674 | 0 | 0 |
| Desktop | 394 | 394 | 0 | 0 |
| **Total** | **1779** | **1779** | **0** | **0** |

Full solution Release build: **PASSED, 0 errors, 22 nullable warnings**. The import-fix acceptance record also records PDB document-checksum matching for all seven submitted production/test files, including the shared fixture in both test assemblies.

`git diff --name-only 4f1603ff16c182b69cc42bdf8edd0a26675f811f 2c8fa8291ff805cf434dc72b11ae8230e5ce16bc` contains only STATUS, ROADMAP, Implementation Plan and Correction governance documents. Accepted code baseline `2c8fa829` therefore has unchanged production and test code relative to the fixed-code evidence. **The 1779 tests were not rerun at docs-only HEAD `2c8fa829` or for this docs-only closure.** Documentation validation consists of scope, consistency, links, SHA/count and Git whitespace/staging checks.

## Product-owner-confirmed actual Windows GUI evidence

The product owner's explicit acceptance statements in this WP-WS-01 thread are the human evidence source. Prior confirmations were restated during final closure review; the final combined session was reported as all passed on 2026-10-17 and its three results were explicitly reconfirmed in the final docs-only closure authorization. No screenshots, raw traces or live invocation counts are invented from that attestation.

| Actual GUI scenario | Result | Evidence scope |
|---|---|---|
| EA → Work Range → one 代入 | PASS | Actual V10 engineering-project scene; initial repaired import accepted 2026-10-11 |
| Undo / Redo | PASS | Owner-observed scope/ticket snapshot restoration |
| Save / Reopen | PASS | Actual engineering-project visible behavior; no field-by-field business-file comparison claimed |
| Switch change → new EA → reapply | PASS | Owner-observed reapplication using current analysis |
| Stale EA refusal | PASS | Final nonempty-content session; stale analysis cannot be applied |
| Nonempty manual content protection | PASS | Owner-observed content preservation after reanalysis/apply and Undo/Redo |
| A — WTA Unrepresentable diagnostic | PASS | Final generated V10 fixture; visible successful scope confirmation and accurate device-specific limitation, without mislabeling it as confirmation failure |
| B — Work Range red overlay | PASS | Final fixture; ticket red-box display, Work Range suppression, exit restoration/clear and coexistence with normal EA coloring |
| C — GroundingPoint G1 coexistence | PASS | Final fixture; actual G1 symbol/number/leader and selected ticket association remain, including visible Save/Reopen observation |

The final A/B/C session used a valid V10 project generated through existing constructors and official persistence from `tests/Shared/WorkScopeCorrectionFixture.cs` (`ConnectedRing`, unresolved second selected integrated device). It was a test fixture, not the owner's original engineering file. Input SHA-256 was `23F20C618C849472E724659F4B6DB119EC4EF363A0BCDE30458A7BB8E0AE7837`. The first selected load switch resolves to Line; the second integrated isolator retains Unknown. G1 (`a6c5b5c9-ea43-4097-851a-5df49de0b585`) is on the first feeder's Deenergized cable terminal; a confirmed test RetainedLive fact explicitly targets the second selected device. The saved fixture passed Desktop scene loading and the actual grounding-safety EA submission path before human use.

Fixture technical preparation established Complete EA, executable confirmation, Line+Unknown selected facts, empty safe handoff, absent new Analysis/Draft and zero preparation Analyzer calls. The scene had no build diagnostics, a physical G1 and one explicit RetainedLive Firebrick rectangle. These are technical preflight facts; actual GUI confirmation supplies only visible behavior. Internal linkage/ID, empty-set and Analyzer semantics remain attributed to formal automated assertions and the separate fixture preflight, rather than inferred from the GUI.

The red overlay contract is precise: EA's energized electrical geometry may remain red when EA display is requested. Work Range suppresses the work-ticket overlay; outside Work Range, a requested confirmed RetainedLive warning may correctly reappear. Hiding ticket-specific GroundingPoint highlighting does not remove the physical grounding symbol. The accepted GUI result does not prohibit all red geometry or erase legitimate WorkTicket safety warnings.

## Formal legacy 14-category acceptance disposition

The correction records the historical remaining GUI categories in prose, not a numbered 14-row original file. The matrix below formally decomposes that list for closure and maps it to the revised contract. It does not claim recovery of an absent numbered artifact or execution of 14/14 dedicated GUI tests. The product owner's approved final closure adopts this disposition. Internal topology facts continue to use the formal Windows automation; superseded Region/Boundary selection interactions are not reinstated.

| Historical category | Current contract and technical acceptance | Dedicated GUI disposition |
|---|---|---|
| 1. WTA conversion-limited handoff | Automated PASS: valid scope retained, Unknown/no guess, no partial safe set or incomplete Analyzer | PASS, final A |
| 2. Multiple Regions | Automated PASS: all disconnected Deenergized membership preserved; user Region review is superseded | NOT RUN / NON-BLOCKING for a topology-specific GUI |
| 3. Multiple Boundaries | Automated PASS: only EA-selected-device facts; all-graph extra-device review is superseded | NOT RUN / NON-BLOCKING for a topology-specific GUI |
| 4. Zero Boundary | Automated PASS: legal structural zero-boundary confirmation, projection, handoff and persistence | NOT RUN / NON-BLOCKING; final A's empty isolation output is not a claimed zero-structural-boundary GUI test |
| 5. Pole direction/limitation | Automated PASS: actual connection/terminal mapping and safe unresolved handling | NOT RUN / NON-BLOCKING for a Pole-specific GUI; final A accepts the visible limitation workflow |
| 6. CustomerStation limitation | Automated PASS: supported contract, no substitution or fabricated side | NOT RUN / NON-BLOCKING for this special topology |
| 7. WTA set-level rejection | Automated PASS: reject whole safe set, no partial handoff or Analyzer | NOT RUN / NON-BLOCKING for the specific set-rejection topology; not relabeled as final A GUI execution |
| 8. Re-confirmation | Automated PASS plus actual switch-change/new-EA/reapply confirmation | PASS |
| 9. Stale EA | Automated PASS plus actual rejection confirmation | PASS |
| 10. GroundingPoint coexistence | Automated PASS for GroundingPointIds; actual G1 visible/persistence confirmation | PASS, final C |
| 11. Save/Open | Automated PASS for V10 fields/references; actual visible Save/Reopen confirmation | PASS for observed GUI; business-file per-field comparison NOT RUN |
| 12. Undo/Redo | Automated PASS for atomicity, IDs and no replay analysis; actual visible restoration confirmed | PASS for observed GUI; GUI invocation counts NOT RUN |
| 13. No-Ticket First Import | Automated Acceptance PASS under explicit owner approval | Dedicated GUI NOT RUN / NON-BLOCKING |
| 14. Shared Scope Isolation | Automated Acceptance PASS under explicit owner approval | Dedicated GUI NOT RUN / NON-BLOCKING |

Work Range red-overlay GUI is separately accepted as final B. Analyzer exactly-once and no replay calls have automated PASS; actual GUI call counts remain NOT RUN.

### Approved No-Ticket and Shared Scope acceptance-method adjustment

On 2026-10-17 the product owner explicitly approved formal Windows automated evidence as the专项 technical acceptance for both items, with final labels **Automated PASS / Dedicated GUI NOT RUN / NON-BLOCKING**. The final docs-only closure authorization reaffirmed these labels. This changes the evidence method, not either business behavior, architecture or the V10 data model.

The plan defines no-ticket creation and shared-scope isolation and the overall Phase 6 GUI gate; it contains no requirement that each of these two items must be independently operated by the owner as a non-substitutable dedicated GUI test. The correction previously listed them as pending GUI. Their pending disposition is now resolved by explicit owner approval rather than silently inferred from automated results.

The formal suites contain `Confirm_WithoutTargetTicketCreatesStableTicketAndScopeWithinOneHistoryEntry`, `Handoff_CompleteConfirmUsesOneHistoryEntryAndRedoDoesNotReanalyze` and `Apply_ConnectedCabinetSwitchesConfirmAndReplayOneCompleteSnapshot`: creation/linkage, complete snapshot, one history entry, full removal/restoration, stable IDs and Analyzer replay counts. `Confirm_SharedScopeRelinksOnlyCurrentTicketAndRedoReusesNewId` and `Handoff_SharedScopeReconfirmUpdatesOnlyTargetTicketAndUndoRedoPreservesOtherTicket` establish A → Y while B and original X remain unchanged, including replay and Analyzer counts. These existing tests are in `tests/DistributionDrawing.Desktop.Tests`; they were not changed for closure.

Dedicated no-ticket navigation/selection and shared-ticket dropdown binding were not separately observed in GUI. The core navigation GUI has owner acceptance, while exact identity and state-isolation facts use automation. The GUI does not expose all scope IDs, so identity assertions remain technical evidence even when a screen looks correct.

## Remaining evidence boundaries, known issues and exclusions

| Item | Final disposition and basis |
|---|---|
| Actual engineering-project Save/Reopen file-level per-field comparison | NOT RUN / NON-BLOCKING. V10 automated round trips and observed GUI Save/Reopen satisfy the approved contract; no independent mandatory real-file per-field collection is specified. No data-equality result is invented. |
| Actual GUI EA/Analyzer call counts | NOT RUN / NON-BLOCKING. Instrumented automated command tests establish the required call/replay behavior; no live count is inferred from a disabled button or absence of generated text. |
| Special topology-specific GUI scenes | Some NOT RUN / NON-BLOCKING under the current-contract mapping above; formal automated PASS is not relabeled GUI PASS. |
| Build nullable warnings | Historical 22 warnings, non-blocking; build has 0 errors. No cleanup of unrelated code is included. |
| GAP/OHL composite deletion Undo issue | Existing accepted non-blocking known issue outside WP-WS-01 / GS closure boundary; neither repaired nor reopened here. |
| V8/V9 migration | Unsupported under the frozen V10 policy; not an unrecorded regression or newly promised capability. |

No unresolved WP-WS-01 P0/P1 production defect was identified in the reviewed evidence or reported in the owner's final acceptance. This is scoped acceptance, not a guarantee that every possible defect has been excluded.

New/deeper ticket 6.x rules, Rule Library, a broad Needs Reanalysis framework, broad WorkTicket UI redesign, Device Interlock, EA-aware export and unrelated technical debt remain outside the package. Previously accepted EA, GS and WTA packages retain their existing states and contracts.

## Final Closure Gate

| Gate | Final result |
|---|---|
| Correct Git baseline, clean tree and no local-only commits before closure | PASS; HEAD/tracking/live remote `2c8fa829`, ahead/behind 0/0 |
| Current product contract has no unresolved material conflict | PASS; correction supersedes the old selection interaction |
| No known unresolved in-scope P0/P1 defect | PASS within the reviewed evidence and owner reports |
| Valid full Windows automated evidence | PASS; fixed production-code SHA `4f1603ff`, 1779/1779, later delta docs-only |
| Actual current GUI workflow and final A/B/C acceptance | PASS, product-owner confirmations |
| Formal legacy checklist disposition | PASS; matrix and explicit No-Ticket / Shared Scope approval preserve evidence boundaries |
| Accepted EA / GS / WTA contracts | PASS within existing full regression, accepted scope and confirmed GUI behavior |
| V10 persistence | PASS within automated field/reference round trips and observed GUI acceptance; stated unmeasured boundaries retained |
| Outstanding STOP | NONE; final GUI evidence gaps resolved, acceptance-method adjustments approved |

**WP-WS-01 = CLOSED / ACCEPTED. Phase 6 = VERIFIED / ACCEPTED. Closure Gate = PASS. Windows automated = PASSED. Windows GUI acceptance = PASSED within approved scope. FormatVersion = V10.**

## Governance and next-package boundary

The independent commit is limited to this closure report plus STATUS, ROADMAP, Implementation Plan and Correction. Commit message: `docs: close WP-WS-01 after final acceptance`. The owner authorized pushing that commit to `origin/wp-ws-01`. WORKFLOW remains unchanged.

WP-WS-01's closure prerequisite for future planning is satisfied. No next Work Package is defined, opened or implementation-authorized by this closure. Any successor requires its own requirement/scope decision and explicit package/phase authorization under WORKFLOW. Main remains unmerged and other package states are unchanged.
