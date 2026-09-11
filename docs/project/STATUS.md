# Current Status

- **Project:** 10kV Distribution Drawing
- **Current Release:** v1.0.0
- **Release State:** Released
- **V1.0 baseline:** `985b6c2cd9a1c0648048f87bf50509d517045bbd`
- **FormatVersion:** V7
- **Current Development Target:** Post-V1 Electrical Model Closure
- **Completed Work Packages:** WP-EM-01 Grounding Presentation Anchor Separation — Closed; WP-EM-02 V7 Format & Migration Foundation — Closed; WP-EM-03 RingCabinet Optional CableTerminal Vertical Slice — Closed; WP-EM-04 GroundingAccessPoint & GroundingTarget Vertical Slice — Closed; WP-EM-05 Grounding Layout & Interaction Closure — Closed (Windows accepted with known limitation); WP-EM-06 Transformer Vertical Slice — Closed; WP-EM-07 CustomerStation Vertical Slice — Closed
- **Grounding Scope Amendment:** Completed / Frozen
- **Post-EM-07 Sequencing Amendment:** Completed / Awaiting ChatGPT Review
- **Next Work Package:** WP-EM-07A Transformer & Pole-Device Grounding Amendment — Not Started / Planned
- **Planned Work Packages:** WP-EM-07B Transformer Naming Amendment — Planned / Not Started; WP-EM-08 Electrical Model Interaction Stabilization — Planned; WP-EM-09 Electrical Model Closure Integration — Planned
- **Blockers:** No WP-EM-07 closure blockers. WP-EM-05 remains Closed with one accepted, deferred RingCabinet above-terminal presentation limitation. WP-EM-06 Windows professional acceptance = PASS. WP-EM-07 Windows automated verification and professional visual acceptance = PASS. WP-EM-07A and WP-EM-07B require their own implementation Repo Audits before implementation. Generic drag and routing stabilization remains assigned to WP-EM-08.

V1.0 provides a standard portable profile and a legacy Windows 10 portable profile. The legacy profile is for older Windows 10 systems that cannot be upgraded or adequately serviced. V1.0 has completed target-machine validation.

## Post-V1 Scope

Post-V1 Requirement Reassessment / Planning is complete. The first confirmed implementation stage is [Post-V1 Electrical Model Closure](POST_V1_ELECTRICAL_MODEL_CLOSURE.md). It does not yet define a V1.1, V1.2, or V2.0 release scope.

WP-EM-01, WP-EM-02, WP-EM-03, WP-EM-04, WP-EM-05, WP-EM-06, and WP-EM-07 are complete following code review, automated verification, and Windows validation. WP-EM-03 completed the RingCabinet optional cable-terminal vertical slice, including nullable V7 persistence, dependency protection, clipboard, rendering, and interval internal-lead preservation when the cable terminal is absent. WP-EM-04 completed the GroundingAccessPoint and typed GroundingTarget vertical slice. WP-EM-05 completed GroundingPoint layout, drag, leader presentation, target affordance, persistence, lifecycle, and Canvas / PNG consistency. WP-EM-06 completed the Transformer vertical slice, including its three professional glyphs, electrical and command foundation, V7 persistence, desktop workflow, and Windows professional acceptance. WP-EM-07 completed the CustomerStation vertical slice, including the formal aggregate, one/two feeder topology, V7 persistence, Clipboard, professional presentation, creation / Inspector workflow, and Windows professional acceptance. The Post-V1 Grounding Scope Amendment, WP-EM-04 closure, WP-EM-05 closure, WP-EM-06 closure, and WP-EM-07 closure are recorded in [Post-V1 Electrical Model Closure](POST_V1_ELECTRICAL_MODEL_CLOSURE.md). Do not enter later Work Packages or the future Annotation / Work-ticket Presentation Layer implicitly.

The Post-EM-07 Sequencing Amendment inserts two independent Post-EM-07 / Pre-EM-08 Work Packages without reopening WP-EM-06 or WP-EM-07. The remaining sequence is WP-EM-07A Transformer & Pole-Device Grounding Amendment, WP-EM-07B Transformer Naming Amendment, WP-EM-08 Electrical Model Interaction Stabilization, and WP-EM-09 Electrical Model Closure Integration. WP-EM-07A is the next Work Package and is Not Started / Planned; WP-EM-07B, WP-EM-08, and WP-EM-09 remain Planned. This amendment does not define a new release version, and FormatVersion remains V7.

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
