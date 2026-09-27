using DistributionDrawing.Desktop.WorkTickets;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Rendering.Wpf.Interaction;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class WorkRangeCanvasActivationTests
{
    [Fact]
    public void ReclickingSelectedObjectRestoresInspectorWithoutChangingSelectionOrCommittingRange()
    {
        var selections = new SelectionManager();
        SelectionReference target = new(SelectionTargetKind.Device, Guid.NewGuid());
        selections.Select(target);
        int selectionChanges = 0;
        selections.SelectionChanged += (_, _) => selectionChanges++;

        bool rangePanelVisible = true;
        bool inspectorVisible = false;
        var pending = new TicketBoundarySlotCollection();
        IsolationBoundary boundary = new(Guid.NewGuid(), BoundarySide.Line, Guid.NewGuid());
        pending.Load([boundary], item => item);
        TicketBoundarySlot[] pendingBeforeClick = pending.Slots.ToArray();
        var commandStack = new CommandStack();
        bool activated = WorkRangeCanvasActivation.ActivateOrdinaryObject(
            TicketRangePickMode.Idle,
            target,
            () =>
            {
                rangePanelVisible = false;
                inspectorVisible = true;
            });
        selections.Select(target);

        Assert.True(activated);
        Assert.False(rangePanelVisible);
        Assert.True(inspectorVisible);
        Assert.Equal(target, selections.Selected);
        Assert.Equal(0, selectionChanges);
        Assert.Equal(pendingBeforeClick, pending.Slots);
        Assert.False(commandStack.CanUndo);
    }

    [Fact]
    public void BoundaryPickerActivationDoesNotRestoreInspector()
    {
        foreach (TicketRangePickMode pickerMode in new[]
                 {
                     TicketRangePickMode.PickingBoundaryDevice,
                     TicketRangePickMode.ChoosingBoundarySide
                 })
        {
            bool rangePanelVisible = true;
            bool inspectorVisible = false;

            bool activated = WorkRangeCanvasActivation.ActivateOrdinaryObject(
                pickerMode,
                new SelectionReference(SelectionTargetKind.Device, Guid.NewGuid()),
                () =>
                {
                    rangePanelVisible = false;
                    inspectorVisible = true;
                });

            Assert.False(activated);
            Assert.True(rangePanelVisible);
            Assert.False(inspectorVisible);
        }
    }
}
