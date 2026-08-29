using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class SelectionRectangleControllerTests
{
    [Fact]
    public void CompleteReplacesSelectionAndRemovesTransientRectangle()
    {
        var manager = new SelectionManager();
        manager.Select(Target(SelectionTargetKind.Device));
        SelectionReference cabinet = Target(SelectionTargetKind.RingCabinet);
        var controller = new SelectionRectangleController(manager);
        controller.Begin(new DocumentPoint(20, 20));
        controller.Update(new DocumentPoint(0, 0));

        IReadOnlyList<SelectionReference> result = controller.Complete(
            Index(cabinet, new DocumentRect(5, 5, 5, 5)));

        Assert.Equal([cabinet], result);
        Assert.Equal([cabinet], manager.SelectionSet.SelectedReferences);
        Assert.False(controller.IsActive);
        Assert.Empty(controller.CreateOverlayElements());
    }

    [Fact]
    public void ShiftCompleteAddsAndMakesLastNewTargetPrimary()
    {
        var manager = new SelectionManager();
        SelectionReference existing = Target(SelectionTargetKind.Device);
        SelectionReference cable = Target(SelectionTargetKind.CableSegment);
        manager.Select(existing);
        var controller = new SelectionRectangleController(manager);
        controller.Begin(new DocumentPoint(0, 0), addToSelection: true);
        controller.Update(new DocumentPoint(20, 20));

        controller.Complete(Index(cable, new DocumentRect(5, 5, 5, 5)));

        Assert.Equal([existing, cable], manager.SelectionSet.SelectedReferences);
        Assert.Equal(cable, manager.Selected);
    }

    [Fact]
    public void CancelKeepsOriginalSelectionAndClearsOverlay()
    {
        var manager = new SelectionManager();
        SelectionReference original = Target(SelectionTargetKind.Device);
        manager.Select(original);
        var controller = new SelectionRectangleController(manager);
        controller.Begin(new DocumentPoint(0, 0));
        controller.Update(new DocumentPoint(20, 20));

        Assert.Single(controller.CreateOverlayElements());
        Assert.True(controller.Cancel());

        Assert.Equal(original, manager.Selected);
        Assert.False(controller.IsActive);
        Assert.Empty(controller.CreateOverlayElements());
    }

    private static SelectionHitTestIndex Index(
        SelectionReference target,
        DocumentRect bounds) => new([new SelectionHitTestEntry(target, bounds, 10)]);

    private static SelectionReference Target(SelectionTargetKind kind) =>
        new(kind, Guid.NewGuid());
}
