using DistributionDrawing.Desktop.ViewModels;
using DistributionDrawing.Desktop.Services;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class DesktopToolboxRuntimeTests
{
    [Fact]
    public void ToolboxTracksCurrentModeWithoutOwningUserActions()
    {
        var toolbox = new ToolboxViewModel();

        Assert.Equal(DesktopToolMode.Select, toolbox.SelectedMode);

        toolbox.SetSelectedMode(DesktopToolMode.CreatePole);
        Assert.Equal(DesktopToolMode.CreatePole, toolbox.SelectedMode);

        toolbox.SetSelectedMode(DesktopToolMode.CreateOverheadLine);
        Assert.Equal(DesktopToolMode.CreateOverheadLine, toolbox.SelectedMode);
    }

    [Fact]
    public void ViewModelExposesTheSameActionInstancesToEveryUiEntry()
    {
        var actions = DesktopCommandRuntimeTests.CreateActions(() => null);
        var viewModel = new MainWindowViewModel(new DesktopShellService(), actions);

        Assert.Same(actions.Save, viewModel.SaveProjectCommand);
        Assert.Same(actions.Copy, viewModel.CopyCommand);
        Assert.Same(actions.Delete, viewModel.DeleteCommand);
        Assert.Same(actions.FitDrawing, viewModel.FitDrawingCommand);
    }
}
