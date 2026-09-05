using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using Xunit;

namespace DistributionDrawing.Domain.Tests;

public sealed class RingCabinetBayMetadataTests
{
    [Fact]
    public void Create_PreservesSequenceAndNonSequentialBayIndexes()
    {
        RingCabinet cabinet = CreateCabinet(10, 3, 8);

        Assert.Equal(new[] { 1, 2, 3 }, cabinet.Intervals.Select(x => x.Sequence));
        Assert.Equal(new[] { 10, 3, 8 }, cabinet.Intervals.Select(x => x.BayIndex));
    }

    [Fact]
    public void IntervalModel_DoesNotExposeFunction()
    {
        Assert.Null(typeof(RingCabinetInterval).GetProperty("Function"));
        Assert.Null(typeof(RingCabinetIntervalDefinition).GetProperty("Function"));
    }

    [Fact]
    public void Definition_RejectsDuplicateBayIndexes()
    {
        RingCabinetIntervalDefinition[] intervals =
        [
            CreateLoadSwitchInterval(1),
            CreateLoadSwitchInterval(1),
            CreateLoadSwitchInterval(3)
        ];

        Assert.Throws<ArgumentException>(() => RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "重复编号环网柜",
            intervals));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ExplicitCreation_RejectsNonPositiveBayIndex(int bayIndex)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateLoadSwitchInterval(bayIndex));
    }

    [Fact]
    public void Restore_PreservesStableIdsSequenceAndBayIndexes()
    {
        RingCabinet original = CreateCabinet(1, 3, 7);

        RingCabinet restored = RingCabinet.Restore(CreateRestoreDefinition(original));

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.MainBusNodeId, restored.MainBusNodeId);
        Assert.Equal(
            original.Intervals.Select(x => x.Sequence),
            restored.Intervals.Select(x => x.Sequence));
        Assert.Equal(
            original.Intervals.Select(x => x.BayIndex),
            restored.Intervals.Select(x => x.BayIndex));
        Assert.Equal(
            original.Intervals.Select(x => x.IntervalId),
            restored.Intervals.Select(x => x.IntervalId));
        Assert.Equal(
            original.ElectricalNodes.Select(x => x.Id).OrderBy(x => x),
            restored.ElectricalNodes.Select(x => x.Id).OrderBy(x => x));
        Assert.Equal(
            original.Terminals.Select(x => x.Id).OrderBy(x => x),
            restored.Terminals.Select(x => x.Id).OrderBy(x => x));
        Assert.Equal(
            original.Intervals.SelectMany(x => x.SwitchDevices)
                .Select(x => x.Id).OrderBy(x => x),
            restored.Intervals.SelectMany(x => x.SwitchDevices)
                .Select(x => x.Id).OrderBy(x => x));
        Assert.Equal(
            original.Intervals.Select(x => x.SwitchAssembly.AssemblyId).OrderBy(x => x),
            restored.Intervals.Select(x => x.SwitchAssembly.AssemblyId).OrderBy(x => x));
    }

    [Fact]
    public void Restore_RejectsDuplicateBayIndexes()
    {
        RingCabinetRestoreDefinition definition = CreateRestoreDefinition(
            CreateCabinet(1, 2, 3));
        RingCabinetIntervalRestoreDefinition[] intervals = definition.Intervals.ToArray();
        intervals[1] = intervals[1] with { BayIndex = intervals[0].BayIndex };

        Assert.Throws<InvalidOperationException>(() => RingCabinet.Restore(
            definition with { Intervals = intervals }));
    }

    [Fact]
    public void Restore_RejectsNonPositiveBayIndex()
    {
        RingCabinetRestoreDefinition definition = CreateRestoreDefinition(
            CreateCabinet(1, 2, 3));
        RingCabinetIntervalRestoreDefinition[] intervals = definition.Intervals.ToArray();
        intervals[0] = intervals[0] with { BayIndex = 0 };

        Assert.Throws<InvalidOperationException>(() => RingCabinet.Restore(
            definition with { Intervals = intervals }));
    }

    [Fact]
    public void IntegratedFeederCreation_PreservesStructuralFacts()
    {
        GroundingStructureKind structure = GroundingStructureKind.UpperLowerGrounding;
        RingCabinetIntervalDefinition[] intervals = new[] { 5, 7, 8, 9 }
            .Select(index => RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                index,
                structure,
                SwitchState.Open,
                SwitchState.Open,
                SwitchState.Open,
                $"负{index}间隔"))
            .ToArray();

        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "一二次融合环网柜",
            intervals));

        Assert.Equal(new[] { 1, 2, 3, 4 }, cabinet.Intervals.Select(x => x.Sequence));
        Assert.Equal(new[] { 5, 7, 8, 9 }, cabinet.Intervals.Select(x => x.BayIndex));
        Assert.All(cabinet.Intervals, interval =>
        {
            Assert.Equal(IntervalKind.IntegratedFeederInterval, interval.IntervalKind);
            Assert.Equal(structure, interval.GroundingStructureKind);
        });
    }

    private static RingCabinet CreateCabinet(params int[] bayIndexes)
    {
        RingCabinetIntervalDefinition[] intervals = bayIndexes
            .Select(CreateLoadSwitchInterval)
            .ToArray();

        return RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "测试环网柜",
            intervals));
    }

    private static RingCabinetIntervalDefinition CreateLoadSwitchInterval(int bayIndex)
    {
        return RingCabinetIntervalDefinition.CreateLoadSwitch(
            bayIndex,
            SwitchState.Open,
            SwitchState.Open,
            $"负{bayIndex}间隔");
    }

    private static RingCabinetRestoreDefinition CreateRestoreDefinition(RingCabinet cabinet)
    {
        RingCabinetIntervalRestoreDefinition[] intervals = cabinet.Intervals
            .Select(interval => new RingCabinetIntervalRestoreDefinition(
                interval.IntervalId,
                interval.ParentCabinetId,
                interval.Sequence,
                interval.BayIndex,
                interval.DisplayName,
                interval.IntervalKind,
                interval.GroundingStructureKind,
                interval.IntermediateNodeId,
                interval.CircuitNodeId,
                interval.EarthNodeId,
                interval.CableTerminalId,
                interval.SwitchAssembly.AssemblyId,
                interval.SwitchDevices
                    .Select(device => new SwitchDeviceRestoreDefinition(
                        device.Id,
                        device.SwitchKind,
                        device.InstallationType,
                        device.TerminalIds[0],
                        device.TerminalIds[1],
                        device.SwitchState!.Value,
                        device.DisplayName!,
                        device.VoltageLevel!,
                        device.DispatchNumber))
                    .ToArray()))
            .ToArray();

        return new RingCabinetRestoreDefinition(
            cabinet.Id,
            cabinet.DisplayName!,
            cabinet.MainBusNodeId,
            intervals);
    }
}
