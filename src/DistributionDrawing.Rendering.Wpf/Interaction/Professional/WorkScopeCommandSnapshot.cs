using DistributionDrawing.Domain.Professional;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Professional;

/// <summary>
/// Complete scalar snapshot used by Add, Remove and Change WorkScope
/// commands. GroundingPointIds are references only; their data is not copied.
/// </summary>
public sealed record WorkScopeCommandSnapshot(
    Guid WorkScopeId,
    BoundaryPointCommandValue StartBoundary,
    BoundaryPointCommandValue EndBoundary,
    string Description,
    IReadOnlyList<Guid> GroundingPointIds)
{
    public static WorkScopeCommandSnapshot From(WorkScope workScope)
    {
        ArgumentNullException.ThrowIfNull(workScope);
        return new WorkScopeCommandSnapshot(
            workScope.WorkScopeId,
            BoundaryPointCommandValue.From(workScope.StartBoundary),
            BoundaryPointCommandValue.From(workScope.EndBoundary),
            workScope.Description,
            workScope.GroundingPointIds.ToArray());
    }

    public IReadOnlyList<Guid> CopyGroundingPointIds()
    {
        return GroundingPointIds.ToArray();
    }
}
