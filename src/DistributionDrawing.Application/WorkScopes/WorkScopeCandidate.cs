using DistributionDrawing.Domain.Devices;

namespace DistributionDrawing.Application.WorkScopes;

public enum WorkScopeCandidateDiagnosticCode
{
    CurrentResultUnavailable,
    NoSeeds,
    FailedAnalysis,
    StaleAnalysis,
    IdentityMismatch,
    ConductingEdgeStateMismatch,
    BoundaryTransitionAmbiguous,
    UnsupportedStructure,
    EmptyCandidate
}

public sealed record WorkScopeCandidateDiagnostic(
    WorkScopeCandidateDiagnosticCode Code,
    Guid? Identity = null,
    string? Detail = null);

public sealed class WorkScopeCandidate
{
    internal WorkScopeCandidate(
        IEnumerable<WorkScopeCandidateRegion> regions,
        IEnumerable<WorkScopeCandidateBoundary> boundaries,
        IEnumerable<WorkScopeCandidateDiagnostic> diagnostics)
    {
        Regions = Array.AsReadOnly(regions.ToArray());
        Boundaries = Array.AsReadOnly(boundaries.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public IReadOnlyList<WorkScopeCandidateRegion> Regions { get; }
    public IReadOnlyList<WorkScopeCandidateBoundary> Boundaries { get; }
    public IReadOnlyList<WorkScopeCandidateDiagnostic> Diagnostics { get; }
    public bool IsEmpty => Regions.Count == 0;
}

public sealed class WorkScopeCandidateRegion
{
    internal WorkScopeCandidateRegion(IEnumerable<Guid> terminalIds, IEnumerable<Guid> electricalNodeIds)
    {
        TerminalIds = Array.AsReadOnly(terminalIds.Order().ToArray());
        ElectricalNodeIds = Array.AsReadOnly(electricalNodeIds.Order().ToArray());
    }

    public IReadOnlyList<Guid> TerminalIds { get; }
    public IReadOnlyList<Guid> ElectricalNodeIds { get; }
}

/// <summary>A transient fact about an EA transition; it is not a WorkScope or WTA boundary.</summary>
public sealed class WorkScopeCandidateBoundary
{
    internal WorkScopeCandidateBoundary(
        Guid switchDeviceId,
        SwitchKind switchKind,
        SwitchInstallationType installationType,
        Guid deenergizedTerminalId,
        Guid energizedTerminalId,
        Guid? topologyParentId,
        Guid? attachedPoleId,
        IEnumerable<Guid> relatedConnectionIds)
    {
        SwitchDeviceId = switchDeviceId;
        SwitchKind = switchKind;
        InstallationType = installationType;
        DeenergizedTerminalId = deenergizedTerminalId;
        EnergizedTerminalId = energizedTerminalId;
        TopologyParentId = topologyParentId;
        AttachedPoleId = attachedPoleId;
        RelatedConnectionIds = Array.AsReadOnly(relatedConnectionIds.Order().ToArray());
    }

    public Guid SwitchDeviceId { get; }
    public SwitchKind SwitchKind { get; }
    public SwitchInstallationType InstallationType { get; }
    public Guid DeenergizedTerminalId { get; }
    public Guid EnergizedTerminalId { get; }
    public Guid? TopologyParentId { get; }
    public Guid? AttachedPoleId { get; }
    public IReadOnlyList<Guid> RelatedConnectionIds { get; }
}

public sealed class WorkScopeCandidateProjection
{
    internal WorkScopeCandidateProjection(
        WorkScopeCandidate? candidate,
        IEnumerable<WorkScopeCandidateDiagnostic> diagnostics)
    {
        Candidate = candidate;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public WorkScopeCandidate? Candidate { get; }
    public IReadOnlyList<WorkScopeCandidateDiagnostic> Diagnostics { get; }
    public bool IsValid => Candidate is not null;
}
