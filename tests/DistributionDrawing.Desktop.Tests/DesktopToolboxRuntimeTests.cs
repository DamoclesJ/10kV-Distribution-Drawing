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

    [Theory]
    [InlineData(DesktopToolMode.Select)]
    [InlineData(DesktopToolMode.CreatePole)]
    [InlineData(DesktopToolMode.CreateRingCabinet)]
    [InlineData(DesktopToolMode.CreateOverheadLine)]
    [InlineData(DesktopToolMode.CreateCable)]
    [InlineData(DesktopToolMode.AddCableTermination)]
    [InlineData(DesktopToolMode.AddPoleSwitch)]
    [InlineData(DesktopToolMode.AddGroundingPoint)]
    [InlineData(DesktopToolMode.AddWorkScope)]
    public void ToolboxExposesOneActiveProfessionalTool(DesktopToolMode mode)
    {
        var toolbox = new ToolboxViewModel();

        toolbox.SetSelectedMode(mode);

        bool[] states =
        [
            toolbox.IsSelectActive,
            toolbox.IsPoleActive,
            toolbox.IsRingCabinetActive,
            toolbox.IsOverheadLineActive,
            toolbox.IsCableActive,
            toolbox.IsCableTerminationActive,
            toolbox.IsPoleSwitchActive,
            toolbox.IsGroundingPointActive,
            toolbox.IsWorkScopeActive
        ];
        Assert.Single(states, active => active);
    }

    [Fact]
    public void CancelOrSessionSwitchCanRestoreSelectState()
    {
        var toolbox = new ToolboxViewModel();
        toolbox.SetSelectedMode(DesktopToolMode.CreateCable);

        toolbox.SetSelectedMode(DesktopToolMode.Select);

        Assert.True(toolbox.IsSelectActive);
        Assert.False(toolbox.IsCableActive);
    }

    [Fact]
    public void ViewModelProjectsEmptyWorkspaceAndEmptyDrawingWithoutChangingActions()
    {
        var actions = DesktopCommandRuntimeTests.CreateActions(() => null);
        var viewModel = new MainWindowViewModel(new DesktopShellService(), actions);

        Assert.True(viewModel.IsWorkspaceEmpty);
        Assert.False(viewModel.IsDrawingEmpty);

        viewModel.UpdatePresentationState(hasActiveSession: true, hasDrawingContent: false);
        Assert.False(viewModel.IsWorkspaceEmpty);
        Assert.True(viewModel.IsDrawingEmpty);

        viewModel.UpdatePresentationState(hasActiveSession: true, hasDrawingContent: true);
        Assert.False(viewModel.IsWorkspaceEmpty);
        Assert.False(viewModel.IsDrawingEmpty);
        Assert.Same(actions.New, viewModel.NewProjectCommand);
        Assert.Same(actions.Open, viewModel.OpenProjectCommand);
    }

    [Fact]
    public void FeedbackTemporarilyOverridesStatusAndRestoresLatestToolHint()
    {
        var actions = DesktopCommandRuntimeTests.CreateActions(() => null);
        var viewModel = new MainWindowViewModel(new DesktopShellService(), actions);

        viewModel.UpdateCanvasState(1, false, "选择对象");
        viewModel.ShowFeedback("  已保存  ");
        Assert.Equal("已保存", viewModel.StatusText);

        viewModel.UpdateCanvasState(1, false, "绘制电缆：请选择终点");
        Assert.Equal("已保存", viewModel.StatusText);

        viewModel.ClearFeedback();
        Assert.Equal("绘制电缆：请选择终点", viewModel.StatusText);
    }

    [Fact]
    public void ToolPalettePlacementChangesPresentationWithoutChangingActiveTool()
    {
        var actions = DesktopCommandRuntimeTests.CreateActions(() => null);
        var viewModel = new MainWindowViewModel(new DesktopShellService(), actions);
        viewModel.Toolbox.SetSelectedMode(DesktopToolMode.CreateOverheadLine);

        viewModel.SetToolPalettePlacement(ToolPalettePlacement.Top);

        Assert.True(viewModel.IsTopToolPalette);
        Assert.False(viewModel.IsLeftToolPalette);
        Assert.Equal(DesktopToolMode.CreateOverheadLine, viewModel.Toolbox.SelectedMode);
        Assert.Same(actions.CreateOverheadLine, viewModel.CreateOverheadLineCommand);

        viewModel.SetToolPalettePlacement(ToolPalettePlacement.Left);
        Assert.True(viewModel.IsLeftToolPalette);
        Assert.Equal(DesktopToolMode.CreateOverheadLine, viewModel.Toolbox.SelectedMode);
    }
}
