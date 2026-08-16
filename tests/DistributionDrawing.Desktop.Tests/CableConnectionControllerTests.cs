using System.IO;
using DistributionDrawing.Desktop.CableConnection;
using DistributionDrawing.Desktop.Workspace;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class CableConnectionControllerTests
{
    [Fact]
    public void PickAndComplete_CreatesCableConnectionAndSelectsCable()
    {
        using TestProject project = CreateProject();
        TerminalAnchorIndex anchors = CreateAnchors(project);
        Guid startTerminalId = project.Cabinet.Intervals[0].ExternalTerminalId;
        Guid endTerminalId = project.CableTerminationCableSideTerminalId;
        var controller = new CableConnectionController(() => project.Session);

        controller.Begin();
        controller.Pick(anchors.PositionOf(startTerminalId), 8);
        controller.Pick(anchors.PositionOf(endTerminalId), 8);
        controller.Complete("YJV22-8.7/15kV", 120);

        CableSegment cable = Assert.Single(project.Document.CableSegments);
        Connection connection = Assert.Single(project.Document.Connections);
        Assert.Equal(startTerminalId, cable.StartTerminalId);
        Assert.Equal(endTerminalId, cable.EndTerminalId);
        Assert.Equal(connection.Id, cable.ConnectionId);
        Assert.Equal("YJV22-8.7/15kV", cable.CableType);
        Assert.Equal(120, cable.Length);
        Assert.Equal(
            new SelectionReference(SelectionTargetKind.CableSegment, cable.Id),
            project.Session.SelectionManager.Selected);
        Assert.Equal(CableConnectionToolState.Idle, controller.State);
    }

    [Fact]
    public void Complete_UndoAndRedoRestoreCableAndConnectionWithSameIds()
    {
        using TestProject project = CreateProject();
        TerminalAnchorIndex anchors = CreateAnchors(project);
        var controller = new CableConnectionController(() => project.Session);
        controller.Begin();
        controller.Pick(
            anchors.PositionOf(project.Cabinet.Intervals[0].ExternalTerminalId),
            8);
        controller.Pick(
            anchors.PositionOf(project.CableTerminationCableSideTerminalId),
            8);
        controller.Complete("YJV22", 80);

        CableSegment cable = Assert.Single(project.Document.CableSegments);
        Guid cableId = cable.Id;
        Guid connectionId = cable.ConnectionId;
        Assert.True(project.Session.CommandStack.Undo());
        Assert.Empty(project.Document.CableSegments);
        Assert.Empty(project.Document.Connections);

        Assert.True(project.Session.CommandStack.Redo());
        CableSegment restored = Assert.Single(project.Document.CableSegments);
        Assert.Equal(cableId, restored.Id);
        Assert.Equal(connectionId, restored.ConnectionId);
        Assert.Single(project.Document.Connections);
    }

    [Fact]
    public void OverheadSideTerminal_IsNotAValidCableEndpoint()
    {
        using TestProject project = CreateProject();
        TerminalAnchorIndex anchors = CreateAnchors(project);
        var controller = new CableConnectionController(() => project.Session);

        controller.Begin();
        controller.Pick(
            anchors.PositionOf(project.Cabinet.Intervals[0].ExternalTerminalId),
            8);

        Assert.Throws<InvalidOperationException>(() =>
            controller.Pick(anchors.PositionOf(project.CableTerminationOverheadSideTerminalId), 8));
        Assert.Empty(project.Document.CableSegments);
        Assert.Empty(project.Document.Connections);
    }

    [Fact]
    public void SaveAndOpen_PreservesCableTopologyAndParameters()
    {
        using TestProject project = CreateProject();
        TerminalAnchorIndex anchors = CreateAnchors(project);
        var controller = new CableConnectionController(() => project.Session);
        controller.Begin();
        controller.Pick(
            anchors.PositionOf(project.Cabinet.Intervals[0].ExternalTerminalId),
            8);
        controller.Pick(
            anchors.PositionOf(project.CableTerminationCableSideTerminalId),
            8);
        controller.Complete("YJV22", 65);
        CableSegment cable = Assert.Single(project.Document.CableSegments);

        Assert.True(project.Workspace.SaveProject());
        var dialogs = new TestDialogs { OpenPath = project.FilePath };
        var reopenedWorkspace = new ProjectWorkspaceController(
            dialogs,
            new DrawingSceneBuilder());
        Assert.True(reopenedWorkspace.OpenProject());
        ProjectRuntimeSession reopened = reopenedWorkspace.CurrentSession!;
        CableSegment restored = Assert.Single(reopened.PersistenceSession.Domain.CableSegments);
        Connection restoredConnection = Assert.Single(
            reopened.PersistenceSession.Domain.Connections);
        Assert.Equal(cable.Id, restored.Id);
        Assert.Equal(cable.StartTerminalId, restored.StartTerminalId);
        Assert.Equal(cable.EndTerminalId, restored.EndTerminalId);
        Assert.Equal(cable.ConnectionId, restoredConnection.Id);
        Assert.Equal(cable.CableType, restored.CableType);
        Assert.Equal(cable.Length, restored.Length);
        Assert.Equal(cable.StartTerminalId, restoredConnection.StartTerminalId);
        Assert.Equal(cable.EndTerminalId, restoredConnection.EndTerminalId);
    }

    private static TestProject CreateProject()
    {
        var dialogs = new TestDialogs
        {
            NewRequest = new NewProjectRequest(
                Path.Combine(Path.GetTempPath(), $"cable-connection-{Guid.NewGuid():N}.kvdrawing"),
                "电缆连接测试",
                null)
        };
        var workspace = new ProjectWorkspaceController(dialogs, new DrawingSceneBuilder());
        Assert.True(workspace.NewProject());
        ProjectRuntimeSession session = workspace.CurrentSession!;
        var document = session.PersistenceSession.Domain;
        var factory = new DeviceCommandFactory();
        AddRingCabinetCommand cabinetCommand = factory.CreateAddRingCabinet(
            document,
            session.Layout,
            new RingCabinetCreationConfiguration(
                "环网柜",
                Enumerable.Range(1, 3).Select(index =>
                    new RingCabinetIntervalCreationConfiguration(
                        index,
                        $"间隔 {index}",
                        IntervalKind.LoadSwitchInterval,
                        null))),
            new DocumentPoint(40, 40));
        cabinetCommand.Execute();
        AddPoleCommand poleCommand = factory.CreateAddPole(
            document,
            session.Layout,
            new DocumentPoint(220, 40));
        poleCommand.Execute();
        AddCableTerminationAttachmentCommand terminationCommand =
            factory.CreateAddCableTerminationAttachment(
                document,
                session.Layout,
                poleCommand.Pole.Id,
                "电缆终端",
                new DocumentPoint(10, 20));
        terminationCommand.Execute();
        session.RebuildScene();
        RingCabinet cabinet = Assert.Single(document.Devices.OfType<RingCabinet>());
        CableTermination termination = Assert.Single(
            document.Devices.OfType<CableTermination>());
        return new TestProject(
            workspace,
            session,
            cabinet,
            termination.CableSideTerminalId,
            termination.OverheadSideTerminalId);
    }

    private static TerminalAnchorIndex CreateAnchors(TestProject project)
    {
        return TerminalAnchorIndex.Build(
            project.Document,
            project.Session.Layout.DrawingLayout,
            project.Session.Layout.RingCabinetLayouts);
    }

    private sealed class TestProject : IDisposable
    {
        private readonly ProjectWorkspaceController _workspace;

        public TestProject(
            ProjectWorkspaceController workspace,
            ProjectRuntimeSession session,
            RingCabinet cabinet,
            Guid cableTerminationCableSideTerminalId,
            Guid cableTerminationOverheadSideTerminalId)
        {
            _workspace = workspace;
            Session = session;
            Cabinet = cabinet;
            CableTerminationCableSideTerminalId = cableTerminationCableSideTerminalId;
            CableTerminationOverheadSideTerminalId = cableTerminationOverheadSideTerminalId;
            FilePath = session.PersistenceSession.FilePath;
        }

        public ProjectRuntimeSession Session { get; }

        public ProjectWorkspaceController Workspace => _workspace;

        public DrawingDocument Document => Session.PersistenceSession.Domain;

        public RingCabinet Cabinet { get; }

        public Guid CableTerminationCableSideTerminalId { get; }

        public Guid CableTerminationOverheadSideTerminalId { get; }

        public string FilePath { get; }

        public void Dispose()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
    }

    private sealed class TestDialogs : IProjectWorkspaceDialogs
    {
        public NewProjectRequest? NewRequest { get; init; }

        public string? OpenPath { get; init; }

        public NewProjectRequest? RequestNewProject() => NewRequest;

        public string? ChooseOpenProject() => OpenPath;

        public string? ChooseSaveAs(string? currentFilePath) => currentFilePath;

        public DirtyDecision ConfirmDirty(string operation) => DirtyDecision.Cancel;

        public void ShowError(string title, string message)
        {
        }
    }
}

internal static class TerminalAnchorIndexTestExtensions
{
    public static DocumentPoint PositionOf(this TerminalAnchorIndex anchors, Guid terminalId)
    {
        return anchors.TryGet(terminalId, out TerminalAnchor anchor)
            ? anchor.Position
            : throw new InvalidOperationException($"Missing test terminal anchor '{terminalId}'.");
    }
}
