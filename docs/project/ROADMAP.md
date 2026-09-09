# Roadmap

## V1.0

**Status:** RELEASED

V1.0 is the current stable foundation for professional electrical drawing.

## Post-V1

**Status:** FIRST STAGE IN PROGRESS

Post-V1 remains the roadmap level; no V1.1, V1.2, or V2.0 scope is defined here.

The first confirmed implementation stage is [Post-V1 Electrical Model Closure](POST_V1_ELECTRICAL_MODEL_CLOSURE.md). Its goal is to close the principal Electrical Model gaps needed for actual 10kV work-ticket drawing and establish the unified FormatVersion 7 persistence baseline before any future Annotation / Work-ticket Presentation Layer.

The Grounding Scope Amendment is complete and frozen. The Interaction Stabilization Amendment establishes the confirmed Work Package order as WP-EM-01 through WP-EM-09:

1. WP-EM-01 Grounding Presentation Anchor Separation — Completed
2. WP-EM-02 V7 Format & Migration Foundation — Completed
3. WP-EM-03 RingCabinet Optional CableTerminal Vertical Slice — Completed / Closed
4. WP-EM-04 GroundingAccessPoint & GroundingTarget Vertical Slice — Closed / Completed
5. WP-EM-05 Grounding Layout & Interaction Closure — Closed (Windows accepted with known limitation)
6. WP-EM-06 Transformer Vertical Slice
7. WP-EM-07 CustomerStation Vertical Slice
8. WP-EM-08 Electrical Model Interaction Stabilization
9. WP-EM-09 Electrical Model Closure Integration

This ordering does not define a new release version. Annotation and Energization remain Post-V1 candidates and are outside this stage.

WP-EM-04 closed at implementation baseline `b15166c96bcf03a38acd2a27d98b597d04b60d4d` after implementation, review, automated validation, Windows runtime validation, and GUI acceptance. WP-EM-05 is now Closed after implementation, focused review, automated verification, and Windows GUI acceptance with one accepted, deferred RingCabinet above-terminal presentation limitation. Generic drag and routing stabilization remains assigned to WP-EM-08, while final integration and regression move to WP-EM-09. WP-EM-06 is the next Work Package.
