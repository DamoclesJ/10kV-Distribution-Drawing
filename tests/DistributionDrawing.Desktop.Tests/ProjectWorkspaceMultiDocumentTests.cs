using System.IO;
using DistributionDrawing.Desktop.Workspace;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Rendering;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class ProjectWorkspaceMultiDocumentTests : IDisposable
{
    private readonly List<string> _paths = [];

    [Fact]
    public void NewAddsAndActivatesUntitledSessionsWithoutRemovingExisting()
    {
        var dialogs = new TestDialogs();
        dialogs.NewRequests.Enqueue(new NewProjectRequest(string.Empty, string.Empty, null));
        dialogs.NewRequests.Enqueue(new NewProjectRequest(string.Empty, string.Empty, null));
        ProjectWorkspaceController controller = CreateController(dialogs);

        Assert.True(controller.NewProject());
        DocumentSession first = controller.ActiveDocumentSession!;
        Assert.Equal("未命名 1", first.DocumentName);
        Assert.True(first.IsUntitled);

        Assert.True(controller.NewProject());
        Assert.Equal(2, controller.Workspace.Sessions.Count);
        Assert.Equal("未命名 2", controller.ActiveDocumentSession!.DocumentName);
        Assert.Contains(first, controller.Workspace.Sessions);
    }

    [Fact]
    public void OpenAddsSessionAndDuplicateCanonicalPathActivatesExisting()
    {
        string firstPath = CreateSavedProject("工程 A");
        string secondPath = CreateSavedProject("工程 B");
        var dialogs = new TestDialogs();
        dialogs.OpenPaths.Enqueue(firstPath);
        dialogs.OpenPaths.Enqueue(secondPath);
        dialogs.OpenPaths.Enqueue(firstPath.ToUpperInvariant());
        ProjectWorkspaceController controller = CreateController(dialogs);

        Assert.True(controller.OpenProject());
        DocumentSession first = controller.ActiveDocumentSession!;
        Assert.True(controller.OpenProject());
        Assert.Equal(2, controller.Workspace.Sessions.Count);
        Assert.True(controller.OpenProject());

        Assert.Same(first, controller.ActiveDocumentSession);
        Assert.Equal(2, controller.Workspace.Sessions.Count);
    }

    [Fact]
    public void SaveOnlyChangesActiveSessionAndClearsItsDirtyMarker()
    {
        var dialogs = new TestDialogs();
        dialogs.NewRequests.Enqueue(new NewProjectRequest(NextPath(), "工程 A", null));
        dialogs.NewRequests.Enqueue(new NewProjectRequest(NextPath(), "工程 B", null));
        ProjectWorkspaceController controller = CreateController(dialogs);
        Assert.True(controller.NewProject());
        DocumentSession first = controller.ActiveDocumentSession!;
        first.RuntimeSession.CommandStack.ExecuteCommand(new TestCommand());
        Assert.True(controller.NewProject());
        DocumentSession second = controller.ActiveDocumentSession!;
        second.RuntimeSession.CommandStack.ExecuteCommand(new TestCommand());

        controller.Workspace.ActivateSession(first);
        Assert.True(controller.SaveProject());

        Assert.False(first.IsDirty);
        Assert.True(second.IsDirty);
        Assert.DoesNotContain("*", first.TabTitle);
        Assert.Contains("*", second.TabTitle);
    }

    [Fact]
    public void SaveAsPersistsUntitledAndRejectsPathOwnedByAnotherSession()
    {
        string firstPath = NextPath();
        string secondPath = NextPath();
        var dialogs = new TestDialogs();
        dialogs.NewRequests.Enqueue(new NewProjectRequest(firstPath, "工程 A", null));
        dialogs.NewRequests.Enqueue(new NewProjectRequest(string.Empty, string.Empty, null));
        dialogs.SaveAsPaths.Enqueue(firstPath);
        dialogs.SaveAsPaths.Enqueue(secondPath);
        ProjectWorkspaceController controller = CreateController(dialogs);
        Assert.True(controller.NewProject());
        Assert.True(controller.NewProject());
        DocumentSession untitled = controller.ActiveDocumentSession!;

        Assert.False(controller.SaveProjectAs());
        Assert.True(untitled.IsUntitled);
        Assert.NotEmpty(dialogs.Errors);

        Assert.True(controller.SaveProjectAs());
        Assert.False(untitled.IsUntitled);
        Assert.Equal(Path.GetFullPath(secondPath), untitled.FilePath);
    }

    [Theory]
    [InlineData(DirtyDecision.Discard, true)]
    [InlineData(DirtyDecision.Cancel, false)]
    public void CloseDirtySessionHonorsDiscardAndCancel(
        DirtyDecision decision,
        bool expectedClosed)
    {
        var dialogs = new TestDialogs();
        dialogs.NewRequests.Enqueue(new NewProjectRequest(NextPath(), "关闭测试", null));
        dialogs.DirtyDecisions.Enqueue(decision);
        ProjectWorkspaceController controller = CreateController(dialogs);
        Assert.True(controller.NewProject());
        controller.CurrentSession!.CommandStack.ExecuteCommand(new TestCommand());

        Assert.Equal(expectedClosed, controller.CloseCurrentProject());
        Assert.Equal(expectedClosed ? 0 : 1, controller.Workspace.Sessions.Count);
    }

    [Fact]
    public void CloseDirtySavePersistsThenLeavesEmptyWorkspace()
    {
        var dialogs = new TestDialogs();
        dialogs.NewRequests.Enqueue(new NewProjectRequest(NextPath(), "保存关闭", null));
        dialogs.DirtyDecisions.Enqueue(DirtyDecision.Save);
        ProjectWorkspaceController controller = CreateController(dialogs);
        Assert.True(controller.NewProject());
        controller.CurrentSession!.CommandStack.ExecuteCommand(new TestCommand());

        Assert.True(controller.CloseCurrentProject());
        Assert.Empty(controller.Workspace.Sessions);
        Assert.Null(controller.ActiveDocumentSession);
    }

    [Fact]
    public void ExitChecksEveryDirtySessionAndCancelStopsExit()
    {
        var dialogs = new TestDialogs();
        dialogs.NewRequests.Enqueue(new NewProjectRequest(NextPath(), "工程 A", null));
        dialogs.NewRequests.Enqueue(new NewProjectRequest(NextPath(), "工程 B", null));
        dialogs.DirtyDecisions.Enqueue(DirtyDecision.Discard);
        dialogs.DirtyDecisions.Enqueue(DirtyDecision.Cancel);
        ProjectWorkspaceController controller = CreateController(dialogs);
        Assert.True(controller.NewProject());
        controller.CurrentSession!.CommandStack.ExecuteCommand(new TestCommand());
        Assert.True(controller.NewProject());
        controller.CurrentSession!.CommandStack.ExecuteCommand(new TestCommand());

        Assert.False(controller.CanCloseApplication());
        Assert.Equal(2, dialogs.ConfirmedDocuments.Count);
        Assert.Equal(2, controller.Workspace.Sessions.Count);
    }

    private ProjectWorkspaceController CreateController(TestDialogs dialogs) =>
        new(dialogs, new DrawingSceneBuilder());

    private string CreateSavedProject(string title)
    {
        string path = NextPath();
        var service = new ProjectService();
        ProjectSession session = service.CreateProject(path, title);
        service.SaveProject(session.Layout);
        return path;
    }

    private string NextPath()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"distribution-drawing-tabs-{Guid.NewGuid():N}.kvdrawing");
        _paths.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (string path in _paths.Where(File.Exists)) File.Delete(path);
    }

    private sealed class TestDialogs : IProjectWorkspaceDialogs
    {
        public Queue<NewProjectRequest?> NewRequests { get; } = new();
        public Queue<string?> OpenPaths { get; } = new();
        public Queue<string?> SaveAsPaths { get; } = new();
        public Queue<DirtyDecision> DirtyDecisions { get; } = new();
        public List<string> Errors { get; } = [];
        public List<string> ConfirmedDocuments { get; } = [];

        public NewProjectRequest? RequestNewProject() =>
            NewRequests.Count == 0 ? null : NewRequests.Dequeue();

        public string? ChooseOpenProject() =>
            OpenPaths.Count == 0 ? null : OpenPaths.Dequeue();

        public string? ChooseSaveAs(string? currentFilePath) =>
            SaveAsPaths.Count == 0 ? null : SaveAsPaths.Dequeue();

        public DirtyDecision ConfirmDirty(string operation) =>
            DirtyDecisions.Count == 0 ? DirtyDecision.Cancel : DirtyDecisions.Dequeue();

        public DirtyDecision ConfirmDirtyDocument(string documentName, string operation)
        {
            ConfirmedDocuments.Add(documentName);
            return ConfirmDirty(operation);
        }

        public void ShowError(string title, string message) => Errors.Add(message);
    }

    private sealed class TestCommand : ICommand
    {
        public void Execute() { }
        public void Undo() { }
        public void Redo() { }
    }
}
