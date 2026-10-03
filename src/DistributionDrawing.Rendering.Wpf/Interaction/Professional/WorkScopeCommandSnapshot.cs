using DistributionDrawing.Domain.Professional;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Professional;

/// <summary>Complete immutable snapshot; replay never consults EA or UI selection.</summary>
public sealed class WorkScopeCommandSnapshot
{
    public WorkScopeCommandSnapshot(Guid workScopeId, IEnumerable<WorkScopeRegion> regions,
        IEnumerable<WorkScopeBoundary> boundaries, string? description = null)
    {
        WorkScope validated = WorkScope.Create(workScopeId, regions, boundaries, description);
        WorkScope copy = WorkScope.Create(workScopeId,
            validated.Regions.Select(region => new WorkScopeRegion(region.TerminalIds, region.ElectricalNodeIds)),
            validated.Boundaries.Select(boundary => new WorkScopeBoundary(boundary.DeviceId, boundary.Side,
                boundary.TerminalId, boundary.ConnectionId)), description);
        WorkScopeId = copy.WorkScopeId;
        Regions = copy.Regions;
        Boundaries = copy.Boundaries;
        Description = copy.Description;
    }

    public Guid WorkScopeId { get; }
    public IReadOnlyList<WorkScopeRegion> Regions { get; }
    public IReadOnlyList<WorkScopeBoundary> Boundaries { get; }
    public string? Description { get; }

    public static WorkScopeCommandSnapshot From(WorkScope scope) =>
        new(scope.WorkScopeId, scope.Regions, scope.Boundaries, scope.Description);
}
