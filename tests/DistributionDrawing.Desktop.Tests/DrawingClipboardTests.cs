using System.IO;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;
using DistributionDrawing.Desktop.Clipboard;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Connections;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class DrawingClipboardTests : IDisposable
{
    private readonly List<string> _paths = [];

    [Fact]
    public void EmptyClipboardAndUnsupportedSelection_FailWithoutMutation()
    {
        ProjectRuntimeSession session = CreateSession("空剪贴板");
        var clipboard = new DrawingClipboardService();

        Assert.False(clipboard.Paste(session).IsSuccess);
        session.SelectionManager.Select(new SelectionReference(
            SelectionTargetKind.GroundingPoint,
            Guid.NewGuid()));
        Assert.False(clipboard.Copy(session).IsSuccess);
        Assert.Empty(session.PersistenceSession.Domain.Devices);
        Assert.False(session.CommandStack.CanUndo);
    }

    [Fact]
    public void PolePaste_RemapsIdsOffsetsAndSelectsNewPole()
    {
        ProjectRuntimeSession session = CreateSession("杆塔复制");
        AddPoleCommand source = AddPole(session, new DocumentPoint(20, 30));
        session.SelectionManager.Select(new SelectionReference(
            SelectionTargetKind.Device,
            source.Pole.Id));
        SelectionSet beforeCopy = session.SelectionManager.SelectionSet;
        var clipboard = new DrawingClipboardService();

        Assert.True(clipboard.Copy(session).IsSuccess);
        Assert.Same(beforeCopy, session.SelectionManager.SelectionSet);
        Assert.True(clipboard.Paste(session).IsSuccess);

        Pole pasted = Assert.Single(
            session.PersistenceSession.Domain.Devices.OfType<Pole>(),
            item => item.Id != source.Pole.Id);
        Assert.NotEqual(source.Pole.Id, pasted.Id);
        Assert.DoesNotContain(
            pasted.OverheadAnchorTerminalIds,
            source.Pole.OverheadAnchorTerminalIds.Contains);
        Assert.Equal(
            new DocumentPoint(30, 40),
            session.Layout.DrawingLayout.Poles[pasted.Id].Position);
        Assert.Equal(
            new SelectionReference(SelectionTargetKind.Device, pasted.Id),
            session.SelectionManager.Selected);
    }

    [Fact]
    public void RepeatedPaste_IncrementsOffsetAndNewCopyResetsSequence()
    {
        ProjectRuntimeSession session = CreateSession("偏移序列");
        AddPoleCommand source = AddPole(session, new DocumentPoint(5, 7));
        session.SelectionManager.Select(new SelectionReference(
            SelectionTargetKind.Device,
            source.Pole.Id));
        var clipboard = new DrawingClipboardService();
        Assert.True(clipboard.Copy(session).IsSuccess);

        Assert.True(clipboard.Paste(session).IsSuccess);
        Assert.True(clipboard.Paste(session).IsSuccess);
        DocumentPoint[] positions = session.Layout.DrawingLayout.Poles.Values
            .Select(item => item.Position)
            .OrderBy(item => item.XMillimeters)
            .ToArray();
        Assert.Contains(new DocumentPoint(15, 17), positions);
        Assert.Contains(new DocumentPoint(25, 27), positions);

        session.SelectionManager.Select(new SelectionReference(
            SelectionTargetKind.Device,
            source.Pole.Id));
        Assert.True(clipboard.Copy(session).IsSuccess);
        Assert.True(clipboard.Paste(session).IsSuccess);
        Assert.Equal(2, session.Layout.DrawingLayout.Poles.Values.Count(item =>
            item.Position == new DocumentPoint(15, 17)));
    }

    [Fact]
    public void Paste_IsOneUndoUnitAndRedoPreservesMappedIdsAndSelection()
    {
        ProjectRuntimeSession session = CreateSession("撤销重做");
        AddPoleCommand first = AddPole(session, new DocumentPoint(10, 10));
        AddPoleCommand second = AddPole(session, new DocumentPoint(80, 10));
        AddOverhead(session, first, second);
        session.SelectionManager.Replace([
            new SelectionReference(SelectionTargetKind.Device, first.Pole.Id),
            new SelectionReference(SelectionTargetKind.Device, second.Pole.Id),
            new SelectionReference(SelectionTargetKind.Connection,
                session.PersistenceSession.Domain.Connections.Single().Id)
        ]);
        var clipboard = new DrawingClipboardService();
        Assert.True(clipboard.Copy(session).IsSuccess);
        int historyBefore = session.CommandStack.History.Count;

        Assert.True(clipboard.Paste(session).IsSuccess);
        Assert.Equal(historyBefore + 1, session.CommandStack.History.Count);
        Guid[] pastedPoleIds = session.SelectionManager.SelectionSet.SelectedReferences
            .Where(item => item.Kind == SelectionTargetKind.Device)
            .Select(item => item.ObjectId)
            .OrderBy(id => id)
            .ToArray();
        Guid pastedConnectionId = session.SelectionManager.SelectionSet.SelectedReferences
            .Single(item => item.Kind == SelectionTargetKind.Connection)
            .ObjectId;

        Assert.True(session.CommandStack.Undo());
        Assert.DoesNotContain(session.PersistenceSession.Domain.Devices,
            item => pastedPoleIds.Contains(item.Id));
        Assert.DoesNotContain(session.PersistenceSession.Domain.Connections,
            item => item.Id == pastedConnectionId);
        Assert.True(session.CommandStack.Redo());
        Assert.All(pastedPoleIds, id => Assert.Contains(
            session.PersistenceSession.Domain.Devices,
            item => item.Id == id));
        Assert.Contains(session.PersistenceSession.Domain.Connections,
            item => item.Id == pastedConnectionId);
    }

    [Fact]
    public void PoleAttachmentSelection_IncludesParentAndPreservesSwitchFactsAndRotation()
    {
        ProjectRuntimeSession session = CreateSession("柱上附件复制");
        AddPoleCommand pole = AddPole(session, new DocumentPoint(50, 60));
        var factory = new DeviceCommandFactory();
        AddPoleSwitchAttachmentCommand addSwitch = factory.CreateAddPoleSwitchAttachment(
            session.PersistenceSession.Domain,
            session.Layout,
            pole.Pole.Id,
            SwitchKind.DropoutFuse,
            new DocumentPoint(12, 4));
        addSwitch.Execute();
        AttachmentLayout rotated = addSwitch.Creation.Layout.RotateBy(3);
        session.Layout.DrawingLayout.Replace(rotated);
        session.PersistenceSession.Domain.ChangeSwitchState(
            addSwitch.Creation.SwitchDevice.Id,
            SwitchState.Closed);
        session.RebuildScene();
        session.SelectionManager.Select(new SelectionReference(
            SelectionTargetKind.PoleAttachment,
            addSwitch.Creation.Attachment.AttachmentId));
        var clipboard = new DrawingClipboardService();

        Assert.True(clipboard.Copy(session).IsSuccess);
        Assert.True(clipboard.Paste(session).IsSuccess);

        PoleAttachment copiedAttachment = Assert.Single(
            session.PersistenceSession.Domain.PoleAttachments,
            item => item.AttachmentId != addSwitch.Creation.Attachment.AttachmentId);
        SwitchDevice copiedSwitch = Assert.IsType<SwitchDevice>(
            session.PersistenceSession.Domain.Devices.Single(item =>
                item.Id == copiedAttachment.AttachedDeviceId));
        Assert.NotEqual(pole.Pole.Id, copiedAttachment.PoleId);
        Assert.Equal(SwitchKind.DropoutFuse, copiedSwitch.SwitchKind);
        Assert.Equal(SwitchState.Closed, copiedSwitch.SwitchState);
        Assert.Equal(3, session.Layout.DrawingLayout.Attachments[
            copiedAttachment.AttachmentId].RotationQuarterTurns);
        Assert.Contains(session.SelectionManager.SelectionSet.SelectedReferences,
            item => item.Kind == SelectionTargetKind.Device &&
                    item.ObjectId == copiedAttachment.PoleId);
    }

    [Fact]
    public void RingCabinetPaste_ClonesWholeAggregateWithStableFactsAndFreshIds()
    {
        ProjectRuntimeSession session = CreateSession("环网柜复制");
        var factory = new DeviceCommandFactory();
        AddRingCabinetCommand command = factory.CreateAddRingCabinet(
            session.PersistenceSession.Domain,
            session.Layout,
            new RingCabinetCreationConfiguration(
                "NK-复制",
                new RingCabinetCreationTemplateFactory().Create(
                    RingCabinetTemplateType.PrimarySecondaryIntegrated,
                    4,
                    includePTInterval: true),
                "10kV测试线路"),
            new DocumentPoint(30, 40));
        command.Execute();
        SwitchDevice sourceSwitch = command.Cabinet.Intervals[0].SwitchDevices[0];
        session.PersistenceSession.Domain.ChangeSwitchState(sourceSwitch.Id, SwitchState.Closed);
        session.RebuildScene();
        session.SelectionManager.Select(new SelectionReference(
            SelectionTargetKind.Device,
            sourceSwitch.Id));
        var clipboard = new DrawingClipboardService();

        Assert.True(clipboard.Copy(session).IsSuccess);
        Assert.True(clipboard.Paste(session).IsSuccess);

        RingCabinet copied = Assert.Single(
            session.PersistenceSession.Domain.Devices.OfType<RingCabinet>(),
            item => item.Id != command.Cabinet.Id);
        Assert.Equal(command.Cabinet.DisplayName, copied.DisplayName);
        Assert.Equal(command.Cabinet.LineName, copied.LineName);
        Assert.Equal(command.Cabinet.Intervals.Count, copied.Intervals.Count);
        Assert.Equal(SwitchState.Closed, copied.Intervals[0].SwitchDevices[0].SwitchState);
        Assert.Empty(command.Cabinet.Intervals.Select(item => item.IntervalId)
            .Intersect(copied.Intervals.Select(item => item.IntervalId)));
        Assert.Equal(
            new DocumentPoint(40, 50),
            session.Layout.RingCabinetLayouts[copied.Id].Position);
    }

    [Fact]
    public void BoundaryConnection_IsOmittedAndNeverReferencesSourceIds()
    {
        ProjectRuntimeSession session = CreateSession("边界线路");
        AddPoleCommand first = AddPole(session, new DocumentPoint(10, 10));
        AddPoleCommand second = AddPole(session, new DocumentPoint(90, 10));
        AddOverhead(session, first, second);
        Connection sourceConnection = session.PersistenceSession.Domain.Connections.Single();
        session.SelectionManager.Replace([
            new SelectionReference(SelectionTargetKind.Device, first.Pole.Id),
            new SelectionReference(SelectionTargetKind.Connection, sourceConnection.Id)
        ]);
        var clipboard = new DrawingClipboardService();

        Assert.True(clipboard.Copy(session).IsSuccess);
        Assert.True(clipboard.Paste(session).IsSuccess);

        Assert.Single(session.PersistenceSession.Domain.Connections);
        Pole copiedPole = session.PersistenceSession.Domain.Devices.OfType<Pole>()
            .Single(item => item.Id != first.Pole.Id && item.Id != second.Pole.Id);
        Assert.All(session.PersistenceSession.Domain.Connections, connection =>
            Assert.DoesNotContain(copiedPole.OverheadAnchorTerminalIds, connection.UsesTerminal));
    }

    [Fact]
    public void CrossSessionPaste_UsesFrozenFragmentAfterSourceChanges()
    {
        ProjectRuntimeSession source = CreateSession("源工程");
        ProjectRuntimeSession target = CreateSession("目标工程");
        AddPoleCommand sourcePole = AddPole(source, new DocumentPoint(25, 35));
        source.SelectionManager.Select(new SelectionReference(
            SelectionTargetKind.Device,
            sourcePole.Pole.Id));
        var clipboard = new DrawingClipboardService();
        Assert.True(clipboard.Copy(source).IsSuccess);
        sourcePole.Pole.RenamePoleNumber("CHANGED-AFTER-COPY");

        Assert.True(clipboard.Paste(target).IsSuccess);

        Pole pasted = Assert.Single(target.PersistenceSession.Domain.Devices.OfType<Pole>());
        Assert.Equal("P-01", pasted.PoleNumber);
        Assert.Equal(new DocumentPoint(35, 45),
            target.Layout.DrawingLayout.Poles[pasted.Id].Position);
        Assert.DoesNotContain(source.PersistenceSession.Domain.Devices,
            item => item.Id == pasted.Id);
    }

    [Fact]
    public void CableTerminationAndCable_CopyOnlyWhenBothEndpointsAreInClosure()
    {
        ProjectRuntimeSession session = CreateSession("电缆复制");
        var factory = new DeviceCommandFactory();
        AddRingCabinetCommand cabinet = factory.CreateAddRingCabinet(
            session.PersistenceSession.Domain,
            session.Layout,
            new RingCabinetCreationConfiguration(
                "电缆柜",
                new RingCabinetCreationTemplateFactory().Create(
                    RingCabinetTemplateType.Conventional,
                    3)),
            new DocumentPoint(20, 20));
        cabinet.Execute();
        AddPoleCommand pole = AddPole(session, new DocumentPoint(180, 30));
        AddCableTerminationAttachmentCommand termination =
            factory.CreateAddCableTerminationAttachment(
                session.PersistenceSession.Domain,
                session.Layout,
                pole.Pole.Id,
                "终端",
                new DocumentPoint(10, 0));
        termination.Execute();
        Guid connectionId = Guid.NewGuid();
        Guid segmentId = Guid.NewGuid();
        Guid start = cabinet.Cabinet.Intervals[0].ExternalTerminalId;
        Guid end = termination.Creation.CableSideTerminal.Id;
        var connection = new Connection(
            connectionId, ConnectionType.Cable, start, end, "C-1", "10kV");
        var segment = new CableSegment(
            segmentId, "C-1", "YJV22", 80, "10kV", connectionId, start, end);
        session.PersistenceSession.Domain.AddCableSegment(segment, connection);
        session.Layout.SetCableRouteGuide(new CableRouteGuide(segmentId, 140));
        session.RebuildScene();
        session.SelectionManager.Replace([
            new SelectionReference(SelectionTargetKind.RingCabinet, cabinet.Cabinet.Id),
            new SelectionReference(SelectionTargetKind.PoleAttachment,
                termination.Creation.Attachment.AttachmentId),
            new SelectionReference(SelectionTargetKind.CableSegment, segmentId)
        ]);
        var clipboard = new DrawingClipboardService();

        Assert.True(clipboard.Copy(session).IsSuccess);
        Assert.True(clipboard.Paste(session).IsSuccess);

        CableSegment copied = Assert.Single(
            session.PersistenceSession.Domain.CableSegments,
            item => item.Id != segmentId);
        Assert.NotEqual(connectionId, copied.ConnectionId);
        Assert.DoesNotContain(new[] { start, end }, id =>
            id == copied.StartTerminalId || id == copied.EndTerminalId);
        Assert.Equal(150, session.Layout.CableRouteGuides[copied.Id].HorizontalYMillimeters);
    }

    [Fact]
    public void PoleJunctionCopy_PreservesSharedNodeAcrossPoleSwitchAndCableTermination()
    {
        ProjectRuntimeSession session = CreateSession("共享节点复制");
        var factory = new DeviceCommandFactory();
        AddPoleCommand pole = AddPole(session, new DocumentPoint(60, 60));
        AddPoleSwitchAttachmentCommand switchCommand = factory.CreateAddPoleSwitchAttachment(
            session.PersistenceSession.Domain,
            session.Layout,
            pole.Pole.Id,
            SwitchKind.IsolationSwitch,
            new DocumentPoint(10, 0));
        switchCommand.Execute();
        AddCableTerminationAttachmentCommand terminationCommand =
            factory.CreateAddCableTerminationAttachment(
                session.PersistenceSession.Domain,
                session.Layout,
                pole.Pole.Id,
                "共享节点终端",
                new DocumentPoint(0, 12));
        terminationCommand.Execute();
        session.RebuildScene();
        session.SelectionManager.Replace([
            new SelectionReference(SelectionTargetKind.PoleAttachment,
                switchCommand.Creation.Attachment.AttachmentId),
            new SelectionReference(SelectionTargetKind.PoleAttachment,
                terminationCommand.Creation.Attachment.AttachmentId)
        ]);
        var clipboard = new DrawingClipboardService();

        Assert.True(clipboard.Copy(session).IsSuccess);
        Assert.True(clipboard.Paste(session).IsSuccess);

        Guid copiedPoleId = session.SelectionManager.SelectionSet.SelectedReferences
            .Single(item => item.Kind == SelectionTargetKind.Device &&
                            session.PersistenceSession.Domain.Devices
                                .OfType<Pole>()
                                .Any(poleItem => poleItem.Id == item.ObjectId))
            .ObjectId;
        PoleAttachment[] copiedAttachments = session.PersistenceSession.Domain.PoleAttachments
            .Where(item => item.PoleId == copiedPoleId)
            .ToArray();
        Assert.Equal(2, copiedAttachments.Length);
        SwitchDevice copiedSwitch = Assert.IsType<SwitchDevice>(
            session.PersistenceSession.Domain.Devices.Single(item =>
                item.Id == copiedAttachments.Single(attachment =>
                    session.PersistenceSession.Domain.Devices.Single(device =>
                        device.Id == attachment.AttachedDeviceId) is SwitchDevice).AttachedDeviceId));
        CableTermination copiedTermination = Assert.IsType<CableTermination>(
            session.PersistenceSession.Domain.Devices.Single(item =>
                item.Id == copiedAttachments.Single(attachment =>
                    session.PersistenceSession.Domain.Devices.Single(device =>
                        device.Id == attachment.AttachedDeviceId) is CableTermination).AttachedDeviceId));
        Pole copiedPole = session.PersistenceSession.Domain.Devices.OfType<Pole>()
            .Single(item => item.Id == copiedPoleId);
        Guid junctionNode = session.PersistenceSession.Domain.Terminals.Single(item =>
            item.Id == copiedPole.OverheadAnchorTerminalIds.Single()).ElectricalNodeId!.Value;

        Assert.Equal(junctionNode, session.PersistenceSession.Domain.Terminals.Single(item =>
            item.Id == copiedSwitch.TerminalIds[0]).ElectricalNodeId);
        Assert.Equal(junctionNode, session.PersistenceSession.Domain.Terminals.Single(item =>
            item.Id == copiedTermination.OverheadSideTerminalId).ElectricalNodeId);
        Assert.NotEqual(junctionNode, session.PersistenceSession.Domain.Terminals.Single(item =>
            item.Id == copiedSwitch.TerminalIds[1]).ElectricalNodeId);
        Assert.NotEqual(junctionNode, session.PersistenceSession.Domain.Terminals.Single(item =>
            item.Id == copiedTermination.CableSideTerminalId).ElectricalNodeId);
    }

    [Fact]
    public void PasteCommandFailure_RollsBackChildrenAndRestoresSelection()
    {
        var selection = new SelectionManager();
        var original = new SelectionReference(SelectionTargetKind.Device, Guid.NewGuid());
        selection.Select(original);
        var first = new TrackingCommand();
        var failing = new TrackingCommand(throwOnExecute: true);
        var command = new PasteSelectionCommand(
            [first, failing],
            selection,
            [new SelectionReference(SelectionTargetKind.Device, Guid.NewGuid())],
            null);

        Assert.Throws<InvalidOperationException>(command.Execute);

        Assert.Equal(0, first.Value);
        Assert.Equal(original, selection.Selected);
    }

    [Fact]
    public void TJunctionCopy_RemapsAllBranchesToOneNewJunctionTerminal()
    {
        ProjectRuntimeSession session = CreateSession("T接复制");
        AddPoleCommand center = AddPole(session, new DocumentPoint(100, 100));
        AddPoleCommand left = AddPole(session, new DocumentPoint(20, 100));
        AddPoleCommand right = AddPole(session, new DocumentPoint(180, 100));
        AddPoleCommand down = AddPole(session, new DocumentPoint(100, 180));
        AddOverhead(session, center, left);
        AddOverhead(session, center, right);
        AddOverhead(session, center, down);
        Guid[] sourcePoleIds = [center.Pole.Id, left.Pole.Id, right.Pole.Id, down.Pole.Id];
        Guid[] sourceConnectionIds = session.PersistenceSession.Domain.Connections
            .Select(item => item.Id)
            .ToArray();
        session.SelectionManager.Replace(
            sourcePoleIds.Select(id => new SelectionReference(SelectionTargetKind.Device, id))
                .Concat(sourceConnectionIds.Select(id => new SelectionReference(
                    SelectionTargetKind.Connection,
                    id))));
        var clipboard = new DrawingClipboardService();

        Assert.True(clipboard.Copy(session).IsSuccess);
        Assert.True(clipboard.Paste(session).IsSuccess);

        Connection[] copiedConnections = session.PersistenceSession.Domain.Connections
            .Where(item => !sourceConnectionIds.Contains(item.Id))
            .ToArray();
        Assert.Equal(3, copiedConnections.Length);
        Guid copiedCenterTerminalId = copiedConnections
            .SelectMany(item => new[] { item.StartTerminalId, item.EndTerminalId })
            .GroupBy(id => id)
            .Single(group => group.Count() == 3)
            .Key;
        Assert.DoesNotContain(center.Terminal.Id, copiedConnections.SelectMany(item =>
            new[] { item.StartTerminalId, item.EndTerminalId }));
        Assert.Contains(session.PersistenceSession.Domain.Terminals,
            item => item.Id == copiedCenterTerminalId && item.AllowsMultipleConnections);
    }

    private ProjectRuntimeSession CreateSession(string title)
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"drawing-clipboard-{Guid.NewGuid():N}.kvdrawing");
        _paths.Add(path);
        var service = new ProjectService();
        ProjectSession persistence = service.CreateProject(path, title);
        return ProjectRuntimeSession.CreateEmpty(persistence, new DrawingSceneBuilder());
    }

    private static AddPoleCommand AddPole(ProjectRuntimeSession session, DocumentPoint position)
    {
        AddPoleCommand command = new DeviceCommandFactory().CreateAddPole(
            session.PersistenceSession.Domain,
            session.Layout,
            position);
        command.Execute();
        session.RebuildScene();
        return command;
    }

    private static void AddOverhead(
        ProjectRuntimeSession session,
        AddPoleCommand first,
        AddPoleCommand second)
    {
        Guid connectionId = Guid.NewGuid();
        var connection = new Connection(
            connectionId,
            ConnectionType.OverheadLine,
            first.Terminal.Id,
            second.Terminal.Id,
            "架空线",
            "10kV");
        var line = new OverheadLine(
            connectionId,
            "JKLYJ-120",
            new[] { first.Pole.Id, second.Pole.Id });
        var layout = new OverheadLineLayout(
            connectionId,
            first.Layout.Position,
            second.Layout.Position);
        new AddOverheadLineCommand(
            session.PersistenceSession.Domain,
            session.Layout,
            connection,
            line,
            layout).Execute();
        session.RebuildScene();
    }

    public void Dispose()
    {
        foreach (string path in _paths)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private sealed class TrackingCommand : ICommand
    {
        private readonly bool _throwOnExecute;

        public TrackingCommand(bool throwOnExecute = false)
        {
            _throwOnExecute = throwOnExecute;
        }

        public int Value { get; private set; }

        public void Execute()
        {
            if (_throwOnExecute) throw new InvalidOperationException("expected failure");
            Value++;
        }

        public void Undo() => Value--;

        public void Redo() => Execute();
    }
}
