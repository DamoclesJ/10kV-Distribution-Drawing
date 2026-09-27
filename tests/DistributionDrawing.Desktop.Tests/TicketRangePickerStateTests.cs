using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Desktop.WorkTickets;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class TicketRangePickerStateTests
{
    [Fact]
    public void BoundaryDevicePickMovesToSideChoiceAndCancelRestoresOriginalSlot()
    {
        var state = new TicketRangePickerState();
        TicketBoundarySlot original = new(Guid.NewGuid(), BoundarySide.Line,
            new IsolationBoundary(Guid.NewGuid(), BoundarySide.Line, Guid.NewGuid()));
        state.BeginBoundary(1, original);

        Assert.Equal(TicketRangePickMode.PickingBoundaryDevice, state.Mode);
        Assert.Equal(1, state.BoundaryIndex);
        state.DevicePicked();
        Assert.Equal(TicketRangePickMode.ChoosingBoundarySide, state.Mode);

        // A repeated canvas-device event cannot advance or restart device picking.
        state.DevicePicked();
        Assert.Equal(TicketRangePickMode.ChoosingBoundarySide, state.Mode);
        Assert.Equal(original, state.Cancel()!.Value.Previous);
        Assert.Equal(TicketRangePickMode.Idle, state.Mode);
        Assert.Null(state.BoundaryIndex);
    }

    [Fact]
    public void EquipmentPickerIsExclusiveAndSuccessfulSideChoiceReturnsToIdle()
    {
        var state = new TicketRangePickerState();
        state.BeginBoundary(0, new TicketBoundarySlot());
        state.DevicePicked();
        state.SideChosen();
        Assert.Equal(TicketRangePickMode.Idle, state.Mode);

        state.BeginEquipment();
        Assert.Equal(TicketRangePickMode.PickingWorkScopeEquipment, state.Mode);
        Assert.Null(state.BoundaryIndex);
        Assert.Null(state.Cancel());
        Assert.Equal(TicketRangePickMode.Idle, state.Mode);
    }

    [Fact]
    public void BoundaryLabelsArePresentationOnlyAndSupportAdditionalSlots()
    {
        Assert.Equal("A", WorkTicketRangeSetup.SlotName(0));
        Assert.Equal("B", WorkTicketRangeSetup.SlotName(1));
        Assert.Equal("C", WorkTicketRangeSetup.SlotName(2));
        Assert.Equal("AA", WorkTicketRangeSetup.SlotName(26));
    }

    [Fact]
    public void SlotBufferStartsWithABSupportsCAndRemovesWithoutDroppingLastSlot()
    {
        var slots = new TicketBoundarySlotCollection();
        slots.Load([], _ => null);
        Assert.Equal(2, slots.Count);
        Assert.Equal("A", WorkTicketRangeSetup.SlotName(0));
        Assert.Equal("B", WorkTicketRangeSetup.SlotName(1));

        slots.Add();
        Assert.Equal(3, slots.Count);
        Assert.Equal("C", WorkTicketRangeSetup.SlotName(2));
        Assert.True(slots.Remove(1));
        Assert.Equal(2, slots.Count);
        Assert.True(slots.Remove(0));
        Assert.False(slots.Remove(0));
        Assert.Single(slots.Slots);
    }
}
