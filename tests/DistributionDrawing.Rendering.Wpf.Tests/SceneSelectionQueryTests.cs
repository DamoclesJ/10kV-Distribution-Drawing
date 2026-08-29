using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class SceneSelectionQueryTests
{
    [Fact]
    public void RectangleSelectsDeviceBoundsAndExactCableAndOverheadSegments()
    {
        SelectionReference pole = Target(SelectionTargetKind.Device);
        SelectionReference cable = Target(SelectionTargetKind.CableSegment);
        SelectionReference overhead = Target(SelectionTargetKind.Connection);
        var index = new SelectionHitTestIndex(
        [
            Entry(pole, new DocumentRect(1, 1, 4, 4)),
            Segment(cable, new DocumentPoint(-20, 8), new DocumentPoint(20, 8)),
            Segment(overhead, new DocumentPoint(8, -20), new DocumentPoint(8, 20))
        ]);

        IReadOnlyList<SelectionReference> result = new SceneSelectionQuery()
            .QueryRectangle(index, new DocumentRect(0, 0, 10, 10));

        Assert.Equal([pole, cable, overhead], result);
    }

    [Fact]
    public void SegmentBoundingBoxDoesNotSelectRouteThatDoesNotCrossRectangle()
    {
        SelectionReference cable = Target(SelectionTargetKind.CableSegment);
        var index = new SelectionHitTestIndex(
        [
            new SelectionHitTestEntry(
                cable,
                new DocumentRect(0, 0, 100, 100),
                30,
                new DocumentPoint(0, 100),
                new DocumentPoint(100, 100))
        ]);

        IReadOnlyList<SelectionReference> result = new SceneSelectionQuery()
            .QueryRectangle(index, new DocumentRect(40, 40, 20, 20));

        Assert.Empty(result);
    }

    [Fact]
    public void MultipleSceneEntriesMapToOneStableSelectionReference()
    {
        SelectionReference device = Target(SelectionTargetKind.Device);
        var index = new SelectionHitTestIndex(
        [
            Entry(device, new DocumentRect(0, 0, 4, 4)),
            Entry(device, new DocumentRect(5, 0, 4, 4)),
            Entry(device with { ParentId = Guid.NewGuid() }, new DocumentRect(10, 0, 4, 4))
        ]);

        IReadOnlyList<SelectionReference> result = new SceneSelectionQuery()
            .QueryRectangle(index, new DocumentRect(-1, -1, 20, 10));

        Assert.Equal([device], result);
    }

    [Fact]
    public void SelectAllUsesFormalHitTestProjectionAndExcludesTerminalHelpers()
    {
        SelectionReference cabinet = Target(SelectionTargetKind.RingCabinet);
        SelectionReference cable = Target(SelectionTargetKind.CableSegment);
        SelectionReference terminal = Target(SelectionTargetKind.Terminal);
        SelectionReference joint = Target(SelectionTargetKind.IntermediateTerminal);
        var index = new SelectionHitTestIndex(
        [
            Entry(cabinet, new DocumentRect(0, 0, 10, 10)),
            Entry(cable, new DocumentRect(20, 0, 10, 10)),
            Entry(terminal, new DocumentRect(40, 0, 2, 2)),
            Entry(joint, new DocumentRect(50, 0, 2, 2))
        ]);

        IReadOnlyList<SelectionReference> result = new SceneSelectionQuery().SelectAll(index);

        Assert.Equal([cabinet, cable], result);
    }

    private static SelectionReference Target(SelectionTargetKind kind) =>
        new(kind, Guid.NewGuid());

    private static SelectionHitTestEntry Entry(
        SelectionReference target,
        DocumentRect bounds) => new(target, bounds, 10);

    private static SelectionHitTestEntry Segment(
        SelectionReference target,
        DocumentPoint start,
        DocumentPoint end) => new(
            target,
            new DocumentRect(
                Math.Min(start.XMillimeters, end.XMillimeters) - 1,
                Math.Min(start.YMillimeters, end.YMillimeters) - 1,
                Math.Abs(end.XMillimeters - start.XMillimeters) + 2,
                Math.Abs(end.YMillimeters - start.YMillimeters) + 2),
            10,
            start,
            end);
}
