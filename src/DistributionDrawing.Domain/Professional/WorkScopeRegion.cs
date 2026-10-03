namespace DistributionDrawing.Domain.Professional;

/// <summary>Immutable confirmed membership; no separate region identity or topology.</summary>
public sealed class WorkScopeRegion
{
    public WorkScopeRegion(IEnumerable<Guid> terminalIds, IEnumerable<Guid> electricalNodeIds)
    {
        TerminalIds = CopyIds(terminalIds, nameof(terminalIds));
        ElectricalNodeIds = CopyIds(electricalNodeIds, nameof(electricalNodeIds));
        if (TerminalIds.Count + ElectricalNodeIds.Count == 0)
            throw new ArgumentException("A work scope region cannot be empty.");
    }

    public IReadOnlyList<Guid> TerminalIds { get; }
    public IReadOnlyList<Guid> ElectricalNodeIds { get; }

    private static IReadOnlyList<Guid> CopyIds(IEnumerable<Guid> ids, string name)
    {
        ArgumentNullException.ThrowIfNull(ids, name);
        Guid[] copy = ids.ToArray();
        if (copy.Any(id => id == Guid.Empty) || copy.Distinct().Count() != copy.Length)
            throw new ArgumentException("Region identities must be nonempty and unique.", name);
        return Array.AsReadOnly(copy);
    }
}
