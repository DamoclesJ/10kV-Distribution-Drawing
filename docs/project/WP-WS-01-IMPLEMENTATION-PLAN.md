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
- **Phase 1:** NOT STARTED

This document records the approved package boundary. Opening the package does not start an implementation phase.

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

- The formal project baseline remains FormatVersion V9 when this package opens.
- The audit confirmed that the frozen WorkScope contract requires a breaking V10 schema, implemented in Phase 1.
- This governance commit does not change `ProjectFileFormat`, production code, DTOs, tests, or the format version.
- No V8/V9 migration is required or planned. Older development project files may be rejected; old development fixtures may be rebuilt.
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
