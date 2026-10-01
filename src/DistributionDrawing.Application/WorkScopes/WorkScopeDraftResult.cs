using System.Collections.Frozen;
using System.Text;
using DistributionDrawing.Application.Energization;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;

namespace DistributionDrawing.Application.WorkScopes;

public enum WorkScopeDraftStatus
{
    Success,
    NoSeeds,
    TooFewBoundaries,
    SourceSetIncomplete,
    ResultNotCurrent,
    ResultNotComplete,
    BoundaryResolutionFailed,
    DisconnectedAnchors,
    AmbiguousRegion,
    EnergizedInsideScope,
    UnknownInsideScope,
    UnsupportedTopology
}

public sealed record WorkScopeRegionItem(Guid Id, string Kind, string Name);

public sealed class WorkScopeDraftResult
{
    private readonly EnergizationResult? _analysisIdentity;
    private readonly string? _context;

    internal WorkScopeDraftResult(WorkScopeDraftStatus status,
        IEnumerable<WorkScopeBoundaryAnchor> anchors,
        IEnumerable<Guid> terminals, IEnumerable<Guid> nodes,
        IEnumerable<Guid> connections, IEnumerable<WorkScopeRegionItem> devices,
        IEnumerable<string> diagnostics, IEnumerable<string> warnings,
        Guid scenarioId, EnergizationResult? analysisIdentity, string? context)
    {
        Status = status;
        BoundaryAnchors = Array.AsReadOnly(anchors.ToArray());
        RegionTerminalIds = terminals.ToFrozenSet();
        RegionNodeIds = nodes.ToFrozenSet();
        RegionConnectionIds = connections.ToFrozenSet();
        RegionDeviceSummary = Array.AsReadOnly(devices.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        Warnings = Array.AsReadOnly(warnings.ToArray());
        GeneratedAgainstScenarioId = scenarioId;
        _analysisIdentity = analysisIdentity;
        _context = context;
    }

    public Guid DraftId { get; } = Guid.NewGuid();
    public WorkScopeDraftStatus Status { get; }
    public IReadOnlyList<WorkScopeBoundaryAnchor> BoundaryAnchors { get; }
    public IReadOnlySet<Guid> RegionTerminalIds { get; }
    public IReadOnlySet<Guid> RegionNodeIds { get; }
    public IReadOnlySet<Guid> RegionConnectionIds { get; }
    public IReadOnlyList<WorkScopeRegionItem> RegionDeviceSummary { get; }
    public IReadOnlyList<string> Diagnostics { get; }
    public IReadOnlyList<string> Warnings { get; }
    public Guid GeneratedAgainstScenarioId { get; }

    public bool IsCurrent(DrawingDocument drawing, EnergizationScenario scenario,
        EnergizationAnalysisState analysis) =>
        Status == WorkScopeDraftStatus.Success &&
        GeneratedAgainstScenarioId == scenario.Id &&
        ReferenceEquals(_analysisIdentity, analysis.CurrentResult) &&
        _context == WorkScopeDraftContext.Capture(drawing, scenario);
}

internal static class WorkScopeDraftContext
{
    public static string Capture(DrawingDocument drawing, EnergizationScenario scenario)
    {
        StringBuilder value = new();
        value.Append(scenario.Id).Append(scenario.IsSourceSetComplete);
        foreach (EnergizedSeed seed in scenario.Seeds.OrderBy(item => item.Id))
            value.Append(seed.Id).Append(seed.BoundaryDeviceId).Append(seed.Side);
        foreach (var device in drawing.Devices.OrderBy(item => item.Id))
        {
            value.Append(device.Id).Append(device.DisplayName).Append(device.SwitchState);
            if (device is SwitchDevice sw)
                value.Append(sw.SwitchKind).Append(sw.FirstTerminalId).Append(sw.SecondTerminalId);
            if (device is Pole pole) value.Append(pole.PoleNumber);
            if (device is CableTermination termination)
                value.Append(termination.CableSideTerminalId)
                    .Append(termination.OverheadSideTerminalId).Append(termination.InternalNodeId);
        }
        foreach (var terminal in drawing.Terminals.OrderBy(item => item.Id))
            value.Append(terminal.Id).Append(terminal.OwnerType).Append(terminal.OwnerId)
                .Append(terminal.Role).Append(terminal.ElectricalNodeId);
        foreach (var node in drawing.ElectricalNodes.OrderBy(item => item.Id))
        {
            value.Append(node.Id).Append(node.Type).Append(node.OwnerId);
            foreach (Guid id in node.TerminalIds.Order()) value.Append(id);
        }
        foreach (var connection in drawing.Connections.OrderBy(item => item.Id))
            value.Append(connection.Id).Append(connection.Type).Append(connection.StartTerminalId)
                .Append(connection.EndTerminalId).Append(connection.DisplayName);
        foreach (var attachment in drawing.PoleAttachments.OrderBy(item => item.AttachmentId))
            value.Append(attachment.AttachmentId).Append(attachment.PoleId)
                .Append(attachment.AttachedDeviceId);
        foreach (var line in drawing.OverheadLines.OrderBy(item => item.ConnectionId))
        {
            value.Append(line.ConnectionId);
            foreach (Guid id in line.SupportPoleIds) value.Append(id);
        }
        foreach (var point in drawing.GroundingPoints.OrderBy(item => item.GroundingPointId))
            value.Append(point.GroundingPointId).Append(point.Target);
        return value.ToString();
    }
}
