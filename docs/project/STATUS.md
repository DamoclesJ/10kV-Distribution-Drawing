# Current Status

- **Project:** 10kV Distribution Drawing
- **Current Release:** v1.0.0
- **Release State:** Released
- **V1.0 baseline:** `985b6c2cd9a1c0648048f87bf50509d517045bbd`
- **FormatVersion:** V7
- **Current Development Target:** Post-V1 Electrical Model Closure
- **Completed Work Packages:** WP-EM-03 RingCabinet Optional CableTerminal Vertical Slice — Closed; WP-EM-04 GroundingAccessPoint & GroundingTarget Vertical Slice — Closed
- **Grounding Scope Amendment:** Completed / Frozen
- **Next Work Package:** WP-EM-05 Grounding Layout & Interaction Closure — Not started / Planned (prerequisite satisfied)
- **Blockers:** No WP-EM-04 closure blockers. Grounding-specific presentation continuity is assigned to WP-EM-05; generic drag and routing stabilization is assigned to WP-EM-08. The final Windows runtime and GUI acceptance were completed for implementation baseline `b15166c96bcf03a38acd2a27d98b597d04b60d4d`.

V1.0 provides a standard portable profile and a legacy Windows 10 portable profile. The legacy profile is for older Windows 10 systems that cannot be upgraded or adequately serviced. V1.0 has completed target-machine validation.

## Post-V1 Scope

Post-V1 Requirement Reassessment / Planning is complete. The first confirmed implementation stage is [Post-V1 Electrical Model Closure](POST_V1_ELECTRICAL_MODEL_CLOSURE.md). It does not yet define a V1.1, V1.2, or V2.0 release scope.

WP-EM-01, WP-EM-02, WP-EM-03, and WP-EM-04 are complete following code review, automated verification, and Windows validation. WP-EM-03 completed the RingCabinet optional cable-terminal vertical slice, including nullable V7 persistence, dependency protection, clipboard, rendering, and interval internal-lead preservation when the cable terminal is absent. WP-EM-04 completed the GroundingAccessPoint and typed GroundingTarget vertical slice, including mounted-switch routing, GAP clearance and presentation, GroundingPoint numbering and location defaults, lifecycle, persistence, and GUI acceptance. The Post-V1 Grounding Scope Amendment and WP-EM-04 closure are recorded in [Post-V1 Electrical Model Closure](POST_V1_ELECTRICAL_MODEL_CLOSURE.md). Do not enter later Work Packages or the future Annotation / Work-ticket Presentation Layer implicitly.

The Interaction Stabilization Amendment establishes the remaining sequence as WP-EM-05 Grounding Layout & Interaction Closure, WP-EM-06 Transformer Vertical Slice, WP-EM-07 CustomerStation Vertical Slice, WP-EM-08 Electrical Model Interaction Stabilization, and WP-EM-09 Electrical Model Closure Integration. WP-EM-05 remains Not Started / Planned and is the next Work Package. This amendment does not define a new release version.

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
