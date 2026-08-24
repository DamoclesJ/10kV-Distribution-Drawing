using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Domain.Tests;

public sealed class SwitchStateOperationTests
{
    [Fact]
    public void ChangeSwitchState_PoleSwitchReturnsExplicitChange()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Switch state test");
        SwitchDevice switchDevice = AddPoleSwitch(document, SwitchState.Open);

        SwitchStateChangeResult result = document.ChangeSwitchState(
            switchDevice.Id,
            SwitchState.Closed);

        Assert.Same(switchDevice, result.SwitchDevice);
        Assert.Equal(SwitchState.Open, result.PreviousState);
        Assert.Equal(SwitchState.Closed, result.CurrentState);
        Assert.Equal(SwitchState.Closed, switchDevice.SwitchState);
    }

    [Fact]
    public void ChangeSwitchState_CabinetSwitchUsesAssemblyInterlock()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Cabinet switch state test");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "测试环网柜",
            [
                CreateLoadSwitchInterval(1),
                CreateLoadSwitchInterval(2),
                CreateLoadSwitchInterval(3)
            ]));
        document.AddDevice(cabinet);
        RingCabinetInterval interval = cabinet.Intervals[0];
        SwitchDevice loadSwitch = interval.SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.LoadSwitch);
        SwitchDevice groundSwitch = interval.SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.GroundSwitch);

        document.ChangeSwitchState(loadSwitch.Id, SwitchState.Closed);

        Assert.Throws<InvalidOperationException>(() =>
            document.ChangeSwitchState(groundSwitch.Id, SwitchState.Closed));
        Assert.Equal(SwitchState.Closed, loadSwitch.SwitchState);
        Assert.Equal(SwitchState.Open, groundSwitch.SwitchState);
    }

    [Fact]
    public void ChangeSwitchState_AfterIntervalTypeChangeUsesCurrentAggregateSwitch()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Interval type change switch test");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "测试环网柜",
            [
                CreateLoadSwitchInterval(1),
                CreateLoadSwitchInterval(2),
                CreateLoadSwitchInterval(3)
            ]));
        document.AddDevice(cabinet);
        Guid intervalId = cabinet.Intervals[0].IntervalId;

        cabinet.ChangeIntervalType(
            intervalId,
            IntervalKind.IntegratedFeederInterval,
            GroundingStructureKind.UpperLowerGrounding);
        SwitchDevice circuitBreaker = cabinet.Intervals[0].SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.CircuitBreaker);

        SwitchStateChangeResult result = document.ChangeSwitchState(
            circuitBreaker.Id,
            SwitchState.Closed);

        Assert.Same(circuitBreaker, result.SwitchDevice);
        Assert.Equal(SwitchState.Open, result.PreviousState);
        Assert.Equal(SwitchState.Closed, circuitBreaker.SwitchState);
    }

    private static SwitchDevice AddPoleSwitch(
        DrawingDocument document,
        SwitchState initialState)
    {
        var pole = new Pole(Guid.NewGuid(), "P-001");
        document.AddDevice(pole);
        Guid firstTerminalId = Guid.NewGuid();
        Guid secondTerminalId = Guid.NewGuid();
        SwitchDevice switchDevice = SwitchDevice.CreateForPole(
            Guid.NewGuid(),
            SwitchKind.LoadSwitch,
            firstTerminalId,
            secondTerminalId,
            initialState);
        var firstTerminal = new Terminal(
            firstTerminalId,
            TopologyOwnerType.Device,
            switchDevice.Id,
            "SwitchTerminal1",
            "10kV",
            true,
            false,
            allowedConnectionTypes: [ConnectionType.OverheadLine]);
        var secondTerminal = new Terminal(
            secondTerminalId,
            TopologyOwnerType.Device,
            switchDevice.Id,
            "SwitchTerminal2",
            "10kV",
            true,
            false,
            allowedConnectionTypes: [ConnectionType.OverheadLine]);
        var attachment = new PoleAttachment(
            Guid.NewGuid(),
            pole.Id,
            switchDevice.Id);

        document.AddPoleSwitchAttachment(
            switchDevice,
            firstTerminal,
            secondTerminal,
            attachment);
        return switchDevice;
    }

    private static RingCabinetIntervalDefinition CreateLoadSwitchInterval(int bayIndex)
    {
        return RingCabinetIntervalDefinition.CreateLoadSwitch(
            bayIndex,
            SwitchState.Open,
            SwitchState.Open,
            $"负{bayIndex}间隔");
    }
}
