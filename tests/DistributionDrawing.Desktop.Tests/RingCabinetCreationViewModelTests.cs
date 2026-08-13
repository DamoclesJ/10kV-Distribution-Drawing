using DistributionDrawing.Desktop.RingCabinetCreation;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class RingCabinetCreationViewModelTests
{
    [Fact]
    public void TryCreateConfiguration_CreatesLoadSwitchCabinetWithoutFunctionInput()
    {
        var viewModel = new RingCabinetCreationViewModel
        {
            DisplayName = "手工创建环网柜"
        };
        foreach (int bayIndex in new[] { 10, 3, 8 })
        {
            viewModel.AddInterval();
            RingCabinetIntervalCreationRowViewModel row = viewModel.Intervals[^1];
            row.BayIndexText = bayIndex.ToString();
            row.DisplayName = $"负{bayIndex}间隔";
            row.IntervalKind = IntervalKind.LoadSwitchInterval;
        }

        bool success = viewModel.TryCreateConfiguration(
            out RingCabinetCreationConfiguration? configuration,
            out string errorMessage);

        Assert.True(success, errorMessage);
        RingCabinetCreationConfiguration result = Assert.IsType<
            RingCabinetCreationConfiguration>(configuration);
        Assert.Equal(new[] { 10, 3, 8 }, result.Intervals.Select(x => x.BayIndex));
        Assert.All(
            typeof(RingCabinetIntervalCreationRowViewModel).GetProperties(),
            property => Assert.NotEqual("Function", property.Name));

        RingCabinet cabinet = new RingCabinetCreationFactory().Create(result);

        Assert.Equal(new[] { 1, 2, 3 }, cabinet.Intervals.Select(x => x.Sequence));
        Assert.Equal(new[] { 10, 3, 8 }, cabinet.Intervals.Select(x => x.BayIndex));
    }
}
