using DistributionDrawing.Application.WorkTickets;

namespace DistributionDrawing.Desktop.WorkTickets;

internal enum TicketRangePickMode
{
    Idle,
    PickingBoundaryDevice,
    ChoosingBoundarySide,
    PickingWorkScopeEquipment
}

internal sealed record TicketBoundarySlot(Guid? DeviceId = null, BoundarySide? Side = null,
    IsolationBoundary? Resolved = null);

internal sealed class TicketBoundarySlotCollection
{
    private readonly List<TicketBoundarySlot> _slots = [];

    public IReadOnlyList<TicketBoundarySlot> Slots => _slots;
    public int Count => _slots.Count;
    public TicketBoundarySlot this[int index] => _slots[index];

    public void Load(IEnumerable<IsolationBoundary> boundaries,
        Func<IsolationBoundary, IsolationBoundary?> resolve)
    {
        _slots.Clear();
        _slots.AddRange(boundaries.Select(boundary => new TicketBoundarySlot(
            boundary.DeviceId, boundary.Side, resolve(boundary))));
        if (_slots.Count == 0) _slots.AddRange([new(), new()]);
    }

    public void Add() => _slots.Add(new TicketBoundarySlot());

    public bool Remove(int index)
    {
        if (_slots.Count <= 1 || index < 0 || index >= _slots.Count) return false;
        _slots.RemoveAt(index);
        return true;
    }

    public void Discard() => _slots.Clear();

    public void Replace(int index, TicketBoundarySlot value) => _slots[index] = value;
}

internal sealed class TicketRangePickerState
{
    public TicketRangePickMode Mode { get; private set; }
    public int? BoundaryIndex { get; private set; }
    public TicketBoundarySlot? PreviousBoundary { get; private set; }

    public void BeginBoundary(int index, TicketBoundarySlot current)
    {
        Mode = TicketRangePickMode.PickingBoundaryDevice;
        BoundaryIndex = index;
        PreviousBoundary = current;
    }

    public void DevicePicked()
    {
        if (Mode == TicketRangePickMode.PickingBoundaryDevice)
            Mode = TicketRangePickMode.ChoosingBoundarySide;
    }

    public void SideChosen()
    {
        if (Mode == TicketRangePickMode.ChoosingBoundarySide) Reset();
    }

    public void BeginEquipment()
    {
        Reset();
        Mode = TicketRangePickMode.PickingWorkScopeEquipment;
    }

    public (int Index, TicketBoundarySlot Previous)? Cancel()
    {
        (int Index, TicketBoundarySlot Previous)? previous =
            (Mode is TicketRangePickMode.PickingBoundaryDevice or TicketRangePickMode.ChoosingBoundarySide) &&
            BoundaryIndex is int index && PreviousBoundary is not null
                ? (index, PreviousBoundary) : null;
        Reset();
        return previous;
    }

    private void Reset()
    {
        Mode = TicketRangePickMode.Idle;
        BoundaryIndex = null;
        PreviousBoundary = null;
    }
}
