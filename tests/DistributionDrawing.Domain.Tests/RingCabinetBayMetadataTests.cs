using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using System.Reflection;
using Xunit;

namespace DistributionDrawing.Domain.Tests;

public sealed class RingCabinetBayMetadataTests
{
    [Fact]
    public void Create_PreservesSequenceBayIndexAndFunction()
    {
        RingCabinet cabinet = CreateCabinet(
            (1, BayFunction.Incoming),
            (2, BayFunction.Outgoing),
            (5, BayFunction.Reserve));

        Assert.Equal(new[] { 1, 2, 3 }, cabinet.Intervals.Select(interval => interval.Sequence));
        Assert.Equal(new[] { 1, 2, 5 }, cabinet.Intervals.Select(interval => interval.BayIndex));
        Assert.Equal(
            new[] { BayFunction.Incoming, BayFunction.Outgoing, BayFunction.Reserve },
            cabinet.Intervals.Select(interval => interval.Function));
    }

    [Fact]
    public void Definition_RejectsDuplicateBayIndexes()
    {
        RingCabinetIntervalDefinition[] intervals =
        [
            CreateInterval(1, BayFunction.Incoming),
            CreateInterval(1, BayFunction.Outgoing),
            CreateInterval(3, BayFunction.Reserve)
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
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateInterval(
            bayIndex,
            BayFunction.Outgoing));
    }

    [Fact]
    public void ExplicitCreation_RejectsUnknownFunction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateInterval(
            1,
            BayFunction.Unknown));
    }

    [Fact]
    public void ExplicitCreation_RejectsUndefinedFunction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateInterval(
            1,
            (BayFunction)int.MaxValue));
    }

    [Fact]
    public void Definition_RejectsUnknownFunction()
    {
        RingCabinetIntervalDefinition interval = CreateUncheckedDefinition(
            1,
            BayFunction.Unknown,
            IntervalKind.LoadSwitchInterval);

        Assert.Throws<ArgumentException>(() => RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "未知功能环网柜",
            [interval]));
    }

    [Fact]
    public void RestoreDefinition_RequiresCompleteBayMetadata()
    {
        ConstructorInfo constructor = Assert.Single(
            typeof(RingCabinetIntervalRestoreDefinition).GetConstructors());
        string[] parameterNames = constructor.GetParameters()
            .Select(parameter => parameter.Name!)
            .ToArray();

        Assert.True(parameterNames.Contains("BayIndex", StringComparer.OrdinalIgnoreCase));
        Assert.True(parameterNames.Contains("Function", StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Restore_PreservesStableIdsAndBayMetadata()
    {
        RingCabinet original = CreateCabinet(
            (1, BayFunction.Incoming),
            (3, BayFunction.Tie),
            (7, BayFunction.Reserve));

        RingCabinet restored = RingCabinet.Restore(CreateRestoreDefinition(original));

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.MainBusNodeId, restored.MainBusNodeId);
        Assert.Equal(
            original.Intervals.Select(interval => interval.IntervalId),
            restored.Intervals.Select(interval => interval.IntervalId));
        Assert.Equal(
            original.Intervals.Select(interval => interval.BayIndex),
            restored.Intervals.Select(interval => interval.BayIndex));
        Assert.Equal(
            original.Intervals.Select(interval => interval.Function),
            restored.Intervals.Select(interval => interval.Function));
        Assert.Equal(
            original.ElectricalNodes.Select(node => node.Id).OrderBy(id => id),
            restored.ElectricalNodes.Select(node => node.Id).OrderBy(id => id));
        Assert.Equal(
            original.Terminals.Select(terminal => terminal.Id).OrderBy(id => id),
            restored.Terminals.Select(terminal => terminal.Id).OrderBy(id => id));
        Assert.Equal(
            original.Intervals.SelectMany(interval => interval.SwitchDevices)
                .Select(device => device.Id).OrderBy(id => id),
            restored.Intervals.SelectMany(interval => interval.SwitchDevices)
                .Select(device => device.Id).OrderBy(id => id));
        Assert.Equal(
            original.Intervals.Select(interval => interval.SwitchAssembly.AssemblyId).OrderBy(id => id),
            restored.Intervals.Select(interval => interval.SwitchAssembly.AssemblyId).OrderBy(id => id));
    }

    [Fact]
    public void Restore_AllowsUnknownFunctionForCompatibility()
    {
        RingCabinet original = CreateCabinet(
            (1, BayFunction.Incoming),
            (2, BayFunction.Outgoing),
            (3, BayFunction.Reserve));
        RingCabinetRestoreDefinition definition = CreateRestoreDefinition(original);
        RingCabinetIntervalRestoreDefinition[] intervals = definition.Intervals.ToArray();
        intervals[1] = intervals[1] with
        {
            BayIndex = 9,
            Function = BayFunction.Unknown
        };

        RingCabinet restored = RingCabinet.Restore(definition with { Intervals = intervals });

        Assert.Equal(9, restored.Intervals[1].BayIndex);
        Assert.Equal(BayFunction.Unknown, restored.Intervals[1].Function);
    }

    [Fact]
    public void Restore_RejectsDuplicateBayIndexes()
    {
        RingCabinet original = CreateCabinet(
            (1, BayFunction.Incoming),
            (2, BayFunction.Outgoing),
            (3, BayFunction.Reserve));
        RingCabinetRestoreDefinition definition = CreateRestoreDefinition(original);
        RingCabinetIntervalRestoreDefinition[] intervals = definition.Intervals.ToArray();
        intervals[1] = intervals[1] with { BayIndex = intervals[0].BayIndex };

        Assert.Throws<InvalidOperationException>(() => RingCabinet.Restore(
            definition with { Intervals = intervals }));
    }

    [Fact]
    public void Restore_RejectsNonPositiveBayIndex()
    {
        RingCabinetRestoreDefinition definition = CreateRestoreDefinition(CreateCabinet(
            (1, BayFunction.Incoming),
            (2, BayFunction.Outgoing),
            (3, BayFunction.Reserve)));
        RingCabinetIntervalRestoreDefinition[] intervals = definition.Intervals.ToArray();
        intervals[0] = intervals[0] with { BayIndex = 0 };

        Assert.Throws<InvalidOperationException>(() => RingCabinet.Restore(
            definition with { Intervals = intervals }));
    }

    [Fact]
    public void Restore_RejectsUndefinedFunction()
    {
        RingCabinetRestoreDefinition definition = CreateRestoreDefinition(CreateCabinet(
            (1, BayFunction.Incoming),
            (2, BayFunction.Outgoing),
            (3, BayFunction.Reserve)));
        RingCabinetIntervalRestoreDefinition[] intervals = definition.Intervals.ToArray();
        intervals[0] = intervals[0] with { Function = (BayFunction)int.MaxValue };

        Assert.Throws<InvalidOperationException>(() => RingCabinet.Restore(
            definition with { Intervals = intervals }));
    }

    [Fact]
    public void LoadSwitchCreation_RejectsPtFunction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RingCabinetIntervalDefinition.CreateLoadSwitch(
                1,
                BayFunction.PT,
                SwitchState.Open,
                SwitchState.Open,
                "PT间隔"));
    }

    [Fact]
    public void IntegratedFeederCreation_RejectsPtFunction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                1,
                BayFunction.PT,
                GroundingStructureKind.UpperLowerGrounding,
                SwitchState.Open,
                SwitchState.Open,
                SwitchState.Open,
                "PT间隔"));
    }

    [Fact]
    public void IntegratedFeederCreation_PreservesKnownFunction()
    {
        RingCabinetIntervalDefinition interval =
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                5,
                BayFunction.Tie,
                GroundingStructureKind.UpperLowerGrounding,
                SwitchState.Open,
                SwitchState.Open,
                SwitchState.Open,
                "负5间隔");

        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "一二次融合环网柜",
            [
                interval,
                CreateIntegratedFeederInterval(7, BayFunction.Outgoing),
                CreateIntegratedFeederInterval(8, BayFunction.Outgoing),
                CreateIntegratedFeederInterval(9, BayFunction.Reserve)
            ]));

        Assert.Equal(5, cabinet.Intervals[0].BayIndex);
        Assert.Equal(BayFunction.Tie, cabinet.Intervals[0].Function);
    }

    [Fact]
    public void Restore_RejectsPtFunctionForExistingIntervalKinds()
    {
        RingCabinetRestoreDefinition definition = CreateRestoreDefinition(CreateCabinet(
            (1, BayFunction.Incoming),
            (2, BayFunction.Outgoing),
            (3, BayFunction.Reserve)));
        RingCabinetIntervalRestoreDefinition[] intervals = definition.Intervals.ToArray();
        intervals[0] = intervals[0] with { Function = BayFunction.PT };

        Assert.Throws<InvalidOperationException>(() => RingCabinet.Restore(
            definition with { Intervals = intervals }));
    }

    private static RingCabinet CreateCabinet(
        params (int BayIndex, BayFunction Function)[] metadata)
    {
        RingCabinetIntervalDefinition[] intervals = metadata
            .Select(item => CreateInterval(item.BayIndex, item.Function))
            .ToArray();

        return RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "测试环网柜",
            intervals));
    }

    private static RingCabinetIntervalDefinition CreateInterval(
        int bayIndex,
        BayFunction function)
    {
        return RingCabinetIntervalDefinition.CreateLoadSwitch(
            bayIndex,
            function,
            SwitchState.Open,
            SwitchState.Open,
            $"负{bayIndex}间隔");
    }

    private static RingCabinetIntervalDefinition CreateIntegratedFeederInterval(
        int bayIndex,
        BayFunction function)
    {
        return RingCabinetIntervalDefinition.CreateIntegratedFeeder(
            bayIndex,
            function,
            GroundingStructureKind.UpperLowerGrounding,
            SwitchState.Open,
            SwitchState.Open,
            SwitchState.Open,
            $"负{bayIndex}间隔");
    }

    private static RingCabinetIntervalDefinition CreateUncheckedDefinition(
        int bayIndex,
        BayFunction function,
        IntervalKind intervalKind)
    {
        ConstructorInfo constructor = typeof(RingCabinetIntervalDefinition).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types:
                [
                    typeof(int),
                    typeof(BayFunction),
                    typeof(IntervalKind),
                    typeof(string),
                    typeof(SwitchState?),
                    typeof(SwitchState?),
                    typeof(SwitchState?),
                    typeof(SwitchState),
                    typeof(GroundingStructureKind?)
                ],
                modifiers: null)
            ?? throw new InvalidOperationException(
                "The RingCabinetIntervalDefinition constructor could not be found.");

        return (RingCabinetIntervalDefinition)constructor.Invoke(
        [
            bayIndex,
            function,
            intervalKind,
            "测试间隔",
            SwitchState.Open,
            null,
            null,
            SwitchState.Open,
            null
        ]);
    }

    private static RingCabinetRestoreDefinition CreateRestoreDefinition(RingCabinet cabinet)
    {
        RingCabinetIntervalRestoreDefinition[] intervals = cabinet.Intervals
            .Select(interval => new RingCabinetIntervalRestoreDefinition(
                interval.IntervalId,
                interval.ParentCabinetId,
                interval.Sequence,
                interval.BayIndex,
                interval.Function,
                interval.DisplayName,
                interval.IntervalKind,
                interval.GroundingStructureKind,
                interval.IntermediateNodeId,
                interval.CircuitNodeId,
                interval.EarthNodeId,
                interval.ExternalTerminalId,
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
