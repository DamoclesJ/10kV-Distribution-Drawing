namespace DistributionDrawing.Desktop.WorkTickets;

internal sealed class WorkTicketRangeNavigationState
{
    private bool _preservePendingEditsOnReturn;

    public void RangeOpenedFromTicketWorkspace() => _preservePendingEditsOnReturn = true;

    public bool ConsumePendingEditsOnReturn()
    {
        bool preserve = _preservePendingEditsOnReturn;
        _preservePendingEditsOnReturn = false;
        return preserve;
    }

    public bool ReturnToTicketWorkspace(Action commitPendingEdits, Action refresh)
    {
        if (ConsumePendingEditsOnReturn()) return false;
        commitPendingEdits();
        refresh();
        return true;
    }

    public void RangeConfirmed() => _preservePendingEditsOnReturn = false;

    public void Reset() => _preservePendingEditsOnReturn = false;
}
