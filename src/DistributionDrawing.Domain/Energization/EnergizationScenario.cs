namespace DistributionDrawing.Domain.Energization;

public sealed class EnergizationScenario
{
    private readonly List<EnergizedSeed> _seeds;

    public EnergizationScenario(
        Guid id,
        IEnumerable<EnergizedSeed>? seeds = null,
        bool isSourceSetComplete = false)
    {
        if (id == Guid.Empty) throw new ArgumentException("Scenario ID is required.", nameof(id));
        _seeds = seeds?.ToList() ?? [];
        if (_seeds.Any(seed => seed is null) ||
            _seeds.Select(seed => seed.Id).Distinct().Count() != _seeds.Count)
            throw new ArgumentException("Seed IDs must be unique.", nameof(seeds));

        Id = id;
        IsSourceSetComplete = isSourceSetComplete;
    }

    public Guid Id { get; }
    public IReadOnlyList<EnergizedSeed> Seeds => _seeds.AsReadOnly();
    /// <summary>Legacy V9 compatibility field; it has no analysis or UI meaning.</summary>
    public bool IsSourceSetComplete { get; private set; }

    public void SetSourceSetComplete(bool complete) => IsSourceSetComplete = complete;

    public void AddSeed(EnergizedSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        if (_seeds.Any(existing => existing.Id == seed.Id))
            throw new InvalidOperationException("Duplicate seed ID.");
        _seeds.Add(seed);
    }

    public void RemoveSeed(Guid seedId)
    {
        int index = _seeds.FindIndex(seed => seed.Id == seedId);
        if (index < 0) throw new InvalidOperationException("Seed does not exist.");
        _seeds.RemoveAt(index);
    }

    public void ReplaceSeed(EnergizedSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        int index = _seeds.FindIndex(existing => existing.Id == seed.Id);
        if (index < 0) throw new InvalidOperationException("Seed does not exist.");
        if (_seeds[index] == seed) return;
        _seeds[index] = seed;
    }
}
