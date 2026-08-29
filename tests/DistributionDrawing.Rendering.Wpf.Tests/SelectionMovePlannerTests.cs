using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class SelectionMovePlannerTests
{
    [Fact]
    public void Create_FoldsPoleAttachmentAndAttachedDeviceIntoSelectedPole()
    {
        PlannerFixture fixture = CreateFixture();
        SelectionSet selection = SelectionSet.Create([
            Device(fixture.Pole.Id),
            Attachment(fixture.Attachment),
            Device(fixture.Switch.Id)
        ], Device(fixture.Switch.Id));

        SelectionMovePlan plan = new SelectionMovePlanner().Create(
            selection,
            Device(fixture.Switch.Id),
            fixture.Document,
            fixture.Layout);

        SelectionMoveRoot root = Assert.Single(plan.Roots);
        Assert.Equal(SelectionMoveRootKind.Pole, root.Kind);
        Assert.Equal(fixture.Pole.Id, root.ObjectId);
        Assert.Same(root, plan.DragAnchorRoot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_MapsAttachmentOrAttachedSwitchToAttachmentRoot(bool selectDevice)
    {
        PlannerFixture fixture = CreateFixture();
        SelectionReference reference = selectDevice
            ? Device(fixture.Switch.Id)
            : Attachment(fixture.Attachment);

        SelectionMovePlan plan = new SelectionMovePlanner().Create(
            SelectionSet.Create([reference]),
            reference,
            fixture.Document,
            fixture.Layout);

        SelectionMoveRoot root = Assert.Single(plan.Roots);
        Assert.Equal(SelectionMoveRootKind.PoleAttachment, root.Kind);
        Assert.Equal(fixture.Attachment.AttachmentId, root.ObjectId);
        Assert.Equal(fixture.Pole.Id, root.ParentPoleId);
    }

    [Fact]
    public void Create_FoldsRingCabinetIntervalIntoSelectedCabinet()
    {
        PlannerFixture fixture = CreateFixture(includeCabinet: true);
        SelectionReference cabinet = new(
            SelectionTargetKind.RingCabinet,
            fixture.CabinetId!.Value);
        SelectionReference interval = new(
            SelectionTargetKind.RingCabinetInterval,
            Guid.NewGuid(),
            fixture.CabinetId);

        SelectionMovePlan plan = new SelectionMovePlanner().Create(
            SelectionSet.Create([cabinet, interval], interval),
            interval,
            fixture.Document,
            fixture.Layout);

        SelectionMoveRoot root = Assert.Single(plan.Roots);
        Assert.Equal(SelectionMoveRootKind.RingCabinet, root.Kind);
        Assert.Equal(fixture.CabinetId, root.ObjectId);
        Assert.Same(root, plan.DragAnchorRoot);
    }

    [Fact]
    public void Create_LineOnlySelectionHasNoMoveRoots()
    {
        PlannerFixture fixture = CreateFixture();
        SelectionReference connection = new(
            SelectionTargetKind.Connection,
            Guid.NewGuid());
        SelectionReference cable = new(
            SelectionTargetKind.CableSegment,
            Guid.NewGuid());

        SelectionMovePlan plan = new SelectionMovePlanner().Create(
            SelectionSet.Create([connection, cable]),
            connection,
            fixture.Document,
            fixture.Layout);

        Assert.False(plan.CanMove);
        Assert.Empty(plan.Roots);
    }

    [Fact]
    public void Create_ClickedLineUsesPrimaryMovableRootAsAnchor()
    {
        PlannerFixture fixture = CreateFixture();
        SelectionReference pole = Device(fixture.Pole.Id);
        SelectionReference connection = new(
            SelectionTargetKind.Connection,
            Guid.NewGuid());

        SelectionMovePlan plan = new SelectionMovePlanner().Create(
            SelectionSet.Create([connection, pole], pole),
            connection,
            fixture.Document,
            fixture.Layout);

        Assert.Equal(fixture.Pole.Id, plan.DragAnchorRoot?.ObjectId);
    }

    private static PlannerFixture CreateFixture(bool includeCabinet = false)
    {
        var pole = new Pole(Guid.NewGuid(), "P-1");
        var poleSwitch = SwitchDevice.CreateForPole(
            Guid.NewGuid(),
            SwitchKind.IsolationSwitch,
            Guid.NewGuid(),
            Guid.NewGuid());
        var attachment = new PoleAttachment(
            Guid.NewGuid(),
            pole.Id,
            poleSwitch.Id);
        var document = new DrawingDocument(Guid.NewGuid(), "Group move planner");
        document.AddDevice(pole);
        document.AddDevice(poleSwitch);
        document.AddPoleAttachment(attachment);
        var drawing = new DrawingLayout();
        drawing.Add(new PoleLayout(pole.Id, new DocumentPoint(10, 20)));
        drawing.Add(new AttachmentLayout(
            attachment.AttachmentId,
            new DocumentPoint(15, 0)));
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

        return new PlannerFixture(
            document,
            pole,
            poleSwitch,
            attachment,
            new RuntimeLayoutDocument(drawing, cabinets),
            cabinetId);
    }

    private static SelectionReference Device(Guid id) => new(
        SelectionTargetKind.Device,
        id);

    private static SelectionReference Attachment(PoleAttachment attachment) => new(
        SelectionTargetKind.PoleAttachment,
        attachment.AttachmentId,
        attachment.PoleId);

    private sealed record PlannerFixture(
        DrawingDocument Document,
        Pole Pole,
        SwitchDevice Switch,
        PoleAttachment Attachment,
        RuntimeLayoutDocument Layout,
        Guid? CabinetId);
}
