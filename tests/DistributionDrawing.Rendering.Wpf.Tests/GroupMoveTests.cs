using DistributionDrawing.Application.Devices;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class GroupMoveTests
{
    [Fact]
    public void DragTwoPoles_AppliesOneUniformDeltaAndPreservesSelection()
    {
        GroupFixture fixture = CreateFixture();
        SelectionReference first = Device(fixture.FirstPole.Id);
        SelectionReference second = Device(fixture.SecondPole.Id);
        var manager = new SelectionManager();
        manager.Replace([first, second], second);
        SelectionSet beforeSelection = manager.SelectionSet;
        DocumentPoint relativeBefore = Difference(
            fixture.SecondPosition,
            fixture.FirstPosition);
        var controller = new DeviceDragController();

        Assert.True(controller.TryBeginGroupDrag(
            manager.SelectionSet,
            first,
            fixture.FirstPosition,
            fixture.Document,
            fixture.Layout));
        Assert.True(controller.IsGroupDrag);
        Assert.True(controller.UpdatePreview(new DocumentPoint(25, 37)));

        DocumentPoint firstMoved = fixture.Layout.DrawingLayout.Poles[
            fixture.FirstPole.Id].Position;
        DocumentPoint secondMoved = fixture.Layout.DrawingLayout.Poles[
            fixture.SecondPole.Id].Position;
        Assert.Equal(new DocumentPoint(25, 37), firstMoved);
        Assert.Equal(relativeBefore, Difference(secondMoved, firstMoved));
        Assert.Same(beforeSelection, manager.SelectionSet);
        Assert.Equal(second, manager.Selected);
    }

    [Fact]
    public void Commit_CreatesOneAtomicCommandWithStableUndoRedo()
    {
        GroupFixture fixture = CreateFixture(includeCabinet: true);
        SelectionReference pole = Device(fixture.FirstPole.Id);
        SelectionReference cabinet = new(
            SelectionTargetKind.RingCabinet,
            fixture.CabinetId!.Value);
        SelectionSet selection = SelectionSet.Create([pole, cabinet], cabinet);
        var controller = new DeviceDragController();

        Assert.True(controller.TryBeginGroupDrag(
            selection,
            pole,
            fixture.FirstPosition,
            fixture.Document,
            fixture.Layout));
        Assert.True(controller.UpdatePreview(new DocumentPoint(35, 45)));
        GroupMoveCommand command = Assert.IsType<GroupMoveCommand>(controller.Commit());
        var stack = new CommandStack();
        stack.ExecuteCommand(command);

        Assert.Single(stack.History);
        Assert.Equal(new DocumentPoint(35, 45), fixture.Layout.DrawingLayout.Poles[
            fixture.FirstPole.Id].Position);
        Assert.Equal(new DocumentPoint(125, 125), fixture.Layout.RingCabinetLayouts[
            fixture.CabinetId.Value].Position);

        Assert.True(stack.Undo());
        Assert.Equal(fixture.FirstPosition, fixture.Layout.DrawingLayout.Poles[
            fixture.FirstPole.Id].Position);
        Assert.Equal(new DocumentPoint(100, 100), fixture.Layout.RingCabinetLayouts[
            fixture.CabinetId.Value].Position);

        Assert.True(stack.Redo());
        Assert.Equal(new DocumentPoint(35, 45), fixture.Layout.DrawingLayout.Poles[
            fixture.FirstPole.Id].Position);
        Assert.Equal(new DocumentPoint(125, 125), fixture.Layout.RingCabinetLayouts[
            fixture.CabinetId.Value].Position);
    }

    [Fact]
    public void SelectedPoleAndAttachment_MovePoleOnceAndKeepAttachmentOffset()
    {
        GroupFixture fixture = CreateFixture(includeAttachment: true);
        AttachmentLayout attachmentBefore = fixture.Layout.DrawingLayout.Attachments[
            fixture.Attachment!.AttachmentId];
        SelectionSet selection = SelectionSet.Create([
            Device(fixture.FirstPole.Id),
            new SelectionReference(
                SelectionTargetKind.PoleAttachment,
                fixture.Attachment.AttachmentId,
                fixture.FirstPole.Id),
            Device(fixture.Switch!.Id)
        ]);
        var controller = new DeviceDragController();

        Assert.True(controller.TryBeginGroupDrag(
            selection,
            Device(fixture.Switch.Id),
            fixture.FirstPosition,
            fixture.Document,
            fixture.Layout));
        Assert.True(controller.UpdatePreview(new DocumentPoint(30, 40)));

        Assert.Equal(new DocumentPoint(30, 40), fixture.Layout.DrawingLayout.Poles[
            fixture.FirstPole.Id].Position);
        Assert.Equal(attachmentBefore, fixture.Layout.DrawingLayout.Attachments[
            fixture.Attachment.AttachmentId]);
        GroupMoveCommand command = Assert.IsType<GroupMoveCommand>(controller.Commit());
        Assert.Single(command.After.Poles);
        Assert.Empty(command.After.Attachments);
    }

    [Fact]
    public void AttachmentOnlyGroupMove_TranslatesOffsetAndPreservesRotation()
    {
        GroupFixture fixture = CreateFixture(includeAttachment: true);
        AttachmentLayout before = fixture.Layout.DrawingLayout.Attachments[
            fixture.Attachment!.AttachmentId];
        SelectionReference switchDevice = Device(fixture.Switch!.Id);
        SelectionSet selection = SelectionSet.Create([switchDevice]);
        var controller = new DeviceDragController();

        Assert.True(controller.TryBeginGroupDrag(
            selection,
            switchDevice,
            before.Offset,
            fixture.Document,
            fixture.Layout));
        Assert.True(controller.UpdatePreview(new DocumentPoint(
            before.Offset.XMillimeters + 12,
            before.Offset.YMillimeters - 8)));

        AttachmentLayout moved = fixture.Layout.DrawingLayout.Attachments[
            fixture.Attachment.AttachmentId];
        Assert.Equal(new DocumentPoint(27, -8), moved.Offset);
        Assert.Equal(before.RotationQuarterTurns, moved.RotationQuarterTurns);
    }

    [Fact]
    public void Cancel_RestoresAllPreviewLayouts()
    {
        GroupFixture fixture = CreateFixture(includeCabinet: true);
        SelectionSet selection = SelectionSet.Create([
            Device(fixture.FirstPole.Id),
            new SelectionReference(
                SelectionTargetKind.RingCabinet,
                fixture.CabinetId!.Value)
        ]);
        var controller = new DeviceDragController();

        Assert.True(controller.TryBeginGroupDrag(
            selection,
            Device(fixture.FirstPole.Id),
            fixture.FirstPosition,
            fixture.Document,
            fixture.Layout));
        Assert.True(controller.UpdatePreview(new DocumentPoint(40, 50)));
        Assert.True(controller.Cancel());

        Assert.Equal(fixture.FirstPosition, fixture.Layout.DrawingLayout.Poles[
            fixture.FirstPole.Id].Position);
        Assert.Equal(new DocumentPoint(100, 100), fixture.Layout.RingCabinetLayouts[
            fixture.CabinetId.Value].Position);
        Assert.False(controller.IsActive);
    }

    [Fact]
    public void Snap_ExcludesMovingPeerAndAppliesSnappedDeltaToWholeGroup()
    {
        GroupFixture fixture = CreateFixture(includeFixedPole: true);
        SelectionSet selection = SelectionSet.Create([
            Device(fixture.FirstPole.Id),
            Device(fixture.SecondPole.Id)
        ]);
        var controller = new DeviceDragController();

        Assert.True(controller.TryBeginGroupDrag(
            selection,
            Device(fixture.FirstPole.Id),
            fixture.FirstPosition,
            fixture.Document,
            fixture.Layout));
        Assert.True(controller.UpdatePreview(new DocumentPoint(47, 10)));

        Assert.Equal(new DocumentPoint(50, 10), fixture.Layout.DrawingLayout.Poles[
            fixture.FirstPole.Id].Position);
        Assert.Equal(new DocumentPoint(110, 40), fixture.Layout.DrawingLayout.Poles[
            fixture.SecondPole.Id].Position);
    }

    [Fact]
    public void GroupMove_DoesNotChangeDomainTopologyOrSwitchState()
    {
        GroupFixture fixture = CreateFixture(includeAttachment: true);
        Guid[] deviceIds = fixture.Document.Devices.Select(item => item.Id).ToArray();
        Guid[] attachmentIds = fixture.Document.PoleAttachments
            .Select(item => item.AttachmentId).ToArray();
        SwitchState state = fixture.Switch!.SwitchState!.Value;
        var controller = new DeviceDragController();
        SelectionSet selection = SelectionSet.Create([
            Device(fixture.FirstPole.Id),
            Device(fixture.SecondPole.Id)
        ]);

        Assert.True(controller.TryBeginGroupDrag(
            selection,
            Device(fixture.FirstPole.Id),
            fixture.FirstPosition,
            fixture.Document,
            fixture.Layout));
        Assert.True(controller.UpdatePreview(new DocumentPoint(30, 30)));
        Assert.IsType<GroupMoveCommand>(controller.Commit());

        Assert.Equal(deviceIds, fixture.Document.Devices.Select(item => item.Id));
        Assert.Equal(attachmentIds, fixture.Document.PoleAttachments.Select(item =>
            item.AttachmentId));
        Assert.Equal(state, fixture.Switch.SwitchState);
        Assert.Empty(fixture.Document.Connections);
    }

    [Fact]
    public void SingleSwitchDeviceDrag_MovesAttachmentWithoutOrbiting()
    {
        GroupFixture fixture = CreateFixture(includeAttachment: true);
        AttachmentLayout before = fixture.Layout.DrawingLayout.Attachments[
            fixture.Attachment!.AttachmentId];
        var controller = new DeviceDragController();

        Assert.True(controller.TryBeginAttachmentDrag(
            Device(fixture.Switch!.Id),
            fixture.Attachment.AttachmentId,
            before.Offset,
            fixture.Layout,
            fixture.FirstPole.Id,
            orbitAroundPole: false));
        Assert.True(controller.UpdatePreview(new DocumentPoint(20, 5)));

        Assert.Equal(new DocumentPoint(20, 5), fixture.Layout.DrawingLayout.Attachments[
            fixture.Attachment.AttachmentId].Offset);
        Assert.IsType<MoveAttachmentCommand>(controller.Commit());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovingOneOrBothEndpoints_ReroutesOverheadLineWithoutTopologyChanges(
        bool moveBothEndpoints)
    {
        OverheadFixture fixture = CreateOverheadFixture();
        SelectionReference first = Device(fixture.FirstPole.Id);
        SelectionReference connection = new(
            SelectionTargetKind.Connection,
            fixture.Connection.Id);
        SelectionReference[] selected = moveBothEndpoints
            ? [first, Device(fixture.SecondPole.Id), connection]
            : [first, connection];
        SelectionSet selection = SelectionSet.Create(selected, first);
        var controller = new DeviceDragController();
        string before = GeometryKey(
            fixture.Builder.Build(fixture.Document, fixture.Layout),
            fixture.Connection.Id);
        Guid[] terminalIds = fixture.Document.Terminals.Select(item => item.Id).ToArray();

        Assert.True(controller.TryBeginGroupDrag(
            selection,
            first,
            fixture.Layout.DrawingLayout.Poles[fixture.FirstPole.Id].Position,
            fixture.Document,
            fixture.Layout));
        Assert.True(controller.UpdatePreview(new DocumentPoint(70, 80)));
        Assert.IsType<GroupMoveCommand>(controller.Commit());
        string after = GeometryKey(
            fixture.Builder.Build(fixture.Document, fixture.Layout),
            fixture.Connection.Id);

        Assert.NotEqual(before, after);
        Assert.Same(fixture.Connection, Assert.Single(fixture.Document.Connections));
        Assert.Same(fixture.Line, Assert.Single(fixture.Document.OverheadLines));
        Assert.Equal(terminalIds, fixture.Document.Terminals.Select(item => item.Id));
        Assert.Equal(fixture.Connection.Id, fixture.Line.ConnectionId);
    }

    private static GroupFixture CreateFixture(
        bool includeCabinet = false,
        bool includeAttachment = false,
        bool includeFixedPole = false)
    {
        var firstPole = new Pole(Guid.NewGuid(), "P-1");
        var secondPole = new Pole(Guid.NewGuid(), "P-2");
        var document = new DrawingDocument(Guid.NewGuid(), "Group move");
        document.AddDevice(firstPole);
        document.AddDevice(secondPole);
        var drawing = new DrawingLayout();
        DocumentPoint firstPosition = new(10, 20);
        DocumentPoint secondPosition = new(70, 50);
        drawing.Add(new PoleLayout(firstPole.Id, firstPosition));
        drawing.Add(new PoleLayout(secondPole.Id, secondPosition));
        if (includeFixedPole)
        {
            var fixedPole = new Pole(Guid.NewGuid(), "P-3");
            document.AddDevice(fixedPole);
            drawing.Add(new PoleLayout(fixedPole.Id, new DocumentPoint(50, 10)));
        }

        SwitchDevice? poleSwitch = null;
        PoleAttachment? attachment = null;
        if (includeAttachment)
        {
            poleSwitch = SwitchDevice.CreateForPole(
                Guid.NewGuid(),
                SwitchKind.IsolationSwitch,
                Guid.NewGuid(),
                Guid.NewGuid(),
                SwitchState.Open);
            attachment = new PoleAttachment(
                Guid.NewGuid(),
                firstPole.Id,
                poleSwitch.Id);
            document.AddDevice(poleSwitch);
            document.AddPoleAttachment(attachment);
            drawing.Add(new AttachmentLayout(
                attachment.AttachmentId,
                new DocumentPoint(15, 0),
                rotationQuarterTurns: 2));
        }

        Guid? cabinetId = includeCabinet ? Guid.NewGuid() : null;
        Dictionary<Guid, RingCabinetLayout> cabinets = [];
        if (cabinetId is Guid id)
        {
            cabinets[id] = new RingCabinetLayout(
                id,
                new DocumentPoint(100, 100),
                80,
                100,
                10,
                []);
        }

        return new GroupFixture(
            document,
            firstPole,
            secondPole,
            poleSwitch,
            attachment,
            new RuntimeLayoutDocument(drawing, cabinets),
            firstPosition,
            secondPosition,
            cabinetId);
    }

    private static OverheadFixture CreateOverheadFixture()
    {
        var factory = new PoleCreationFactory();
        PoleCreationResult first = factory.Create("P-11");
        PoleCreationResult second = factory.Create("P-12");
        var document = new DrawingDocument(Guid.NewGuid(), "Group route");
        AddPoleAggregate(document, first);
        AddPoleAggregate(document, second);
        Guid firstTerminalId = Assert.Single(first.Pole.OverheadAnchorTerminalIds);
        Guid secondTerminalId = Assert.Single(second.Pole.OverheadAnchorTerminalIds);
        var connection = new Connection(
            Guid.NewGuid(),
            ConnectionType.OverheadLine,
            firstTerminalId,
            secondTerminalId,
            "架空线路",
            "10kV");
        var line = new OverheadLine(
            connection.Id,
            "JKLYJ",
            [first.Pole.Id, second.Pole.Id]);
        document.AddConnection(connection);
        document.AddOverheadLine(line);
        var drawing = new DrawingLayout();
        drawing.Add(new PoleLayout(first.Pole.Id, new DocumentPoint(40, 50)));
        drawing.Add(new PoleLayout(second.Pole.Id, new DocumentPoint(240, 160)));
        drawing.Add(new OverheadLineLayout(
            connection.Id,
            new DocumentPoint(0, 0),
            new DocumentPoint(1, 1)));
        return new OverheadFixture(
            document,
            first.Pole,
            second.Pole,
            connection,
            line,
            new RuntimeLayoutDocument(
                drawing,
                new Dictionary<Guid, RingCabinetLayout>()),
            new DrawingSceneBuilder());
    }

    private static void AddPoleAggregate(
        DrawingDocument document,
        PoleCreationResult result)
    {
        document.AddDevice(result.Pole);
        foreach (ElectricalNode node in result.ElectricalNodes)
        {
            document.AddElectricalNode(node);
        }

        foreach (Terminal terminal in result.Terminals)
        {
            document.AddTerminal(terminal);
        }
    }

    private static string GeometryKey(DrawingScene scene, Guid targetId) =>
        string.Join(';', scene.Elements.OfType<SceneLine>()
            .Where(line => line.TargetId == targetId)
            .Select(line =>
                $"{line.Start.XMillimeters:R},{line.Start.YMillimeters:R}-" +
                $"{line.End.XMillimeters:R},{line.End.YMillimeters:R}"));

    private static SelectionReference Device(Guid id) => new(
        SelectionTargetKind.Device,
        id);

    private static DocumentPoint Difference(DocumentPoint value, DocumentPoint origin) => new(
        value.XMillimeters - origin.XMillimeters,
        value.YMillimeters - origin.YMillimeters);

    private sealed record GroupFixture(
        DrawingDocument Document,
        Pole FirstPole,
        Pole SecondPole,
        SwitchDevice? Switch,
        PoleAttachment? Attachment,
        RuntimeLayoutDocument Layout,
        DocumentPoint FirstPosition,
        DocumentPoint SecondPosition,
        Guid? CabinetId);

    private sealed record OverheadFixture(
        DrawingDocument Document,
        Pole FirstPole,
        Pole SecondPole,
        Connection Connection,
        OverheadLine Line,
        RuntimeLayoutDocument Layout,
        DrawingSceneBuilder Builder);
}
