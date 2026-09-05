using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using Xunit;

namespace DistributionDrawing.Domain.Tests;

public sealed class PTIntervalTests
{
    [Fact]
    public void CreatePT_CreatesIsolationAndGroundSwitchesWithStableIds()
    {
        RingCabinet cabinet = RingCabinet.Create(
            RingCabinetDefinition.Create(
                Guid.NewGuid(),
                "PT test cabinet",
                [RingCabinetIntervalDefinition.CreatePT(
                    7,
                    SwitchState.Closed,
                    SwitchState.Open,
                    "负7 PT间隔")]));

        RingCabinetInterval interval = Assert.Single(cabinet.Intervals);

        Assert.Equal(IntervalKind.PTInterval, interval.IntervalKind);
        Assert.Equal(
            [SwitchKind.IsolationSwitch, SwitchKind.GroundSwitch],
            interval.SwitchDevices.Select(device => device.SwitchKind));
        Assert.All(interval.SwitchDevices, device => Assert.NotEqual(Guid.Empty, device.Id));
        Assert.Equal(SwitchState.Closed, interval.SwitchDevices[0].SwitchState);
        Assert.Equal(SwitchState.Open, interval.SwitchDevices[1].SwitchState);
        Assert.Equal(CabinetCompositionKind.PTOnly, cabinet.CompositionKind);
    }

    [Fact]
    public void RestorePT_PreservesIntervalSwitchAndTopologyIds()
    {
        Guid cabinetId = Guid.NewGuid();
        RingCabinet original = RingCabinet.Create(
            RingCabinetDefinition.Create(
                cabinetId,
                "PT restore test cabinet",
                [RingCabinetIntervalDefinition.CreatePT(
                    7,
                    SwitchState.Open,
                    SwitchState.Closed)]));
        RingCabinetInterval source = Assert.Single(original.Intervals);
        RingCabinetIntervalRestoreDefinition sourceDefinition = new(
            source.IntervalId,
            source.ParentCabinetId,
            source.Sequence,
            source.BayIndex,
            source.DisplayName,
            source.IntervalKind,
            source.GroundingStructureKind,
            source.IntermediateNodeId,
            source.CircuitNodeId,
            source.EarthNodeId,
            source.CableTerminalId,
            source.SwitchAssembly.AssemblyId,
            source.SwitchDevices.Select(device => new SwitchDeviceRestoreDefinition(
                device.Id,
                device.SwitchKind,
                device.InstallationType,
                device.TerminalIds[0],
                device.TerminalIds[1],
                device.SwitchState!.Value,
                device.DisplayName!,
                device.VoltageLevel!,
                device.DispatchNumber)).ToArray());

        RingCabinet restored = RingCabinet.Restore(
            new RingCabinetRestoreDefinition(
                original.Id,
                original.DisplayName!,
                original.MainBusNodeId,
                [sourceDefinition]));
        RingCabinetInterval target = Assert.Single(restored.Intervals);

        Assert.Equal(source.IntervalId, target.IntervalId);
        Assert.Equal(source.SwitchAssembly.AssemblyId, target.SwitchAssembly.AssemblyId);
        Assert.Equal(
            source.SwitchDevices.Select(device => device.Id),
            target.SwitchDevices.Select(device => device.Id));
        Assert.Equal(
            original.Terminals.Select(terminal => terminal.Id).Order(),
            restored.Terminals.Select(terminal => terminal.Id).Order());
        Assert.Equal(
            source.SwitchDevices.Select(device => device.SwitchState),
            target.SwitchDevices.Select(device => device.SwitchState));
    }
}
