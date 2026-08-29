using System.IO;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;
using DistributionDrawing.Desktop.Workspace;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;
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
    public void PTTemplateCreation_IsAtomicSelectableAndRoundTripsDeterministicLayout()
    {
        using var files = new TemporaryProjectFiles();
        string path = files.Next();
        var dialogs = new TestDialogs
        {
            NewRequest = new NewProjectRequest(path, "PT 模板工程", null)
        };
        var controller = CreateController(dialogs);
        Assert.True(controller.NewProject());
        ProjectRuntimeSession session = controller.CurrentSession!;
        RingCabinetTemplate template = new RingCabinetCreationTemplateFactory().Create(
            RingCabinetTemplateType.PrimarySecondaryIntegrated,
            4,
            includePTInterval: true);
        AddRingCabinetCommand command = new DeviceCommandFactory().CreateAddRingCabinet(
            session.PersistenceSession.Domain,
            session.Layout,
            new RingCabinetCreationConfiguration("用户 PT 柜", template),
            new DocumentPoint(35, 45));
        Guid cabinetId = command.Cabinet.Id;
        Guid[] intervalIds = command.Cabinet.Intervals
            .Select(interval => interval.IntervalId)
            .ToArray();

        session.CommandStack.ExecuteCommand(command);
        session.RebuildScene();
        RingCabinetInterval pt = Assert.Single(command.Cabinet.Intervals.Where(interval =>
            interval.IntervalKind == IntervalKind.PTInterval));
        Assert.Contains(session.Scene.Elements.OfType<SceneText>(), text => text.Text == "PT");
        Assert.Contains(session.Scene.HitTestIndex.Entries, entry =>
            entry.Target.Kind == SelectionTargetKind.RingCabinetInterval &&
            entry.Target.ObjectId == pt.IntervalId);

        Assert.True(session.CommandStack.Undo());
        Assert.DoesNotContain(session.PersistenceSession.Domain.Devices, device => device.Id == cabinetId);
        Assert.False(session.Layout.RingCabinetLayouts.ContainsKey(cabinetId));
        Assert.True(session.CommandStack.Redo());
        RingCabinet redone = Assert.Single(
            session.PersistenceSession.Domain.Devices.OfType<RingCabinet>());
        Assert.Equal(intervalIds, redone.Intervals.Select(interval => interval.IntervalId));

        session.RebuildScene();
        Assert.True(controller.SaveProject());
        DocumentPoint before = session.Layout.RingCabinetLayouts[cabinetId]
            .IntervalLayouts[pt.IntervalId]
            .PTSymbolPosition!.Value;
        dialogs.OpenPath = path;
        Assert.True(controller.OpenProject());
        ProjectRuntimeSession reopened = controller.CurrentSession!;
        RingCabinet restored = Assert.Single(
            reopened.PersistenceSession.Domain.Devices.OfType<RingCabinet>());
        RingCabinetInterval restoredPT = Assert.Single(restored.Intervals.Where(interval =>
            interval.IntervalKind == IntervalKind.PTInterval));

        Assert.Equal("用户 PT 柜", restored.DisplayName);
        Assert.Equal(intervalIds, restored.Intervals.Select(interval => interval.IntervalId));
        Assert.Equal(
            before,
            reopened.Layout.RingCabinetLayouts[cabinetId]
                .IntervalLayouts[restoredPT.IntervalId]
                .PTSymbolPosition);
        Assert.Contains(reopened.Scene.Elements.OfType<SceneText>(), text => text.Text == "PT");
    }

    [Fact]
    public void PoleSwitchRotation_RoundTripPreservesOrientationIdentityAndAnchors()
    {
        using var files = new TemporaryProjectFiles();
        string path = files.Next();
        var dialogs = new TestDialogs
        {
            NewRequest = new NewProjectRequest(path, "柱上开关旋转工程", null)
        };
        var controller = CreateController(dialogs);
        Assert.True(controller.NewProject());
        ProjectRuntimeSession session = controller.CurrentSession!;
        var factory = new DeviceCommandFactory();
        AddPoleCommand addPole = factory.CreateAddPole(
            session.PersistenceSession.Domain,
            session.Layout,
            new DocumentPoint(80, 90));
        session.CommandStack.ExecuteCommand(addPole);
        AddPoleSwitchAttachmentCommand addSwitch = factory.CreateAddPoleSwitchAttachment(
            session.PersistenceSession.Domain,
            session.Layout,
            addPole.Pole.Id,
            SwitchKind.CircuitBreaker,
            PoleProfessionalGeometry.GetDefaultAttachmentOffset(
                SwitchKind.CircuitBreaker));
        session.CommandStack.ExecuteCommand(addSwitch);
        Guid attachmentId = addSwitch.Creation.Attachment.AttachmentId;
        AttachmentLayout before = session.Layout.DrawingLayout.Attachments[attachmentId];
        session.CommandStack.ExecuteCommand(new ChangeAttachmentLayoutCommand(
            session.Layout.DrawingLayout,
            before,
            before.RotateBy(1)));
        session.RebuildScene();
        SwitchDevice originalSwitch = addSwitch.Creation.SwitchDevice;
        Guid[] terminalIds = originalSwitch.TerminalIds.ToArray();
        Guid?[] nodeIds = terminalIds.Select(id => session.PersistenceSession.Domain.Terminals
            .Single(terminal => terminal.Id == id).ElectricalNodeId).ToArray();

        Assert.True(controller.SaveProject());
        dialogs.OpenPath = path;
        Assert.True(controller.OpenProject());
        ProjectRuntimeSession reopened = controller.CurrentSession!;
        SwitchDevice restoredSwitch = Assert.Single(
            reopened.PersistenceSession.Domain.Devices.OfType<SwitchDevice>());
        AttachmentLayout restoredLayout = reopened.Layout.DrawingLayout.Attachments[attachmentId];
        PoleLayout restoredPoleLayout = reopened.Layout.DrawingLayout.Poles[addPole.Pole.Id];
        PoleAttachmentGeometry restoredGeometry = PoleProfessionalGeometry.GetAttachmentGeometry(
            restoredPoleLayout,
            restoredLayout,
            SymbolLibrary.ResolveAttachmentKind(restoredSwitch));
        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            reopened.PersistenceSession.Domain,
            reopened.Layout.DrawingLayout,
            reopened.Layout.RingCabinetLayouts,
            reopened.PersistenceSession.Domain.Connections,
            reopened.PersistenceSession.Domain.CableSegments);

        Assert.Equal(1, restoredLayout.RotationQuarterTurns);
        Assert.Equal(originalSwitch.Id, restoredSwitch.Id);
        Assert.Equal(terminalIds, restoredSwitch.TerminalIds);
        Assert.Equal(nodeIds, terminalIds.Select(id => reopened.PersistenceSession.Domain.Terminals
            .Single(terminal => terminal.Id == id).ElectricalNodeId));
        Assert.True(anchors.TryGet(terminalIds[0], out TerminalAnchor first));
        Assert.True(anchors.TryGet(terminalIds[1], out TerminalAnchor second));
        Assert.Equal(restoredGeometry.FirstTerminal, first.Position);
        Assert.Equal(restoredGeometry.SecondTerminal, second.Position);
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
