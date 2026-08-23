using DistributionDrawing.Application.Devices;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class CableTerminationOrbitDragTests
{
    [Fact]
    public void DraggingCableTermination_OrbitsPoleAndKeepsOuterTipAsCableAnchor()
    {
        Guid poleId = Guid.NewGuid();
        Guid attachmentId = Guid.NewGuid();
        var pole = new PoleLayout(poleId, new DocumentPoint(20, 30));
        var initial = new AttachmentLayout(attachmentId, new DocumentPoint(14, 2));
        var drawing = new DrawingLayout();
        drawing.Add(pole);
        drawing.Add(initial);
        var runtime = new RuntimeLayoutDocument(
            drawing,
            new Dictionary<Guid, RingCabinetLayout>());
        var controller = new DeviceDragController();
        DocumentPoint poleCenter = PoleProfessionalGeometry.GetPoleCenter(pole);

        Assert.True(controller.TryBeginDrag(
            new SelectionReference(SelectionTargetKind.PoleAttachment, attachmentId),
            new DocumentPoint(poleCenter.XMillimeters + 20, poleCenter.YMillimeters),
            runtime,
            poleId));
        Assert.True(controller.UpdatePreview(
            new DocumentPoint(poleCenter.XMillimeters, poleCenter.YMillimeters + 30)));

        AttachmentLayout moved = drawing.Attachments[attachmentId];
        PoleAttachmentGeometry geometry = PoleProfessionalGeometry.GetAttachmentGeometry(
            pole,
            moved,
            SymbolKind.CableTermination);
        double tangentDistance = Distance(poleCenter, geometry.SecondTerminal);
        double tipDistance = Distance(poleCenter, geometry.FirstTerminal);

        Assert.Equal(DrawingMetrics.Default.Pole.PoleRadius, tangentDistance, 6);
        Assert.Equal(
            DrawingMetrics.Default.Pole.PoleRadius +
            DrawingMetrics.Default.CableTermination.TriangleHeight,
            tipDistance,
            6);
        Assert.IsType<MoveAttachmentCommand>(controller.Commit());
    }

    [Fact]
    public void MoveCableTermination_ReroutesCableAndOverhead_UndoRedoPreserveTopology()
    {
        OrbitFixture fixture = CreateConnectedFixture();
        AttachmentLayout before = fixture.Layout.DrawingLayout.Attachments[
            fixture.FirstAttachment.AttachmentId];
        PoleLayout poleLayout = fixture.Layout.DrawingLayout.Poles[fixture.FirstPole.Id];
        DocumentPoint poleCenter = PoleProfessionalGeometry.GetPoleCenter(poleLayout);
        TerminalAnchor beforeCableAnchor = CableAnchor(fixture);
        string cableBefore = GeometryKey(
            fixture.Builder.Build(fixture.Document, fixture.Layout),
            fixture.Cable.Id);
        string overheadBefore = GeometryKey(
            fixture.Builder.Build(fixture.Document, fixture.Layout),
            fixture.Overhead.ConnectionId);
        Guid cableId = fixture.Cable.Id;
        Guid cableConnectionId = fixture.Cable.ConnectionId;
        Guid overheadConnectionId = fixture.Overhead.ConnectionId;
        Guid[] terminalIds =
        [
            fixture.Cable.StartTerminalId,
            fixture.Cable.EndTerminalId,
            fixture.OverheadConnection.StartTerminalId,
            fixture.OverheadConnection.EndTerminalId
        ];
        var controller = new DeviceDragController();

        Assert.True(controller.TryBeginDrag(
            new SelectionReference(
                SelectionTargetKind.PoleAttachment,
                fixture.FirstAttachment.AttachmentId),
            before.Offset,
            fixture.Layout,
            fixture.FirstPole.Id));
        Assert.True(controller.UpdatePreview(new DocumentPoint(
            poleCenter.XMillimeters,
            poleCenter.YMillimeters + 60)));
        var command = Assert.IsType<MoveAttachmentCommand>(controller.Commit());
        var stack = new CommandStack();
        stack.ExecuteCommand(command);

        AttachmentLayout moved = fixture.Layout.DrawingLayout.Attachments[
            fixture.FirstAttachment.AttachmentId];
        TerminalAnchor movedCableAnchor = CableAnchor(fixture);
        TerminalAnchor movedOverheadAnchor = OverheadAnchor(fixture);
        PoleAttachmentGeometry movedGeometry = PoleProfessionalGeometry.GetAttachmentGeometry(
            poleLayout,
            moved,
            SymbolKind.CableTermination);
        DrawingScene movedScene = fixture.Builder.Build(fixture.Document, fixture.Layout);
        string cableMoved = GeometryKey(movedScene, fixture.Cable.Id);
        string overheadMoved = GeometryKey(movedScene, fixture.Overhead.ConnectionId);

        Assert.NotEqual(before.Offset, moved.Offset);
        Assert.Equal(movedGeometry.FirstTerminal, movedCableAnchor.Position);
        Assert.Equal(movedGeometry.SecondTerminal, movedOverheadAnchor.Position);
        Assert.Equal(DrawingMetrics.Default.Pole.PoleRadius,
            Distance(poleCenter, movedGeometry.SecondTerminal), 6);
        Assert.True(Distance(poleCenter, movedGeometry.FirstTerminal) >
            Distance(poleCenter, movedGeometry.SecondTerminal));
        Assert.NotEqual(beforeCableAnchor.Position, movedCableAnchor.Position);
        Assert.NotEqual(cableBefore, cableMoved);
        Assert.NotEqual(overheadBefore, overheadMoved);
        AssertTopologyUnchanged(
            fixture,
            cableId,
            cableConnectionId,
            overheadConnectionId,
            terminalIds);

        Assert.True(stack.Undo());
        Assert.Equal(before.Offset, fixture.Layout.DrawingLayout.Attachments[
            fixture.FirstAttachment.AttachmentId].Offset);
        Assert.Equal(cableBefore, GeometryKey(
            fixture.Builder.Build(fixture.Document, fixture.Layout), fixture.Cable.Id));
        Assert.Equal(overheadBefore, GeometryKey(
            fixture.Builder.Build(fixture.Document, fixture.Layout),
            fixture.Overhead.ConnectionId));

        Assert.True(stack.Redo());
        Assert.Equal(moved.Offset, fixture.Layout.DrawingLayout.Attachments[
            fixture.FirstAttachment.AttachmentId].Offset);
        Assert.Equal(cableMoved, GeometryKey(
            fixture.Builder.Build(fixture.Document, fixture.Layout), fixture.Cable.Id));
        Assert.Equal(overheadMoved, GeometryKey(
            fixture.Builder.Build(fixture.Document, fixture.Layout),
            fixture.Overhead.ConnectionId));
        AssertTopologyUnchanged(
            fixture,
            cableId,
            cableConnectionId,
            overheadConnectionId,
            terminalIds);
    }

    private static OrbitFixture CreateConnectedFixture()
    {
        var factory = new PoleCreationFactory();
        PoleCreationResult first = factory.CreateWithAttachments(
            "P-1",
            PoleType.Cement,
            null,
            switchKinds: null,
            includeCableTerminal: true);
        PoleCreationResult second = factory.CreateWithAttachments(
            "P-2",
            PoleType.Cement,
            null,
            switchKinds: null,
            includeCableTerminal: true);
        var document = new DrawingDocument(Guid.NewGuid(), "Orbit route test");
        AddPoleAggregate(document, first);
        AddPoleAggregate(document, second);
        CableTermination firstTermination = Assert.Single(
            first.Devices.OfType<CableTermination>());
        CableTermination secondTermination = Assert.Single(
            second.Devices.OfType<CableTermination>());
        PoleAttachment firstAttachment = Assert.Single(
            first.Attachments,
            attachment => attachment.AttachedDeviceId == firstTermination.Id);
        Guid cableConnectionId = Guid.NewGuid();
        var cableConnection = new Connection(
            cableConnectionId,
            ConnectionType.Cable,
            firstTermination.CableSideTerminalId,
            secondTermination.CableSideTerminalId,
            "电缆",
            "10kV");
        var cable = new CableSegment(
            Guid.NewGuid(),
            "电缆",
            "YJV22",
            100,
            "10kV",
            cableConnectionId,
            cableConnection.StartTerminalId,
            cableConnection.EndTerminalId);
        document.AddCableSegment(cable, cableConnection);
        Guid overheadConnectionId = Guid.NewGuid();
        var overheadConnection = new Connection(
            overheadConnectionId,
            ConnectionType.OverheadLine,
            firstTermination.OverheadSideTerminalId,
            secondTermination.OverheadSideTerminalId,
            "架空线",
            "10kV");
        document.AddConnection(overheadConnection);
        var overhead = new OverheadLine(
            overheadConnectionId,
            "JKLYJ",
            [first.Pole.Id, second.Pole.Id]);
        document.AddOverheadLine(overhead);

        var drawing = new DrawingLayout();
        drawing.Add(new PoleLayout(first.Pole.Id, new DocumentPoint(30, 30)));
        drawing.Add(new PoleLayout(second.Pole.Id, new DocumentPoint(220, 100)));
        foreach (PoleAttachment attachment in first.Attachments.Concat(second.Attachments))
        {
            drawing.Add(new AttachmentLayout(attachment.AttachmentId, new DocumentPoint(14, 2)));
        }
        drawing.Add(new OverheadLineLayout(
            overheadConnectionId,
            new DocumentPoint(0, 0),
            new DocumentPoint(1, 1)));
        return new OrbitFixture(
            document,
            first.Pole,
            firstAttachment,
            firstTermination,
            cable,
            overheadConnection,
            overhead,
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
        foreach (Device device in result.Devices)
        {
            document.AddDevice(device);
        }
        foreach (ElectricalNode node in result.ElectricalNodes)
        {
            document.AddElectricalNode(node);
        }
        foreach (Terminal terminal in result.Terminals)
        {
            document.AddTerminal(terminal);
        }
        foreach (PoleAttachment attachment in result.Attachments)
        {
            document.AddPoleAttachment(attachment);
        }
    }

    private static TerminalAnchor CableAnchor(OrbitFixture fixture)
    {
        TerminalAnchorIndex index = TerminalAnchorIndex.Build(
            fixture.Document,
            fixture.Layout.DrawingLayout,
            fixture.Layout.RingCabinetLayouts,
            fixture.Document.Connections,
            fixture.Document.CableSegments);
        Assert.True(index.TryGet(
            fixture.FirstTermination.CableSideTerminalId,
            out TerminalAnchor anchor));
        return anchor;
    }

    private static TerminalAnchor OverheadAnchor(OrbitFixture fixture)
    {
        TerminalAnchorIndex index = TerminalAnchorIndex.Build(
            fixture.Document,
            fixture.Layout.DrawingLayout,
            fixture.Layout.RingCabinetLayouts,
            fixture.Document.Connections,
            fixture.Document.CableSegments);
        Assert.True(index.TryGet(
            fixture.FirstTermination.OverheadSideTerminalId,
            out TerminalAnchor anchor));
        return anchor;
    }

    private static string GeometryKey(DrawingScene scene, Guid targetId) =>
        string.Join(';', scene.Elements.OfType<SceneLine>()
            .Where(line => line.TargetId == targetId)
            .Select(line =>
                $"{line.Start.XMillimeters:R},{line.Start.YMillimeters:R}-" +
                $"{line.End.XMillimeters:R},{line.End.YMillimeters:R}"));

    private static void AssertTopologyUnchanged(
        OrbitFixture fixture,
        Guid cableId,
        Guid cableConnectionId,
        Guid overheadConnectionId,
        IReadOnlyList<Guid> terminalIds)
    {
        Assert.Equal(cableId, fixture.Cable.Id);
        Assert.Equal(cableConnectionId, fixture.Cable.ConnectionId);
        Assert.Equal(overheadConnectionId, fixture.Overhead.ConnectionId);
        Assert.Equal(terminalIds,
        [
            fixture.Cable.StartTerminalId,
            fixture.Cable.EndTerminalId,
            fixture.OverheadConnection.StartTerminalId,
            fixture.OverheadConnection.EndTerminalId
        ]);
    }

    private static double Distance(DocumentPoint first, DocumentPoint second)
    {
        double x = second.XMillimeters - first.XMillimeters;
        double y = second.YMillimeters - first.YMillimeters;
        return Math.Sqrt(x * x + y * y);
    }

    private sealed record OrbitFixture(
        DrawingDocument Document,
        Pole FirstPole,
        PoleAttachment FirstAttachment,
        CableTermination FirstTermination,
        CableSegment Cable,
        Connection OverheadConnection,
        OverheadLine Overhead,
        RuntimeLayoutDocument Layout,
        DrawingSceneBuilder Builder);
}
