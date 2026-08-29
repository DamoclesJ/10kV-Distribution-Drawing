using System.IO;
using DistributionDrawing.Desktop.Viewport;
using DistributionDrawing.Desktop.Workspace;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Rendering;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class DesktopWorkspaceTests : IDisposable
{
    private readonly List<string> _paths = [];

    [Fact]
    public void WorkspaceStoresTwoSessionsAndActivatesEachWithOneNotification()
    {
        DocumentSession first = CreateSession("工程 A");
        DocumentSession second = CreateSession("工程 B");
        var workspace = new DesktopWorkspace();
        var changes = new List<ActiveDocumentSessionChangedEventArgs>();
        workspace.ActiveSessionChanged += (_, args) => changes.Add(args);

        workspace.AddSession(first);
        workspace.AddSession(second, activate: false);

        Assert.Equal([first, second], workspace.Sessions);
        DocumentSession active = Assert.IsType<DocumentSession>(workspace.ActiveSession);
        Assert.Same(first, active);
        Assert.Same(first.RuntimeSession.Scene, active.RuntimeSession.Scene);

        workspace.ActivateSession(second);

        active = Assert.IsType<DocumentSession>(workspace.ActiveSession);
        Assert.Same(second, active);
        Assert.Same(second.RuntimeSession.Scene, active.RuntimeSession.Scene);
        Assert.Equal(2, changes.Count);
        Assert.Null(changes[0].Previous);
        Assert.Same(first, changes[0].Current);
        Assert.Same(first, changes[1].Previous);
        Assert.Same(second, changes[1].Current);
    }

    [Fact]
    public void CommandHistoryDirtyAndSavePointsAreIsolatedPerSession()
    {
        DocumentSession first = CreateSession("工程 A");
        DocumentSession second = CreateSession("工程 B");
        var firstCommand1 = new CounterCommand();
        var firstCommand2 = new CounterCommand();
        var secondCommand = new CounterCommand();

        first.RuntimeSession.CommandStack.ExecuteCommand(firstCommand1);
        first.RuntimeSession.CommandStack.ExecuteCommand(firstCommand2);
        second.RuntimeSession.CommandStack.ExecuteCommand(secondCommand);

        Assert.True(first.IsDirty);
        Assert.True(second.IsDirty);
        Assert.True(first.RuntimeSession.CommandStack.Undo());
        Assert.Equal(1, firstCommand1.Value);
        Assert.Equal(0, firstCommand2.Value);
        Assert.Equal(1, secondCommand.Value);

        first.RuntimeSession.CommandStack.MarkSaved();

        Assert.False(first.IsDirty);
        Assert.True(second.IsDirty);
        Assert.True(second.RuntimeSession.CommandStack.Undo());
        Assert.Equal(0, secondCommand.Value);
        Assert.False(second.IsDirty);
    }

    [Fact]
    public void SelectionAndSceneRemainOwnedByTheirRuntimeSession()
    {
        DocumentSession first = CreateSession("工程 A");
        DocumentSession second = CreateSession("工程 B");
        var firstSelection = new SelectionReference(
            SelectionTargetKind.Device,
            Guid.NewGuid());
        var firstSecondarySelection = new SelectionReference(
            SelectionTargetKind.Connection,
            Guid.NewGuid());
        var secondSelection = new SelectionReference(
            SelectionTargetKind.RingCabinet,
            Guid.NewGuid());

        first.RuntimeSession.SelectionManager.Replace(
            [firstSelection, firstSecondarySelection]);
        second.RuntimeSession.SelectionManager.Select(secondSelection);

        Assert.Equal(
            [firstSelection, firstSecondarySelection],
            first.RuntimeSession.SelectionManager.SelectionSet.SelectedReferences);
        Assert.Equal(firstSecondarySelection, first.RuntimeSession.SelectionManager.Selected);
        Assert.Equal(secondSelection, second.RuntimeSession.SelectionManager.Selected);
        Assert.Single(second.RuntimeSession.SelectionManager.SelectionSet.SelectedReferences);
        Assert.NotSame(first.RuntimeSession.Scene, second.RuntimeSession.Scene);
        Assert.NotSame(
            first.RuntimeSession.SelectionManager,
            second.RuntimeSession.SelectionManager);
    }

    [Fact]
    public void FileTitleAndViewStateAreIndependentPerSession()
    {
        DocumentSession first = CreateSession("工程 A");
        DocumentSession second = CreateSession("工程 B");
        var firstView = new DocumentViewState(1.5, 120, -30);
        var secondView = new DocumentViewState(0.75, -45, 88);

        first.UpdateViewState(firstView);
        second.UpdateViewState(secondView);

        Assert.NotEqual(first.FilePath, second.FilePath);
        Assert.Equal("工程 A", first.DisplayTitle);
        Assert.Equal("工程 B", second.DisplayTitle);
        Assert.Equal(firstView, first.ViewState);
        Assert.Equal(secondView, second.ViewState);
    }

    [Fact]
    public void DirtyNotificationsCoverExecuteUndoRedoAndMarkSaved()
    {
        DocumentSession session = CreateSession("通知工程");
        int stackStateChanges = 0;
        int stackDirtyChanges = 0;
        int documentDirtyChanges = 0;
        session.RuntimeSession.CommandStack.StateChanged += (_, _) => stackStateChanges++;
        session.RuntimeSession.CommandStack.DirtyChanged += (_, _) => stackDirtyChanges++;
        session.DirtyChanged += (_, _) => documentDirtyChanges++;

        session.RuntimeSession.CommandStack.ExecuteCommand(new CounterCommand());
        Assert.True(session.RuntimeSession.CommandStack.Undo());
        Assert.True(session.RuntimeSession.CommandStack.Redo());
        session.RuntimeSession.CommandStack.MarkSaved();

        Assert.Equal(4, stackStateChanges);
        Assert.Equal(4, stackDirtyChanges);
        Assert.Equal(4, documentDirtyChanges);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void RemoveInactiveActiveAndLastSessionSelectsDeterministicFallback()
    {
        DocumentSession first = CreateSession("工程 A");
        DocumentSession second = CreateSession("工程 B");
        DocumentSession third = CreateSession("工程 C");
        var workspace = new DesktopWorkspace();
        workspace.AddSession(first);
        workspace.AddSession(second, activate: false);
        workspace.AddSession(third, activate: false);

        Assert.True(workspace.RemoveSession(second));
        Assert.Same(first, workspace.ActiveSession);
        Assert.Equal([first, third], workspace.Sessions);

        Assert.True(workspace.RemoveSession(first));
        Assert.Same(third, workspace.ActiveSession);
        Assert.Single(workspace.Sessions);

        Assert.True(workspace.RemoveSession(third));
        Assert.Null(workspace.ActiveSession);
        Assert.Empty(workspace.Sessions);
    }

    [Fact]
    public void CanonicalPathLookupIsFullPathAndCaseInsensitive()
    {
        DocumentSession session = CreateSession("路径工程");
        var workspace = new DesktopWorkspace();
        workspace.AddSession(session);
        string alternate = Path.GetFullPath(session.FilePath).ToUpperInvariant();

        Assert.Same(session, workspace.FindByCanonicalPath(alternate));
        Assert.Null(workspace.FindByCanonicalPath(NextPath()));
    }

    [Fact]
    public void ViewportStateCanBeCapturedAndRestoredWithoutChangingDocumentState()
    {
        DocumentSession session = CreateSession("视图工程");
        var viewport = new CanvasViewportController();
        var expected = new DocumentViewState(1.5, 90, -25);

        viewport.RestoreState(expected);
        session.UpdateViewState(viewport.CaptureState());
        viewport.Reset();
        viewport.RestoreState(session.ViewState);

        Assert.Equal(expected, viewport.CaptureState());
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void SaveAsUpdatesOnlyTheOwningDocumentSessionPath()
    {
        DocumentSession first = CreateSession("工程 A");
        DocumentSession second = CreateSession("工程 B");
        string originalSecondPath = second.FilePath;
        string newPath = NextPath();

        ProjectSession saved = first.ProjectService.SaveProjectAs(
            newPath,
            first.RuntimeSession.PersistenceSession.Layout);
        first.RuntimeSession.AcceptSavedSession(saved);

        Assert.Equal(Path.GetFullPath(newPath), first.FilePath);
        Assert.Equal("工程 A", first.DisplayTitle);
        Assert.Equal(originalSecondPath, second.FilePath);
    }

    [Fact]
    public void ActivateSessionsCanCaptureAndRestoreIndependentViewStates()
    {
        DocumentSession first = CreateSession("工程 A");
        DocumentSession second = CreateSession("工程 B");
        var firstView = new DocumentViewState(1.5, 80, -20);
        var secondView = new DocumentViewState(0.75, -40, 60);
        first.UpdateViewState(firstView);
        second.UpdateViewState(secondView);
        var workspace = new DesktopWorkspace();
        var viewport = new CanvasViewportController();
        workspace.ActiveSessionChanging += (_, args) =>
        {
            args.Previous?.UpdateViewState(viewport.CaptureState());
        };
        workspace.ActiveSessionChanged += (_, args) =>
        {
            viewport.RestoreState(args.Current?.ViewState ?? DocumentViewState.Default);
        };

        workspace.AddSession(first);
        workspace.AddSession(second, activate: false);
        Assert.Equal(firstView, viewport.CaptureState());

        workspace.ActivateSession(second);
        Assert.Equal(secondView, viewport.CaptureState());

        viewport.RestoreState(new DocumentViewState(0.9, -12, 33));
        workspace.ActivateSession(first);
        Assert.Equal(firstView, viewport.CaptureState());
        Assert.Equal(new DocumentViewState(0.9, -12, 33), second.ViewState);
    }

    private DocumentSession CreateSession(string title)
    {
        string path = NextPath();
        var service = new ProjectService();
        ProjectSession persistence = service.CreateProject(path, title);
        return new DocumentSession(
            service,
            ProjectRuntimeSession.CreateEmpty(
                persistence,
                new DrawingSceneBuilder()));
    }

    private string NextPath()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"distribution-drawing-workspace-{Guid.NewGuid():N}.kvdrawing");
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

    private sealed class CounterCommand : ICommand
    {
        public int Value { get; private set; }

        public void Execute() => Value++;

        public void Undo() => Value--;

        public void Redo() => Value++;
    }
}
