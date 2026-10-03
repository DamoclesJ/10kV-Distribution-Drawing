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
- **Phase 2:** IMPLEMENTED / CANDIDATE; Windows automated verification PENDING
- **Phase 3:** NOT STARTED

This document records the approved package boundary. Opening the package does not start an implementation phase.

## Phase 1 acceptance

Phase 1 — Domain + V10 is VERIFIED / ACCEPTED at `47349b5c369c7cefb9e9f5a9b233b6f36ea5f6a3`. The accepted model includes immutable multi-Region snapshots with Terminal/Node membership, 0..N structural typed Boundaries, optional Description, complete command snapshots, V10-only persistence, and Domain referential-integrity protection. The existing WTA `BoundarySide` contract is shared from Domain without changing its values. WorkScope does not own GroundingPoint; WorkTicket references and Equipment semantics remain intact.

- Accepted SHA: `47349b5c369c7cefb9e9f5a9b233b6f36ea5f6a3`.
- Windows automated verification: PASSED — Domain 236/236, Application 243/243, Infrastructure 151/151, Rendering.Wpf 674/674, Desktop 374/374; total 1678/1678, 0 failed, 0 skipped.
- Full solution build: 0 errors.
- Current FormatVersion: V10. V8/V9 migration is not supported. Windows GUI acceptance is NOT RUN and remains Phase 6; it is not part of Phase 1 acceptance.
- STOP-P1-01 through STOP-P1-06: NONE.
- WP-WS-01 stays OPEN / IN PROGRESS. Phase 3 — Confirmation + CommandStack is NOT STARTED.

## Phase 2 candidate progress

Phase 2 — EA → WorkScope Candidate Projector is IMPLEMENTED / CANDIDATE. The transient Application model contains normalized Regions, switch transition facts, and diagnostics; it is derived only from `EnergizationAnalysisState.CurrentResult` plus current drawing topology. The projector reuses `ElectricalConnectivityGraphBuilder`, verifies current point identities and conducting-edge identity against the EA result, excludes Earth and GroundSwitch-to-Earth identities, and finds Deenergized connected components including isolated terminals. Transition facts retain switch identity/kind/installation, energized and Deenergized terminal identities, parent/pole identity, and related connection identities. The projector does not mutate `DrawingDocument.WorkScopes`, WorkTicket data, EA state, CommandStack, or persistence, and does not invoke WorkTicketAnalyzer.

- Added Application projector coverage: 16 cases including validity gates, empty candidate, disconnected components, isolated terminals, Earth exclusion, conducting-edge consistency, RingCabinet load switch/breaker/isolator, pole switch and OHL topology, Cable and CableTermination, Transformer HV leaf, independent CustomerStation feeders, historical WorkScope independence, multi-Seed ordering, and read-only behavior.
- macOS Release verification: Domain 236/236, Application 259/259, Infrastructure 151/151; full solution build passed with 0 errors.
- Rendering.Wpf and Desktop full test execution could not start because this macOS host has no `Microsoft.WindowsDesktop.App` runtime. Their projects compile in the full solution build. Windows automated verification remains PENDING; this candidate is not Phase 2 acceptance. Windows GUI acceptance remains NOT RUN / Phase 6.
- FormatVersion remains V10. No Candidate persistence, confirmation, WorkTicket mutation, Analyzer handoff, EA propagation change, or GS authority change was introduced.
- STOP-P2-01 through STOP-P2-08: NONE. WP-WS-01 remains OPEN / IN PROGRESS; Phase 3 is NOT STARTED.

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
