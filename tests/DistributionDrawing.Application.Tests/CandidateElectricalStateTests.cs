using DistributionDrawing.Application.Energization;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class CandidateElectricalStateTests
{
    [Fact]
    public void Create_CopiesOverridesAndFullSeedSnapshotWithoutMutatingFacts()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "candidate state");
        SwitchDevice first = AddSwitch(drawing, SwitchState.Open);
        SwitchDevice second = AddSwitch(drawing, SwitchState.Closed);
        var seed = new EnergizedSeed(Guid.NewGuid(), first.Id, EnergizationSide.Line);
        var scenario = new EnergizationScenario(Guid.NewGuid(), [seed], true);
        var overrides = new Dictionary<Guid, SwitchState>
        {
            [first.Id] = SwitchState.Closed
        };

        CandidateElectricalState candidate = CandidateElectricalState.Create(
            drawing, overrides, scenario);
        overrides[second.Id] = SwitchState.Open;
        scenario.RemoveSeed(seed.Id);

        Assert.Equal(SwitchState.Closed, candidate.GetSwitchState(first));
        Assert.Equal(SwitchState.Closed, candidate.GetSwitchState(second));
        Assert.Single(candidate.SwitchOverrides);
        Assert.Equal(seed, Assert.Single(candidate.Seeds));
        Assert.True(candidate.IsSourceSetComplete);
        Assert.Empty(scenario.Seeds);
        Assert.Equal(SwitchState.Open, first.SwitchState);
        Assert.Equal(SwitchState.Closed, second.SwitchState);
        Assert.Equal(seed, Assert.Single(candidate.CreateScenarioSnapshot().Seeds));
    }

    [Fact]
    public void Create_RejectsUnknownSwitchAndUndefinedState()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "candidate state validation");
        var scenario = new EnergizationScenario(Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => CandidateElectricalState.Create(
            drawing,
            new Dictionary<Guid, SwitchState> { [Guid.NewGuid()] = SwitchState.Closed },
            scenario));

        SwitchDevice device = AddSwitch(drawing, SwitchState.Open);
        Assert.Throws<ArgumentOutOfRangeException>(() => CandidateElectricalState.Create(
            drawing,
            new Dictionary<Guid, SwitchState> { [device.Id] = (SwitchState)99 },
            scenario));
    }

    private static SwitchDevice AddSwitch(DrawingDocument drawing, SwitchState state)
    {
        SwitchDevice device = SwitchDevice.CreateForPole(
            Guid.NewGuid(), SwitchKind.IsolationSwitch, Guid.NewGuid(), Guid.NewGuid(), state);
        drawing.AddDevice(device);
        return device;
    }
}
