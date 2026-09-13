# Current Status

- **Project:** 10kV Distribution Drawing
- **Current Release:** v1.0.0
- **Release State:** Released
- **V1.0 baseline:** `985b6c2cd9a1c0648048f87bf50509d517045bbd`
- **FormatVersion:** V7
- **Current Development Target:** Post-V1 Electrical Model Closure
- **Completed Work Packages:** WP-EM-01 Grounding Presentation Anchor Separation — Closed; WP-EM-02 V7 Format & Migration Foundation — Closed; WP-EM-03 RingCabinet Optional CableTerminal Vertical Slice — Closed; WP-EM-04 GroundingAccessPoint & GroundingTarget Vertical Slice — Closed; WP-EM-05 Grounding Layout & Interaction Closure — Closed (Windows accepted with known limitation); WP-EM-06 Transformer Vertical Slice — Closed; WP-EM-07 CustomerStation Vertical Slice — Closed
- **Grounding Scope Amendment:** Completed / Frozen
- **WP-EM-07A State:** Closed / Archived / Final accepted implementation `c852d7a1f2477628664f8aeca8bfb23cf9ee3b06`
- **Post-EM-07 Sequencing Amendment:** Completed / Frozen
- **Next Work Package:** WP-EM-08 Electrical Model Interaction Stabilization — Requirements Refinement / Implementation Not Started
- **Internal Implementation Plan:** WP-EM-08 Slice A — Move Capability Closure — Requirements Frozen / Implementation Not Started; WP-EM-08 Slice B — Transactional Drag Stabilization — Requirements Refined / Not Started; WP-EM-08 Slice C — Route Continuity Stabilization — Requirements Refined / Characterization Pending
- **Planned Work Packages:** WP-EM-09 Electrical Model Closure Integration — Planned / Integration-only
- **WP-EM-08 Governance:** Implementation Audit & Scope Revalidation passed at `c4f61653f923f985bcd8835d9735c68decee783e`; Scope Amendment completed; implementation has not started; FormatVersion remains V7. WP-EM-08 remains one Work Package and one Codex Thread; its three slices are internal implementation increments, not additional Work Packages.
- **Blockers:** No WP-EM-07A or WP-EM-07B blockers. WP-EM-08 Slice A and Slice B may proceed independently through their slice-level Requirements Freeze / implementation review sequence. Slice C retains one characterization decision point: whether Transformer / CustomerStation presentation bounds join the routing obstacle set. This does not block Slice A or Slice B.

V1.0 provides a standard portable profile and a legacy Windows 10 portable profile. The legacy profile is for older Windows 10 systems that cannot be upgraded or adequately serviced. V1.0 has completed target-machine validation.

## Post-V1 Scope

Post-V1 Requirement Reassessment / Planning is complete. The first confirmed implementation stage is [Post-V1 Electrical Model Closure](POST_V1_ELECTRICAL_MODEL_CLOSURE.md). It does not yet define a V1.1, V1.2, or V2.0 release scope.

WP-EM-01, WP-EM-02, WP-EM-03, WP-EM-04, WP-EM-05, WP-EM-06, and WP-EM-07 are complete following code review, automated verification, and Windows validation. WP-EM-03 completed the RingCabinet optional cable-terminal vertical slice, including nullable V7 persistence, dependency protection, clipboard, rendering, and interval internal-lead preservation when the cable terminal is absent. WP-EM-04 completed the GroundingAccessPoint and typed GroundingTarget vertical slice. WP-EM-05 completed GroundingPoint layout, drag, leader presentation, target affordance, persistence, lifecycle, and Canvas / PNG consistency. WP-EM-06 completed the Transformer vertical slice, including its three professional glyphs, electrical and command foundation, V7 persistence, desktop workflow, and Windows professional acceptance. WP-EM-07 completed the CustomerStation vertical slice, including the formal aggregate, one/two feeder topology, V7 persistence, Clipboard, professional presentation, creation / Inspector workflow, and Windows professional acceptance. The Post-V1 Grounding Scope Amendment, WP-EM-04 closure, WP-EM-05 closure, WP-EM-06 closure, and WP-EM-07 closure are recorded in [Post-V1 Electrical Model Closure](POST_V1_ELECTRICAL_MODEL_CLOSURE.md). Do not enter later Work Packages or the future Annotation / Work-ticket Presentation Layer implicitly.

The Post-EM-07 Sequencing Amendment inserts two independent Post-EM-07 / Pre-EM-08 Work Packages without reopening WP-EM-06 or WP-EM-07. WP-EM-07A is Closed / Archived after implementation, review, Windows automated verification, and Windows professional acceptance at `c852d7a1f2477628664f8aeca8bfb23cf9ee3b06`. The Windows Professional Acceptance Amendment adds `GroundingAccessPlacementSide` (`PoleSide | AdjacentEndpointSide`) and `GroundingAccessLineSide.TransformerSide`, allowing two independent conductor GAP identities on the same legal pole-mounted Transformer short OHL. V7 remains additive-compatible: old records without placement normalize to `PoleSide`; no V8 migration is authorized. WP-EM-07B is Closed / Archived after implementation, Windows automated verification, and professional acceptance: all three Transformer kinds use the single required `Device.DisplayName`, presented in Chinese as “变压器名称”; the additive document-level `TransformerNamingContractVersion` discriminator distinguishes pre-WP-EM-07B V7 compatibility mode from current strict V7 naming mode. Legacy incomplete Transformers cannot be saved until all names are completed. WP-EM-08 has advanced to Requirements Refinement after its implementation audit and Scope Amendment; production implementation has not started. WP-EM-09 remains Planned / Integration-only.

### WP-EM-08 Requirements Refinement

- Purpose: complete formal Device move capability, transactional drag legality, transient `LastValid`, non-modal drag feedback, route-presentation continuity, and cross-device regression without creating a generic routing framework.
- Move policy: Pole and RingCabinet support direct drag; CableTermination retains pole-orbit drag; GroundingPoint retains symbol-offset drag; Transformer and CustomerStation direct drag are assigned to WP-EM-08 Slice A. SwitchDevice / ordinary PoleAttachment remain unavailable for independent free drag and continue to move with their parent Pole or through existing attachment layout / rotation / property commands. GroundingAccessPoint remains derived from its typed electrical route and is not directly draggable.
- Illegal geometry is limited to provable Layout / Routing invalidity such as non-finite coordinates, loss of two distinct route points, insufficient required-stub capacity, impossible required direction / no-backtracking constraints, scoped routing invariants, and typed layout invariant violations. Overlap, canvas bounds, screen-coordinate Pole ordering, generic collision, and route crossing are not new business-invalid rules.
- `LastValid`, invalid candidates, drag feedback, route-family identity, hysteresis cache, and drag transaction state are runtime interaction facts and are not persisted. Invalid candidates keep the gesture active and restore or retain `LastValid`; release commits `LastValid`, while a gesture with no legal movement creates no history entry.
- Internal implementation slices: Slice A — Move Capability Closure; Slice B — Transactional Drag Stabilization; Slice C — Route Continuity Stabilization. They are independently reviewable increments within the single WP-EM-08 Work Package and do not require separate Codex Threads. Route continuity begins with characterization; no hysteresis threshold is frozen by this refinement.
- Slice A Requirements Freeze: add direct drag and group-move participation for all Transformer kinds and all CustomerStation configurations by changing only `TransformerLayout.Position` or `CustomerStationLayout.Position`. Reuse the existing Canvas pointer lifecycle, `DeviceDragController`, typed drag state, `CommandStack`, and `RefreshDrawingScene()` path. Preserve stable Device, Terminal, feeder, switch, connection, grounding, topology, naming, and orientation identities and facts.
- Slice A acceptance requires direct-drag, preview, live anchor / connected-route / grounding-presentation rebuild, Cancel-to-`Before`, Commit, Undo / Redo, selection / hit-test, group move, persistence round-trip, unsupported Switch / GAP drag regressions, and Windows professional acceptance. Slice A does not introduce `LastValid`, invalid-candidate continuation, non-modal invalid feedback, transaction/history redesign, route hysteresis, `RouteFamilyKey`, obstacle policy, collision or bounds rules, waypoint editing, or router rewrite.
- Persistence schema impact is expected to be none. FormatVersion remains V7. WP-EM-09 remains Integration-only and cannot absorb first implementation of WP-EM-08 capabilities.

### WP-EM-05 Closure Evidence

- Implementation and routing-fix baselines: `be7f2bd29875a112f01b8c93b7c85df6927b8871`, `17a1dba489bcf37a21693d6b55df653153979002`, `3d35ed30909cfb8e57ccabca093431e178cc7627`, `ebd062ae876e85d7581ba8ebe3b894fd66b1b835`, and `4f6b314a883d29b9c9afa1a385b5ec4c406809dc`.
- GAP interaction, snap / clamp / layout, Pole CableTermination manual routing, RingCabinet default / below-terminal routing, GroundingPoint drag, Undo / Redo, V7 persistence, and Canvas / PNG shared scene passed review and Windows acceptance.
- Windows professional acceptance: **Passed with Known Limitation**.
- Accepted Known Limitation (Deferred): RingCabinet cable-side GroundingPoint manually dragged above the cable terminal may still produce visual interference between the grounding leader and the interval internal vertical lead. This is presentation-only; it does not change electrical facts, `GroundingTarget`, `RingCabinetLayout`, interval geometry, or Terminal geometry. Default / below-terminal placement, GAP, and Pole CableTermination behavior remain accepted. A future option may constrain the symbol outside the upper terminal region, but that option is not implemented or frozen as a requirement.

### WP-EM-03 Closure Evidence

- `LoadSwitchInterval` and `IntegratedFeederInterval` support cable-terminal present / absent; `PTInterval` does not support the optional-terminal operation.
- Cable / Connection, `GroundingPoint`, and `WorkScope` dependency protection remains enforced.
- Undo / Redo preserves the cable-terminal identity contract.
- V7 nullable `CableTerminalId` persistence and V6 → V7 migration compatibility are complete.
- Clipboard mixed terminal presence is supported.
- An absent terminal produces no triangle, terminal anchor, or cable target; the interval internal lead remains rendered.
- Windows build, automated tests, and manual validation passed.

### WP-EM-04 Closure Evidence

- Implementation baseline: `b15166c96bcf03a38acd2a27d98b597d04b60d4d` (`fix(grounding): use local cable termination location`).
- Implementation and focused reviews passed; final Windows runtime validation reported all discovered tests passed with failed = 0 and skipped = 0.
- Domain.Tests: 113/113 passed; Infrastructure.Tests: 80/80 passed; full solution build passed.
- GUI acceptance passed for mounted-switch pole movement, GAP and mounted-switch coexistence, vertical GAP half-edge presentation, RingCabinet and local CableTermination GroundingPoint locations, and Lxx/Sxx numbering.
- FormatVersion remains V7; no migration was added.
- Grounding-specific GAP / GroundingPoint presentation continuity during legal route rebuilds is assigned to WP-EM-05. Generic pole/device drag route continuity and route-family hysteresis, illegal-geometry last-valid-position, and continuous-drag non-modal feedback are assigned to WP-EM-08.
