using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class LayoutSnapServiceTests
{
    [Fact]
    public void Snap_AlignsPoleCentersWithinTolerance()
    {
        Guid movingId = Guid.NewGuid();
        Guid fixedId = Guid.NewGuid();
        var drawingLayout = new DrawingLayout();
        drawingLayout.Add(new PoleLayout(movingId, new DocumentPoint(0, 0)));
        drawingLayout.Add(new PoleLayout(fixedId, new DocumentPoint(50, 40)));
        var runtime = new RuntimeLayoutDocument(drawingLayout, new Dictionary<Guid, RingCabinetLayout>());

        DocumentPoint snapped = new LayoutSnapService().Snap(
            new SelectionReference(SelectionTargetKind.Device, movingId),
            new DocumentPoint(47, 10),
            runtime);

        Assert.Equal(50, snapped.XMillimeters);
        Assert.Equal(10, snapped.YMillimeters);
    }

    [Fact]
    public void Snap_DoesNotChangePositionOutsideTolerance()
    {
        Guid movingId = Guid.NewGuid();
        Guid fixedId = Guid.NewGuid();
        var drawingLayout = new DrawingLayout();
        drawingLayout.Add(new PoleLayout(movingId, new DocumentPoint(0, 0)));
        drawingLayout.Add(new PoleLayout(fixedId, new DocumentPoint(50, 40)));
        var runtime = new RuntimeLayoutDocument(drawingLayout, new Dictionary<Guid, RingCabinetLayout>());
        DocumentPoint candidate = new(44, 10);

        DocumentPoint snapped = new LayoutSnapService().Snap(
            new SelectionReference(SelectionTargetKind.Device, movingId),
            candidate,
            runtime);

        Assert.Equal(candidate, snapped);
    }

    [Fact]
    public void DeviceDrag_SnapsBothAxesAndUndoRedoUseSnappedPosition()
    {
        Guid movingId = Guid.NewGuid();
        Guid fixedId = Guid.NewGuid();
        var drawingLayout = new DrawingLayout();
        drawingLayout.Add(new PoleLayout(movingId, new DocumentPoint(0, 0)));
        drawingLayout.Add(new PoleLayout(fixedId, new DocumentPoint(50, 40)));
        var runtime = new RuntimeLayoutDocument(drawingLayout, new Dictionary<Guid, RingCabinetLayout>());
        var controller = new DeviceDragController();
        SelectionReference target = new(SelectionTargetKind.Device, movingId);

        Assert.True(controller.TryBeginDrag(target, new DocumentPoint(0, 0), runtime));
        Assert.True(controller.UpdatePreview(new DocumentPoint(47, 43)));
        ICommand command = Assert.IsAssignableFrom<ICommand>(controller.Commit());
        Assert.Equal(new DocumentPoint(50, 40), drawingLayout.Poles[movingId].Position);

        command.Undo();
        Assert.Equal(new DocumentPoint(0, 0), drawingLayout.Poles[movingId].Position);
        command.Redo();
        Assert.Equal(new DocumentPoint(50, 40), drawingLayout.Poles[movingId].Position);
    }

    [Fact]
    public void Snap_AlignsRingCabinetCenterWithPoleCenter()
    {
        Guid poleId = Guid.NewGuid();
        Guid cabinetId = Guid.NewGuid();
        var drawingLayout = new DrawingLayout();
        drawingLayout.Add(new PoleLayout(poleId, new DocumentPoint(100, 100)));
        var cabinet = new RingCabinetLayout(
            cabinetId,
            new DocumentPoint(0, 0),
            60,
            100,
            20,
            []);
        var runtime = new RuntimeLayoutDocument(
            drawingLayout,
            new Dictionary<Guid, RingCabinetLayout> { [cabinetId] = cabinet });

        DocumentPoint snapped = new LayoutSnapService().Snap(
            new SelectionReference(SelectionTargetKind.RingCabinet, cabinetId),
            new DocumentPoint(74, 57),
            runtime);

        Assert.Equal(new DocumentPoint(77, 57), snapped);
    }
}
