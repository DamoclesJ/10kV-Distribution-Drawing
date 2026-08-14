using DistributionDrawing.Desktop.Services;
using DistributionDrawing.Desktop.ViewModels;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class DesktopToolboxRuntimeTests
{
    [Fact]
    public void ToolboxCommands_SelectCreateModesWithoutCreatingDomainObjects()
    {
        var calls = new List<string>();
        var viewModel = new MainWindowViewModel(
            new DesktopShellService(),
            () => { },
            () => { },
            () => { },
            () => { },
            () => { },
            () => { },
            () => { },
            selectMode: () => calls.Add("select"),
            createRingCabinetMode: () => calls.Add("ring-cabinet"),
            createPoleMode: () => calls.Add("pole"));

        viewModel.Toolbox.SelectModeCommand.Execute(null);
        Assert.Equal(DesktopToolMode.Select, viewModel.Toolbox.SelectedMode);

        viewModel.Toolbox.CreatePoleModeCommand.Execute(null);
        Assert.Equal(DesktopToolMode.CreatePole, viewModel.Toolbox.SelectedMode);

        viewModel.Toolbox.CreateRingCabinetModeCommand.Execute(null);
        Assert.Equal(DesktopToolMode.CreateRingCabinet, viewModel.Toolbox.SelectedMode);
        Assert.Equal(["select", "pole", "ring-cabinet"], calls);
    }
}
