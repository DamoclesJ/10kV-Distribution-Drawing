using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
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
        Assert.Equal("负3", target.BusinessNumber);
        Assert.Equal(IntervalKind.PTInterval, target.IntervalKind);
        Assert.Equal("负3-2", NumberFor(target, SwitchKind.IsolationSwitch));
        Assert.Equal("负3-7", NumberFor(target, SwitchKind.GroundSwitch));
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
            Assert.Equal("负3", NumberFor(target, SwitchKind.CircuitBreaker));
            Assert.Equal(
                structure == GroundingStructureKind.LowerLowerGrounding ? "负3-2" : "负3-4",
                NumberFor(target, SwitchKind.IsolationSwitch));
            Assert.Equal(
                structure == GroundingStructureKind.UpperIsolationGrounding ? "负3-47" : "负3-7",
                NumberFor(target, SwitchKind.GroundSwitch));
        }
    }

    [Fact]
    public void PTMigration_MovesTheSinglePTAndRestoresTheReplacedStandardType()
    {
        RingCabinet cabinet = CreatePTAndIntegratedCabinet();
        RingCabinetInterval existingPT = GetInterval(cabinet, 2);
        RingCabinetInterval candidate = GetInterval(cabinet, 4);

        cabinet.ChangeIntervalType(candidate.IntervalId, IntervalKind.PTInterval);

        RingCabinetInterval restored = GetInterval(cabinet, 2);
        Assert.Equal(existingPT.IntervalId, restored.IntervalId);
        Assert.Equal(IntervalKind.IntegratedFeederInterval, restored.IntervalKind);
        Assert.Equal(
            GroundingStructureKind.UpperIsolationGrounding,
            restored.GroundingStructureKind);
        Assert.Equal(IntervalKind.PTInterval, GetInterval(cabinet, 4).IntervalKind);
        Assert.Single(cabinet.Intervals, interval =>
            interval.IntervalKind == IntervalKind.PTInterval);
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
        Assert.Equal("负4-2", NumberFor(newPT, SwitchKind.IsolationSwitch));
        Assert.Equal("负4-7", NumberFor(newPT, SwitchKind.GroundSwitch));
    }

    [Fact]
    public void PTMigration_InConventionalCabinetRestoresLoadSwitchStandardType()
    {
        RingCabinet cabinet = CreateLoadSwitchAndPTCabinet();

        cabinet.ChangeIntervalType(GetInterval(cabinet, 3).IntervalId, IntervalKind.PTInterval);

        Assert.Equal(IntervalKind.PTInterval, GetInterval(cabinet, 3).IntervalKind);
        Assert.Equal(IntervalKind.LoadSwitchInterval, GetInterval(cabinet, 5).IntervalKind);
        Assert.Single(cabinet.Intervals, interval =>
            interval.IntervalKind == IntervalKind.PTInterval);
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

    [Theory]
    [InlineData(IntervalKind.IntegratedFeederInterval)]
    [InlineData(IntervalKind.PTInterval)]
    public void LoadSwitchTypeChange_PreservesSlotIdentity(IntervalKind sourceKind)
    {
        RingCabinet cabinet = sourceKind == IntervalKind.IntegratedFeederInterval
            ? CreateTwoLoadAndIntegratedCabinet()
            : CreatePTMixedCabinet();
        RingCabinetInterval source = GetInterval(cabinet, 3);
        Guid intervalId = source.IntervalId;
        int sequence = source.Sequence;
        int bayIndex = source.BayIndex;

        cabinet.ChangeIntervalType(intervalId, IntervalKind.LoadSwitchInterval);

        RingCabinetInterval target = GetInterval(cabinet, 3);
        Assert.Equal(intervalId, target.IntervalId);
        Assert.Equal(sequence, target.Sequence);
        Assert.Equal(bayIndex, target.BayIndex);
        Assert.Equal(IntervalKind.LoadSwitchInterval, target.IntervalKind);
        Assert.Equal("负3-7", NumberFor(target, SwitchKind.GroundSwitch));
        Assert.Null(NumberFor(target, SwitchKind.LoadSwitch));
    }

    [Theory]
    [InlineData(GroundingStructureKind.UpperLowerGrounding)]
    [InlineData(GroundingStructureKind.LowerLowerGrounding)]
    public void LoadSwitchTypeChange_ToIntegratedFeederSupportsExplicitStructure(
        GroundingStructureKind structure)
    {
        RingCabinet cabinet = CreateLoadSwitchAndPTCabinet();
        RingCabinetInterval source = GetInterval(cabinet, 3);

        cabinet.ChangeIntervalType(
            source.IntervalId,
            IntervalKind.IntegratedFeederInterval,
            structure);

        RingCabinetInterval target = GetInterval(cabinet, 3);
        Assert.Equal(IntervalKind.IntegratedFeederInterval, target.IntervalKind);
        Assert.Equal(structure, target.GroundingStructureKind);
        Assert.Equal("负3", NumberFor(target, SwitchKind.CircuitBreaker));
    }

    [Theory]
    [InlineData(GroundingStructureKind.UpperLowerGrounding)]
    [InlineData(GroundingStructureKind.LowerLowerGrounding)]
    public void SameTypeIntegratedFeeder_ChangingStructureReplacesInternalObjects(
        GroundingStructureKind targetStructure)
    {
        RingCabinet cabinet = CreateMixedCabinet();
        RingCabinetInterval source = GetInterval(cabinet, 3);
        Guid intervalId = source.IntervalId;
        Guid oldAssemblyId = source.SwitchAssembly.AssemblyId;
        Guid[] oldSwitchIds = source.SwitchDevices.Select(device => device.Id).ToArray();
        Guid[] oldTerminalIds = source.SwitchDevices.SelectMany(device => device.TerminalIds).ToArray();
        Guid[] oldNodeIds = [source.IntermediateNodeId!.Value, source.CircuitNodeId, source.EarthNodeId];

            cabinet.ChangeIntervalType(
                intervalId,
                IntervalKind.IntegratedFeederInterval,
                targetStructure);

        RingCabinetInterval target = GetInterval(cabinet, 3);
        Assert.Equal(intervalId, target.IntervalId);
        Assert.Equal(source.Sequence, target.Sequence);
        Assert.Equal(source.BayIndex, target.BayIndex);
        Assert.Equal(targetStructure, target.GroundingStructureKind);
        Assert.NotEqual(oldAssemblyId, target.SwitchAssembly.AssemblyId);
        Assert.DoesNotContain(oldSwitchIds, id => target.SwitchDevices.Any(device => device.Id == id));
        Assert.DoesNotContain(oldTerminalIds, id => target.SwitchDevices.Any(device => device.OwnsTerminal(id)));
        Assert.DoesNotContain(oldNodeIds, id =>
            target.IntermediateNodeId == id || target.CircuitNodeId == id || target.EarthNodeId == id);
    }

    [Fact]
    public void PTMigration_ReplacesBothIntervalsAtomicallyWithoutChangingOtherIntervals()
    {
        RingCabinet cabinet = CreatePTAndIntegratedCabinet();
        RingCabinetInterval untouched = GetInterval(cabinet, 1);
        IntervalSnapshot untouchedSnapshot = Snapshot(untouched);

        cabinet.ChangeIntervalType(GetInterval(cabinet, 4).IntervalId, IntervalKind.PTInterval);

        AssertSnapshotEqual(untouchedSnapshot, Snapshot(GetInterval(cabinet, 1)));
        Assert.Equal(IntervalKind.IntegratedFeederInterval, GetInterval(cabinet, 2).IntervalKind);
        Assert.Equal(IntervalKind.PTInterval, GetInterval(cabinet, 4).IntervalKind);
        Assert.Single(cabinet.Intervals, interval =>
            interval.IntervalKind == IntervalKind.PTInterval);
    }

    [Fact]
    public void RestoreRejectsMultiplePTIntervals()
    {
        Guid cabinetId = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() => RingCabinet.Restore(
            new RingCabinetRestoreDefinition(
                cabinetId,
                "Invalid two PT cabinet",
                Guid.NewGuid(),
                [
                    CreatePTRestoreDefinition(cabinetId, 1, 1),
                    CreatePTRestoreDefinition(cabinetId, 2, 2)
                ])));
    }

    [Fact]
    public void RestoreState_RejectsDifferentCabinetIdWithoutChangingAggregate()
    {
        RingCabinet cabinet = CreateMixedCabinet();
        CabinetSnapshot before = Snapshot(cabinet);
        RingCabinetRestoreDefinition invalid = cabinet.CaptureRestoreDefinition() with
        {
            CabinetId = Guid.NewGuid()
        };

        Assert.Throws<ArgumentException>(() => cabinet.RestoreState(invalid));

        AssertCabinetSnapshotEqual(before, Snapshot(cabinet));
    }

    [Fact]
    public void RestoreState_RejectsDifferentMainBusNodeIdWithoutChangingAggregate()
    {
        RingCabinet cabinet = CreateMixedCabinet();
        CabinetSnapshot before = Snapshot(cabinet);
        RingCabinetRestoreDefinition invalid = cabinet.CaptureRestoreDefinition() with
        {
            MainBusNodeId = Guid.NewGuid()
        };

        Assert.Throws<ArgumentException>(() => cabinet.RestoreState(invalid));

        AssertCabinetSnapshotEqual(before, Snapshot(cabinet));
    }

    [Fact]
    public void RestoreState_ValidSnapshotsRestoreTypeChangeStates()
    {
        RingCabinet cabinet = CreateMixedCabinet();
        Guid intervalId = GetInterval(cabinet, 3).IntervalId;
        RingCabinetRestoreDefinition before = cabinet.CaptureRestoreDefinition();

        cabinet.ChangeIntervalType(intervalId, IntervalKind.PTInterval);
        RingCabinetRestoreDefinition after = cabinet.CaptureRestoreDefinition();

        cabinet.RestoreState(before);
        Assert.Equal(IntervalKind.IntegratedFeederInterval, GetInterval(cabinet, 3).IntervalKind);
        cabinet.RestoreState(after);
        Assert.Equal(IntervalKind.PTInterval, GetInterval(cabinet, 3).IntervalKind);
    }

    [Fact]
    public void DocumentSynchronization_TypeChangeAndChangeBackLeaveNoDeviceOwnerOrphans()
    {
        RingCabinet cabinet = CreateMixedCabinet();
        var document = new DrawingDocument(Guid.NewGuid(), "Topology invariant");
        document.AddDevice(cabinet);
        Guid intervalId = GetInterval(cabinet, 3).IntervalId;

        cabinet.ChangeIntervalType(intervalId, IntervalKind.LoadSwitchInterval);
        document.SynchronizeRingCabinetAggregate(cabinet);
        AssertDeviceTopologyOwnersExist(document);

        cabinet.ChangeIntervalType(
            intervalId,
            IntervalKind.IntegratedFeederInterval,
            GroundingStructureKind.UpperIsolationGrounding);
        document.SynchronizeRingCabinetAggregate(cabinet);
        AssertDeviceTopologyOwnersExist(document);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void DocumentSynchronization_PTMigrationFromEitherEndLeavesNoDeviceOwnerOrphans(
        int originalPTBay)
    {
        RingCabinet cabinet = CreateIntegratedCabinetWithPTAt(originalPTBay);
        var document = new DrawingDocument(Guid.NewGuid(), "Topology invariant");
        document.AddDevice(cabinet);
        RingCabinetInterval target = GetInterval(cabinet, 3);

        cabinet.ChangeIntervalType(target.IntervalId, IntervalKind.PTInterval);
        document.SynchronizeRingCabinetAggregate(cabinet);

        Assert.Equal(IntervalKind.PTInterval, GetInterval(cabinet, 3).IntervalKind);
        Assert.Equal(
            IntervalKind.IntegratedFeederInterval,
            GetInterval(cabinet, originalPTBay).IntervalKind);
        Assert.Single(cabinet.Intervals, interval =>
            interval.IntervalKind == IntervalKind.PTInterval);
        AssertDeviceTopologyOwnersExist(document);
    }

    [Fact]
    public void DocumentSynchronization_MiddlePTCanMigrateAgainWithoutOwnerOrphans()
    {
        RingCabinet cabinet = CreateIntegratedCabinetWithPTAt(5);
        var document = new DrawingDocument(Guid.NewGuid(), "Topology invariant");
        document.AddDevice(cabinet);

        cabinet.ChangeIntervalType(GetInterval(cabinet, 3).IntervalId, IntervalKind.PTInterval);
        document.SynchronizeRingCabinetAggregate(cabinet);
        cabinet.ChangeIntervalType(GetInterval(cabinet, 2).IntervalId, IntervalKind.PTInterval);
        document.SynchronizeRingCabinetAggregate(cabinet);

        Assert.Equal(IntervalKind.PTInterval, GetInterval(cabinet, 2).IntervalKind);
        Assert.Equal(IntervalKind.IntegratedFeederInterval, GetInterval(cabinet, 3).IntervalKind);
        Assert.Single(cabinet.Intervals, interval =>
            interval.IntervalKind == IntervalKind.PTInterval);
        AssertDeviceTopologyOwnersExist(document);
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

    private static RingCabinet CreateIntegratedCabinetWithPTAt(int ptBay)
    {
        RingCabinetIntervalDefinition[] definitions = Enumerable.Range(1, 5)
            .Select(index => index == ptBay
                ? RingCabinetIntervalDefinition.CreatePT(
                    index,
                    SwitchState.Open,
                    SwitchState.Open)
                : RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                    index,
                    GroundingStructureKind.UpperIsolationGrounding,
                    SwitchState.Open,
                    SwitchState.Open,
                    SwitchState.Open))
            .ToArray();
        return RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "Integrated cabinet with PT",
            definitions));
    }

    private static void AssertDeviceTopologyOwnersExist(DrawingDocument document)
    {
        Assert.All(document.ElectricalNodes.Where(node =>
                node.OwnerType == TopologyOwnerType.Device),
            node => Assert.Contains(document.Devices, device => device.Id == node.OwnerId));
        Assert.All(document.Terminals.Where(terminal =>
                terminal.OwnerType == TopologyOwnerType.Device),
            terminal => Assert.Contains(document.Devices, device =>
                device.Id == terminal.OwnerId));
    }

    private static RingCabinet CreateLoadSwitchAndPTCabinet()
    {
        return RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "Load switch and PT cabinet",
            [
                RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
                RingCabinetIntervalDefinition.CreateLoadSwitch(3, SwitchState.Open, SwitchState.Open),
                RingCabinetIntervalDefinition.CreatePT(5, SwitchState.Open, SwitchState.Open)
            ]));
    }

    private static RingCabinetIntervalRestoreDefinition CreatePTRestoreDefinition(
        Guid cabinetId,
        int sequence,
        int bayIndex)
    {
        Guid intervalId = Guid.NewGuid();
        Guid circuitNodeId = Guid.NewGuid();
        Guid earthNodeId = Guid.NewGuid();
        Guid externalTerminalId = Guid.NewGuid();
        Guid isolationId = Guid.NewGuid();
        Guid groundId = Guid.NewGuid();

        return new RingCabinetIntervalRestoreDefinition(
            intervalId,
            cabinetId,
            sequence,
            bayIndex,
            $"PT {bayIndex}",
            IntervalKind.PTInterval,
            null,
            null,
            circuitNodeId,
            earthNodeId,
            externalTerminalId,
            Guid.NewGuid(),
            [
                new SwitchDeviceRestoreDefinition(
                    isolationId,
                    SwitchKind.IsolationSwitch,
                    SwitchInstallationType.CabinetInterval,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    SwitchState.Open,
                    $"PT {bayIndex} isolation",
                    "10kV",
                    null),
                new SwitchDeviceRestoreDefinition(
                    groundId,
                    SwitchKind.GroundSwitch,
                    SwitchInstallationType.CabinetInterval,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    SwitchState.Open,
                    $"PT {bayIndex} ground",
                    "10kV",
                    null)
            ]);
    }

    private static RingCabinet CreateTwoLoadAndIntegratedCabinet()
    {
        return RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "Two load switches and integrated cabinet",
            [
                RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
                RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                    3,
                    GroundingStructureKind.UpperIsolationGrounding,
                    SwitchState.Open,
                    SwitchState.Open,
                    SwitchState.Open),
                RingCabinetIntervalDefinition.CreateLoadSwitch(5, SwitchState.Open, SwitchState.Open)
            ]));
    }

    private static IntervalSnapshot Snapshot(RingCabinetInterval interval)
    {
        return new IntervalSnapshot(
            interval.IntervalId,
            interval.Sequence,
            interval.BayIndex,
            interval.IntervalKind,
            interval.GroundingStructureKind,
            interval.SwitchAssembly.AssemblyId,
            interval.SwitchDevices
                .Select(device => new SwitchSnapshot(
                    device.Id,
                    device.SwitchKind,
                    device.SwitchState,
                    device.TerminalIds.ToArray()))
                .ToArray(),
            interval.CircuitNodeId,
            interval.IntermediateNodeId,
            interval.EarthNodeId,
            interval.ExternalTerminalId);
    }

    private static CabinetSnapshot Snapshot(RingCabinet cabinet)
    {
        return new CabinetSnapshot(
            cabinet.Id,
            cabinet.MainBusNodeId,
            cabinet.Intervals.Select(Snapshot).ToArray(),
            cabinet.ElectricalNodes.Select(node => node.Id).ToArray());
    }

    private static void AssertCabinetSnapshotEqual(
        CabinetSnapshot expected,
        CabinetSnapshot actual)
    {
        Assert.Equal(expected.CabinetId, actual.CabinetId);
        Assert.Equal(expected.MainBusNodeId, actual.MainBusNodeId);
        Assert.Equal(expected.NodeIds, actual.NodeIds);
        Assert.Equal(expected.Intervals.Count, actual.Intervals.Count);
        for (int index = 0; index < expected.Intervals.Count; index++)
        {
            AssertSnapshotEqual(expected.Intervals[index], actual.Intervals[index]);
        }
    }

    private static void AssertSnapshotEqual(
        IntervalSnapshot expected,
        IntervalSnapshot actual)
    {
        Assert.Equal(expected.IntervalId, actual.IntervalId);
        Assert.Equal(expected.Sequence, actual.Sequence);
        Assert.Equal(expected.BayIndex, actual.BayIndex);
        Assert.Equal(expected.IntervalKind, actual.IntervalKind);
        Assert.Equal(expected.GroundingStructureKind, actual.GroundingStructureKind);
        Assert.Equal(expected.SwitchAssemblyId, actual.SwitchAssemblyId);
        Assert.Equal(expected.CircuitNodeId, actual.CircuitNodeId);
        Assert.Equal(expected.IntermediateNodeId, actual.IntermediateNodeId);
        Assert.Equal(expected.EarthNodeId, actual.EarthNodeId);
        Assert.Equal(expected.ExternalTerminalId, actual.ExternalTerminalId);
        Assert.Equal(expected.Switches.Select(switchSnapshot => switchSnapshot.Id),
            actual.Switches.Select(switchSnapshot => switchSnapshot.Id));
        Assert.Equal(expected.Switches.Select(switchSnapshot => switchSnapshot.Kind),
            actual.Switches.Select(switchSnapshot => switchSnapshot.Kind));
        Assert.Equal(expected.Switches.Select(switchSnapshot => switchSnapshot.State),
            actual.Switches.Select(switchSnapshot => switchSnapshot.State));
        Assert.Equal(
            expected.Switches.Select(switchSnapshot => switchSnapshot.TerminalIds),
            actual.Switches.Select(switchSnapshot => switchSnapshot.TerminalIds));
    }

    private sealed record IntervalSnapshot(
        Guid IntervalId,
        int Sequence,
        int BayIndex,
        IntervalKind IntervalKind,
        GroundingStructureKind? GroundingStructureKind,
        Guid SwitchAssemblyId,
        IReadOnlyList<SwitchSnapshot> Switches,
        Guid CircuitNodeId,
        Guid? IntermediateNodeId,
        Guid EarthNodeId,
        Guid ExternalTerminalId);

    private sealed record CabinetSnapshot(
        Guid CabinetId,
        Guid MainBusNodeId,
        IReadOnlyList<IntervalSnapshot> Intervals,
        IReadOnlyList<Guid> NodeIds);

    private sealed record SwitchSnapshot(
        Guid Id,
        SwitchKind Kind,
        SwitchState? State,
        IReadOnlyList<Guid> TerminalIds);

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
