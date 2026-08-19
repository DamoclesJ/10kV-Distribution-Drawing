using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Desktop.RingCabinetCreation;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class RingCabinetCreationViewModelTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void ConventionalTemplate_AutomaticallyCreatesNamedLoadSwitchIntervals(int count)
    {
        RingCabinet cabinet = CreateCabinet(
            RingCabinetTemplateType.Conventional,
            count,
            includePT: false,
            "用户输入的环网柜");

        Assert.Equal("用户输入的环网柜", cabinet.DisplayName);
        Assert.Equal(count, cabinet.Intervals.Count);
        Assert.All(cabinet.Intervals, interval =>
            Assert.Equal(IntervalKind.LoadSwitchInterval, interval.IntervalKind));
        Assert.Equal(
            Enumerable.Range(1, count).Select(index => $"负{index}"),
            cabinet.Intervals.Select(interval => interval.DisplayName));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    public void IntegratedTemplate_AutomaticallyCreatesNamedFeederIntervals(int count)
    {
        RingCabinet cabinet = CreateCabinet(
            RingCabinetTemplateType.PrimarySecondaryIntegrated,
            count,
            includePT: false,
            "一二次融合柜");

        Assert.Equal(count, cabinet.Intervals.Count);
        Assert.All(cabinet.Intervals, interval =>
            Assert.Equal(IntervalKind.IntegratedFeederInterval, interval.IntervalKind));
        Assert.Equal(
            Enumerable.Range(1, count).Select(index => $"负{index}"),
            cabinet.Intervals.Select(interval => interval.DisplayName));
    }

    [Fact]
    public void IntegratedTemplate_CanIncludeFormalPTInterval()
    {
        RingCabinet cabinet = CreateCabinet(
            RingCabinetTemplateType.PrimarySecondaryIntegrated,
            4,
            includePT: true,
            "带 PT 融合柜");

        RingCabinetInterval pt = Assert.Single(cabinet.Intervals.Where(interval =>
            interval.IntervalKind == IntervalKind.PTInterval));
        Assert.Equal("PT", pt.DisplayName);
        Assert.Contains(pt.SwitchDevices, device => device.SwitchKind == SwitchKind.IsolationSwitch);
        Assert.Contains(pt.SwitchDevices, device => device.SwitchKind == SwitchKind.GroundSwitch);
    }

    private static RingCabinet CreateCabinet(
        RingCabinetTemplateType type,
        int count,
        bool includePT,
        string name)
    {
        var viewModel = new RingCabinetCreationViewModel
        {
            DisplayName = name,
            CabinetType = type,
            BusinessIntervalCount = count,
            IncludePTInterval = includePT
        };

        Assert.True(
            viewModel.TryCreateConfiguration(
                out RingCabinetCreationConfiguration? configuration,
                out string error),
            error);
        return new RingCabinetCreationFactory().Create(configuration!);
    }
}
