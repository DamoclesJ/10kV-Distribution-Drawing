# WP-WS-01 — EA-derived WorkScope & Confirmation

## Governance and baseline

- **Status:** OPEN / IN PROGRESS
- **Requirements Freeze:** COMPLETE
- **Architecture / Data Model Freeze:** COMPLETE
- **Implementation Plan Freeze:** v1.0 APPROVED
- **STOP-01 through STOP-08:** NONE
- **Implementation branch:** `wp-ws-01`
- **Implementation base:** `main = origin/main = 70c321c1faa6c4c67c01d4d942d8ad5e596bd15d`
- **FormatVersion at package opening:** V9
- **Phase 1:** VERIFIED / ACCEPTED at `47349b5c369c7cefb9e9f5a9b233b6f36ea5f6a3`
- **Phase 2:** VERIFIED / ACCEPTED at `4f50bc134ab1f556340ecf0225594edaa2117e5e`; Windows automated verification PASSED (1697/1697, 0 failed, 0 skipped), full solution build 0 errors
- **Phase 3:** VERIFIED / ACCEPTED at `2444fdf8a7e4e19440a3054fcc3fa0bcfae9c4ab`; Windows automated verification PASSED (1717/1717, 0 failed, 0 skipped)

This document records the approved package boundary. Opening the package does not start an implementation phase.

## Phase 1 acceptance

Phase 1 — Domain + V10 is VERIFIED / ACCEPTED at `47349b5c369c7cefb9e9f5a9b233b6f36ea5f6a3`. The accepted model includes immutable multi-Region snapshots with Terminal/Node membership, 0..N structural typed Boundaries, optional Description, complete command snapshots, V10-only persistence, and Domain referential-integrity protection. The existing WTA `BoundarySide` contract is shared from Domain without changing its values. WorkScope does not own GroundingPoint; WorkTicket references and Equipment semantics remain intact.

- Accepted SHA: `47349b5c369c7cefb9e9f5a9b233b6f36ea5f6a3`.
- Windows automated verification: PASSED — Domain 236/236, Application 243/243, Infrastructure 151/151, Rendering.Wpf 674/674, Desktop 374/374; total 1678/1678, 0 failed, 0 skipped.
- Full solution build: 0 errors.
- Current FormatVersion: V10. V8/V9 migration is not supported. Windows GUI acceptance is NOT RUN and remains Phase 6; it is not part of Phase 1 acceptance.
- STOP-P1-01 through STOP-P1-06: NONE.
- WP-WS-01 stays OPEN / IN PROGRESS. Phase 3 — Confirmation + CommandStack is NOT STARTED.

## Phase 2 acceptance

Phase 2 — EA → WorkScope Candidate Projector is VERIFIED / ACCEPTED at `4f50bc134ab1f556340ecf0225594edaa2117e5e`. WP-WS-01 remains OPEN / IN PROGRESS. Phase 3 — Confirmation + CommandStack is NOT STARTED.

Windows automated verification PASSED on the accepted candidate:

- Domain: 236/236.
- Application: 262/262.
- Infrastructure: 151/151.
- Rendering.Wpf: 674/674.
- Desktop: 374/374.
- Total: 1697/1697 passed, 0 failed, 0 skipped.
- Full solution build: 0 errors.

The accepted `WorkScopeCandidate` contract is Application transient derived state. It comes only from the current valid `EnergizationAnalysisState.CurrentResult` and current drawing topology. It is not persisted, does not create a formal WorkScope, does not modify WorkTicket data, does not use CommandStack, and does not invoke WorkTicketAnalyzer. Regions are the connected components derived from EA Deenergized state and current conducting topology; the model supports one or more disconnected Regions, isolated terminals, branch topology, multi-Seed analysis, a fully Deenergized business graph, and an empty business candidate. Earth Terminals and ElectricalNodes are excluded, and GroundSwitch-to-Earth does not form an ordinary WorkScope Boundary. A Candidate Boundary is derived from an open, non-conducting SwitchDevice whose sides transition between Energized and Deenergized; it is not inferred by taking the inverse of `ConductingEdges`.

Structurally equivalent Candidates are deterministic across Seed ordering, dictionary/hash iteration, device and connection creation order, and UI selection. Boundary facts retain switch identity, kind and installation, Energized and Deenergized terminal identities, parent/pole identity, and related connection identities. The projector validates current EA point identities and conducting-edge identity against the current drawing, and does not mutate WorkScope, WorkTicket, or EA state.

Windows acceptance evidence verified:

- EA authority and stale / failed / no-seed input gates.
- Earth exclusion and GroundSwitch-to-Earth exclusion.
- Disconnected components, isolated Deenergized terminals, multi-Seed analysis, whole-business-graph Deenergized state, and empty business Candidate.
- One Deenergized Region surrounded by multiple Boundaries.
- CustomerStation single-feeder projection and dual-feeder isolation.
- Device creation-order determinism and Candidate read-only behavior.
- Phase 1 regression and EA / GS / WTA regression.

The final Application suite includes the three completed acceptance coverage cases: one Region with multiple Boundaries, CustomerStation single feeder, and device creation-order determinism. `FormatVersion` remains V10; V8/V9 migration is not supported. Phase 1 and Phase 2 acceptance do not include Windows GUI acceptance, which remains NOT RUN and is reserved for Phase 6. STOP-P2-01 through STOP-P2-08 remain NONE. No Phase 3 work is authorized by this acceptance record.

## Phase 3 acceptance — Confirmation + CommandStack

**Status: VERIFIED / ACCEPTED.** Accepted candidate: `2444fdf8a7e4e19440a3054fcc3fa0bcfae9c4ab`. Phase 3 confirms a persisted WorkScope snapshot only from current valid EA through the Phase 2 Candidate. This acceptance does not start Phase 4. `FormatVersion` remains V10; no persistence schema change was made.

### Confirmation authority and freshness

- The confirmation authority is the current valid `EnergizationAnalysisState.CurrentResult` projected to a transient WorkScope Candidate. Manual legacy Boundary A/B selection is not a fallback.
- The planner reprojects at preparation time and allows confirmation only when the reviewed and current Candidates are structurally equivalent.
- No-current, stale, failed, no-seed, empty Candidate, reviewed/current mismatch, blocking diagnostic, and ambiguous ticket/scope states are rejected.
- Rejection does not mutate Domain WorkScope state, WorkTicket state, or CommandStack history.

### Candidate materialization and linkage

- Candidate Region `TerminalIds` and `ElectricalNodeIds` are materialized into persisted WorkScope Regions.
- Candidate Boundaries are materialized as Domain `WorkScopeBoundary` values, not WTA `IsolationBoundary` values.
- The accepted cases include multi-Region, zero Boundary, multiple Boundaries, RingCabinet, Pole, and CustomerStation.
- The normal confirmed linkage is one WorkTicket to one WorkScope (`WorkScopeIds.Count = 1`), with the WorkScope containing 1..N Regions.
- First confirmation supports an existing ticket with no scope and the no-ticket case; no-ticket creation captures TicketId, WorkScopeId, and linkage in the same command.

### Re-confirmation and ticket preservation

- Exclusive re-confirmation keeps the existing WorkScopeId, replaces its Regions/Boundaries snapshot, preserves Description, and preserves all other ticket facts.
- Shared re-confirmation forks a new WorkScope Y for Ticket A while Ticket B and shared WorkScope X remain unchanged. Undo restores A → X and removes Y; Redo restores A → the same Y ID.
- `WorkScopeIds.Count > 1` is rejected; Phase 3 does not guess, merge, or clear existing scope references.
- Phase 3 changes only `WorkScopeIds` and the corresponding Domain WorkScope aggregate. It preserves `IsolationBoundaries`, `WorkScopeItems`, `GroundingPointIds`, Task, Analysis, Draft, UserFacts, rule/phrase versions, analyzed fingerprint, and other existing ticket facts.

### Atomic command, Undo/Redo, and EA impact

- One Confirm is one atomic `ConfirmWorkScopeCommand` and exactly one CommandStack history entry.
- The command captures complete Before/After snapshots. Undo/Redo replay those snapshots and stable TicketId/WorkScopeId identities; they do not rerun EA, reproject Candidate, or regenerate identifiers.
- Injected partial Execute, Undo, and Redo failures restore the corresponding full Domain/Ticket state without partial mutation.
- `ConfirmWorkScopeCommand.AffectsEnergization = false`. Windows runtime acceptance verified that CurrentResult remains current and the same instance through Confirm/Undo/Redo, with no EA `Changed` event.

### Phase boundary and exclusions

Phase 3 does not implement automatic IsolationBoundary projection, WTA boundary resolver integration, Analyzer invocation, Analysis/Draft/Fingerprint regeneration, WorkTicket navigation, or 6.x rule changes. These remain outside this acceptance. Phase 4 — WorkScopeBoundary → IsolationBoundary Projection is **NOT STARTED**. Windows GUI acceptance is **NOT RUN** and remains Phase 6.

### Windows acceptance evidence

- Domain: 236/236.
- Application: 274/274.
- Infrastructure: 151/151.
- Rendering.Wpf: 674/674.
- Desktop: 382/382.
- Total: 1717/1717 passed, 0 failed, 0 skipped.
- Full solution Release build: 0 errors, 22 nullable warnings.
- Freshness, first confirmation, no-ticket creation, exclusive/shared re-confirmation, multiple-scope rejection, Candidate materialization, ticket preservation, one history entry, failure atomicity, `AffectsEnergization = false`, snapshot Redo, and Phase 1 / Phase 2 / EA / GS / WTA regression coverage: **PASSED**.
- STOP-P3-01 through STOP-P3-08: NONE.

## Phase 4 — WorkScopeBoundary → WTA IsolationBoundary Projection

**Status: IMPLEMENTED / CANDIDATE.** This phase is a read-only Application projection from one persisted Confirmed WorkScope plus current persisted drawing topology. The WorkScope snapshot is authoritative; the projector does not read EA `CurrentResult`, WorkTicket state, CommandStack, or Analyzer, and it does not mutate production state. `FormatVersion` remains V10.

The projection reports `Complete`, `Unrepresentable`, or `Invalid`, per-boundary A/B/C/Invalid classifications, stable diagnostic codes, deterministic ordering, normalized WTA outputs with source attribution, and an empty safe handoff collection unless the whole result is Complete. Zero-boundary WorkScopes produce Complete with an empty output. Any C-class boundary makes the whole result Unrepresentable; no guessed WTA boundary is emitted for Pole Equal/Unresolved directions or CustomerStation incoming isolators. GroundSwitch-to-Earth is rejected as grounding authority, and unsupported device boundaries do not invent isolation devices. Existing WTA `WorkTicketRangeSetup.TryResolve` and `FirstKindRulePack.BoundaryIssue` validate representable mappings.

macOS verification passed: Domain 236/236, Application 288/288, Infrastructure 151/151; full solution Release build completed with 0 errors. Windows `Rendering.Wpf` and `Desktop` full suites and fixed-SHA Windows automated verification remain PENDING. Windows GUI acceptance remains NOT RUN and belongs to Phase 6. `STOP-P4-01` through `STOP-P4-08`: NONE. Phase 5 — WorkTicket Handoff is NOT STARTED.

## Goal and normal flow

```text
Drawing → Seed + Seed Side → planned SwitchState → EA
        → WorkScope Candidate → User Confirm → Confirmed WorkScope
        → WorkTicket → existing WorkTicketAnalyzer exactly once
```

After EA completes, the normal flow does not ask the user to manually select Boundary A / Boundary B again.

## Frozen architecture and data constraints

### Confirmed WorkScope

- A Confirmed WorkScope is the persisted snapshot explicitly confirmed by the user from the current valid EA Result.
- It supports 1..N Regions. Each Region represents one Deenergized connected component and stores Terminal / ElectricalNode membership.
- Region membership does not overlap. A WorkScope may contain multiple disconnected Regions.
- It supports 0..N Boundaries.
- `StartBoundary + EndBoundary` is not the formal WorkScope authority. Device, Cable, and OHL membership is projection, not duplicate authority.
- Description is optional metadata. WorkScope does not own GroundingPoint references.
- Later EA or topology-state changes do not silently regenerate an already confirmed snapshot.

### Candidate and topology

- Candidate is EA-derived, transient, and never persisted.
- Candidate uses the existing electrical topology and the current valid EA point-state labels. It does not create a second electrical topology or change EA propagation.
- Earth terminals/nodes and GroundSwitch-to-Earth paths are excluded from WorkScope Candidate.
- Disconnected Deenergized components remain separate Regions. Unresolvable or stale EA input cannot produce a Candidate.

### Grounding and ticket adapter

- GroundingPoint authority remains with GS. GroundingPoint is not WorkScope ownership or membership authority.
- Existing WTA `IsolationBoundary` is retained as the adapter input from WorkScope Boundary to WorkTicketAnalyzer. It is not WorkScope authority.
- When all required boundaries map deterministically, Confirm hands the ticket to the existing WorkTicketAnalyzer exactly once.
- A boundary that cannot be mapped deterministically is never guessed. The system emits a diagnostic and preserves the Confirmed WorkScope snapshot.

### Confirmation and history

- Confirmation is one user action and one CommandStack Undo / Redo action covering the Confirmed WorkScope and WorkTicket handoff mutations.
- Undo restores every pre-Confirm object state. Redo restores the captured snapshot and does not rerun EA or regenerate Candidate.

## Schema policy

- The formal project baseline was FormatVersion V9 when this package opened; current accepted FormatVersion is V10.
- The audit confirmed that the frozen WorkScope contract requires a breaking V10 schema, implemented and accepted in Phase 1.
- This governance commit does not change `ProjectFileFormat`, production code, DTOs, tests, or the format version.
- V8/V9 migration is not supported. Older development project files may be rejected; old development fixtures may be rebuilt.
- Correctness takes priority over legacy compatibility.

## Fixed implementation phases

1. **Domain + V10** — Implement the Domain snapshot contract and V10 persistence schema.
2. **EA → Candidate Projector** — Derive transient Regions and transitions from existing topology and valid EA state.
3. **Confirmation + CommandStack** — Commit one captured Confirm snapshot as one Undo / Redo action.
4. **IsolationBoundary Projection** — Adapt representable WorkScope boundaries to existing WTA inputs; retain diagnostics for unmappable transitions.
5. **WorkTicket Handoff** — Update the current WorkTicket and invoke the existing Analyzer once for a fully projectable Confirm.
6. **Windows GUI Acceptance** — Validate the committed implementation on Windows, including Save/Open, Confirm, Undo/Redo, and diagnostics.

The phase order and goals are fixed. No phase is started by this governance record.

## Out of scope

- New 6.1 or 6.2 rules.
- Deepening 6.3 / 6.4 / 6.5 / 16.1 rules.
- Rule Library.
- A complete Needs Reanalysis framework.
- Broad WorkTicket UI redesign.
- Changes to EA propagation semantics or GS authority.
- Device Interlock.
- EA-aware export.
- Repair of the known composite GAP/OHL deletion Undo issue.
- Unrelated historical technical debt.

## Closed package boundaries

WP-WTA-01, WP-EA-01A, WP-EA-01B, and WP-GS-01 remain CLOSED / ACCEPTED. This package does not reopen them. No WP-EA-01C exists.
