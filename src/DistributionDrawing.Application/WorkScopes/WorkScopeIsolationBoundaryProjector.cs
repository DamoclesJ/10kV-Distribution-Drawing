using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.WorkScopes;

public enum WorkScopeIsolationBoundaryProjectionStatus
{
    Complete,
    Unrepresentable,
    Invalid
}

public enum WorkScopeBoundaryClassification
{
    A,
    B,
    C,
    Invalid
}

public enum WorkScopeBoundaryProjectionDiagnosticCode
{
    MissingWorkScope,
    InvalidWorkScope,
    MissingDevice,
    MissingTerminal,
    MissingConnection,
    InvalidBoundaryOwnership,
    UnsupportedCustomerStationBoundary,
    UnresolvedPoleDirection,
    WtaResolverRejected,
    GroundingBoundary,
    UnsupportedInstallationType,
    DuplicateNormalizedOutput
}

public sealed record WorkScopeBoundaryProjectionDiagnostic(
    WorkScopeBoundaryProjectionDiagnosticCode Code,
    string Message,
    WorkScopeBoundary? SourceBoundary = null);

public sealed record WorkScopeBoundaryProjection(
    WorkScopeBoundary SourceBoundary,
    WorkScopeBoundaryClassification Classification,
    IsolationBoundary? IsolationBoundary,
    IReadOnlyList<WorkScopeBoundaryProjectionDiagnostic> Diagnostics);

public sealed record ProjectedIsolationBoundary(
    IsolationBoundary IsolationBoundary,
    IReadOnlyList<WorkScopeBoundary> SourceBoundaries);

public sealed class WorkScopeIsolationBoundaryProjection
{
    internal WorkScopeIsolationBoundaryProjection(
        WorkScopeIsolationBoundaryProjectionStatus status,
        IEnumerable<WorkScopeBoundaryProjection> boundaryResults,
        IEnumerable<ProjectedIsolationBoundary> projectedBoundaries,
        IEnumerable<WorkScopeBoundaryProjectionDiagnostic> diagnostics)
    {
        Status = status;
        BoundaryResults = Array.AsReadOnly(boundaryResults.ToArray());
        ProjectedBoundaries = Array.AsReadOnly(projectedBoundaries.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        IsolationBoundaries = status == WorkScopeIsolationBoundaryProjectionStatus.Complete
            ? Array.AsReadOnly(ProjectedBoundaries.Select(item => item.IsolationBoundary).ToArray())
            : Array.Empty<IsolationBoundary>();
    }

    public WorkScopeIsolationBoundaryProjectionStatus Status { get; }
    public bool IsComplete => Status == WorkScopeIsolationBoundaryProjectionStatus.Complete;
    public IReadOnlyList<WorkScopeBoundaryProjection> BoundaryResults { get; }

    /// <summary>Distinct successful mappings, retained for review even when the whole projection is partial.</summary>
    public IReadOnlyList<ProjectedIsolationBoundary> ProjectedBoundaries { get; }

    /// <summary>Safe handoff collection; partial or invalid projections always expose an empty collection.</summary>
    public IReadOnlyList<IsolationBoundary> IsolationBoundaries { get; }
    public IReadOnlyList<WorkScopeBoundaryProjectionDiagnostic> Diagnostics { get; }
}

/// <summary>Projects one persisted WorkScope snapshot to the existing WTA boundary contract without mutation.</summary>
public sealed class WorkScopeIsolationBoundaryProjector
{
    private readonly FirstKindRulePack _rules = new();

    public WorkScopeIsolationBoundaryProjection Project(DrawingDocument drawing, Guid workScopeId)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        WorkScope? scope = drawing.WorkScopes.SingleOrDefault(item => item.WorkScopeId == workScopeId);
        if (scope is null)
        {
            return Result(WorkScopeIsolationBoundaryProjectionStatus.Invalid, [], [],
                [Diagnostic(WorkScopeBoundaryProjectionDiagnosticCode.MissingWorkScope,
                    $"WorkScope '{workScopeId}' does not exist.")]);
        }

        if (scope.Regions.Count == 0)
        {
            return Result(WorkScopeIsolationBoundaryProjectionStatus.Invalid, [], [],
                [Diagnostic(WorkScopeBoundaryProjectionDiagnosticCode.InvalidWorkScope,
                    "A confirmed WorkScope must contain at least one Region.")]);
        }

        WorkScopeBoundaryProjectionDiagnostic? referenceIssue = ValidateReferences(drawing, scope);
        if (referenceIssue is not null)
            return Result(WorkScopeIsolationBoundaryProjectionStatus.Invalid, [], [], [referenceIssue]);

        WorkScopeBoundaryProjection[] boundaryResults = scope.Boundaries
            .OrderBy(item => item.DeviceId)
            .ThenBy(item => item.Side)
            .ThenBy(item => item.TerminalId)
            .ThenBy(item => item.ConnectionId)
            .Select(boundary => ProjectBoundary(drawing, boundary))
            .ToArray();
        WorkScopeBoundaryProjectionDiagnostic[] diagnostics = boundaryResults
            .SelectMany(item => item.Diagnostics)
            .OrderBy(item => item.Code)
            .ThenBy(item => item.SourceBoundary?.DeviceId)
            .ThenBy(item => item.SourceBoundary?.Side)
            .ThenBy(item => item.SourceBoundary?.TerminalId)
            .ThenBy(item => item.SourceBoundary?.ConnectionId)
            .ToArray();

        var mappedGroups = boundaryResults.Where(item => item.IsolationBoundary is not null)
            .GroupBy(item => item.IsolationBoundary!)
            .OrderBy(group => group.Key.DeviceId)
            .ThenBy(group => group.Key.Side)
            .ThenBy(group => group.Key.TerminalId)
            .ThenBy(group => group.Key.ConnectionId)
            .ToArray();
        ProjectedIsolationBoundary[] projected = mappedGroups.Select(group =>
            new ProjectedIsolationBoundary(group.Key,
                Array.AsReadOnly(group.Select(item => item.SourceBoundary)
                    .OrderBy(item => item.DeviceId)
                    .ThenBy(item => item.Side)
                    .ThenBy(item => item.TerminalId)
                    .ThenBy(item => item.ConnectionId)
                    .ToArray()))).ToArray();

        WorkScopeBoundaryProjectionDiagnostic[] duplicateDiagnostics = mappedGroups
            .Where(group => group.Count() > 1)
            .Select(group => Diagnostic(WorkScopeBoundaryProjectionDiagnosticCode.DuplicateNormalizedOutput,
                "Multiple WorkScope boundaries resolve to the same WTA IsolationBoundary; output was deduplicated.",
                group.Select(item => item.SourceBoundary).OrderBy(item => item.DeviceId).First()))
            .ToArray();
        diagnostics = diagnostics.Concat(duplicateDiagnostics)
            .OrderBy(item => item.Code)
            .ThenBy(item => item.SourceBoundary?.DeviceId)
            .ThenBy(item => item.SourceBoundary?.Side)
            .ThenBy(item => item.SourceBoundary?.TerminalId)
            .ThenBy(item => item.SourceBoundary?.ConnectionId)
            .ToArray();

        WorkScopeIsolationBoundaryProjectionStatus status = boundaryResults.Any(item =>
                item.Classification == WorkScopeBoundaryClassification.Invalid)
            ? WorkScopeIsolationBoundaryProjectionStatus.Invalid
            : boundaryResults.Any(item => item.Classification == WorkScopeBoundaryClassification.C)
                ? WorkScopeIsolationBoundaryProjectionStatus.Unrepresentable
                : WorkScopeIsolationBoundaryProjectionStatus.Complete;
        return Result(status, boundaryResults, projected, diagnostics);
    }

    private WorkScopeBoundaryProjection ProjectBoundary(DrawingDocument drawing, WorkScopeBoundary source)
    {
        SwitchDevice? device = drawing.Devices.OfType<SwitchDevice>()
            .SingleOrDefault(item => item.Id == source.DeviceId);
        if (device is null)
        {
            if (drawing.Devices.Any(item => item.Id == source.DeviceId))
                return Unrepresentable(source, WorkScopeBoundaryProjectionDiagnosticCode.UnsupportedInstallationType,
                    "The boundary device exists but is not a WTA-resolvable switch device.");
            return Invalid(source, WorkScopeBoundaryProjectionDiagnosticCode.MissingDevice,
                "The WorkScope boundary device does not exist.");
        }

        if (device.SwitchKind == SwitchKind.GroundSwitch)
            return Invalid(source, WorkScopeBoundaryProjectionDiagnosticCode.GroundingBoundary,
                "GroundSwitch is GS grounding authority and cannot be projected as an ordinary WTA isolation boundary.");

        if (device.InstallationType == SwitchInstallationType.CustomerStationIncomingFeeder)
            return ProjectCustomerStationBoundary(drawing, device, source);

        if (device.InstallationType == SwitchInstallationType.Pole)
            return ProjectPoleBoundary(drawing, device, source);

        if (device.InstallationType != SwitchInstallationType.CabinetInterval)
            return Unrepresentable(source, WorkScopeBoundaryProjectionDiagnosticCode.UnsupportedInstallationType,
                "The current WTA boundary contract does not support this switch installation type.");

        if (source.Side is not (BoundarySide.Bus or BoundarySide.Line))
            return Invalid(source, WorkScopeBoundaryProjectionDiagnosticCode.InvalidBoundaryOwnership,
                "A RingCabinet boundary must use its persisted Bus or Line side.");

        if (source.TerminalId is Guid terminalId &&
            !drawing.Terminals.Any(item => item.Id == terminalId && device.TerminalIds.Contains(item.Id)))
            return Invalid(source, WorkScopeBoundaryProjectionDiagnosticCode.MissingTerminal,
                "The boundary terminal is missing or does not belong to its switch.");

        if (source.ConnectionId is Guid connectionId &&
            !drawing.Connections.Any(item => item.Id == connectionId))
            return Invalid(source, WorkScopeBoundaryProjectionDiagnosticCode.MissingConnection,
                "The boundary connection does not exist.");

        if (!WorkTicketRangeSetup.TryResolve(drawing, device.Id, source.Side,
                out IsolationBoundary? resolved, out string issue))
            return Unrepresentable(source, WorkScopeBoundaryProjectionDiagnosticCode.WtaResolverRejected,
                $"The existing WTA resolver rejected this boundary: {issue}");

        if (!MatchesPersistedIdentity(source, resolved!, out string? mismatch))
            return Invalid(source, WorkScopeBoundaryProjectionDiagnosticCode.InvalidBoundaryOwnership, mismatch!);

        return ValidateResolved(drawing, source, resolved!,
            device.SwitchKind == SwitchKind.LoadSwitch && IsExact(source, resolved!)
                ? WorkScopeBoundaryClassification.A
                : WorkScopeBoundaryClassification.B);
    }

    private static WorkScopeBoundaryProjectionDiagnostic? ValidateReferences(
        DrawingDocument drawing,
        WorkScope scope)
    {
        foreach (WorkScopeRegion region in scope.Regions)
        {
            if (region.TerminalIds.Any(id => !drawing.Terminals.Any(item => item.Id == id)) ||
                region.ElectricalNodeIds.Any(id => !drawing.ElectricalNodes.Any(item => item.Id == id)))
                return Diagnostic(WorkScopeBoundaryProjectionDiagnosticCode.InvalidWorkScope,
                    "The WorkScope Region contains a missing Terminal or ElectricalNode reference.");
        }

        foreach (WorkScopeBoundary boundary in scope.Boundaries)
        {
            if (!drawing.Devices.Any(item => item.Id == boundary.DeviceId))
                return Diagnostic(WorkScopeBoundaryProjectionDiagnosticCode.MissingDevice,
                    "The WorkScope boundary device does not exist.", boundary);
            if (boundary.TerminalId is Guid terminalId)
            {
                Terminal? terminal = drawing.Terminals.SingleOrDefault(item => item.Id == terminalId);
                if (terminal is null)
                    return Diagnostic(WorkScopeBoundaryProjectionDiagnosticCode.MissingTerminal,
                        "The WorkScope boundary terminal does not exist.", boundary);
                if (!TerminalBelongsToDevice(drawing, terminal, boundary.DeviceId))
                    return Diagnostic(WorkScopeBoundaryProjectionDiagnosticCode.InvalidBoundaryOwnership,
                        "The WorkScope boundary terminal is not owned by its boundary device.", boundary);
            }
            if (boundary.ConnectionId is Guid connectionId)
            {
                Connection? connection = drawing.Connections.SingleOrDefault(item => item.Id == connectionId);
                if (connection is null)
                    return Diagnostic(WorkScopeBoundaryProjectionDiagnosticCode.MissingConnection,
                        "The WorkScope boundary connection does not exist.", boundary);
                bool incident = boundary.TerminalId is Guid id
                    ? connection.UsesTerminal(id)
                    : drawing.Terminals.Where(item => TerminalBelongsToDevice(drawing, item, boundary.DeviceId))
                        .Any(item => connection.UsesTerminal(item.Id));
                if (!incident)
                    return Diagnostic(WorkScopeBoundaryProjectionDiagnosticCode.InvalidBoundaryOwnership,
                        "The WorkScope boundary connection is not incident to its boundary device or terminal.", boundary);
            }
        }

        try
        {
            drawing.ValidateWorkScopeReferences(scope);
        }
        catch (InvalidOperationException error)
        {
            return Diagnostic(WorkScopeBoundaryProjectionDiagnosticCode.InvalidWorkScope, error.Message);
        }
        return null;
    }

    private static bool TerminalBelongsToDevice(DrawingDocument drawing, Terminal terminal, Guid deviceId) =>
        terminal.OwnerType == TopologyOwnerType.Device && terminal.OwnerId == deviceId ||
        drawing.Devices.OfType<RingCabinet>().Any(cabinet => cabinet.Id == deviceId &&
            cabinet.Terminals.Any(item => item.Id == terminal.Id)) ||
        drawing.CustomerStations.Any(station => station.Id == deviceId && station.IncomingFeeders.Any(feeder =>
            feeder.CableTerminalId == terminal.Id || feeder.StationTerminalId == terminal.Id));

    private WorkScopeBoundaryProjection ProjectPoleBoundary(
        DrawingDocument drawing, SwitchDevice device, WorkScopeBoundary source)
    {
        if (source.Side is not (BoundarySide.SmallerNumber or BoundarySide.LargerNumber))
            return Invalid(source, WorkScopeBoundaryProjectionDiagnosticCode.InvalidBoundaryOwnership,
                "A Pole boundary must use the persisted SmallerNumber or LargerNumber side.");
        if (drawing.PoleAttachments.Count(item => item.AttachedDeviceId == device.Id) != 1 ||
            !drawing.Devices.OfType<Pole>().Any(pole => drawing.PoleAttachments.Any(item =>
                item.AttachedDeviceId == device.Id && item.PoleId == pole.Id)))
            return Invalid(source, WorkScopeBoundaryProjectionDiagnosticCode.InvalidBoundaryOwnership,
                "The Pole switch does not have one valid attached-pole identity.");
        if (source.TerminalId is Guid terminalId &&
            !drawing.Terminals.Any(item => item.Id == terminalId && device.TerminalIds.Contains(item.Id)))
            return Invalid(source, WorkScopeBoundaryProjectionDiagnosticCode.MissingTerminal,
                "The boundary terminal is missing or does not belong to the Pole switch.");
        if (source.ConnectionId is Guid connectionId &&
            !drawing.Connections.Any(item => item.Id == connectionId))
            return Invalid(source, WorkScopeBoundaryProjectionDiagnosticCode.MissingConnection,
                "The boundary connection does not exist.");

        if (!WorkTicketRangeSetup.TryResolve(drawing, device.Id, source.Side,
                out IsolationBoundary? resolved, out _))
            return Unrepresentable(source, WorkScopeBoundaryProjectionDiagnosticCode.UnresolvedPoleDirection,
                "The Pole direction is Equal, Unresolved, or cannot be represented by the existing WTA resolver.");

        if (!MatchesPersistedIdentity(source, resolved!, out string? mismatch))
            return Invalid(source, WorkScopeBoundaryProjectionDiagnosticCode.InvalidBoundaryOwnership, mismatch!);
        return ValidateResolved(drawing, source, resolved!, WorkScopeBoundaryClassification.B);
    }

    private WorkScopeBoundaryProjection ProjectCustomerStationBoundary(
        DrawingDocument drawing, SwitchDevice device, WorkScopeBoundary source)
    {
        IncomingFeeder? feeder = drawing.CustomerStations.SelectMany(item => item.IncomingFeeders)
            .SingleOrDefault(item => item.IsolationSwitch.Id == device.Id);
        if (feeder is null || feeder.IsolationSwitch.ParentId != feeder.IncomingFeederId)
            return Invalid(source, WorkScopeBoundaryProjectionDiagnosticCode.InvalidBoundaryOwnership,
                "The CustomerStation isolation switch has no valid incoming-feeder owner.");

        Guid? expectedTerminal = source.Side switch
        {
            BoundarySide.Source => feeder.CableTerminalId,
            BoundarySide.Load => feeder.StationTerminalId,
            _ => null
        };
        if (expectedTerminal is null)
            return Invalid(source, WorkScopeBoundaryProjectionDiagnosticCode.InvalidBoundaryOwnership,
                "A CustomerStation boundary must identify its persisted Source or Load side.");
        if (source.TerminalId is Guid terminalId && terminalId != expectedTerminal)
            return Invalid(source, WorkScopeBoundaryProjectionDiagnosticCode.InvalidBoundaryOwnership,
                "The CustomerStation terminal does not match the persisted WorkScope side.");
        if (source.ConnectionId is Guid connectionId &&
            drawing.Connections.Single(item => item.Id == connectionId).UsesTerminal(expectedTerminal.Value) == false)
            return Invalid(source, WorkScopeBoundaryProjectionDiagnosticCode.InvalidBoundaryOwnership,
                "The CustomerStation connection does not touch the terminal on its persisted WorkScope side.");

        return Unrepresentable(source,
            WorkScopeBoundaryProjectionDiagnosticCode.UnsupportedCustomerStationBoundary,
            "The current WTA AvailableSides/resolver contract does not support CustomerStation incoming isolators.");
    }

    private WorkScopeBoundaryProjection ValidateResolved(
        DrawingDocument drawing,
        WorkScopeBoundary source,
        IsolationBoundary resolved,
        WorkScopeBoundaryClassification classification)
    {
        string? issue = _rules.BoundaryIssue(drawing, resolved);
        if (issue is not null)
            return Unrepresentable(source, WorkScopeBoundaryProjectionDiagnosticCode.WtaResolverRejected,
                $"The existing WTA boundary validation rejected the mapped value: {issue}");
        return new WorkScopeBoundaryProjection(source, classification, resolved, []);
    }

    private static bool MatchesPersistedIdentity(
        WorkScopeBoundary source,
        IsolationBoundary resolved,
        out string? issue)
    {
        if (source.TerminalId is Guid terminalId && resolved.TerminalId != terminalId)
        {
            issue = "The persisted WorkScope terminal does not match the terminal resolved by WTA.";
            return false;
        }
        if (source.ConnectionId is Guid connectionId && resolved.ConnectionId != connectionId)
        {
            issue = "The persisted WorkScope connection does not match the connection resolved by WTA.";
            return false;
        }
        issue = null;
        return true;
    }

    private static bool IsExact(WorkScopeBoundary source, IsolationBoundary mapped) =>
        source.DeviceId == mapped.DeviceId && source.Side == mapped.Side &&
        source.TerminalId == mapped.TerminalId && source.ConnectionId == mapped.ConnectionId;

    private static WorkScopeBoundaryProjection Unrepresentable(
        WorkScopeBoundary source,
        WorkScopeBoundaryProjectionDiagnosticCode code,
        string message) => new(source, WorkScopeBoundaryClassification.C, null,
        [Diagnostic(code, message, source)]);

    private static WorkScopeBoundaryProjection Invalid(
        WorkScopeBoundary source,
        WorkScopeBoundaryProjectionDiagnosticCode code,
        string message) => new(source, WorkScopeBoundaryClassification.Invalid, null,
        [Diagnostic(code, message, source)]);

    private static WorkScopeBoundaryProjectionDiagnostic Diagnostic(
        WorkScopeBoundaryProjectionDiagnosticCode code,
        string message,
        WorkScopeBoundary? source = null) => new(code, message, source);

    private static WorkScopeIsolationBoundaryProjection Result(
        WorkScopeIsolationBoundaryProjectionStatus status,
        IEnumerable<WorkScopeBoundaryProjection> boundaries,
        IEnumerable<ProjectedIsolationBoundary> projected,
        IEnumerable<WorkScopeBoundaryProjectionDiagnostic> diagnostics) =>
        new(status, boundaries, projected, diagnostics);
}
