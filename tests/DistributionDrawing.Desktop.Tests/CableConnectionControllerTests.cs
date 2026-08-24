using System.IO;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;
using DistributionDrawing.Desktop.CableConnection;
using DistributionDrawing.Desktop.CableTerminationCreation;
using DistributionDrawing.Desktop.ConnectionEditing;
using DistributionDrawing.Desktop.DrawingTools;
using DistributionDrawing.Desktop.Placement;
using DistributionDrawing.Desktop.PoleSwitchCreation;
using DistributionDrawing.Desktop.Workspace;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class CableConnectionControllerTests
{
    [Fact]
    public void BeginPole_CancelsPartiallyPickedCableBeforeCanvasPlacement()
    {
        using TestProject project = CreateProject();
        TerminalAnchorIndex anchors = CreateAnchors(project);
        var placement = new PlacementController(() => project.Session);
        var overheadLine = new OverheadLineConnectionController(() => project.Session);
        var cableTermination = new CableTerminationAttachmentController(() => project.Session);
        var cable = new CableConnectionController(() => project.Session);
        var reconnect = new CableReconnectController(() => project.Session);
        var poleSwitch = new PoleSwitchAttachmentController(() => project.Session);
        var coordinator = new DrawingToolCoordinator(
            placement,
            overheadLine,
            cableTermination,
            cable,
            reconnect,
            poleSwitch);
        coordinator.BeginCable();
        coordinator.HandleClick(
            anchors.PositionOf(project.Cabinet.Intervals[0].ExternalTerminalId),
            8);
        Assert.Equal(CableConnectionToolState.PickingEndTerminal, cable.State);
        int poleCount = project.Document.Devices.OfType<Pole>().Count();

        coordinator.BeginPole();
        bool handled = coordinator.HandleClick(new DocumentPoint(300, 180), 8);

        Assert.True(handled);
        Assert.Equal(CableConnectionToolState.Idle, cable.State);
        Assert.Equal(PlacementMode.Idle, placement.Mode);
        Assert.Equal(poleCount + 1, project.Document.Devices.OfType<Pole>().Count());
        Assert.Empty(project.Document.CableSegments);
    }

    [Fact]
    public void BeginRingCabinet_CancelsPartiallyPickedCableBeforeCanvasPlacement()
    {
        using TestProject project = CreateProject();
        TerminalAnchorIndex anchors = CreateAnchors(project);
        var placement = new PlacementController(() => project.Session);
        var overheadLine = new OverheadLineConnectionController(() => project.Session);
        var cableTermination = new CableTerminationAttachmentController(() => project.Session);
        var cable = new CableConnectionController(() => project.Session);
        var reconnect = new CableReconnectController(() => project.Session);
        var poleSwitch = new PoleSwitchAttachmentController(() => project.Session);
        var coordinator = new DrawingToolCoordinator(
            placement,
            overheadLine,
            cableTermination,
            cable,
            reconnect,
            poleSwitch);
        coordinator.BeginCable();
        coordinator.HandleClick(
            anchors.PositionOf(project.Cabinet.Intervals[0].ExternalTerminalId),
            8);
        Assert.Equal(CableConnectionToolState.PickingEndTerminal, cable.State);

        coordinator.BeginRingCabinet(new RingCabinetCreationConfiguration(
            "新环网柜",
            new RingCabinetCreationTemplateFactory().Create(
                RingCabinetTemplateType.Conventional,
                3)));

        Assert.Equal(CableConnectionToolState.Idle, cable.State);
        Assert.Equal(PlacementMode.PlacingRingCabinet, placement.Mode);
        Assert.Empty(project.Document.CableSegments);
    }

    [Fact]
    public void CablePreview_UsesOnlyOrthogonalDashedSegments()
    {
        using TestProject project = CreateProject();
        TerminalAnchorIndex anchors = CreateAnchors(project);
        Guid startTerminalId = project.Cabinet.Intervals[0].ExternalTerminalId;
        var controller = new CableConnectionController(() => project.Session);
        controller.Begin();
        controller.Pick(anchors.PositionOf(startTerminalId), 8);
        controller.UpdatePointer(new DocumentPoint(180, 115));

        SceneLine[] preview = controller.CreatePreviewElements().OfType<SceneLine>().ToArray();

        Assert.True(preview.Length >= 2);
        Assert.All(preview, line =>
        {
            Assert.Equal(SceneStrokeStyle.Dashed, line.StrokeStyle);
            Assert.True(
                line.Start.XMillimeters == line.End.XMillimeters ||
                line.Start.YMillimeters == line.End.YMillimeters);
        });
    }

    [Fact]
    public void OverheadPreview_UsesOnlyOrthogonalSolidSegments()
    {
        using TestProject project = CreateProject();
        TerminalAnchorIndex anchors = CreateAnchors(project);
        Pole pole = Assert.Single(project.Document.Devices.OfType<Pole>());
        Guid startTerminalId = Assert.Single(pole.OverheadAnchorTerminalIds);
        var controller = new OverheadLineConnectionController(() => project.Session);
        controller.Begin();
        controller.Pick(anchors.PositionOf(startTerminalId), 8);
        controller.UpdatePointer(new DocumentPoint(170, 105));

        SceneLine[] preview = controller.CreatePreviewElements().OfType<SceneLine>().ToArray();

        Assert.True(preview.Length >= 2);
        Assert.All(preview, line =>
        {
            Assert.Equal(SceneStrokeStyle.Solid, line.StrokeStyle);
            Assert.True(
                line.Start.XMillimeters == line.End.XMillimeters ||
                line.Start.YMillimeters == line.End.YMillimeters);
        });
    }

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
    public void Complete_WhenTerminalExitIsBlocked_RollsBackCableAndCommandHistory()
    {
        using TestProject project = CreateProject();
        TerminalAnchorIndex anchors = CreateAnchors(project);
        Guid startTerminalId = project.Cabinet.Intervals[0].ExternalTerminalId;
        Guid endTerminalId = project.CableTerminationCableSideTerminalId;
        DocumentPoint start = anchors.PositionOf(startTerminalId);
        var controller = new CableConnectionController(() => project.Session);
        controller.Begin();
        controller.Pick(start, 8);
        controller.Pick(anchors.PositionOf(endTerminalId), 8);
        var factory = new DeviceCommandFactory();
        AddPoleCommand blockingPole = factory.CreateAddPole(
            project.Document,
            project.Session.Layout,
            new DocumentPoint(
                start.XMillimeters - DrawingMetrics.Default.Pole.PoleRadius,
                start.YMillimeters + 10));
        blockingPole.Execute();
        project.Session.RebuildScene();
        SelectionReference? selectionBefore = project.Session.SelectionManager.Selected;

        Assert.Throws<InvalidOperationException>(() =>
            controller.Complete("YJV22", 80));

        Assert.Empty(project.Document.CableSegments);
        Assert.Empty(project.Document.Connections);
        Assert.Empty(project.Session.CommandStack.History);
        Assert.False(project.Session.CommandStack.IsDirty);
        Assert.Equal(selectionBefore, project.Session.SelectionManager.Selected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompletedCableRoute_KeepsCabinetFiftyMillimeterStubForEitherEndpointOrder(
        bool cabinetIsEnd)
    {
        using TestProject project = CreateProject();
        TerminalAnchorIndex anchors = CreateAnchors(project);
        Guid cabinetTerminalId = project.Cabinet.Intervals[0].ExternalTerminalId;
        Guid poleTerminalId = project.CableTerminationCableSideTerminalId;
        var controller = new CableConnectionController(() => project.Session);
        controller.Begin();
        controller.Pick(
            anchors.PositionOf(cabinetIsEnd ? poleTerminalId : cabinetTerminalId),
            8);
        controller.Pick(
            anchors.PositionOf(cabinetIsEnd ? cabinetTerminalId : poleTerminalId),
            8);
        controller.Complete("YJV22", 80);

        CableSegment cable = Assert.Single(project.Document.CableSegments);
        AssertCabinetStub(project, cable, cabinetTerminalId);
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
    public void RemoveSelected_DeletesCableAndConnection_UndoRedoPreserveIds()
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
        Connection connection = Assert.Single(project.Document.Connections);
        Guid cableId = cable.Id;
        Guid connectionId = connection.Id;
        project.Session.SelectionManager.Select(
            new SelectionReference(SelectionTargetKind.CableSegment, cableId));

        bool sceneChanged = false;
        controller.VisualChanged += (_, _) => sceneChanged = true;
        controller.RemoveSelected();

        Assert.True(sceneChanged);
        Assert.Empty(project.Document.CableSegments);
        Assert.Empty(project.Document.Connections);
        Assert.Null(project.Session.SelectionManager.Selected);

        Assert.True(project.Session.CommandStack.Undo());
        CableSegment restoredCable = Assert.Single(project.Document.CableSegments);
        Connection restoredConnection = Assert.Single(project.Document.Connections);
        Assert.Same(cable, restoredCable);
        Assert.Same(connection, restoredConnection);
        Assert.Equal(cableId, restoredCable.Id);
        Assert.Equal(connectionId, restoredConnection.Id);

        Assert.True(project.Session.CommandStack.Redo());
        Assert.Empty(project.Document.CableSegments);
        Assert.Empty(project.Document.Connections);
    }

    [Fact]
    public void RemoveSelected_WhenCableDoesNotExist_PreservesSelectionAndDocument()
    {
        using TestProject project = CreateProject();
        var cable = new CableSegment(
            Guid.NewGuid(),
            "电缆",
            "YJV22",
            80,
            "10kV",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
        SelectionReference selection = new(SelectionTargetKind.CableSegment, cable.Id);
        project.Session.SelectionManager.Select(selection);
        var controller = new CableConnectionController(() => project.Session);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            controller.RemoveSelected);

        Assert.Contains("电缆", exception.Message, StringComparison.Ordinal);
        Assert.Equal(selection, project.Session.SelectionManager.Selected);
        Assert.Empty(project.Document.CableSegments);
        Assert.Empty(project.Document.Connections);
        Assert.Empty(project.Session.CommandStack.History);
    }

    [Fact]
    public void ReconnectEnd_UsesCableTerminalPickingAndPreservesStableIds()
    {
        using TestProject project = CreateProject();
        _ = CreateCable(project);
        CableSegment cable = Assert.Single(project.Document.CableSegments);
        Guid cableId = cable.Id;
        Guid connectionId = cable.ConnectionId;
        Guid originalStart = cable.StartTerminalId;
        Guid originalEnd = cable.EndTerminalId;
        Guid newEnd = project.Cabinet.Intervals[1].ExternalTerminalId;

        project.Session.SelectionManager.Select(
            new SelectionReference(SelectionTargetKind.CableSegment, cable.Id));
        var reconnect = new CableReconnectController(() => project.Session);
        reconnect.BeginEnd();
        reconnect.Pick(CreateAnchors(project).PositionOf(newEnd), 8);

        CableSegment changed = Assert.Single(project.Document.CableSegments);
        Assert.Equal(cableId, changed.Id);
        Assert.Equal(connectionId, changed.ConnectionId);
        Assert.Equal(originalStart, changed.StartTerminalId);
        Assert.Equal(newEnd, changed.EndTerminalId);
        Assert.Equal(
            new SelectionReference(SelectionTargetKind.CableSegment, cableId),
            project.Session.SelectionManager.Selected);

        Assert.True(project.Session.CommandStack.Undo());
        CableSegment undone = Assert.Single(project.Document.CableSegments);
        Assert.Equal(originalEnd, undone.EndTerminalId);
        Assert.Equal(connectionId, undone.ConnectionId);

        Assert.True(project.Session.CommandStack.Redo());
        CableSegment redone = Assert.Single(project.Document.CableSegments);
        Assert.Equal(newEnd, redone.EndTerminalId);
        Assert.Equal(connectionId, redone.ConnectionId);
    }

    [Fact]
    public void ReconnectAndDeviceMoves_KeepCabinetStubAcrossUndoRedo()
    {
        using TestProject project = CreateProject();
        _ = CreateCable(project);
        CableSegment cable = Assert.Single(project.Document.CableSegments);
        Guid cableId = cable.Id;
        Guid connectionId = cable.ConnectionId;
        Guid newStart = project.Cabinet.Intervals[1].ExternalTerminalId;
        project.Session.SelectionManager.Select(
            new SelectionReference(SelectionTargetKind.CableSegment, cable.Id));
        var reconnect = new CableReconnectController(() => project.Session);
        reconnect.BeginStart();
        reconnect.Pick(CreateAnchors(project).PositionOf(newStart), 8);
        cable = Assert.Single(project.Document.CableSegments);
        AssertCabinetStub(project, cable, newStart);

        RingCabinetLayout cabinetBefore = project.Session.Layout.RingCabinetLayouts[
            project.Cabinet.Id];
        var moveCabinet = new MoveRingCabinetCommand(
            project.Session.Layout,
            project.Cabinet.Id,
            cabinetBefore.Position,
            new DocumentPoint(
                cabinetBefore.Position.XMillimeters + 30,
                cabinetBefore.Position.YMillimeters + 20));
        project.Session.CommandStack.ExecuteCommand(moveCabinet);
        project.Session.RebuildScene();
        AssertCabinetStub(project, cable, newStart);
        Assert.True(project.Session.CommandStack.Undo());
        project.Session.RebuildScene();
        AssertCabinetStub(project, cable, newStart);
        Assert.True(project.Session.CommandStack.Redo());
        project.Session.RebuildScene();
        AssertCabinetStub(project, cable, newStart);

        PoleLayout poleBefore = Assert.Single(project.Session.Layout.DrawingLayout.Poles.Values);
        var movePole = new MoveCommand(
            project.Session.Layout.DrawingLayout,
            poleBefore,
            poleBefore.MoveTo(new DocumentPoint(
                poleBefore.Position.XMillimeters + 40,
                poleBefore.Position.YMillimeters + 25)));
        project.Session.CommandStack.ExecuteCommand(movePole);
        project.Session.RebuildScene();
        AssertCabinetStub(project, cable, newStart);
        Assert.True(project.Session.CommandStack.Undo());
        project.Session.RebuildScene();
        AssertCabinetStub(project, cable, newStart);
        Assert.True(project.Session.CommandStack.Redo());
        project.Session.RebuildScene();
        AssertCabinetStub(project, cable, newStart);

        CableSegment current = Assert.Single(project.Document.CableSegments);
        Assert.Equal(cableId, current.Id);
        Assert.Equal(connectionId, current.ConnectionId);
    }

    [Fact]
    public void ReconnectOverheadSide_IsRejectedAndPreservesSelectionAndHistory()
    {
        using TestProject project = CreateProject();
        _ = CreateCable(project);
        CableSegment cable = Assert.Single(project.Document.CableSegments);
        SelectionReference selection = new(SelectionTargetKind.CableSegment, cable.Id);
        project.Session.SelectionManager.Select(selection);
        Guid originalEnd = cable.EndTerminalId;
        int historyCount = project.Session.CommandStack.History.Count;

        var reconnect = new CableReconnectController(() => project.Session);
        reconnect.BeginEnd();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            reconnect.Pick(
                CreateAnchors(project).PositionOf(
                    project.CableTerminationOverheadSideTerminalId),
                8));

        Assert.Contains("电缆", exception.Message, StringComparison.Ordinal);
        CableSegment unchanged = Assert.Single(project.Document.CableSegments);
        Assert.Equal(originalEnd, unchanged.EndTerminalId);
        Assert.Equal(selection, project.Session.SelectionManager.Selected);
        Assert.Equal(historyCount, project.Session.CommandStack.History.Count);
        Assert.True(reconnect.IsActive);
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
        CableTermination termination = Assert.Single(
            project.Document.Devices.OfType<CableTermination>());
        PoleAttachment attachment = Assert.Single(
            project.Document.PoleAttachments,
            item => item.AttachedDeviceId == termination.Id);
        PoleLayout pole = Assert.Single(project.Session.Layout.DrawingLayout.Poles.Values);
        AttachmentLayout beforeLayout = project.Session.Layout.DrawingLayout.Attachments[
            attachment.AttachmentId];
        DocumentPoint poleCenter = PoleProfessionalGeometry.GetPoleCenter(pole);
        DocumentPoint afterOffset = PoleProfessionalGeometry.GetCableTerminationOffset(
            pole,
            beforeLayout,
            new DocumentPoint(poleCenter.XMillimeters, poleCenter.YMillimeters + 50));
        project.Session.CommandStack.ExecuteCommand(new MoveAttachmentCommand(
            project.Session.Layout.DrawingLayout,
            attachment.AttachmentId,
            beforeLayout.Offset,
            afterOffset));
        project.Session.RebuildScene();
        DocumentPoint anchorBeforeSave = CreateAnchors(project).PositionOf(
            termination.CableSideTerminalId);
        string routeBeforeSave = CableGeometryKey(project.Session.Scene, cable.Id);
        AssertCabinetStub(
            project,
            cable,
            project.Cabinet.Intervals[0].ExternalTerminalId);

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
        Assert.Equal(routeBeforeSave, CableGeometryKey(reopened.Scene, restored.Id));
        Assert.Equal(afterOffset, reopened.Layout.DrawingLayout.Attachments[
            attachment.AttachmentId].Offset);
        TerminalAnchorIndex reopenedAnchors = TerminalAnchorIndex.Build(
            reopened.PersistenceSession.Domain,
            reopened.Layout.DrawingLayout,
            reopened.Layout.RingCabinetLayouts,
            reopened.PersistenceSession.Domain.Connections,
            reopened.PersistenceSession.Domain.CableSegments);
        Assert.Equal(
            anchorBeforeSave,
            reopenedAnchors.PositionOf(termination.CableSideTerminalId));
        PoleAttachmentGeometry restoredGeometry = PoleProfessionalGeometry.GetAttachmentGeometry(
            reopened.Layout.DrawingLayout.Poles[attachment.PoleId],
            reopened.Layout.DrawingLayout.Attachments[attachment.AttachmentId],
            DistributionDrawing.Rendering.Wpf.Symbols.Library.SymbolKind.CableTermination);
        Assert.Equal(
            restoredGeometry.FirstTerminal,
            reopenedAnchors.PositionOf(termination.CableSideTerminalId));
        AssertCabinetStub(
            reopened,
            restored,
            project.Cabinet.Intervals[0].ExternalTerminalId);
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
                new RingCabinetCreationTemplateFactory().Create(
                    RingCabinetTemplateType.Conventional,
                    3)),
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
            project.Session.Layout.RingCabinetLayouts,
            project.Document.Connections,
            project.Document.CableSegments);
    }

    private static CableConnectionController CreateCable(TestProject project)
    {
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
        return controller;
    }

    private static string CableGeometryKey(DrawingScene scene, Guid cableId)
    {
        return string.Join(';', scene.Elements.OfType<SceneLine>()
            .Where(line => line.TargetId == cableId)
            .Select(line =>
                $"{line.Start.XMillimeters:R},{line.Start.YMillimeters:R}-" +
                $"{line.End.XMillimeters:R},{line.End.YMillimeters:R}"));
    }

    private static void AssertCabinetStub(
        TestProject project,
        CableSegment cable,
        Guid cabinetTerminalId) =>
        AssertCabinetStub(project.Session, cable, cabinetTerminalId);

    private static void AssertCabinetStub(
        ProjectRuntimeSession session,
        CableSegment cable,
        Guid cabinetTerminalId)
    {
        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            session.PersistenceSession.Domain,
            session.Layout.DrawingLayout,
            session.Layout.RingCabinetLayouts,
            session.PersistenceSession.Domain.Connections,
            session.PersistenceSession.Domain.CableSegments);
        Assert.True(anchors.TryGet(cabinetTerminalId, out TerminalAnchor terminalAnchor));
        DocumentPoint terminal = terminalAnchor.Position;
        SceneLine line = Assert.Single(
            session.Scene.Elements.OfType<SceneLine>(),
            candidate => candidate.TargetId == cable.Id &&
                (candidate.Start == terminal || candidate.End == terminal));
        DocumentPoint away = line.Start == terminal ? line.End : line.Start;
        double minimum = DrawingMetrics.Default.CableTermination
            .CableTerminalExitMinimumStubLength;
        TerminalAnchorDirection routeDirection = terminalAnchor.Direction;
        switch (routeDirection)
        {
            case TerminalAnchorDirection.Down:
                Assert.Equal(terminal.XMillimeters, away.XMillimeters);
                Assert.True(away.YMillimeters - terminal.YMillimeters >= minimum);
                break;
            case TerminalAnchorDirection.Up:
                Assert.Equal(terminal.XMillimeters, away.XMillimeters);
                Assert.True(terminal.YMillimeters - away.YMillimeters >= minimum);
                break;
            case TerminalAnchorDirection.Left:
                Assert.Equal(terminal.YMillimeters, away.YMillimeters);
                Assert.True(terminal.XMillimeters - away.XMillimeters >= minimum);
                break;
            case TerminalAnchorDirection.Right:
                Assert.Equal(away.YMillimeters, terminal.YMillimeters);
                Assert.True(away.XMillimeters - terminal.XMillimeters >= minimum);
                break;
            default:
                throw new Xunit.Sdk.XunitException(
                    $"Cabinet terminal direction must be explicit, was {terminalAnchor.Direction}.");
        }
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
