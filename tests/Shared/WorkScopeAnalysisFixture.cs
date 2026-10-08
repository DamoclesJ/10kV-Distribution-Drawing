using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DistributionDrawing.Application.Energization;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;

namespace DistributionDrawing.TestSupport;

// Retains the explicit scenario used by existing EA fixtures; no topology or EA logic is duplicated.
internal static class WorkScopeAnalysisFixture
{
    private static readonly ConditionalWeakTable<EnergizationAnalysisState, EnergizationScenario> Scenarios = new();
    private static readonly ConcurrentDictionary<Guid, EnergizedSeed> Seeds = new();

    public static void Execute(EnergizationAnalysisState state, DrawingDocument drawing,
        EnergizationScenario scenario, bool showOverlay = true)
    {
        Scenarios.Remove(state);
        Scenarios.Add(state, scenario);
        foreach (EnergizedSeed seed in scenario.Seeds) Seeds[seed.Id] = seed;
        state.Execute(drawing, scenario, showOverlay);
    }

    public static EnergizationScenario ScenarioFor(EnergizationAnalysisState state)
    {
        if (Scenarios.TryGetValue(state, out EnergizationScenario? scenario)) return scenario;
        // Synthetic-result tests retain the IDs of their originating explicit fixture seeds.
        Guid[] ids = state.LatestResult?.Terminals.Values.SelectMany(point => point.EnergizedBy)
            .Distinct().ToArray() ?? [];
        return new EnergizationScenario(Guid.NewGuid(), ids.Select(id => Seeds[id]));
    }
}
