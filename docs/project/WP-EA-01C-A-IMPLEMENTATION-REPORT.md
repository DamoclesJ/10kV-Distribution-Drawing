# WP-EA-01C-A Implementation Report — EA → Structural Deenergized WorkScope Draft

## State and baseline

- **Branch:** `wp-ea-01c-a`
- **Base:** `origin/wp-ea-01b` at `8ddace96206dcb45f0a2fb1b06f93e1057fd36a2`; the branch was created after resetting the clean local `wp-ea-01b` to that SHA.
- **State at implementation review:** Mac implementation verified. Candidate commit and push are a separate step; Windows acceptance and closure remain outstanding.
- **Frozen contracts retained:** WP-EA-01A/01B Seed, typed Side, Boundary Policy, SourceSetComplete, EnergizationAnalyzer, precise runtime result and rendering behavior; WP-WTA-01 WorkRange/WorkTicket confirmation and persistence.

## Design and behavior

1. **Structural graph:** `WorkScopeStructuralGraph` reuses `ElectricalConnectivityGraphBuilder` for actual Terminal, ElectricalNode and Connection relationships, including Cable, OHL and the CableTermination `CableSideTerminal → InternalNode → OverheadSideTerminal` path. It does not use drawing geometry or 01B visual segments.
2. **Switch traversal:** it removes the EA graph's state-dependent closed-switch edges, then adds a structural crossing for each non-boundary LoadSwitch, IsolationSwitch, CircuitBreaker and DropoutFuse. Open and Closed produce the same structural range. GroundSwitch and Earth branches are excluded.
3. **Boundary blocking:** only switches declared by the current Scenario Seeds have their crossing omitted. Their WorkSide terminal is included as an edge of the region; the boundary device itself is described as a boundary and is excluded from the derived device summary.
4. **Seed opposite-side resolution:** `WorkScopeBoundaryResolver` derives typed `WorkSide` and calls the existing `EnergizationBoundaryPolicy` separately for source and work sides. Both distinct terminal identities and switch ownership must be proved. The runtime anchor keeps SeedId, BoundaryDeviceId, typed sides, both terminal IDs and optional node IDs.
5. **RingCabinet:** `Bus → Line` and `Line → Bus`; the source and work terminals are resolved by the existing EA policy, never from UI labels. An arbitrary cabinet switch is not promoted into an eligible Seed.
6. **Pole:** `LargerNumber → SmallerNumber` and the reverse use the existing topology and pole-order proof. Missing or coincident sides return `BoundaryResolutionFailed`.
7. **DropoutFuse:** an eligible, dual-side-resolvable fuse may be a Seed Boundary. An internal fuse is traversed regardless of Open/Closed. The Draft warns that installed/removed fuse-tube state needs field verification; no such state is inferred.
8. **GroundSwitch:** the EA Boundary Policy rejects it as a Seed; the structural graph also excludes its crossing and Earth branch.
9. **Two and three-plus boundaries:** one algorithm resolves every Seed, blocks every declared crossing, and checks that all WorkSide anchors lie in one structural component. The result remains a runtime multi-boundary Draft; it is not compressed into a two-boundary Domain model. Zero Seeds gives `NoSeeds`; one gives `TooFewBoundaries`.
10. **Ambiguity:** disconnected WorkSides give `DisconnectedAnchors`. Repeated Boundary devices or any SourceSide terminal reachable in the candidate component give `AmbiguousRegion`. A modelled harmless spur can remain in the component; the Draft warns that field external connections still require review.
11. **Safety validation:** Scenario and structural topology determine shape first. Only a Current, Complete `EnergizationResult` with `IsSourceSetComplete` can validate it. Every candidate Terminal and ElectricalNode must have a precise `Deenergized` result. An Energized or Unknown/missing point hard-blocks, with the most specific available Terminal/device or Node diagnostic. Connection membership comes from structural edges with both endpoints in the region; its safety follows their precise terminal states. Hazard aggregates, red visuals and labels are never consulted.
12. **Grounding diagnostic:** if a precise energized terminal also has an explicit GroundingPoint or EA GroundingSwitch connection, the energized hard block mentions that fact. No GroundingTarget conflict engine or Interlock rule was added.
13. **Draft model:** `WorkScopeDraftResult` holds status, anchors, frozen terminal/node/connection sets, derived device summary, diagnostics, warnings, scenario identity and EA result identity. Summary objects are for review only; none become `WorkScopeItems` or work objects.
14. **Freshness:** `IsCurrent` requires the same Current EA result object, scenario ID and captured Scenario/topology/switch-state context. Seed, completeness, topology or switch changes, result invalidation and reanalysis invalidate an old successful Draft. Regeneration produces a new DraftId.
15. **Desktop UI:** the EA panel has **“根据带电边界自动确定停电工作范围”** when a Current Complete result exists. It displays each boundary's device and source/work sides, derived cabinets/cables/terminations/OHL/poles/devices, warnings and specific Chinese failure messages. It labels a successful result as an unconfirmed Draft and shows when it becomes stale.
16. **CommandStack and persistence:** generation only calculates a runtime value; it issues no command and changes no Domain, WorkTicket, WorkScope or FormatVersion V9 data. A Draft disappears on save/reopen. The old “工作范围” entry remains.
17. **WTA impact:** none. No call to `WorkTicketRangeSetup.Confirm` or `WorkTicketChangeCommand`; no change to IsolationBoundaries, WorkScopeItems, ticket sections 6.1–6.4, WorkTicket analyzer or persistence. Confirmation and WorkTicket navigation belong to WP-EA-01C-B.

## Verification on Mac

| Check | Result |
| --- | --- |
| `dotnet build src/DistributionDrawing.sln --no-restore -v quiet` | Passed, 0 errors; latest run 3 NU1900 warnings from inaccessible NuGet vulnerability cache |
| Domain tests | 177/177 passed |
| Application tests | 238/238 passed, including 22 focused `WorkScopeDraftTests` |
| Infrastructure tests | 113/113 passed |
| Rendering.Wpf.Tests project compile | Passed, 0 errors |
| Desktop.Tests project compile | Passed, 0 errors |
| `git diff --check` | Passed |

The focused tests cover typed cabinet and pole sides, unresolved pole side, GroundSwitch exclusion, DropoutFuse, two and three Boundary regions, Open/Closed internal isolation/load/breaker/fuse, CableTermination with Cable and OHL, disconnected and bypassed-source ambiguity, source-side exclusion, Current/Complete preconditions, Energized and Unknown precise points, grounding diagnostic, and runtime freshness. The tests verify no Domain WorkScope is created by generation. Existing Domain/Infrastructure tests and unchanged persistence code provide compatibility evidence; the new Draft has no serializer.

## Limits and remaining acceptance

- The structural candidate is based on **the current drawing and modelled topology**. It cannot prove unmodelled field feeds, all possible external connections, or the physical installed/removed state of a fuse. The warning states this limit.
- A valid disconnected multi-region work area does not produce a Draft; all WorkSides must face one component. A single Boundary is never expanded into a guessed open-ended area.
- Connection state is validated through its endpoint terminals; `EnergizationResult` does not expose a separate connection-state map for this Draft.
- No full Device Interlock, GroundingTarget conflict engine, automatic ticket fields, WorkTicket confirmation, ticket navigation or 01C-B work is included.
- Mac build and test results establish compile and non-Windows test evidence only. Windows WPF runtime tests and GUI review of button placement, boundary labels, warning/failure rendering and stale behavior remain required on a committed and pushed candidate before Windows acceptance. No Windows pass is claimed here.
