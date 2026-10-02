using DistributionDrawing.Domain.Energization;
using Xunit;

namespace DistributionDrawing.Domain.Tests;

public sealed class EnergizationScenarioTests
{
    [Fact]
    public void EditingAuthoritativeSourcesPreservesLegacyV9CompatibilityField()
    {
        var scenario = new EnergizationScenario(Guid.NewGuid());
        EnergizedSeed first = new(Guid.NewGuid(), Guid.NewGuid(), EnergizationSide.Bus);
        EnergizedSeed second = new(Guid.NewGuid(), Guid.NewGuid(), EnergizationSide.Line);

        scenario.SetSourceSetComplete(true);
        scenario.AddSeed(first);
        Assert.True(scenario.IsSourceSetComplete);
        scenario.SetSourceSetComplete(true);
        scenario.ReplaceSeed(new EnergizedSeed(first.Id, second.BoundaryDeviceId,
            EnergizationSide.Bus));
        Assert.True(scenario.IsSourceSetComplete);
        scenario.SetSourceSetComplete(true);
        scenario.ReplaceSeed(new EnergizedSeed(first.Id, second.BoundaryDeviceId,
            EnergizationSide.Line));
        Assert.True(scenario.IsSourceSetComplete);
        scenario.AddSeed(second);
        scenario.SetSourceSetComplete(true);
        scenario.RemoveSeed(second.Id);
        Assert.True(scenario.IsSourceSetComplete);
    }

    [Fact]
    public void SeedIdsAreStableAndUnique()
    {
        EnergizedSeed seed = new(Guid.NewGuid(), Guid.NewGuid(), EnergizationSide.Bus);
        Assert.Throws<ArgumentException>(() => new EnergizationScenario(Guid.NewGuid(),
            [seed, seed]));
        var scenario = new EnergizationScenario(Guid.NewGuid(), [seed]);
        Assert.Equal(seed.Id, Assert.Single(scenario.Seeds).Id);
        Assert.Throws<InvalidOperationException>(() => scenario.AddSeed(seed));
    }
}
