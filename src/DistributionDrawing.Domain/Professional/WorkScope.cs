namespace DistributionDrawing.Domain.Professional;

/// <summary>Persisted confirmed snapshot. DrawingDocument validates its stable references.</summary>
public sealed class WorkScope
{
    private WorkScope(Guid workScopeId, IEnumerable<WorkScopeRegion> regions,
        IEnumerable<WorkScopeBoundary> boundaries, string? description)
    {
        if (workScopeId == Guid.Empty)
            throw new ArgumentException("Work scope ID cannot be empty.", nameof(workScopeId));
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(boundaries);
        WorkScopeRegion[] regionCopy = regions.ToArray();
        WorkScopeBoundary[] boundaryCopy = boundaries.ToArray();
        if (regionCopy.Length == 0 || regionCopy.Any(region => region is null))
            throw new ArgumentException("A work scope requires nonnull regions.", nameof(regions));
        Guid[] terminals = regionCopy.SelectMany(region => region.TerminalIds).ToArray();
        Guid[] nodes = regionCopy.SelectMany(region => region.ElectricalNodeIds).ToArray();
        if (terminals.Distinct().Count() != terminals.Length || nodes.Distinct().Count() != nodes.Length)
            throw new ArgumentException("Work scope regions cannot overlap.", nameof(regions));
        if (boundaryCopy.Any(boundary => boundary is null) ||
            boundaryCopy.Distinct().Count() != boundaryCopy.Length)
            throw new ArgumentException("Work scope boundaries must be nonnull and structurally unique.", nameof(boundaries));
        WorkScopeId = workScopeId;
        Regions = Array.AsReadOnly(regionCopy);
        Boundaries = Array.AsReadOnly(boundaryCopy);
        Description = description?.Trim();
    }

    public Guid WorkScopeId { get; }
    public IReadOnlyList<WorkScopeRegion> Regions { get; private set; }
    public IReadOnlyList<WorkScopeBoundary> Boundaries { get; private set; }
    public string? Description { get; private set; }

    public static WorkScope Create(Guid workScopeId, IEnumerable<WorkScopeRegion> regions,
        IEnumerable<WorkScopeBoundary> boundaries, string? description = null)
        => new(workScopeId, regions, boundaries, description);

    internal void Update(WorkScope replacement)
    {
        Regions = replacement.Regions;
        Boundaries = replacement.Boundaries;
        Description = replacement.Description;
    }
}
