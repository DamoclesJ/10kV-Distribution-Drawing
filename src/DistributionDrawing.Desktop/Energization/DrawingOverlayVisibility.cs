namespace DistributionDrawing.Desktop.Energization;

public sealed record DrawingOverlayVisibility(bool ShowEnergization, bool ShowWorkTicket)
{
    public static DrawingOverlayVisibility Resolve(
        bool drawingVisible,
        bool eaPanelOpen,
        bool eaResultCurrent,
        bool energizationDisplayRequested,
        bool workTicketRequested,
        bool workRangeOpen = false)
    {
        bool showEnergization = drawingVisible && eaResultCurrent &&
            energizationDisplayRequested;
        return new DrawingOverlayVisibility(
            showEnergization,
            drawingVisible && !eaPanelOpen && !workRangeOpen &&
            workTicketRequested && !showEnergization);
    }
}
