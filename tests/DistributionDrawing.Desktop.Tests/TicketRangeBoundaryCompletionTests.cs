using DistributionDrawing.Application.Devices;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Desktop.WorkTickets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.PropertyInspector;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class TicketRangeBoundaryCompletionTests
{
    [Fact]
    public void RingAndPoleBoundarySideCompletionIsAtomicAndCanConfirmTogether()
    {
        (DrawingDocument drawing, SwitchDevice ringSwitch, PoleSetup pole) = CreateDrawing();
        var slots = new TicketBoundarySlotCollection();
        slots.Load([], _ => null);
        var picker = new TicketRangePickerState();

        PickAndChoose(slots, picker, drawing, 0, ringSwitch, BoundarySide.Line);
        PickAndChoose(slots, picker, drawing, 1, pole.Switch, BoundarySide.SmallerNumber);

        Assert.Equal(TicketRangePickMode.Idle, picker.Mode);
        Assert.Null(picker.BoundaryIndex);
        Assert.Null(picker.PreviousBoundary);
        Assert.Equal(pole.Switch.Id, slots[1].DeviceId);
        Assert.Equal(BoundarySide.SmallerNumber, slots[1].Side);
        IsolationBoundary resolvedPoleBoundary = slots[1].Resolved
            ?? throw new InvalidOperationException("Pole boundary was not resolved.");
        Assert.Equal(pole.Switch.FirstTerminalId, resolvedPoleBoundary.TerminalId);
        Assert.Equal(pole.SmallerConnection, resolvedPoleBoundary.ConnectionId);

        var tickets = new WorkTicketDataRoot(drawing.Id);
        var commandStack = new CommandStack();
        WorkTicketSession ticket = WorkTicketRangeCommit.Apply(drawing, tickets, commandStack,
            null, slots.Slots.Select(slot => slot.Resolved).ToArray(), new WorkTask("", ""));

        Assert.Equal(2, ticket.IsolationBoundaries.Count);
        Assert.Single(commandStack.History);
        Assert.Equal(2, ticket.IsolationBoundaries.Select(item => item.DeviceId).Distinct().Count());
    }

    [Fact]
    public void UnprovenPoleSideDoesNotCompletePickerOrPassConfirm()
    {
        (DrawingDocument drawing, _, PoleSetup pole) = CreateDrawing(connectLarger: false);
        var slots = new TicketBoundarySlotCollection();
        slots.Load([], _ => null);
        slots.Replace(1, new TicketBoundarySlot(pole.Switch.Id));
        var picker = new TicketRangePickerState();
        picker.BeginBoundary(1, new TicketBoundarySlot());
        picker.DevicePicked();

        Assert.Equal([BoundarySide.SmallerNumber],
            TicketRangeSideSelection.ResolvableSides(drawing, pole.Switch));
        Assert.False(TicketRangeSideSelection.TryChoose(slots, picker, drawing, 1,
            BoundarySide.LargerNumber, out _));
        Assert.Equal(TicketRangePickMode.ChoosingBoundarySide, picker.Mode);
        Assert.Null(slots[1].Resolved);
        Assert.Throws<InvalidOperationException>(() => WorkTicketRangeSetup.Confirm(
            drawing, WorkTicketSession.Create(), slots.Slots.Select(slot => slot.Resolved).ToArray()));
    }

    [Fact]
    public void NormalClickAfterPoleSideImmediatelyShowsInspectorAndKeepsPendingSlots()
    {
        (DrawingDocument drawing, _, PoleSetup pole) = CreateDrawing();
        var slots = new TicketBoundarySlotCollection();
        slots.Load([], _ => null);
        slots.Replace(1, new TicketBoundarySlot(pole.Switch.Id));
        var picker = new TicketRangePickerState();
        picker.BeginBoundary(1, new TicketBoundarySlot());
        picker.DevicePicked();
        Assert.True(TicketRangeSideSelection.TryChoose(slots, picker, drawing, 1,
            BoundarySide.SmallerNumber, out _));

        var normalTarget = new SelectionReference(SelectionTargetKind.Device, Guid.NewGuid());
        SelectionReference? inspectorTarget = null;
        bool activated = WorkRangeCanvasActivation.ActivateOrdinaryObject(picker.Mode,
            normalTarget, () => inspectorTarget = normalTarget);

        Assert.True(activated);
        Assert.Equal(normalTarget, inspectorTarget);
        Assert.Equal(TicketRangePickMode.Idle, picker.Mode);
        Assert.Equal(pole.Switch.Id, slots[1].DeviceId);
        Assert.NotNull(slots[1].Resolved);
    }

    [Fact]
    public void PoleBoundaryOverlayTargetsActualSwitchSymbolAndDoesNotDependOnSelection()
    {
        PoleCreationResult aggregate = new PoleCreationFactory().CreateWithAttachments(
            "P02", PoleType.Cement, null, [SwitchKind.IsolationSwitch], includeCableTerminal: false);
        SwitchDevice poleSwitch = Assert.Single(aggregate.Devices.OfType<SwitchDevice>());
        PoleAttachment attachment = Assert.Single(aggregate.Attachments);
        var drawing = new DrawingDocument(Guid.NewGuid(), "柱上边界绘制");
        drawing.AddDevice(aggregate.Pole);
        foreach (Device device in aggregate.Devices) drawing.AddDevice(device);
        foreach (ElectricalNode node in aggregate.ElectricalNodes) drawing.AddElectricalNode(node);
        foreach (Terminal terminal in aggregate.Terminals) drawing.AddTerminal(terminal);
        drawing.AddPoleAttachment(attachment);

        var layout = new DrawingLayout();
        var poleLayout = new PoleLayout(aggregate.Pole.Id, new DocumentPoint(40, 50));
        var attachmentLayout = new AttachmentLayout(attachment.AttachmentId,
            PoleProfessionalGeometry.GetDefaultAttachmentOffset(SwitchKind.IsolationSwitch));
        layout.Add(poleLayout);
        layout.Add(attachmentLayout);
        DrawingScene scene = new DrawingSceneBuilder().Build(drawing,
            new RuntimeLayoutDocument(layout, new Dictionary<Guid, RingCabinetLayout>()));

        SelectionHitTestEntry switchTarget = Assert.Single(scene.HitTestIndex.FindAll(
            new SelectionReference(SelectionTargetKind.Device, poleSwitch.Id)));
        SelectionHitTestEntry attachmentTarget = Assert.Single(scene.HitTestIndex.FindAll(
            new SelectionReference(SelectionTargetKind.PoleAttachment, attachment.AttachmentId)));
        Assert.NotEqual(aggregate.Pole.Id, switchTarget.Target.ObjectId);
        Assert.Equal(attachment.AttachmentId, attachmentTarget.Target.ObjectId);
        var resolver = new SelectionObjectResolver();
        resolver.SetSource(new PropertyInspectionSource
        {
            Document = drawing,
            Devices = drawing.Devices.ToArray(),
            Poles = drawing.Devices.OfType<Pole>().ToArray(),
            PoleAttachments = drawing.PoleAttachments.ToArray(),
            DrawingLayout = layout,
            HitTestIndex = scene.HitTestIndex
        });
        ResolvedSelection resolvedSelection = resolver.Resolve(switchTarget.Target)
            ?? throw new InvalidOperationException("Pole switch selection did not resolve.");
        Assert.Equal(poleSwitch.Id, resolvedSelection.SwitchDevice!.Id);
        Assert.Equal(attachment.AttachmentId, resolvedSelection.PoleAttachment!.AttachmentId);

        var owner = new WorkTicketRangeOwner(drawing.Id, Guid.NewGuid());
        IsolationBoundary?[] pending = [new IsolationBoundary(poleSwitch.Id, BoundarySide.SmallerNumber,
            poleSwitch.FirstTerminalId)];
        IReadOnlyList<SceneElement> selectedOverlay = WorkTicketOverlayBuilder.BuildBoundarySelection(
            scene.HitTestIndex, owner, drawing.Id, owner.TicketId, pending);
        IReadOnlyList<SceneElement> otherSelectionOverlay = WorkTicketOverlayBuilder.BuildBoundarySelection(
            scene.HitTestIndex, owner, drawing.Id, owner.TicketId, pending);
        IReadOnlyList<SceneElement> selectionOverlayBefore = SelectionOverlayBuilder.CreateElements(
            scene.HitTestIndex, switchTarget.Target);
        SelectionReference poleTarget = new(SelectionTargetKind.Device, aggregate.Pole.Id);
        IReadOnlyList<SceneElement> selectionOverlayAfter = SelectionOverlayBuilder.CreateElements(
            scene.HitTestIndex, poleTarget);

        SceneRectangle halo = Assert.IsType<SceneRectangle>(Assert.Single(selectedOverlay,
            element => element is SceneRectangle));
        Assert.Equal(new DocumentRect(switchTarget.Bounds.XMillimeters - 3,
            switchTarget.Bounds.YMillimeters - 3, switchTarget.Bounds.WidthMillimeters + 6,
            switchTarget.Bounds.HeightMillimeters + 6), halo.Bounds);
        Assert.Equal(selectedOverlay, otherSelectionOverlay);
        Assert.NotEqual(selectionOverlayBefore, selectionOverlayAfter);
    }

    [Fact]
    public void RingBoundaryOverlayStillTargetsRealCabinetSwitchSceneEntry()
    {
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "一号柜",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Closed, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Closed, SwitchState.Open)]));
        SwitchDevice ringSwitch = GetMainLoadSwitch(cabinet.Intervals[0]);
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(cabinet,
            new DocumentPoint(10, 10));
        DrawingScene scene = new DrawingSceneBuilder().Build(cabinet, layout);
        SelectionHitTestEntry switchTarget = Assert.Single(scene.HitTestIndex.FindAll(
            new SelectionReference(SelectionTargetKind.Device, ringSwitch.Id)));

        Guid projectId = Guid.NewGuid();
        Guid ticketId = Guid.NewGuid();
        IReadOnlyList<SceneElement> overlay = WorkTicketOverlayBuilder.BuildBoundarySelection(
            scene.HitTestIndex, new WorkTicketRangeOwner(projectId, ticketId), projectId, ticketId,
            [new IsolationBoundary(ringSwitch.Id, BoundarySide.Line, ringSwitch.SecondTerminalId)]);

        SceneRectangle halo = Assert.IsType<SceneRectangle>(Assert.Single(overlay,
            element => element is SceneRectangle));
        Assert.Equal(new DocumentRect(switchTarget.Bounds.XMillimeters - 3,
            switchTarget.Bounds.YMillimeters - 3, switchTarget.Bounds.WidthMillimeters + 6,
            switchTarget.Bounds.HeightMillimeters + 6), halo.Bounds);
        Assert.Contains(overlay, element => element is SceneText { Text: "[A]" });
    }

    private static void PickAndChoose(TicketBoundarySlotCollection slots,
        TicketRangePickerState picker, DrawingDocument drawing, int index,
        SwitchDevice device, BoundarySide side)
    {
        picker.BeginBoundary(index, slots[index]);
        slots.Replace(index, new TicketBoundarySlot(device.Id));
        picker.DevicePicked();
        Assert.Contains(side, TicketRangeSideSelection.ResolvableSides(drawing, device));
        Assert.True(TicketRangeSideSelection.TryChoose(slots, picker, drawing, index, side,
            out string issue), issue);
        Assert.Equal(TicketRangePickMode.Idle, picker.Mode);
        Assert.Null(picker.BoundaryIndex);
        Assert.Null(picker.PreviousBoundary);
    }

    private static SwitchDevice GetMainLoadSwitch(RingCabinetInterval interval)
    {
        SwitchDevice loadSwitch = Assert.Single(interval.SwitchDevices,
            device => device.SwitchKind == SwitchKind.LoadSwitch);
        SwitchDevice groundSwitch = Assert.Single(interval.SwitchDevices,
            device => device.SwitchKind == SwitchKind.GroundSwitch);
        Assert.NotEqual(loadSwitch.Id, groundSwitch.Id);
        return loadSwitch;
    }

    private static (DrawingDocument Drawing, SwitchDevice RingSwitch, PoleSetup Pole) CreateDrawing(
        bool connectLarger = true)
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "Ring + Pole Boundary");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "一号柜",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Closed, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Closed, SwitchState.Open)]));
        drawing.AddDevice(cabinet);
        SwitchDevice ringSwitch = GetMainLoadSwitch(cabinet.Intervals[0]);

        Pole smallerPole = new(Guid.NewGuid(), "P01");
        Pole currentPole = new(Guid.NewGuid(), "P02");
        Pole largerPole = new(Guid.NewGuid(), "P03");
        drawing.AddDevice(smallerPole);
        drawing.AddDevice(currentPole);
        drawing.AddDevice(largerPole);
        Terminal smallerAnchor = smallerPole.CreateOverheadAnchorTerminal(Guid.NewGuid());
        Terminal largerAnchor = largerPole.CreateOverheadAnchorTerminal(Guid.NewGuid());
        drawing.AddTerminal(smallerAnchor);
        drawing.AddTerminal(largerAnchor);
        SwitchDevice poleSwitch = SwitchDevice.CreateForPole(Guid.NewGuid(),
            SwitchKind.IsolationSwitch, Guid.NewGuid(), Guid.NewGuid(), displayName: "P02隔离刀闸");
        drawing.AddDevice(poleSwitch);
        drawing.AddTerminal(new Terminal(poleSwitch.FirstTerminalId, TopologyOwnerType.Device,
            poleSwitch.Id, "SwitchLeftTerminal", "10kV", true, true, null,
            [ConnectionType.OverheadLine]));
        drawing.AddTerminal(new Terminal(poleSwitch.SecondTerminalId, TopologyOwnerType.Device,
            poleSwitch.Id, "SwitchRightTerminal", "10kV", true, false, null,
            [ConnectionType.OverheadLine]));
        drawing.AddPoleAttachment(new PoleAttachment(Guid.NewGuid(), currentPole.Id, poleSwitch.Id));
        var smallerConnection = new Connection(Guid.NewGuid(), ConnectionType.OverheadLine,
            smallerAnchor.Id, poleSwitch.FirstTerminalId, "P01-P02", "10kV");
        drawing.AddConnection(smallerConnection);
        drawing.AddOverheadLine(new OverheadLine(smallerConnection.Id, "JKLYJ",
            [smallerPole.Id, currentPole.Id]));
        Guid? largerConnectionId = null;
        if (connectLarger)
        {
            var largerConnection = new Connection(Guid.NewGuid(), ConnectionType.OverheadLine,
                poleSwitch.SecondTerminalId, largerAnchor.Id, "P02-P03", "10kV");
            drawing.AddConnection(largerConnection);
            drawing.AddOverheadLine(new OverheadLine(largerConnection.Id, "JKLYJ",
                [currentPole.Id, largerPole.Id]));
            largerConnectionId = largerConnection.Id;
        }
        return (drawing, ringSwitch, new PoleSetup(poleSwitch, smallerConnection.Id,
            largerConnectionId));
    }

    private sealed record PoleSetup(SwitchDevice Switch, Guid SmallerConnection,
        Guid? LargerConnection);
}
