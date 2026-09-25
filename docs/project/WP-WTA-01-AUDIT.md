# WP-WTA-01 architecture audit and V8 decision

## 2026-09-25 architecture amendment

The user confirmed all older projects are test projects. WP-WTA-01 uses a clean V8 format switch: `WorkTicketData` is required, and only V8 opens. There is no V7 migration, old-version round trip, unknown-field preservation, or write compatibility with older DistributionDrawing releases. The V7 analysis below records the original read-only audit and is superseded for persistence decisions. This change belongs to work-ticket persistence and does not reopen the Electrical Model.

Starting baseline: `3328d15d6ed6d090cd601a82e54a9c502748ab99` on clean `main`, equal to `origin/main`. Its format was V7. The closed Electrical Model and performance/export packages are outside this work.

## Existing seams

- `DrawingDocument` owns devices, terminals, connections, ring cabinets, work scopes, grounding points, and grounding access points. `RingCabinetInterval` has its own `IntervalKind`, `GroundingStructureKind`, ordered `Sequence`, switch devices, and business numbers. Rules must evaluate each interval separately. Optional `ElectricalNode.ElectricalState` can explicitly state energized/deenergized; absent state and switch `OperationalState.Running` cannot by themselves prove site energization.
- `WorkScope` already identifies two typed `BoundaryPoint` values, but it is a professional drawing object and does not express the larger outage/isolation scope. A ticket must reference a work scope separately from its own isolation boundaries.
- `GroundingPoint.Target` distinguishes terminal and GAP; `GroundingAccessPoint` carries connection and side. Neither implies that a location should be created automatically.
- `CommandStack` in Rendering.Wpf tracks Undo/Redo and dirty checkpoints. `ProjectRuntimeSession` owns that stack and the runtime drawing projection. Desktop `MainWindow` binds document sessions and builds the drawing scene.
- `SelectionManager`, `SelectionHitTestIndex`, and `SelectionOverlayBuilder` already provide stable object selection and visual bounds, so ticket items can locate corresponding objects without changing Domain.
- `ProjectFileDocument` and `ProjectFilePayload` carry separate optional sections. `ProjectService` reconstructs Domain, Professional and Layout in order. The historical `docs/work-ticket-data-architecture-design.md` was written for FormatVersion 2 and an all-manual ticket; this package's newer requirements supersede those assumptions.

## Addition and boundaries

Add `Application/WorkTickets` for ticket setup, typed model references, ordered measure facts, analysis, rule packs, phrase generation, drafts, provenance, confirmation and stale state. A ticket reads Domain but never mutates it. V8 requires `workTicketData` and restores it after Domain/Professional validation. Add `Desktop/WorkTickets` for the complete ticket shell, setup editor, text copy, locating related objects and a presentation-only drawing overlay. No new project is necessary: the existing Application, Infrastructure, and Desktop dependency direction supports this split.

There is no blocking Electrical Model fact gap for a conservative first vertical slice. Unknown live relations, physical proximity, red-cloth placement, barrier limits, and fuse removal state must stay user supplied or pending confirmation. The existing switch model identifies `DropoutFuse` but has only Open/Closed; a ticket must represent fuse-tube removal as a separate user-confirmed setup fact, without altering Domain.

## Persistence risk

The original optional V7 section could be discarded by an older executable on save. The user authorized a clean V8 switch: all V8 files require `workTicketData`, and every other format version is rejected. The V7 migration code and upgrade-save-as path were removed. Unknown fields are rejected in V8 rather than silently dropped; unknown-field preservation is not implemented.

## Verification plan

1. Application tests for interval-specific 6.1 chains, ordered 6.3 and 16.1 derivation, provenance, and stale state.
2. Infrastructure V8 round trip, required-section, version rejection, and invalid-reference tests.
3. Desktop build and focused workflow tests where platform support permits; Windows WPF runtime and final visual acceptance require a committed/pushed revision after review.

## Internal implementation stages (original plan; persistence steps superseded)

1. Audit the V7 facts and Appendix 1, then freeze the setup/analysis boundary in this document.
2. Add Application ticket state, rules, ordered measures, draft provenance and copy contract; verify focused rule tests.
3. Superseded: the original optional V7 persistence and old-file compatibility plan is canceled.
4. Add Desktop first-level workspace and drawing linkage; cross-build the complete solution.
5. Run Domain/Application/Infrastructure tests locally. Desktop/Rendering test execution and visual/professional acceptance require Windows after review and a committed/pushed revision.

The user supplied the consultation-draft PDF of 《电气工作票填用规范及样票汇编（配电部分）》 outside the repository. Chapter 1 defines the first-kind ticket for high-voltage outage or electrical isolation. Chapter 3 specifies the six measure columns and prohibits advance entry of actual execution time, person, or check marks for 6.3. Appendix 1 is on PDF pages 193–196 (printed pages 184–187) and fixes the complete column order. This is a consultation draft; production wording requires the user's professional acceptance.

## Review state

The implementation is frozen as the `wp-wta-01` Windows acceptance candidate. It adds per-interval 6.1 rules for load-switch and the three integrated-feeder grounding structures, conservative pole/fuse measures, 6.2 default through the rule pack, ordered 6.3 measures, explicit-model/live recommendations for 6.4, manual 6.5 risk entry, and 16.1 derivation only from reversible 6.3 measures. Drafts keep generated/current text, references, origin, completion, analysis fingerprint, and rule/phrase versions. A first-level Desktop ticket workspace preserves the Appendix 1 column sequence and leaves execution records blank. Required V8 persistence, copy, undo/redo, and drawing references are connected. Review fixes now guard boundary sides and referenced deletion, keep item provenance through edits, require explicit confirmation, invalidate risk edits, and use the selected ticket for overlay.

The pre-review test baseline was Domain 175/175, Application 162/162, Infrastructure 144/144. Review-fix verification is reported separately in the WP-WTA-01 Fix Report. WPF test execution and GUI/professional acceptance are blocked on this macOS host by absence of the `Microsoft.WindowsDesktop.App` runtime and require a reviewed, committed and pushed revision on Windows. The visible ticket shell is not yet professionally accepted. V7 and earlier files are intentionally rejected by this development baseline.
