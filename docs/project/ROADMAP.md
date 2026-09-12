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
9. WP-EM-07B Transformer Naming Amendment — Requirements Frozen / Implementation Not Started
10. WP-EM-08 Electrical Model Interaction Stabilization — Deferred / Not Started
11. WP-EM-09 Electrical Model Closure Integration — Planned

WP-EM-07A and WP-EM-07B are Post-EM-07 / Pre-EM-08 amendment Work Packages. They do not reopen WP-EM-06 or WP-EM-07. This ordering does not define a new release version. No V1.1, V1.2, or V2.0 release scope is defined; Annotation and Energization remain Post-V1 candidates and are outside this stage.

WP-EM-04 closed at implementation baseline `b15166c96bcf03a38acd2a27d98b597d04b60d4d` after implementation, review, automated validation, Windows runtime validation, and GUI acceptance. WP-EM-05 is now Closed after implementation, focused review, automated verification, and Windows GUI acceptance with one accepted, deferred RingCabinet above-terminal presentation limitation. WP-EM-06 is also Closed after implementation, review, Windows automated validation, and Windows professional GUI acceptance. WP-EM-07 is Closed after implementation, code review, Windows automated verification, and Windows professional visual acceptance. WP-EM-07A is Closed / Archived after implementation, targeted review, Windows automated verification, and Windows professional acceptance at `c852d7a1f2477628664f8aeca8bfb23cf9ee3b06`; its additive V7 amendment introduces `GroundingAccessPlacementSide` and `TransformerSide` without V8. WP-EM-07B follows it with requirements frozen and implementation not started. Its single naming fact is required `Device.DisplayName`, shown as “变压器名称”; V7 remains additive-compatible through a controlled legacy incomplete state that must be completed before Save / Save As. Generic drag and routing stabilization remains assigned to WP-EM-08, while final integration and regression remain assigned to WP-EM-09.
