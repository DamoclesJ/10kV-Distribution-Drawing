using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Application.Devices;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Desktop.WorkTickets;
using DistributionDrawing.Desktop;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Scene;
using System.Windows.Media;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.PropertyInspector;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Rendering;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class TicketRangeBoundaryCompletionTests
{
    [Fact]
    public void ConfirmedRetainedLiveTicketFactIsTheRedRectangleOverlaySource()
    {
        Guid deviceId = Guid.NewGuid();
        var index = new SelectionHitTestIndex([
            new SelectionHitTestEntry(new SelectionReference(SelectionTargetKind.Device, deviceId),
                new DocumentRect(10, 20, 30, 12), 1)
        ]);
        WorkTicketSession ticket = WorkTicketSession.Create() with
        {
            UserFacts = [new UserTicketFact("RetainedLive", "邻近带电设备",
                [new TicketReference(TicketReferenceKind.Device, deviceId)], true)]
        };

        SceneRectangle overlay = Assert.IsType<SceneRectangle>(Assert.Single(
            WorkTicketOverlayBuilder.Build(index, ticket)));

        Assert.Equal(Colors.Firebrick, overlay.Stroke);
    }

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

    [Theory]
    [InlineData("P01", "P02")]
    [InlineData("P1", "P2")]
    [InlineData("P9", "P10")]
    [InlineData("新11", "新12")]
    [InlineData("新1500001", "新1500002")]
    [InlineData("P-01#", "P-02#")]
    public void PoleSideComparesSharedPrefixAndTrailingDigitsNumerically(
        string smallerPoleNumber, string currentPoleNumber)
    {
        (DrawingDocument drawing, _, PoleSetup pole) = CreateDrawing(connectLarger: false,
            smallerPoleNumber, currentPoleNumber);

        Assert.True(WorkTicketRangeSetup.TryResolve(drawing, pole.Switch.Id,
            BoundarySide.SmallerNumber, out IsolationBoundary? boundary, out string issue), issue);
        Assert.Equal(pole.Switch.FirstTerminalId, boundary!.TerminalId);
        Assert.Equal(pole.SmallerConnection, boundary.ConnectionId);
        Assert.False(WorkTicketRangeSetup.TryResolve(drawing, pole.Switch.Id,
            BoundarySide.LargerNumber, out _, out _));
    }

    [Fact]
    public void NumberedThreePoleChainResolvesEndAndMiddlePoleDirectionsFromTopology()
    {
        (DrawingDocument drawing, SwitchDevice firstSwitch, SwitchDevice middleSwitch,
            SwitchDevice lastSwitch, Guid firstMiddleConnection, Guid middleLastConnection) =
            CreateNumberedPoleChain("新11", "新1500001", "新1500002");

        Assert.True(WorkTicketRangeSetup.TryResolve(drawing, lastSwitch.Id,
            BoundarySide.SmallerNumber, out IsolationBoundary? lastTowardMiddle, out string issue), issue);
        Assert.Equal(lastSwitch.FirstTerminalId, lastTowardMiddle!.TerminalId);
        Assert.Equal(middleLastConnection, lastTowardMiddle.ConnectionId);

        Assert.True(WorkTicketRangeSetup.TryResolve(drawing, firstSwitch.Id,
            BoundarySide.LargerNumber, out IsolationBoundary? firstTowardMiddle, out issue), issue);
        Assert.Equal(firstSwitch.SecondTerminalId, firstTowardMiddle!.TerminalId);
        Assert.Equal(firstMiddleConnection, firstTowardMiddle.ConnectionId);

        Assert.True(WorkTicketRangeSetup.TryResolve(drawing, middleSwitch.Id,
            BoundarySide.SmallerNumber, out IsolationBoundary? middleTowardFirst, out issue), issue);
        Assert.Equal(firstMiddleConnection, middleTowardFirst!.ConnectionId);
        Assert.True(WorkTicketRangeSetup.TryResolve(drawing, middleSwitch.Id,
            BoundarySide.LargerNumber, out IsolationBoundary? middleTowardLast, out issue), issue);
        Assert.Equal(middleLastConnection, middleTowardLast!.ConnectionId);
    }

    [Theory]
    [InlineData("A01", "B02", "C03")]
    [InlineData("P1-1", "P1-2", "P1-3")]
    [InlineData("P9", "P-10", "P11")]
    public void AmbiguousPoleNumberPrefixesOrSegmentsLeaveBothSidesUnresolved(
        string smallerPoleNumber, string currentPoleNumber, string largerPoleNumber)
    {
        (DrawingDocument drawing, _, PoleSetup pole) = CreateDrawing(
            connectLarger: true, smallerPoleNumber, currentPoleNumber, largerPoleNumber);

        Assert.False(WorkTicketRangeSetup.TryResolve(drawing, pole.Switch.Id,
            BoundarySide.SmallerNumber, out _, out _));
        Assert.False(WorkTicketRangeSetup.TryResolve(drawing, pole.Switch.Id,
            BoundarySide.LargerNumber, out _, out _));
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
            DrawingRightPanelMode.WorkRange, normalTarget,
            () => inspectorTarget = normalTarget);

        Assert.True(activated);
        Assert.Equal(normalTarget, inspectorTarget);
        Assert.Equal(TicketRangePickMode.Idle, picker.Mode);
        Assert.Equal(pole.Switch.Id, slots[1].DeviceId);
        Assert.NotNull(slots[1].Resolved);
    }

    [Fact]
    public void PoleSwitchSelectionStillTargetsTheActualSwitchSymbol()
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

        IReadOnlyList<SceneElement> selectionOverlayBefore = SelectionOverlayBuilder.CreateElements(
            scene.HitTestIndex, switchTarget.Target);
        SelectionReference poleTarget = new(SelectionTargetKind.Device, aggregate.Pole.Id);
        IReadOnlyList<SceneElement> selectionOverlayAfter = SelectionOverlayBuilder.CreateElements(
            scene.HitTestIndex, poleTarget);

        Assert.NotEqual(selectionOverlayBefore, selectionOverlayAfter);
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
        bool connectLarger = true, string smallerPoleNumber = "P01",
        string currentPoleNumber = "P02", string largerPoleNumber = "P03")
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "Ring + Pole Boundary");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "一号柜",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Closed, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Closed, SwitchState.Open)]));
        drawing.AddDevice(cabinet);
        SwitchDevice ringSwitch = GetMainLoadSwitch(cabinet.Intervals[0]);

        Pole smallerPole = new(Guid.NewGuid(), smallerPoleNumber);
        Pole currentPole = new(Guid.NewGuid(), currentPoleNumber);
        Pole largerPole = new(Guid.NewGuid(), largerPoleNumber);
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

    private static (DrawingDocument Drawing, SwitchDevice FirstSwitch,
        SwitchDevice MiddleSwitch, SwitchDevice LastSwitch,
        Guid FirstMiddleConnection, Guid MiddleLastConnection) CreateNumberedPoleChain(
            string firstNumber, string middleNumber, string lastNumber)
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "编号柱上开关拓扑");
        Pole firstPole = new(Guid.NewGuid(), firstNumber);
        Pole middlePole = new(Guid.NewGuid(), middleNumber);
        Pole lastPole = new(Guid.NewGuid(), lastNumber);
        drawing.AddDevice(firstPole);
        drawing.AddDevice(middlePole);
        drawing.AddDevice(lastPole);

        SwitchDevice firstSwitch = AddPoleSwitch(drawing, firstPole);
        SwitchDevice middleSwitch = AddPoleSwitch(drawing, middlePole);
        SwitchDevice lastSwitch = AddPoleSwitch(drawing, lastPole);
        Guid firstMiddleConnection = AddOverheadConnection(drawing,
            firstSwitch.SecondTerminalId, middleSwitch.FirstTerminalId,
            [firstPole.Id, middlePole.Id], $"{firstNumber}-{middleNumber}");
        Guid middleLastConnection = AddOverheadConnection(drawing,
            middleSwitch.SecondTerminalId, lastSwitch.FirstTerminalId,
            [middlePole.Id, lastPole.Id], $"{middleNumber}-{lastNumber}");

        return (drawing, firstSwitch, middleSwitch, lastSwitch,
            firstMiddleConnection, middleLastConnection);
    }

    private static SwitchDevice AddPoleSwitch(DrawingDocument drawing, Pole pole)
    {
        SwitchDevice switchDevice = SwitchDevice.CreateForPole(Guid.NewGuid(),
            SwitchKind.IsolationSwitch, Guid.NewGuid(), Guid.NewGuid(),
            displayName: $"{pole.PoleNumber}隔离刀闸");
        drawing.AddDevice(switchDevice);
        drawing.AddTerminal(new Terminal(switchDevice.FirstTerminalId, TopologyOwnerType.Device,
            switchDevice.Id, "SwitchLeftTerminal", "10kV", true, true, null,
            [ConnectionType.OverheadLine]));
        drawing.AddTerminal(new Terminal(switchDevice.SecondTerminalId, TopologyOwnerType.Device,
            switchDevice.Id, "SwitchRightTerminal", "10kV", true, false, null,
            [ConnectionType.OverheadLine]));
        drawing.AddPoleAttachment(new PoleAttachment(Guid.NewGuid(), pole.Id, switchDevice.Id));
        return switchDevice;
    }

    private static Guid AddOverheadConnection(DrawingDocument drawing, Guid startTerminalId,
        Guid endTerminalId, IReadOnlyList<Guid> supportPoleIds, string displayName)
    {
        var connection = new Connection(Guid.NewGuid(), ConnectionType.OverheadLine,
            startTerminalId, endTerminalId, displayName, "10kV");
        drawing.AddConnection(connection);
        drawing.AddOverheadLine(new OverheadLine(connection.Id, "JKLYJ", supportPoleIds));
        return connection.Id;
    }

    private sealed record PoleSetup(SwitchDevice Switch, Guid SmallerConnection,
        Guid? LargerConnection);
}
