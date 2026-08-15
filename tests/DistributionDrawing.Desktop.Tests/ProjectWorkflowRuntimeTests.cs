using System.IO;
using DistributionDrawing.Desktop.Workspace;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Rendering;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class ProjectWorkflowRuntimeTests
{
    [Fact]
    public void NewProject_CreatesSessionAndScene()
    {
        using var files = new TemporaryProjectFiles();
        var dialogs = new TestDialogs
        {
            NewRequest = new NewProjectRequest(files.Next(), "新工程", "测试工程")
        };
        var controller = CreateController(dialogs);

        Assert.True(controller.NewProject());
        Assert.NotNull(controller.CurrentSession);
        Assert.NotNull(controller.CurrentSession!.PersistenceSession.Domain);
        Assert.NotNull(controller.CurrentSession.Scene);
        Assert.False(controller.IsDirty);
    }

    [Fact]
    public void SaveAndOpen_PersistsAndReplacesSession()
    {
        using var files = new TemporaryProjectFiles();
        string path = files.Next();
        var dialogs = new TestDialogs
        {
            NewRequest = new NewProjectRequest(path, "工作流工程", null)
        };
        var controller = CreateController(dialogs);

        Assert.True(controller.NewProject());
        ProjectRuntimeSession originalSession = controller.CurrentSession!;
        originalSession.CommandStack.ExecuteCommand(new TestCommand());
        Assert.True(controller.IsDirty);

        Assert.True(controller.SaveProject());
        Assert.False(controller.IsDirty);

        dialogs.OpenPath = path;
        Assert.True(controller.OpenProject());
        Assert.NotSame(originalSession, controller.CurrentSession);
        Assert.NotNull(controller.CurrentSession!.Scene);
        Assert.False(controller.IsDirty);
    }

    [Fact]
    public void CommandChange_MarksDirty_AndUndoReturnsToSavedState()
    {
        using var files = new TemporaryProjectFiles();
        var dialogs = new TestDialogs
        {
            NewRequest = new NewProjectRequest(files.Next(), "Dirty 工程", null)
        };
        var controller = CreateController(dialogs);

        Assert.True(controller.NewProject());
        ProjectRuntimeSession session = controller.CurrentSession!;
        var command = new TestCommand();

        session.CommandStack.ExecuteCommand(command);
        Assert.True(controller.IsDirty);
        Assert.True(session.CommandStack.Undo());
        Assert.False(controller.IsDirty);
    }

    [Fact]
    public void OpenFailure_PreservesCurrentSessionAndReportsError()
    {
        using var files = new TemporaryProjectFiles();
        var dialogs = new TestDialogs
        {
            NewRequest = new NewProjectRequest(files.Next(), "当前工程", null),
            OpenPath = files.Next()
        };
        var controller = CreateController(dialogs);

        Assert.True(controller.NewProject());
        ProjectRuntimeSession current = controller.CurrentSession!;

        Assert.False(controller.OpenProject());
        Assert.Same(current, controller.CurrentSession);
        Assert.NotEmpty(dialogs.Errors);
    }

    private static ProjectWorkspaceController CreateController(TestDialogs dialogs)
    {
        return new ProjectWorkspaceController(dialogs, new DrawingSceneBuilder());
    }

    private sealed class TestDialogs : IProjectWorkspaceDialogs
    {
        public NewProjectRequest? NewRequest { get; init; }

        public string? OpenPath { get; set; }

        public List<string> Errors { get; } = [];

        public NewProjectRequest? RequestNewProject() => NewRequest;

        public string? ChooseOpenProject() => OpenPath;

        public string? ChooseSaveAs(string? currentFilePath) => currentFilePath;

        public DirtyDecision ConfirmDirty(string operation) => DirtyDecision.Cancel;

        public void ShowError(string title, string message) => Errors.Add(title);
    }

    private sealed class TestCommand : ICommand
    {
        public void Execute()
        {
        }

        public void Undo()
        {
        }

        public void Redo()
        {
        }
    }

    private sealed class TemporaryProjectFiles : IDisposable
    {
        private readonly List<string> _paths = [];

        public string Next()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                $"distribution-drawing-workflow-{Guid.NewGuid():N}.kvdrawing");
            _paths.Add(path);
            return path;
        }

        public void Dispose()
        {
            foreach (string path in _paths)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
