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
        return new(scenario, [.. scenario.Seeds, seed], false);
    }

    public static EnergizationScenarioCommand Remove(EnergizationScenario scenario, Guid seedId)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (!scenario.Seeds.Any(item => item.Id == seedId))
            throw new InvalidOperationException("Seed does not exist.");
        return new(scenario, scenario.Seeds.Where(item => item.Id != seedId).ToArray(), false);
    }

    public static EnergizationScenarioCommand Replace(EnergizationScenario scenario, EnergizedSeed seed)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(seed);
        EnergizedSeed[] after = scenario.Seeds.ToArray();
        int index = Array.FindIndex(after, item => item.Id == seed.Id);
        if (index < 0) throw new InvalidOperationException("Seed does not exist.");
        if (after[index] != seed) after[index] = seed;
        return new(scenario, after, after.SequenceEqual(scenario.Seeds)
            ? scenario.IsSourceSetComplete : false);
    }

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
        // Preserve the stable scenario object and exact seed order. Domain mutations
        // revoke completeness; restore the captured confirmation only at the end.
        foreach (EnergizedSeed seed in _scenario.Seeds.ToArray())
            _scenario.RemoveSeed(seed.Id);
        foreach (EnergizedSeed seed in seeds)
            _scenario.AddSeed(seed);
        _scenario.SetSourceSetComplete(complete);
    }
}
