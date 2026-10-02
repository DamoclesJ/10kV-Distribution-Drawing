using DistributionDrawing.Application.Energization;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class EnergizationUiTests
{
    [Fact]
    public void ScenarioCommandsRestoreSeedOrderIdentityAndLegacyField()
    {
        EnergizedSeed a = new(Guid.NewGuid(), Guid.NewGuid(), EnergizationSide.Bus);
        EnergizedSeed b = new(Guid.NewGuid(), Guid.NewGuid(), EnergizationSide.Line);
        var scenario = new EnergizationScenario(Guid.NewGuid(), [a, b], true);
        var remove = EnergizationScenarioCommand.Remove(scenario, a.Id);
        remove.Execute();
        Assert.Equal([b.Id], scenario.Seeds.Select(seed => seed.Id));
        Assert.True(scenario.IsSourceSetComplete);
        remove.Undo();
        Assert.Equal([a.Id, b.Id], scenario.Seeds.Select(seed => seed.Id));
        Assert.True(scenario.IsSourceSetComplete);
        remove.Redo();
        Assert.Equal([b.Id], scenario.Seeds.Select(seed => seed.Id));

        remove.Undo();
        var replacement = new EnergizedSeed(b.Id, Guid.NewGuid(), EnergizationSide.Bus);
        var replace = EnergizationScenarioCommand.Replace(scenario, replacement);
        replace.Execute();
        Assert.Equal(replacement, scenario.Seeds[1]);
        Assert.True(scenario.IsSourceSetComplete);
        replace.Undo();
        Assert.Equal([a, b], scenario.Seeds);
        Assert.True(scenario.IsSourceSetComplete);
        replace.Redo();
        Assert.Equal(replacement, scenario.Seeds[1]);

        var add = EnergizationScenarioCommand.Add(scenario,
            new EnergizedSeed(Guid.NewGuid(), Guid.NewGuid(), EnergizationSide.Line));
        add.Execute();
        Assert.Equal(3, scenario.Seeds.Count);
        add.Undo();
        Assert.Equal([a.Id, b.Id], scenario.Seeds.Select(seed => seed.Id));
        add.Redo();
        Assert.Equal(3, scenario.Seeds.Count);
    }

    [Fact]
    public void IdenticalReplaceAndLegacyFieldCommandAreNoOps()
    {
        EnergizedSeed seed = new(Guid.NewGuid(), Guid.NewGuid(), EnergizationSide.Bus);
        var scenario = new EnergizationScenario(Guid.NewGuid(), [seed], true);
        Assert.False(EnergizationScenarioCommand.Replace(scenario, seed).HasChanges);
        Assert.False(EnergizationScenarioCommand.SetComplete(scenario, true).HasChanges);
        var confirm = EnergizationScenarioCommand.SetComplete(scenario, false);
        Assert.True(confirm.HasChanges);
        confirm.Execute();
        Assert.False(scenario.IsSourceSetComplete);
        confirm.Undo();
        Assert.True(scenario.IsSourceSetComplete);
    }

    [Fact]
    public void CabinetCandidatesUsePolicyAndHideUnsupportedSwitches()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "EA");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(), "柜 A",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(1,
                SwitchState.Open, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateIntegratedFeeder(2,
                GroundingStructureKind.LowerLowerGrounding,
                SwitchState.Open, SwitchState.Open, SwitchState.Open)]));
        drawing.AddDevice(cabinet);

        IReadOnlyList<EnergizationBoundaryCandidate> candidates =
            new EnergizationUiService().Candidates(drawing, cabinet.Id);
        Assert.Equal(4, candidates.Count);
        Assert.All(candidates, candidate => Assert.True(candidate.IsResolvable));
        Assert.Contains(candidates, candidate => candidate.DeviceKind == SwitchKind.LoadSwitch);
        Assert.Contains(candidates, candidate => candidate.DeviceKind == SwitchKind.CircuitBreaker);
        Assert.DoesNotContain(candidates, candidate =>
            candidate.DeviceKind is SwitchKind.GroundSwitch or SwitchKind.IsolationSwitch);
        var service = new EnergizationUiService();
        var scenario = new EnergizationScenario(Guid.NewGuid(),
            [new EnergizedSeed(Guid.NewGuid(), candidates[0].DeviceId, candidates[0].Side)]);
        Assert.True(service.Analyze(drawing, scenario).IsSuccess);
        Assert.False(scenario.IsSourceSetComplete);
    }

    [Fact]
    public void UnresolvedPoleSideIsVisibleAndFailsAnalysis()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "EA pole");
        var pole = new Pole(Guid.NewGuid(), "P02");
        SwitchDevice switchDevice = SwitchDevice.CreateForPole(Guid.NewGuid(),
            SwitchKind.IsolationSwitch, Guid.NewGuid(), Guid.NewGuid(),
            displayName: "柱上隔离开关");
        drawing.AddDevice(pole);
        drawing.AddDevice(switchDevice);
        drawing.AddPoleAttachment(new PoleAttachment(Guid.NewGuid(), pole.Id,
            switchDevice.Id));
        var service = new EnergizationUiService();
        EnergizationBoundaryCandidate[] candidates = service.Candidates(drawing,
            pole.Id).ToArray();
        Assert.Equal(2, candidates.Length);
        Assert.All(candidates, candidate =>
        {
            Assert.False(candidate.IsResolvable);
            Assert.Equal(EnergizationDiagnosticCode.UnresolvedSide, candidate.Diagnostic);
        });
        EnergizedSeed seed = new(Guid.NewGuid(), switchDevice.Id,
            EnergizationSide.SmallerNumber);
        var scenario = new EnergizationScenario(Guid.NewGuid(), [seed]);
        Assert.False(service.Analyze(drawing, scenario).IsSuccess);
        Assert.Equal(EnergizationDiagnosticCode.UnresolvedSide,
            service.DescribeSeed(drawing, seed).Diagnostic);
    }

    [Fact]
    public void CandidateAndSeedShareProfessionalNamesFromCabinetAndPoleFacts()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "EA names");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(), "辛15KB5",
            [RingCabinetIntervalDefinition.CreateIntegratedFeeder(4,
                GroundingStructureKind.UpperIsolationGrounding,
                SwitchState.Open, SwitchState.Open, SwitchState.Open, "负4"),
             RingCabinetIntervalDefinition.CreateLoadSwitch(5,
                 SwitchState.Open, SwitchState.Open, "负5")]));
        drawing.AddDevice(cabinet);
        RingCabinetInterval interval = cabinet.Intervals.Single(item =>
            item.IntervalKind == IntervalKind.IntegratedFeederInterval);
        SwitchDevice isolator = interval.SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.IsolationSwitch);
        SwitchDevice groundSwitch = interval.SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.GroundSwitch);
        var service = new EnergizationUiService();

        EnergizationBoundaryCandidate cabinetCandidate = service.Candidates(drawing, cabinet.Id)
            .First(candidate => candidate.DeviceId == isolator.Id);
        Assert.Equal("辛15KB5负4-4隔离开关", cabinetCandidate.DeviceName);
        EnergizedSeed cabinetSeed = new(Guid.NewGuid(), isolator.Id, cabinetCandidate.Side);
        Assert.Equal(cabinetCandidate.DeviceName,
            service.DescribeSeed(drawing, cabinetSeed).DeviceName);

        EnergizationSeedDisplay groundSeed = service.DescribeSeed(drawing,
            new EnergizedSeed(Guid.NewGuid(), groundSwitch.Id, EnergizationSide.Bus));
        Assert.Equal("辛15KB5负4-47接地刀闸", groundSeed.DeviceName);
        Assert.DoesNotContain("/", groundSeed.DeviceName);
        Assert.DoesNotContain("GroundSwitch", groundSeed.DeviceName);

        var pole = new Pole(Guid.NewGuid(), "P02");
        SwitchDevice poleSwitch = SwitchDevice.CreateForPole(Guid.NewGuid(),
            SwitchKind.IsolationSwitch, Guid.NewGuid(), Guid.NewGuid());
        drawing.AddDevice(pole);
        drawing.AddDevice(poleSwitch);
        drawing.AddPoleAttachment(new PoleAttachment(Guid.NewGuid(), pole.Id,
            poleSwitch.Id));
        EnergizationBoundaryCandidate poleCandidate = service.Candidates(drawing, pole.Id)
            .First();
        Assert.Equal("P02（隔离开关）", poleCandidate.DeviceName);
        Assert.DoesNotContain("PoleSwitch", poleCandidate.DeviceName);
        Assert.Equal(poleCandidate.DeviceName, service.DescribeSeed(drawing,
            new EnergizedSeed(Guid.NewGuid(), poleSwitch.Id, poleCandidate.Side)).DeviceName);

        var fusePole = new Pole(Guid.NewGuid(), "P06");
        SwitchDevice fuse = SwitchDevice.CreateForPole(Guid.NewGuid(),
            SwitchKind.DropoutFuse, Guid.NewGuid(), Guid.NewGuid());
        drawing.AddDevice(fusePole);
        drawing.AddDevice(fuse);
        drawing.AddPoleAttachment(new PoleAttachment(Guid.NewGuid(), fusePole.Id, fuse.Id));
        EnergizationBoundaryCandidate fuseCandidate = service.Candidates(drawing, fusePole.Id)
            .First(candidate => candidate.DeviceId == fuse.Id);
        Assert.Equal("P06（跌落式熔断器）", fuseCandidate.DeviceName);
        Assert.DoesNotContain("PoleSwitch", fuseCandidate.DeviceName);
    }

    [Fact]
    public void AnalysisStateNeverExposesStaleResultAsCurrent()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "EA");
        var scenario = new EnergizationScenario(Guid.NewGuid());
        var state = new EnergizationAnalysisState();
        Assert.Equal(EnergizationFreshness.NotAnalyzed, state.Freshness);
        state.Execute(drawing, scenario);
        Assert.Equal(EnergizationValidity.NoSeeds, state.LatestResult!.Validity);
        Assert.Null(state.CurrentResult);
        Assert.False(state.CanShowOverlay);
        Assert.Contains(state.LatestDiagnostics, item => item.Message == "当前未配置电源点");
        state.Invalidate();
        Assert.Equal(EnergizationFreshness.Stale, state.Freshness);
        Assert.Null(state.CurrentResult);
        Assert.False(state.CanShowOverlay);
        state.Execute(drawing, scenario);
        Assert.Equal(EnergizationFreshness.Current, state.Freshness);
    }

    [Fact]
    public void FailedReanalysisWithdrawsPreviousSuccessfulResultWithoutUnknownStates()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "EA result lifecycle");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "柜 A",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open)]));
        drawing.AddDevice(cabinet);
        SwitchDevice source = cabinet.Intervals[0].SwitchDevices.Single(device => device.SwitchKind == SwitchKind.LoadSwitch);
        var scenario = new EnergizationScenario(Guid.NewGuid(),
            [new EnergizedSeed(Guid.NewGuid(), source.Id, EnergizationSide.Bus)]);
        var state = new EnergizationAnalysisState();
        state.Execute(drawing, scenario);
        EnergizationResult success = state.CurrentResult!;
        Assert.True(success.IsSuccess);
        Assert.True(state.CanShowOverlay);
        state.Invalidate();
        Assert.Null(state.CurrentResult);
        Assert.False(state.CanShowOverlay);
        state.Execute(drawing, scenario);
        scenario.AddSeed(new EnergizedSeed(Guid.NewGuid(), Guid.NewGuid(), EnergizationSide.Bus));
        state.Execute(drawing, scenario);
        Assert.Equal(EnergizationValidity.Failed, state.LatestResult!.Validity);
        Assert.Empty(state.LatestResult.Terminals);
        Assert.Empty(state.LatestResult.Nodes);
        Assert.Null(state.CurrentResult);
        Assert.False(state.CanShowOverlay);
        Assert.NotEmpty(state.LatestDiagnostics);
    }
}
