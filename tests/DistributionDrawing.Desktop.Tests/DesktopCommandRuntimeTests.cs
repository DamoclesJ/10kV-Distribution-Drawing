using System.IO;
using System.Windows.Input;
using DistributionDrawing.Desktop.Actions;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Rendering;
using Xunit;
using DrawingCommand = DistributionDrawing.Rendering.Wpf.Interaction.ICommand;

namespace DistributionDrawing.Desktop.Tests;

public sealed class DesktopCommandRuntimeTests : IDisposable
{
    private readonly List<string> _paths = [];

    [Fact]
    public void DesktopAction_UsesCanExecuteAndInvokesHandler()
    {
        bool enabled = false;
        int calls = 0;
        var action = new DesktopAction(() => calls++, () => enabled);

        Assert.False(action.CanExecute(null));
        action.Execute(null);
        Assert.Equal(0, calls);

        enabled = true;
        action.Execute(null);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void DesktopActionRefreshRaisesCanExecuteChanged()
    {
        var action = new DesktopAction(() => { });
        int notifications = 0;
        action.CanExecuteChanged += (_, _) => notifications++;

        action.Refresh();

        Assert.Equal(1, notifications);
    }

    [Fact]
    public void UndoAndRedoActionsAlwaysUseCurrentActiveSession()
    {
        ProjectRuntimeSession first = CreateSession("工程 A");
        ProjectRuntimeSession second = CreateSession("工程 B");
        var firstCommand = new CounterCommand();
        var secondCommand = new CounterCommand();
        first.CommandStack.ExecuteCommand(firstCommand);
        second.CommandStack.ExecuteCommand(secondCommand);
        ProjectRuntimeSession? active = first;
        var actions = CreateActions(
            () => active,
            undo: () => active!.CommandStack.Undo(),
            redo: () => active!.CommandStack.Redo());

        actions.Undo.Execute(null);
        Assert.Equal(0, firstCommand.Value);
        Assert.Equal(1, secondCommand.Value);

        active = second;
        actions.Undo.Execute(null);
        actions.Redo.Execute(null);
        Assert.Equal(0, firstCommand.Value);
        Assert.Equal(1, secondCommand.Value);
    }

    [Fact]
    public void CopyDeleteAndPasteUseCurrentSelectionAndClipboardState()
    {
        ProjectRuntimeSession session = CreateSession("操作工程");
        int copies = 0;
        int deletes = 0;
        int pastes = 0;
        bool clipboardHasContent = false;
        var actions = CreateActions(
            () => session,
            clipboard: () => clipboardHasContent,
            copy: () => copies++,
            delete: () => deletes++,
            paste: () => pastes++);

        Assert.False(actions.Copy.CanExecute(null));
        Assert.False(actions.Delete.CanExecute(null));
        Assert.False(actions.Paste.CanExecute(null));

        session.SelectionManager.Select(new SelectionReference(
            SelectionTargetKind.Device,
            Guid.NewGuid()));
        Assert.True(actions.Copy.CanExecute(null));
        Assert.True(actions.Delete.CanExecute(null));
        actions.Copy.Execute(null);
        actions.Delete.Execute(null);

        clipboardHasContent = true;
        Assert.True(actions.Paste.CanExecute(null));
        actions.Paste.Execute(null);
        Assert.Equal((1, 1, 1), (copies, deletes, pastes));
    }

    [Fact]
    public void CancelActionUsesOneSharedCancellationHandler()
    {
        int cancellations = 0;
        DesktopUserActions actions = CreateActions(
            () => null,
            cancel: () => cancellations++);

        actions.CancelCurrentOperation.Execute(null);

        Assert.Equal(1, cancellations);
    }

    [Theory]
    [InlineData(Key.A, ModifierKeys.Control)]
    [InlineData(Key.C, ModifierKeys.Control)]
    [InlineData(Key.V, ModifierKeys.Control)]
    [InlineData(Key.Delete, ModifierKeys.None)]
    [InlineData(Key.Z, ModifierKeys.Control)]
    [InlineData(Key.Y, ModifierKeys.Control)]
    public void TextInputFocusKeepsEditingShortcutsOutOfDrawingCommands(
        Key key,
        ModifierKeys modifiers)
    {
        Assert.Equal(
            DesktopShortcutAction.None,
            DesktopShortcutPolicy.Resolve(
                key,
                modifiers,
                textInputFocused: true,
                interactionIdle: true));
    }

    [Fact]
    public void EscapeCancelsActiveDrawingEvenWhenTextInputIsFocused()
    {
        Assert.Equal(
            DesktopShortcutAction.Cancel,
            DesktopShortcutPolicy.Resolve(
                Key.Escape,
                ModifierKeys.None,
                textInputFocused: true,
                interactionIdle: false));
        Assert.Equal(
            DesktopShortcutAction.None,
            DesktopShortcutPolicy.Resolve(
                Key.Escape,
                ModifierKeys.None,
                textInputFocused: true,
                interactionIdle: true));
    }

    [Fact]
    public void SessionDependentActionsTrackCurrentWorkspaceStateWithoutRecreation()
    {
        ProjectRuntimeSession? active = null;
        DesktopUserActions actions = CreateActions(() => active);

        Assert.False(actions.Save.CanExecute(null));
        Assert.False(actions.CreatePole.CanExecute(null));
        Assert.False(actions.ExportPng.CanExecute(null));

        active = CreateSession("激活工程");

        Assert.True(actions.Save.CanExecute(null));
        Assert.True(actions.CreatePole.CanExecute(null));
        Assert.True(actions.ExportPng.CanExecute(null));

        active = null;

        Assert.False(actions.Save.CanExecute(null));
        Assert.False(actions.CreatePole.CanExecute(null));
        Assert.False(actions.ExportPng.CanExecute(null));
    }

    internal static DesktopUserActions CreateActions(
        Func<ProjectRuntimeSession?> activeSession,
        Func<bool>? clipboard = null,
        Func<bool>? idle = null,
        Func<bool>? rotate = null,
        Func<bool>? switchOperation = null,
        Func<bool>? reconnect = null,
        Action? undo = null,
        Action? redo = null,
        Action? copy = null,
        Action? paste = null,
        Action? delete = null,
        Action? cancel = null)
    {
        Action noOp = () => { };
        return new DesktopUserActions(
            new DesktopActionContext
            {
                ActiveSession = activeSession,
                HasClipboardContent = clipboard ?? (() => false),
                IsInteractionIdle = idle ?? (() => true),
                CanRotateSelection = rotate ?? (() => false),
                CanOperateSwitch = switchOperation ?? (() => false),
                CanReconnectCable = reconnect ?? (() => false),
                CanAddPoleAttachment = () => false
            },
            new DesktopUserActionHandlers
            {
                New = noOp,
                Open = noOp,
                Save = noOp,
                SaveAs = noOp,
                CloseDocument = noOp,
                Exit = noOp,
                ExportPng = noOp,
                Undo = undo ?? noOp,
                Redo = redo ?? noOp,
                Copy = copy ?? noOp,
                Paste = paste ?? noOp,
                SelectAll = noOp,
                Delete = delete ?? noOp,
                CancelCurrentOperation = cancel ?? noOp,
                Select = noOp,
                CreatePole = noOp,
                CreateRingCabinet = noOp,
                CreateOverheadLine = noOp,
                CreateCable = noOp,
                AddCableTermination = noOp,
                AddPoleSwitch = noOp,
                AddGroundingPoint = noOp,
                AddWorkScope = noOp,
                ZoomIn = noOp,
                ZoomOut = noOp,
                FitDrawing = noOp,
                ToggleGrid = noOp,
                TypographySettings = noOp,
                RotateLeft = noOp,
                RotateRight = noOp,
                SwitchOperation = noOp,
                ReconnectCableStart = noOp,
                ReconnectCableEnd = noOp
            },
            new RecordingMessageService());
    }

    private ProjectRuntimeSession CreateSession(string title)
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"distribution-drawing-actions-{Guid.NewGuid():N}.kvdrawing");
        _paths.Add(path);
        var service = new ProjectService();
        ProjectSession persistence = service.CreateProject(path, title);
        return ProjectRuntimeSession.CreateEmpty(persistence, new DrawingSceneBuilder());
    }

    public void Dispose()
    {
        foreach (string path in _paths.Where(File.Exists))
        {
            File.Delete(path);
        }
    }

    private sealed class CounterCommand : DrawingCommand
    {
        public int Value { get; private set; }
        public void Execute() => Value++;
        public void Undo() => Value--;
        public void Redo() => Value++;
    }

    private sealed class RecordingMessageService : IDesktopMessageService
    {
        public void ShowError(string title, string message) { }
        public void ShowWarning(string title, string message) { }
        public bool Confirm(string title, string message) => true;
        public DistributionDrawing.Desktop.Workspace.DirtyDecision ConfirmSaveChanges(
            string documentName) =>
            DistributionDrawing.Desktop.Workspace.DirtyDecision.Cancel;
    }
}
