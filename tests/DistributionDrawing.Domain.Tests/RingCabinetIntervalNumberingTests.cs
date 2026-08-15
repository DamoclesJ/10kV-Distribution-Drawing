using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using Xunit;

namespace DistributionDrawing.Domain.Tests;

public sealed class RingCabinetIntervalNumberingTests
{
    [Theory]
    [InlineData(1, "-1")]
    [InlineData(3, "-3")]
    [InlineData(7, "-7")]
    public void BusinessNumber_UsesBayIndex(int bayIndex, string expected)
    {
        RingCabinetInterval interval = CreateCabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(
                bayIndex,
                SwitchState.Open,
                SwitchState.Open)).Intervals.Single(item => item.BayIndex == bayIndex);

        Assert.Equal(expected, interval.BusinessNumber);
        Assert.Equal(1, interval.Sequence);
        Assert.Equal(bayIndex, interval.BayIndex);
    }

    [Theory]
    [InlineData(GroundingStructureKind.UpperIsolationGrounding, "-3", "-3-4", "-3-47")]
    [InlineData(GroundingStructureKind.UpperLowerGrounding, "-3", "-3-4", "-3-7")]
    [InlineData(GroundingStructureKind.LowerLowerGrounding, "-3", "-3-2", "-3-7")]
    public void IntegratedFeeder_ReturnsStructureSpecificNumbers(
        GroundingStructureKind structure,
        string circuitBreakerNumber,
        string isolationNumber,
        string groundNumber)
    {
        RingCabinetInterval interval = CreateCabinet(
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                3,
                structure,
                SwitchState.Open,
                SwitchState.Open,
                SwitchState.Open)).Intervals.Single(item => item.BayIndex == 3);

        Assert.Equal("-3", interval.BusinessNumber);
        Assert.Equal(circuitBreakerNumber, NumberFor(interval, SwitchKind.CircuitBreaker));
        Assert.Equal(isolationNumber, NumberFor(interval, SwitchKind.IsolationSwitch));
        Assert.Equal(groundNumber, NumberFor(interval, SwitchKind.GroundSwitch));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    public void PT_ReturnsNumbersFromItsBayIndex(int bayIndex)
    {
        RingCabinetInterval interval = CreateCabinet(
            RingCabinetIntervalDefinition.CreatePT(
                bayIndex,
                SwitchState.Open,
                SwitchState.Open)).Intervals.Single(item => item.BayIndex == bayIndex);

        Assert.Equal($"-{bayIndex}", interval.BusinessNumber);
        Assert.Equal($"-{bayIndex}-2", NumberFor(interval, SwitchKind.IsolationSwitch));
        Assert.Equal($"-{bayIndex}-7", NumberFor(interval, SwitchKind.GroundSwitch));
    }

    [Fact]
    public void LoadSwitch_ReturnsConfirmedCableSideGroundNumber()
    {
        RingCabinetInterval interval = CreateCabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(
                5,
                SwitchState.Open,
                SwitchState.Open)).Intervals.Single(item => item.BayIndex == 5);

        Assert.Equal("-5-7", NumberFor(interval, SwitchKind.GroundSwitch));
        Assert.Null(NumberFor(interval, SwitchKind.LoadSwitch));
    }

    [Fact]
    public void ReadingNumbers_DoesNotChangeIdentityOrTopologyFacts()
    {
        RingCabinet cabinet = CreateCabinet(
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                3,
                GroundingStructureKind.UpperIsolationGrounding,
                SwitchState.Open,
                SwitchState.Open,
                SwitchState.Open));
        RingCabinetInterval interval = cabinet.Intervals.Single(item => item.BayIndex == 3);
        Guid intervalId = interval.IntervalId;
        Guid[] switchIds = interval.SwitchDevices.Select(device => device.Id).ToArray();
        Guid[] terminalIds = cabinet.Terminals.Select(terminal => terminal.Id).Order().ToArray();
        Guid[] nodeIds = cabinet.ElectricalNodes.Select(node => node.Id).Order().ToArray();
        GroundingStructureKind? structure = interval.GroundingStructureKind;

        _ = interval.BusinessNumber;
        foreach (SwitchDevice switchDevice in interval.SwitchDevices)
        {
            _ = interval.GetSwitchBusinessNumber(switchDevice.Id);
        }

        Assert.Equal(intervalId, interval.IntervalId);
        Assert.Equal(switchIds, interval.SwitchDevices.Select(device => device.Id));
        Assert.Equal(terminalIds, cabinet.Terminals.Select(terminal => terminal.Id).Order());
        Assert.Equal(nodeIds, cabinet.ElectricalNodes.Select(node => node.Id).Order());
        Assert.Equal(structure, interval.GroundingStructureKind);
    }

    private static string? NumberFor(RingCabinetInterval interval, SwitchKind kind)
    {
        SwitchDevice switchDevice = Assert.Single(
            interval.SwitchDevices,
            device => device.SwitchKind == kind);
        return interval.GetSwitchBusinessNumber(switchDevice.Id);
    }

    private static RingCabinet CreateCabinet(RingCabinetIntervalDefinition definition)
    {
        int[] otherBayIndexes = [1, 3, 5, 7];
        RingCabinetIntervalDefinition[] definitions = [
            definition,
            .. otherBayIndexes
                .Where(bayIndex => bayIndex != definition.BayIndex)
                .Take(definition.IntervalKind switch
                {
                    IntervalKind.LoadSwitchInterval => 2,
                    IntervalKind.IntegratedFeederInterval => 3,
                    _ => 0
                })
                .Select(bayIndex => definition.IntervalKind switch
                {
                    IntervalKind.LoadSwitchInterval =>
                        RingCabinetIntervalDefinition.CreateLoadSwitch(
                            bayIndex,
                            SwitchState.Open,
                            SwitchState.Open),
                    IntervalKind.IntegratedFeederInterval =>
                        RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                            bayIndex,
                            definition.GroundingStructureKind!.Value,
                            SwitchState.Open,
                            SwitchState.Open,
                            SwitchState.Open),
                    _ => throw new InvalidOperationException()
                })
        ];

        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "Numbering test cabinet",
            definitions));

        return cabinet;
    }
}
