using DistributionDrawing.Domain.Professional;
using System.Windows.Media;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Desktop.WorkTickets;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class WorkTicketRangeOverlayTests
{
    [Fact]
    public void BoundaryHighlightUsesWholeDeviceHaloAndSlotLabelRegardlessOfSide()
    {
        Guid deviceId = Guid.NewGuid();
        Guid projectId = Guid.NewGuid();
        Guid ticketId = Guid.NewGuid();
        var index = new SelectionHitTestIndex([
            new SelectionHitTestEntry(new SelectionReference(SelectionTargetKind.Device, deviceId),
                new DocumentRect(10, 20, 30, 12), 1)
        ]);

        IReadOnlyList<SceneElement> lineSide = WorkTicketOverlayBuilder.BuildBoundarySelection(index,
            new WorkTicketRangeOwner(projectId, ticketId), projectId, ticketId,
            [new IsolationBoundary(deviceId, BoundarySide.Line)]);
        IReadOnlyList<SceneElement> busSide = WorkTicketOverlayBuilder.BuildBoundarySelection(index,
            new WorkTicketRangeOwner(projectId, ticketId), projectId, ticketId,
            [new IsolationBoundary(deviceId, BoundarySide.Bus)]);

        SceneRectangle halo = Assert.IsType<SceneRectangle>(Assert.Single(lineSide,
            element => element is SceneRectangle));
        SceneText label = Assert.IsType<SceneText>(Assert.Single(lineSide,
            element => element is SceneText));
        Assert.Equal(new DocumentRect(7, 17, 36, 18), halo.Bounds);
        Assert.Equal("[A]", label.Text);
        Assert.Equal(halo, Assert.Single(busSide, element => element is SceneRectangle));
        Assert.Equal(label, Assert.Single(busSide, element => element is SceneText));
        Assert.Equal(Colors.SteelBlue, halo.Stroke);
    }

    [Fact]
    public void PendingBoundaryOverlayIsIsolatedByTicketAndProjectIdentity()
    {
        Guid deviceId = Guid.NewGuid();
        Guid projectA = Guid.NewGuid();
        Guid projectB = Guid.NewGuid();
        Guid ticketA = Guid.NewGuid();
        Guid ticketB = Guid.NewGuid();
        var index = new SelectionHitTestIndex([
            new SelectionHitTestEntry(new SelectionReference(SelectionTargetKind.Device, deviceId),
                new DocumentRect(10, 20, 30, 12), 1)
        ]);
        WorkTicketRangeOwner owner = new(projectA, ticketA);
        IsolationBoundary?[] pending = [new IsolationBoundary(deviceId, BoundarySide.Line)];

        IReadOnlyList<SceneElement> ticketBOverlay = WorkTicketOverlayBuilder.BuildBoundarySelection(
            index, owner, projectA, ticketB, pending);
        IReadOnlyList<SceneElement> otherProject = WorkTicketOverlayBuilder.BuildBoundarySelection(
            index, owner, projectB, ticketA, pending);
        IReadOnlyList<SceneElement> restoredTicketA = WorkTicketOverlayBuilder.BuildBoundarySelection(
            index, owner, projectA, ticketA, pending);

        Assert.Empty(ticketBOverlay);
        Assert.Empty(otherProject);
        Assert.Equal(2, restoredTicketA.Count);
    }
}
