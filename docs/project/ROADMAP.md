# Roadmap

## V1.0

**Status:** RELEASED

V1.0 is the current stable foundation for professional electrical drawing.

## Post-V1

**Status:** FIRST STAGE IN PROGRESS

Post-V1 remains the roadmap level; no V1.1, V1.2, or V2.0 scope is defined here.

The first confirmed implementation stage is [Post-V1 Electrical Model Closure](POST_V1_ELECTRICAL_MODEL_CLOSURE.md). Its goal is to close the principal Electrical Model gaps needed for actual 10kV work-ticket drawing and establish the unified FormatVersion 7 persistence baseline before any future Annotation / Work-ticket Presentation Layer.

The Grounding Scope Amendment is complete and frozen. The Post-EM-07 Sequencing Amendment inserts two independent amendments before interaction stabilization and establishes the confirmed Work Package order as follows:

1. WP-EM-01 Grounding Presentation Anchor Separation — Closed
2. WP-EM-02 V7 Format & Migration Foundation — Closed
3. WP-EM-03 RingCabinet Optional CableTerminal Vertical Slice — Completed / Closed
4. WP-EM-04 GroundingAccessPoint & GroundingTarget Vertical Slice — Closed / Completed
5. WP-EM-05 Grounding Layout & Interaction Closure — Closed (Windows accepted with known limitation)
6. WP-EM-06 Transformer Vertical Slice — Completed / Closed (implementation, review, Windows automated validation, and Windows professional GUI acceptance completed)
7. WP-EM-07 CustomerStation Vertical Slice — Closed
8. WP-EM-07A Transformer & Pole-Device Grounding Amendment — Closed / Archived (final accepted SHA `c852d7a1f2477628664f8aeca8bfb23cf9ee3b06`)
9. WP-EM-07B Transformer Naming Amendment — Closed / Archived
10. WP-EM-08 Electrical Model Interaction Stabilization — Requirements Refinement / Implementation Not Started; refined into three internal implementation slices
11. WP-EM-09 Electrical Model Closure Integration — Planned / Integration-only

WP-EM-08 Implementation Audit & Scope Revalidation passed at `c4f61653f923f985bcd8835d9735c68decee783e`, and its Scope Amendment is complete. WP-EM-08 is now in Requirements Refinement with implementation not started. It remains one Work Package and one Codex Thread, implemented incrementally through three independently reviewable internal implementation slices. These slices do not create additional Work Packages or require separate Codex Threads:

1. **Implementation Slice A — Move Capability Closure — Requirements Frozen / Implementation Not Started:** add direct drag for every Transformer kind and CustomerStation configuration and integrate them with group move, while preserving Pole drag, RingCabinet drag, CableTermination pole-orbit drag, and GroundingPoint symbol-offset drag. Movement changes only the corresponding persisted Layout position; all electrical, aggregate, grounding, naming, orientation, and stable identity facts remain unchanged. SwitchDevice / ordinary PoleAttachment and GroundingAccessPoint remain unavailable for independent free drag. This slice reuses current scene rebuild and route behavior and does not implement `LastValid`, a new drag transaction framework, route-family hysteresis, or routing-obstacle policy changes.
2. **Implementation Slice B — Transactional Drag Stabilization — Requirements Refined / Not Started:** establish explicit preview results, candidate validation, transient `LastValid`, invalid-candidate rollback with gesture continuation, invalid-release behavior, non-modal feedback, and CommandStack / Layout / scene atomicity. It does not implement route-family hysteresis, a generic router rewrite, or a new collision model.
3. **Implementation Slice C — Route Continuity Stabilization — Requirements Refined / Characterization Pending:** characterize reproducible route flips before freezing a switching policy; introduce typed transient per-Connection continuity state only if the evidence requires it; cover cache invalidation and grounding / GAP / short-OHL regression; and complete Windows cross-device acceptance. Transformer / CustomerStation routing-obstacle participation is decided inside this slice after characterization.

Slice A is frozen only at slice level. WP-EM-08 remains one Work Package in Requirements Refinement / Implementation Not Started; freezing Slice A does not freeze the overall Work Package or advance Slice B, Slice C, or WP-EM-09.

WP-EM-08 has no expected persistence schema impact and keeps FormatVersion V7. `LastValid`, invalid candidates, feedback, route-family identity, hysteresis cache, and transaction state remain transient. The accepted WP-EM-05 RingCabinet above-terminal grounding presentation limitation remains outside WP-EM-08. WP-EM-09 stays Integration-only and does not introduce Transformer drag, CustomerStation drag, `LastValid`, hysteresis, or other new business capability for the first time.

WP-EM-07A and WP-EM-07B are Post-EM-07 / Pre-EM-08 amendment Work Packages. They do not reopen WP-EM-06 or WP-EM-07. This ordering does not define a new release version. No V1.1, V1.2, or V2.0 release scope is defined; Annotation and Energization remain Post-V1 candidates and are outside this stage.

WP-EM-04 closed at implementation baseline `b15166c96bcf03a38acd2a27d98b597d04b60d4d` after implementation, review, automated validation, Windows runtime validation, and GUI acceptance. WP-EM-05 is now Closed after implementation, focused review, automated verification, and Windows GUI acceptance with one accepted, deferred RingCabinet above-terminal presentation limitation. WP-EM-06 is also Closed after implementation, review, Windows automated validation, and Windows professional GUI acceptance. WP-EM-07 is Closed after implementation, code review, Windows automated verification, and Windows professional visual acceptance. WP-EM-07A is Closed / Archived after implementation, targeted review, Windows automated verification, and Windows professional acceptance at `c852d7a1f2477628664f8aeca8bfb23cf9ee3b06`; its additive V7 amendment introduces `GroundingAccessPlacementSide` and `TransformerSide` without V8. WP-EM-07B is Closed / Archived after implementation, Windows automated verification, and professional acceptance. Its single naming fact is required `Device.DisplayName`, shown as “变压器名称”; the additive document-level `TransformerNamingContractVersion` discriminator keeps `FormatVersion = V7` while separating legacy compatibility mode from current strict naming mode. Legacy incomplete state must be completed before Save / Save As. Generic drag and routing stabilization remains assigned to WP-EM-08, while final integration and regression remain assigned to WP-EM-09.
