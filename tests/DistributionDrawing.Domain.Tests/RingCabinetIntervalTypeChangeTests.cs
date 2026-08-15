using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using Xunit;

namespace DistributionDrawing.Domain.Tests;

public sealed class RingCabinetIntervalTypeChangeTests
{
    [Fact]
    public void IntegratedFeederToPT_PreservesSlotAndCreatesPTStructure()
    {
        RingCabinet cabinet = CreateMixedCabinet();
        RingCabinetInterval source = GetInterval(cabinet, 3);
        Guid cabinetId = cabinet.Id;
        Guid intervalId = source.IntervalId;
        Guid[] oldSwitchIds = source.SwitchDevices.Select(device => device.Id).ToArray();
        Guid[] oldTerminalIds = source.SwitchDevices.SelectMany(device => device.TerminalIds).ToArray();
        Guid[] oldNodeIds = [
            source.IntermediateNodeId!.Value,
            source.CircuitNodeId,
            source.EarthNodeId
        ];

        cabinet.ChangeIntervalType(intervalId, IntervalKind.PTInterval);

        RingCabinetInterval target = GetInterval(cabinet, 3);
        Assert.Equal(cabinetId, cabinet.Id);
        Assert.Equal(intervalId, target.IntervalId);
        Assert.Equal(source.Sequence, target.Sequence);
        Assert.Equal(source.BayIndex, target.BayIndex);
        Assert.Equal("-3", target.BusinessNumber);
        Assert.Equal(IntervalKind.PTInterval, target.IntervalKind);
        Assert.Equal("-3-2", NumberFor(target, SwitchKind.IsolationSwitch));
        Assert.Equal("-3-7", NumberFor(target, SwitchKind.GroundSwitch));
        Assert.DoesNotContain(oldSwitchIds, id => target.SwitchDevices.Any(device => device.Id == id));
        Assert.DoesNotContain(oldTerminalIds, id => target.SwitchDevices.Any(device => device.OwnsTerminal(id)));
        Assert.DoesNotContain(oldNodeIds, id =>
            target.CircuitNodeId == id || target.EarthNodeId == id);
    }

    [Fact]
    public void PTToIntegratedFeeder_PreservesSlotAndSupportsEachStructure()
    {
        foreach (GroundingStructureKind structure in Enum.GetValues<GroundingStructureKind>())
        {
            RingCabinet cabinet = CreatePTMixedCabinet();
            RingCabinetInterval source = GetInterval(cabinet, 3);

            cabinet.ChangeIntervalType(
                source.IntervalId,
                IntervalKind.IntegratedFeederInterval,
                structure);

            RingCabinetInterval target = GetInterval(cabinet, 3);
            Assert.Equal(IntervalKind.IntegratedFeederInterval, target.IntervalKind);
            Assert.Equal(structure, target.GroundingStructureKind);
            Assert.Equal("-3", NumberFor(target, SwitchKind.CircuitBreaker));
            Assert.Equal(
                structure == GroundingStructureKind.LowerLowerGrounding ? "-3-2" : "-3-4",
                NumberFor(target, SwitchKind.IsolationSwitch));
            Assert.Equal(
                structure == GroundingStructureKind.UpperIsolationGrounding ? "-3-47" : "-3-7",
                NumberFor(target, SwitchKind.GroundSwitch));
        }
    }

    [Fact]
    public void PTUniqueness_RejectsSecondPTWithoutChangingEitherInterval()
    {
        RingCabinet cabinet = CreatePTAndIntegratedCabinet();
        RingCabinetInterval existingPT = GetInterval(cabinet, 2);
        RingCabinetInterval candidate = GetInterval(cabinet, 4);
        Guid[] candidateSwitchIds = candidate.SwitchDevices.Select(device => device.Id).ToArray();
        Guid[] candidateTerminalIds = candidate.SwitchDevices.SelectMany(device => device.TerminalIds).ToArray();
        Guid[] candidateNodeIds = [candidate.IntermediateNodeId!.Value, candidate.CircuitNodeId, candidate.EarthNodeId];

        Assert.Throws<InvalidOperationException>(() =>
            cabinet.ChangeIntervalType(candidate.IntervalId, IntervalKind.PTInterval));

        Assert.Equal(IntervalKind.PTInterval, GetInterval(cabinet, 2).IntervalKind);
        Assert.Equal(existingPT.IntervalId, GetInterval(cabinet, 2).IntervalId);
        RingCabinetInterval unchanged = GetInterval(cabinet, 4);
        Assert.Equal(IntervalKind.IntegratedFeederInterval, unchanged.IntervalKind);
        Assert.Equal(candidateSwitchIds, unchanged.SwitchDevices.Select(device => device.Id));
        Assert.Equal(candidateTerminalIds, unchanged.SwitchDevices.SelectMany(device => device.TerminalIds));
        Assert.Equal(candidateNodeIds[0], unchanged.IntermediateNodeId);
        Assert.Equal(candidateNodeIds[1], unchanged.CircuitNodeId);
        Assert.Equal(candidateNodeIds[2], unchanged.EarthNodeId);
    }

    [Fact]
    public void PTMigration_AllowsReplacingPTThenUsingAnotherSlot()
    {
        RingCabinet cabinet = CreatePTAndIntegratedCabinet();
        RingCabinetInterval pt = GetInterval(cabinet, 2);
        RingCabinetInterval other = GetInterval(cabinet, 4);

        cabinet.ChangeIntervalType(
            pt.IntervalId,
            IntervalKind.IntegratedFeederInterval,
            GroundingStructureKind.UpperIsolationGrounding);
        cabinet.ChangeIntervalType(other.IntervalId, IntervalKind.PTInterval);

        Assert.DoesNotContain(cabinet.Intervals, interval =>
            interval.IntervalKind == IntervalKind.PTInterval && interval.BayIndex == 2);
        RingCabinetInterval newPT = GetInterval(cabinet, 4);
        Assert.Equal(IntervalKind.PTInterval, newPT.IntervalKind);
        Assert.Equal("-4-2", NumberFor(newPT, SwitchKind.IsolationSwitch));
        Assert.Equal("-4-7", NumberFor(newPT, SwitchKind.GroundSwitch));
    }

    [Fact]
    public void InvalidConfiguration_FailsBeforeChangingTheOriginalStructure()
    {
        RingCabinet cabinet = CreateMixedCabinet();
        RingCabinetInterval source = GetInterval(cabinet, 3);
        IntervalKind originalKind = source.IntervalKind;
        Guid[] originalSwitchIds = source.SwitchDevices.Select(device => device.Id).ToArray();
        Guid[] originalTerminalIds = source.SwitchDevices.SelectMany(device => device.TerminalIds).ToArray();
        Guid? originalIntermediateNodeId = source.IntermediateNodeId;
        Guid originalCircuitNodeId = source.CircuitNodeId;
        Guid originalEarthNodeId = source.EarthNodeId;
        int originalSequence = source.Sequence;
        int originalBayIndex = source.BayIndex;

        Assert.Throws<ArgumentException>(() =>
            cabinet.ChangeIntervalType(source.IntervalId, IntervalKind.IntegratedFeederInterval));

        RingCabinetInterval unchanged = GetInterval(cabinet, 3);
        Assert.Equal(originalKind, unchanged.IntervalKind);
        Assert.Equal(originalSwitchIds, unchanged.SwitchDevices.Select(device => device.Id));
        Assert.Equal(originalTerminalIds, unchanged.SwitchDevices.SelectMany(device => device.TerminalIds));
        Assert.Equal(originalIntermediateNodeId, unchanged.IntermediateNodeId);
        Assert.Equal(originalCircuitNodeId, unchanged.CircuitNodeId);
        Assert.Equal(originalEarthNodeId, unchanged.EarthNodeId);
        Assert.Equal(originalSequence, unchanged.Sequence);
        Assert.Equal(originalBayIndex, unchanged.BayIndex);
    }

    private static RingCabinet CreateMixedCabinet()
    {
        return RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "Mixed cabinet",
            [
                RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
                RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                    3,
                    GroundingStructureKind.UpperIsolationGrounding,
                    SwitchState.Open,
                    SwitchState.Open,
                    SwitchState.Open)
            ]));
    }

    private static RingCabinet CreatePTMixedCabinet()
    {
        return RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "PT mixed cabinet",
            [
                RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
                RingCabinetIntervalDefinition.CreatePT(3, SwitchState.Open, SwitchState.Open),
                RingCabinetIntervalDefinition.CreateLoadSwitch(5, SwitchState.Open, SwitchState.Open)
            ]));
    }

    private static RingCabinet CreatePTAndIntegratedCabinet()
    {
        return RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "PT and integrated cabinet",
            [
                RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
                RingCabinetIntervalDefinition.CreatePT(2, SwitchState.Open, SwitchState.Open),
                RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                    4,
                    GroundingStructureKind.UpperIsolationGrounding,
                    SwitchState.Open,
                    SwitchState.Open,
                    SwitchState.Open)
            ]));
    }

    private static RingCabinetInterval GetInterval(RingCabinet cabinet, int bayIndex)
    {
        return Assert.Single(cabinet.Intervals, interval => interval.BayIndex == bayIndex);
    }

    private static string? NumberFor(RingCabinetInterval interval, SwitchKind kind)
    {
        SwitchDevice switchDevice = Assert.Single(
            interval.SwitchDevices,
            device => device.SwitchKind == kind);
        return interval.GetSwitchBusinessNumber(switchDevice.Id);
    }
}
