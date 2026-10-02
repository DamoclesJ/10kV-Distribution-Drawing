using DistributionDrawing.Domain.Energization;

namespace DistributionDrawing.Application.Energization;

/// <summary>One reversible edit of the project-owned scenario.</summary>
public sealed class EnergizationScenarioCommand
{
    private readonly EnergizationScenario _scenario;
    private readonly EnergizedSeed[] _before;
    private readonly EnergizedSeed[] _after;
    private readonly bool _beforeComplete;
    private readonly bool _afterComplete;

    private EnergizationScenarioCommand(
        EnergizationScenario scenario,
        EnergizedSeed[] after,
        bool afterComplete)
    {
        _scenario = scenario;
        _before = scenario.Seeds.ToArray();
        _beforeComplete = scenario.IsSourceSetComplete;
        _after = after;
        _afterComplete = afterComplete;
    }

    public bool HasChanges => _beforeComplete != _afterComplete ||
        !_before.SequenceEqual(_after);

    public static EnergizationScenarioCommand Add(EnergizationScenario scenario, EnergizedSeed seed)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(seed);
        if (scenario.Seeds.Any(item => item.Id == seed.Id))
            throw new InvalidOperationException("Duplicate seed ID.");
        return new(scenario, [.. scenario.Seeds, seed], scenario.IsSourceSetComplete);
    }

    public static EnergizationScenarioCommand Remove(EnergizationScenario scenario, Guid seedId)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (!scenario.Seeds.Any(item => item.Id == seedId))
            throw new InvalidOperationException("Seed does not exist.");
        return new(scenario, scenario.Seeds.Where(item => item.Id != seedId).ToArray(), scenario.IsSourceSetComplete);
    }

    public static EnergizationScenarioCommand Replace(EnergizationScenario scenario, EnergizedSeed seed)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(seed);
        EnergizedSeed[] after = scenario.Seeds.ToArray();
        int index = Array.FindIndex(after, item => item.Id == seed.Id);
        if (index < 0) throw new InvalidOperationException("Seed does not exist.");
        if (after[index] != seed) after[index] = seed;
        return new(scenario, after, scenario.IsSourceSetComplete);
    }

    /// <summary>Remove only references retired by this drawing edit; do not guess replacement boundaries.</summary>
    public static EnergizationScenarioCommand RemoveDeletedBoundaries(
        EnergizationScenario scenario, IReadOnlySet<Guid> deletedDeviceIds)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(deletedDeviceIds);
        return new(scenario, scenario.Seeds.Where(seed =>
            !deletedDeviceIds.Contains(seed.BoundaryDeviceId)).ToArray(), scenario.IsSourceSetComplete);
    }

    /// <summary>Compatibility edit only; the analyzer does not consume this legacy field.</summary>
    public static EnergizationScenarioCommand SetComplete(
        EnergizationScenario scenario, bool complete)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return new(scenario, scenario.Seeds.ToArray(), complete);
    }

    public void Execute() => Apply(_after, _afterComplete);
    public void Undo() => Apply(_before, _beforeComplete);
    public void Redo() => Apply(_after, _afterComplete);

    private void Apply(IReadOnlyList<EnergizedSeed> seeds, bool complete)
    {
        // Preserve the stable scenario, exact seed order and legacy V9 field.
        foreach (EnergizedSeed seed in _scenario.Seeds.ToArray())
            _scenario.RemoveSeed(seed.Id);
        foreach (EnergizedSeed seed in seeds)
            _scenario.AddSeed(seed);
        _scenario.SetSourceSetComplete(complete);
    }
}
