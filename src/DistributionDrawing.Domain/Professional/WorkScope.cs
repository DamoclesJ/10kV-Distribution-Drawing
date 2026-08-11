namespace DistributionDrawing.Domain.Professional;

/// <summary>
/// User-defined work boundary. Cross-document references are validated by
/// DrawingDocument before an instance is added or updated.
/// </summary>
public sealed class WorkScope
{
    private IReadOnlyList<Guid> _groundingPointIds;

    private WorkScope(
        Guid workScopeId,
        BoundaryPoint startBoundary,
        BoundaryPoint endBoundary,
        string description,
        IEnumerable<Guid> groundingPointIds)
    {
        if (workScopeId == Guid.Empty)
        {
            throw new ArgumentException("Work scope ID cannot be empty.", nameof(workScopeId));
        }

        ArgumentNullException.ThrowIfNull(startBoundary);
        ArgumentNullException.ThrowIfNull(endBoundary);
        if (startBoundary.TerminalId == endBoundary.TerminalId)
        {
            throw new ArgumentException(
                "A work scope must have two different boundary terminals.",
                nameof(endBoundary));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Work scope description is required.", nameof(description));
        }

        ArgumentNullException.ThrowIfNull(groundingPointIds);
        Guid[] ids = groundingPointIds.ToArray();
        if (ids.Any(id => id == Guid.Empty))
        {
            throw new ArgumentException(
                "Grounding point IDs cannot be empty.",
                nameof(groundingPointIds));
        }

        if (ids.Distinct().Count() != ids.Length)
        {
            throw new ArgumentException(
                "Grounding point IDs cannot be duplicated.",
                nameof(groundingPointIds));
        }

        WorkScopeId = workScopeId;
        StartBoundary = startBoundary;
        EndBoundary = endBoundary;
        Description = description.Trim();
        _groundingPointIds = Array.AsReadOnly(ids);
    }

    public Guid WorkScopeId { get; }

    public BoundaryPoint StartBoundary { get; private set; }

    public BoundaryPoint EndBoundary { get; private set; }

    public string Description { get; private set; }

    public IReadOnlyList<Guid> GroundingPointIds => _groundingPointIds;

    public static WorkScope Create(
        Guid workScopeId,
        BoundaryPoint startBoundary,
        BoundaryPoint endBoundary,
        string description,
        IEnumerable<Guid>? groundingPointIds = null)
    {
        return new WorkScope(
            workScopeId,
            startBoundary,
            endBoundary,
            description,
            groundingPointIds ?? []);
    }

    internal void Update(
        BoundaryPoint startBoundary,
        BoundaryPoint endBoundary,
        string description,
        IEnumerable<Guid> groundingPointIds)
    {
        WorkScope replacement = Create(
            WorkScopeId,
            startBoundary,
            endBoundary,
            description,
            groundingPointIds);

        StartBoundary = replacement.StartBoundary;
        EndBoundary = replacement.EndBoundary;
        Description = replacement.Description;
        _groundingPointIds = replacement._groundingPointIds;
    }
}
