# P0-7-E-2-C-2-C-1 Thin Coordinator + Full BuildResult Implementation Plan

## 1. Purpose and current readiness

P0-7-E-2-C-2-C connects the two committed build stages without changing either stage's responsibilities:

```text
RingCabinetTemplateBuildRequest
        |
        v
RingCabinetTemplateBuildCoordinator
        |
        +--> RingCabinetTemplateDomainBuilder
        |        |
        |        v
        |    RingCabinetDomainBuildResult
        |
        +--> RingCabinetTemplateLayoutBuilder
                 |
                 v
             RingCabinetLayoutBuildResult
        |
        v
RingCabinetTemplateBuildResult
```

The current code is ready for this coordination layer:

- `DistributionDrawing.Application` owns the immutable template model and Template-to-Domain builder;
- `DistributionDrawing.Rendering.Wpf` owns `DocumentPoint`, `RingCabinetLayout`, and the Domain-to-RuntimeLayout builder;
- both stages already expose typed outcomes and typed failures;
- `AddRingCabinetCommand` already accepts the resulting `RingCabinet` and `RingCabinetLayout` and validates their cabinet IDs;
- neither builder modifies a project, command history, selection, or persistence state.

No production-code blocker exists for implementing the thin coordinator.

## 2. Actual API baseline

### 2.1 Application APIs

The committed Application stage contains:

- `RingCabinetTemplate`, including `LayoutRule` and derived `RequiredCapabilities`;
- `RingCabinetTemplateDomainBuilder.Build(RingCabinetTemplate?, string?)`;
- `RingCabinetDomainBuildOutcome`;
- `RingCabinetDomainBuildResult`, containing `Definition`, `Cabinet`, and a frozen capability snapshot;
- `RingCabinetDomainBuildFailure` and `RingCabinetDomainBuildFailureKind`;
- Domain failure kinds `InvalidTemplate`, `UnsupportedCapability`, and `DomainCreationFailure`.

### 2.2 Rendering.Wpf APIs

The committed RuntimeLayout stage contains:

- `RingCabinetTemplateLayoutBuilder.Build(RingCabinetDomainBuildResult?, RingCabinetLayoutRule?, DocumentPoint)`;
- `RingCabinetLayoutBuildOutcome`;
- `RingCabinetLayoutBuildResult`, containing `RingCabinetLayout`;
- `RingCabinetLayoutBuildFailure` and `RingCabinetLayoutBuildFailureKind`;
- Layout failure kinds `InvalidInput`, `MissingRequiredCapability`, `UnsupportedCapability`, `UnsupportedLayoutRule`, and `LayoutCreationFailure`.

The Layout builder verifies the required Layout capability, rejects PT/DTU capabilities, validates the rule and finite position, and delegates all geometry to `RingCabinetLayoutFactory`.

### 2.3 Later integration APIs

The current `AddRingCabinetCommand` constructor receives:

- `DrawingDocument`;
- `RuntimeLayoutDocument`;
- `RingCabinet`;
- `RingCabinetLayout`.

It rejects a cabinet/layout ID mismatch, adds both objects atomically as far as the current command boundary permits, removes both on Undo, and reuses the same objects on Redo.

`CommandStack`, `SelectionTransition`, and `ProjectRuntimeSession` are Desktop/editor integration concerns and remain outside C-2-C.

## 3. Coordinator layer decision

Add the coordinator to `DistributionDrawing.Rendering.Wpf`, under the existing template-building namespace:

```text
DistributionDrawing.Rendering.Wpf.Templates.RingCabinets.Building
```

This is the only current layer that may depend on both builders while preserving the established direction:

```text
Rendering.Wpf
    -> Application
        -> Domain
```

The coordinator must not be placed in Application because that would require `Application -> Rendering.Wpf` for `DocumentPoint`, `RingCabinetLayout`, and the Layout builder. Domain must remain unaware of both Application templates and RuntimeLayout.

## 4. Thin coordinator responsibility

Recommended type name:

```text
RingCabinetTemplateBuildCoordinator
```

Its `Build` operation performs exactly these steps:

1. validate that a request exists;
2. call `RingCabinetTemplateDomainBuilder.Build` once;
3. if Domain build fails, map the Domain failure and return immediately;
4. pass the successful Domain result, `request.Template.LayoutRule`, and `request.Position` to `RingCabinetTemplateLayoutBuilder.Build` once;
5. if Layout build fails, map the Layout failure and return immediately;
6. combine both successful stage results into a full result.

The coordinator must not:

- remap `BayTemplate` or `EquipmentConfiguration`;
- create a `RingCabinetDefinition` or call `RingCabinet.Create`;
- generate IDs;
- re-check PT, DTU, or other capabilities;
- interpret or replace the Layout rule;
- calculate any geometry;
- modify `DrawingDocument`, `RuntimeLayoutDocument`, or `ProjectRuntimeSession`;
- create or execute a command;
- change selection, Dirty state, scene, or inspector state.

## 5. Build request

Add an immutable request:

```text
RingCabinetTemplateBuildRequest
├── Template: RingCabinetTemplate
├── DisplayName: string
└── Position: DocumentPoint
```

Recommended rules:

- no public setters;
- require a non-null Template in the constructor;
- preserve DisplayName as request input and let the existing Domain builder own blank-name validation and normalization;
- preserve Position unchanged and let the existing Layout builder own finite-coordinate validation.

The request must not include `LayoutRule`. `RingCabinetTemplate.LayoutRule` is the single source of truth. A second rule in the request would allow conflicting facts.

The request also excludes Project, Session, CommandStack, Selection, DrawingScene, DTO, and persistence state.

## 6. Full build result

Add an immutable result:

```text
RingCabinetTemplateBuildResult
├── DomainResult: RingCabinetDomainBuildResult
├── LayoutResult: RingCabinetLayoutBuildResult
├── Definition: RingCabinetDefinition              (convenience projection)
├── Cabinet: RingCabinet                            (convenience projection)
├── Layout: RingCabinetLayout                       (convenience projection)
└── RequiredCapabilities: IReadOnlySet<TemplateCapability>
                                                   (convenience projection)
```

Composition is preferred over copying stage data. `Definition`, `Cabinet`, `Layout`, and `RequiredCapabilities` should return the exact values held by the two stage results. This preserves object identity, avoids duplicate snapshots, and keeps future stage-specific diagnostics available internally without exposing two Outcomes to callers.

The full result does not contain Template, Position, Scene, Project, Command, or Selection state.

## 7. Identity invariant

The full-result constructor must enforce:

```text
DomainResult.Cabinet.Id == LayoutResult.Layout.CabinetId
```

It must reject a mismatch with `ArgumentException` because such a mismatch is a programming/construction error, not a normal build failure.

The coordinator passes the exact Domain result produced by its single Domain-builder invocation into the Layout builder and then places that same result alongside the returned Layout result. It must not look up, clone, restore, or rebuild a cabinet between stages.

This guarantees that the full result contains the `RingCabinet`, interval IDs, switch IDs, and Layout generated from one build operation. Stable-ID equality is the cross-layer identity contract already used by `AddRingCabinetCommand`.

## 8. Unified outcome and failure model

Add one coordinator-facing outcome:

```text
RingCabinetTemplateBuildOutcome
├── IsSuccess
├── Result: RingCabinetTemplateBuildResult?
└── Failure: RingCabinetTemplateBuildFailure?
```

Add a unified failure kind:

```text
RingCabinetTemplateBuildFailureKind
├── InvalidTemplate
├── UnsupportedCapability
├── DomainCreationFailure
├── InvalidLayoutInput
├── MissingRequiredCapability
├── UnsupportedLayoutRule
└── LayoutCreationFailure
```

Add a failure stage:

```text
RingCabinetTemplateBuildFailureStage
├── Coordinator
├── Domain
└── Layout
```

`Coordinator` is used only for a missing build request. A full-result identity mismatch is a programming error and remains an exception rather than being converted into a normal build failure. Normal validation stays in the stage that owns it.

`RingCabinetTemplateBuildFailure` should expose read-only, typed fields sufficient for future UI handling:

- `Stage`;
- `Kind`;
- `Message`;
- frozen `UnsupportedCapabilities`;
- optional `MissingCapability`;
- optional `UnsupportedRuleId`;
- optional `Cause` for Domain/Layout creation failures.

The caller must not inspect exception messages or source failure type names. Static mapping methods such as `FromDomainFailure` and `FromLayoutFailure` should exhaustively map each existing enum value. An unrecognized future enum value should throw `ArgumentOutOfRangeException`, forcing the coordinator mapping to be consciously updated rather than silently misclassifying it.

## 9. Failure mapping

Recommended mapping:

| Source stage | Source failure | Unified stage | Unified kind |
| --- | --- | --- | --- |
| Domain | InvalidTemplate | Domain | InvalidTemplate |
| Domain | UnsupportedCapability | Domain | UnsupportedCapability |
| Domain | DomainCreationFailure | Domain | DomainCreationFailure |
| Layout | InvalidInput | Layout | InvalidLayoutInput |
| Layout | MissingRequiredCapability | Layout | MissingRequiredCapability |
| Layout | UnsupportedCapability | Layout | UnsupportedCapability |
| Layout | UnsupportedLayoutRule | Layout | UnsupportedLayoutRule |
| Layout | LayoutCreationFailure | Layout | LayoutCreationFailure |

Using `Stage` preserves whether an `UnsupportedCapability` arose during Domain or Layout processing without duplicating kinds such as `DomainUnsupportedCapability` and `LayoutUnsupportedCapability`.

## 10. Failure propagation and side effects

Domain failure flow:

```text
DomainBuilder.Build
        -> failed Domain outcome
        -> map failure
        -> return failed coordinator outcome
```

The Layout builder is not called.

Layout failure flow:

```text
DomainBuilder.Build
        -> successful in-memory Domain result
LayoutBuilder.Build
        -> failed Layout outcome
        -> map failure
        -> discard candidate result
        -> return failed coordinator outcome
```

The created Domain aggregate remains an uncommitted in-memory candidate and becomes unreachable after the operation. The coordinator does not add it to a project, create a command, or record selection. Therefore Layout failure creates no project half-state and no Dirty transition.

The coordinator never returns a partial success result.

## 11. Stable-ID contract

The coordinator must not call `Guid.NewGuid`, `RingCabinet.Create`, or either builder more than once per build request.

All Domain stable IDs originate from the one Domain-builder invocation. The Layout builder consumes that result and references its IDs. The coordinator only combines references.

Two separate coordinator Build calls are two separate creation attempts and may produce different IDs. Undo/Redo reuse is handled later by retaining the first successful full result in a command; the coordinator is not called during Redo.

## 12. Capability boundary

The coordinator implements no third capability policy.

- Domain capability support remains in `RingCabinetTemplateDomainBuilder`;
- Layout capability support and PT/DTU defensive guards remain in `RingCabinetTemplateLayoutBuilder`;
- the coordinator only maps typed failures.

It must not inspect `BayTemplate`, `SecondaryConfiguration`, PT, DTU, or `RequiredCapabilities` to make an independent decision.

## 13. LayoutRule and Position

The coordinator passes:

```text
request.Template.LayoutRule
```

directly to the Layout builder. There is no default substitution, fallback, rule-name inference, or request-level override.

The coordinator passes `request.Position` unchanged to the Layout builder. Position remains instance data, never enters `RingCabinetTemplate`, and is not supplied to the Domain builder.

## 14. Existing manual creation factory

Do not modify `RingCabinetCreationFactory` in C-2-C.

The two paths have different entry models and may coexist:

```text
Existing manual creation configuration
    -> RingCabinetCreationFactory

Template request
    -> RingCabinetTemplateBuildCoordinator
```

Consolidating creation paths would expand scope and is unnecessary for producing a valid full template build result.

## 15. Command integration readiness

The proposed full result supplies exactly the future command inputs:

```text
result.Cabinet
result.Layout
```

`AddRingCabinetCommand` already verifies their cabinet IDs and stores both object references. E-3 can use `DeviceCommandFactory` or a dedicated thin command-integration controller to bind the result to the current `DrawingDocument` and `RuntimeLayoutDocument`.

C-2-C does not modify `AddRingCabinetCommand`, `DeviceCommandFactory`, `CommandStack`, `SelectionTransition`, or `ProjectRuntimeSession`.

## 16. Undo/Redo contract for E-3

Freeze the following rule for later integration:

```text
First execution:
Coordinator.Build() exactly once
    -> Full BuildResult
    -> AddRingCabinetCommand stores Cabinet + Layout

Undo:
Remove the same Cabinet + Layout

Redo:
Re-add the same Cabinet + Layout
```

Redo must never call the coordinator or either builder. Rebuilding would generate new Domain IDs and break selection, persistence references, and command identity.

## 17. Coordinator test plan

Add tests to `tests/DistributionDrawing.Rendering.Wpf.Tests`:

1. LoadSwitch template builds a full result with the expected cabinet and layout.
2. IntegratedFeeder template builds successfully and preserves grounding structure through Domain while Layout covers its switches.
3. Mixed LoadSwitch/IntegratedFeeder template builds successfully.
4. Request Position appears unchanged in `result.Layout.Position`.
5. `result.Cabinet.Id` equals `result.Layout.CabinetId`.
6. Template bay order produces Domain Sequence order; non-continuous BayIndex values remain unchanged and do not reorder Layout intervals.
7. PT template returns a Domain-stage `UnsupportedCapability` and no full result.
8. DTU template returns a Domain-stage `UnsupportedCapability` and no full result.
9. Unknown `Template.LayoutRule` allows Domain creation, then returns a Layout-stage `UnsupportedLayoutRule` and no full result.
10. A two-LoadSwitch-bay template returns Domain-stage `DomainCreationFailure` and no full result.
11. Two separate successful Build calls produce different cabinet and internal Domain IDs.
12. Constructing a full result from mismatched Domain/Layout cabinet IDs is rejected.
13. A null request returns a Coordinator-stage `InvalidTemplate` and no result.
14. Unified failure fields preserve unsupported capabilities, missing capability, unsupported RuleId, and causes from their source failures.

No test should duplicate bay mapping or geometry logic. Assertions should observe the actual Domain and Layout outputs.

## 18. Interface and test-double decision

Do not introduce builder interfaces in the first coordinator implementation.

Both existing builders are deterministic, in-memory services with no project side effects. Real integration tests can verify the externally important short-circuit behavior:

- PT/DTU return a Domain-stage failure;
- invalid two-bay input returns a Domain-stage failure;
- no full result or Layout is returned.

An exact invocation-count spy would require interfaces only for an implementation-detail assertion. That cost is not justified now. Introduce minimal interfaces later only if builders acquire expensive or external dependencies, or if precise call-order observability becomes a production requirement.

## 19. Planned files

Expected new production files under:

```text
src/DistributionDrawing.Rendering.Wpf/Templates/RingCabinets/Building/
```

- `RingCabinetTemplateBuildRequest.cs` — immutable Template, DisplayName, and Position input;
- `RingCabinetTemplateBuildCoordinator.cs` — thin two-stage orchestration;
- `RingCabinetTemplateBuildResult.cs` — composed full result and identity check;
- `RingCabinetTemplateBuildOutcome.cs` — success/failure union;
- `RingCabinetTemplateBuildFailure.cs` — unified typed failure data and stage mappings;
- `RingCabinetTemplateBuildFailureKind.cs` — unified failure categories;
- `RingCabinetTemplateBuildFailureStage.cs` — Coordinator/Domain/Layout diagnostics.

Expected test modification:

```text
tests/DistributionDrawing.Rendering.Wpf.Tests/
```

- add `RingCabinetTemplateBuildCoordinatorTests.cs`.

No project-reference change should be necessary because Rendering.Wpf already references Application and the Rendering test project already references both projects.

Do not modify Domain, Application, Infrastructure, Persistence, Desktop, Command, Selection, or the manual creation factory.

## 20. C-2-C and E-3 boundary

C-2-C ends at:

```text
Template
    -> Domain
    -> RuntimeLayout
    -> Full BuildResult
```

The result remains an uncommitted candidate.

E-3 begins at:

```text
Full BuildResult
    -> AddRingCabinetCommand
    -> CommandStack
    -> Scene refresh
    -> SelectionTransition
    -> Dirty / Undo / Redo
```

C-2-C must not add the result to Project or pre-implement any E-3 behavior.

## 21. Risks and controls

### 21.1 Unified outcome complexity

Risk: a third failure model can merely duplicate two existing models.

Control: keep it as a small adapter with exhaustive mappings, shared kinds where semantics match, and a Stage field for source diagnostics. Do not create free-form metadata or nested source Outcomes.

### 21.2 Cabinet/Layout mismatch

Risk: callers could combine Cabinet A with Layout B.

Control: compose the exact stage results and enforce cabinet-ID equality in the full-result constructor. `AddRingCabinetCommand` retains its independent ID guard as defense in depth.

### 21.3 Repeated Domain build

Risk: accidental retry or duplicate invocation inside one coordinator call changes IDs.

Control: one linear implementation with one local `domainOutcome` and one local `layoutOutcome`; no retry or fallback path.

### 21.4 Layout failure after Domain creation

Risk: an in-memory aggregate exists although no full result is produced.

Control: builders are side-effect free relative to Project; discard the candidate and return no partial result. No rollback API is needed.

### 21.5 Capability duplication

Risk: coordinator policy diverges from stage builders.

Control: coordinator never reads capabilities to decide support; it only maps stage failures.

### 21.6 LayoutRule dual source

Risk: Request and Template provide conflicting rules.

Control: omit LayoutRule from Request and always use `Template.LayoutRule`.

### 21.7 Redo rebuilding

Risk: future Redo calls the coordinator and creates new IDs.

Control: freeze the E-3 contract that Command stores and reuses the first full result's Cabinet and Layout.

## 22. Implementation order

1. add immutable request and full-result types;
2. add failure stage, unified kind, and typed failure mappings;
3. add unified outcome;
4. implement the linear coordinator using the two concrete builders;
5. add success, identity, short-circuit, and failure-mapping tests;
6. run `git diff --check` and the Rendering.Wpf test project where .NET is available;
7. perform a read-only Final Review before commit.

## 23. Decision

Proceed with C-2-C implementation in `DistributionDrawing.Rendering.Wpf` using concrete builders, one immutable request, one composed full result with an ID invariant, and one stage-aware unified failure model.

No interface, Project mutation, Command integration, Selection handling, or Application/Domain change is required.
