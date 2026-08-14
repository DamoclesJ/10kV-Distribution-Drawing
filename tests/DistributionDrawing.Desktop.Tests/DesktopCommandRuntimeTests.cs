using DistributionDrawing.Desktop.Services;
using DistributionDrawing.Desktop.ViewModels;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class DesktopCommandRuntimeTests
{
    [Fact]
    public void Commands_InvokeProvidedApplicationEntrypoints()
    {
        var calls = new List<string>();
        var viewModel = CreateViewModel(calls);

        viewModel.NewProjectCommand.Execute(null);
        viewModel.OpenProjectCommand.Execute(null);
        viewModel.SaveProjectCommand.Execute(null);
        viewModel.UndoCommand.Execute(null);
        viewModel.RedoCommand.Execute(null);
        viewModel.DeleteCommand.Execute(null);
        viewModel.CancelCommand.Execute(null);

        Assert.Equal(
            ["new", "open", "save", "undo", "redo", "delete", "cancel"],
            calls);
    }

    [Fact]
    public void UndoRedoAndDelete_ExposeCurrentCanExecuteState()
    {
        bool canUndo = false;
        bool canRedo = true;
        bool canDelete = false;
        var viewModel = new MainWindowViewModel(
            new DesktopShellService(),
            () => { },
            () => { },
            () => { },
            () => { },
            () => { },
            () => { },
            () => { },
            () => canUndo,
            () => canRedo,
            () => canDelete);

        Assert.False(viewModel.UndoCommand.CanExecute(null));
        Assert.True(viewModel.RedoCommand.CanExecute(null));
        Assert.False(viewModel.DeleteCommand.CanExecute(null));

        canUndo = true;
        canRedo = false;
        canDelete = true;
        viewModel.RefreshCommandStates();

        Assert.True(viewModel.UndoCommand.CanExecute(null));
        Assert.False(viewModel.RedoCommand.CanExecute(null));
        Assert.True(viewModel.DeleteCommand.CanExecute(null));
    }

    private static MainWindowViewModel CreateViewModel(List<string> calls)
    {
        return new MainWindowViewModel(
            new DesktopShellService(),
            () => calls.Add("new"),
            () => calls.Add("open"),
            () => calls.Add("save"),
            () => calls.Add("undo"),
            () => calls.Add("redo"),
            () => calls.Add("delete"),
            () => calls.Add("cancel"));
    }
}
