using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class CableRouteDragControllerTests
{
    [Fact]
    public void DragMiddleHorizontalSegment_CommitsAndSupportsUndoRedo()
    {
        Guid cableId = Guid.NewGuid();
        var target = new SelectionReference(SelectionTargetKind.CableSegment, cableId);
        SelectionHitTestEntry[] segments =
        [
            Segment(target, new DocumentPoint(0, 0), new DocumentPoint(0, 50)),
            Segment(target, new DocumentPoint(0, 50), new DocumentPoint(100, 50)),
            Segment(target, new DocumentPoint(100, 50), new DocumentPoint(100, 0))
        ];
        var layout = new RuntimeLayoutDocument(new DrawingLayout(), new Dictionary<Guid, RingCabinetLayout>());
        var controller = new CableRouteDragController();

        Assert.True(controller.TryBeginDrag(segments[1], segments, layout));
        Assert.True(controller.UpdatePreview(new DocumentPoint(40, 80)));
        ICommand command = Assert.IsAssignableFrom<ICommand>(controller.Commit());
        var stack = new CommandStack();
        stack.ExecuteCommand(command);

        Assert.Equal(80, layout.CableRouteGuides[cableId].HorizontalYMillimeters);
        Assert.True(stack.Undo());
        Assert.DoesNotContain(cableId, layout.CableRouteGuides.Keys);
        Assert.True(stack.Redo());
        Assert.Equal(80, layout.CableRouteGuides[cableId].HorizontalYMillimeters);
    }

    [Fact]
    public void TerminalSegmentsCannotBeDragged()
    {
        Guid cableId = Guid.NewGuid();
        var target = new SelectionReference(SelectionTargetKind.CableSegment, cableId);
        SelectionHitTestEntry[] segments =
        [
            Segment(target, new DocumentPoint(0, 0), new DocumentPoint(50, 0)),
            Segment(target, new DocumentPoint(50, 0), new DocumentPoint(50, 50)),
            Segment(target, new DocumentPoint(50, 50), new DocumentPoint(100, 50))
        ];
        var layout = new RuntimeLayoutDocument(new DrawingLayout(), new Dictionary<Guid, RingCabinetLayout>());
        var controller = new CableRouteDragController();

        Assert.False(controller.TryBeginDrag(segments[0], segments, layout));
        Assert.False(controller.TryBeginDrag(segments[2], segments, layout));
        Assert.Empty(layout.CableRouteGuides);
    }

    private static SelectionHitTestEntry Segment(
        SelectionReference target,
        DocumentPoint start,
        DocumentPoint end)
    {
        return new SelectionHitTestEntry(
            target,
            new DocumentRect(
                Math.Min(start.XMillimeters, end.XMillimeters) - 1,
                Math.Min(start.YMillimeters, end.YMillimeters) - 1,
                Math.Abs(end.XMillimeters - start.XMillimeters) + 2,
                Math.Abs(end.YMillimeters - start.YMillimeters) + 2),
            30,
            start,
            end);
    }
}
