using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class PTIntervalApplicationTests
{
    [Fact]
    public void ApplicationCanPreparePTIntervalDefinitionWithoutReplacingSwitchDeviceKinds()
    {
        RingCabinetIntervalDefinition definition =
            RingCabinetIntervalDefinition.CreatePT(
                7,
                SwitchState.Closed,
                SwitchState.Open,
                "负7 PT间隔");

        RingCabinet cabinet = RingCabinet.Create(
            RingCabinetDefinition.Create(
                Guid.NewGuid(),
                "Application PT test cabinet",
                [definition]));
        RingCabinetInterval interval = Assert.Single(cabinet.Intervals);

        Assert.Equal(IntervalKind.PTInterval, interval.IntervalKind);
        Assert.Equal(
            [SwitchKind.IsolationSwitch, SwitchKind.GroundSwitch],
            interval.SwitchDevices.Select(device => device.SwitchKind));
        Assert.Equal(SwitchState.Closed, interval.SwitchDevices[0].SwitchState);
        Assert.Equal(SwitchState.Open, interval.SwitchDevices[1].SwitchState);
    }
}
