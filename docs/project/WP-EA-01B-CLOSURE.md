# WP-EA-01B Closure — Scenario UI & Energization Visualization

## Status

**CLOSED / ACCEPTED.** The implementation candidate is `d5ad046b515c7d90c9ac1511d0a71ba694771b6a` on `wp-ea-01b`. The accepted implementation history was preserved by fast-forwarding the formal branch.

## Scope delivered

WP-EA-01B (`Scenario UI & Energization Visualization`) delivered and froze:

- Energization Scenario configuration UI, professional Candidate naming, typed Source Side selection, multiple Seeds, and source-set completeness.
- Explicit Start Analysis; after analysis, return to ordinary Drawing while retaining the Current EnergizationResult display.
- Real-time reanalysis for SwitchState Open / Close / Undo / Redo.
- Native scene-state rendering using precise Terminal / Node / Edge identity for Ring Cabinet, PT, Cable, CableTermination, OHL, Pole, pole number, and device labels.
- Unknown visual semantics, aggregate Hazard visuals, and the GroundSwitch visual exception.
- Selection / HitTest coexistence and show / hide energization display.

## Frozen contracts

### Analysis

- A Seed is a known energized Boundary plus a Side. Multiple Seeds are supported, and `EnergizedBy` preserves every contributing source.
- An incomplete source set never permits an unreached area to be labeled Deenergized. Deenergized is emitted only when the source-set completeness condition is satisfied.
- A GroundSwitch Earth branch does not participate in ordinary energization propagation.

### Runtime

- `EnergizationResult` is runtime-only and is not persisted.
- A SwitchState change can trigger real-time reanalysis.
- Topology or Scenario mutation makes the current result stale. A stale result is not displayed as Current.

### Rendering

- Rendering does not create electrical facts. Visual state is resolved from explicit Terminal / Node / Edge identity; spatial proximity is not used to infer electrical connectivity.
- Energized, Deenergized, and Unknown styles apply to the original drawing primitives. Precise electrical state remains separate from aggregate Hazard visuals.

### Hazard visuals

- For ordinary equipment, any Energized member produces the red Hazard visual.
- For GroundSwitch, the label, ground symbol, and Earth branch remain normal black.

## Acceptance evidence

### Windows automated acceptance

All five test projects passed on the accepted candidate:

| Test project | Passed | Failed | Skipped | Total |
| --- | ---: | ---: | ---: | ---: |
| Domain | 177 | 0 | 0 | 177 |
| Application | 216 | 0 | 0 | 216 |
| Infrastructure | 113 | 0 | 0 | 113 |
| Rendering.Wpf | 657 | 0 | 0 | 657 |
| Desktop | 360 | 0 | 0 | 360 |
| **Total** | **1523** | **0** | **0** | **1523** |

### Windows GUI acceptance

The user completed actual Windows GUI operation and confirmed that the feature met this phase's requirements. Result: **PASSED / ACCEPTED**.

## Deferred / out of scope

The following are not WP-EA-01B closure blockers and require their own future scope:

- **EA state-aware export:** current PNG / export output does not include runtime Energization visual styles.
- **EA → WorkScope Draft:** derive a work-scope draft from the non-source sides of multiple source boundaries and connect it to WorkTicket.
- **WorkScope / WorkTicket integration:** plan and implement as a later package.
- **WorkRange UX:** decide in the later WorkScope package whether the independent WorkRange entry remains or joins the EA / WorkTicket flow.
- **Device Interlock:** breaker / isolation / GroundSwitch operational interlocks and prevention of illegal combinations belong in a separate package, not in the Analyzer.
- **Grounding conflict:** GroundingTarget / energization conflict handling remains later work.

These items are recommendations for separate planning only; this closure does not start them.
