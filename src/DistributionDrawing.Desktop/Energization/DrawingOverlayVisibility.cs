namespace DistributionDrawing.Desktop.Energization;

public sealed record DrawingOverlayVisibility(bool ShowEnergization, bool ShowWorkTicket)
{
    public static DrawingOverlayVisibility Resolve(
        bool drawingVisible, bool eaPanelOpen, bool eaResultCurrent,
        bool workTicketRequested) => new(
            drawingVisible && eaPanelOpen && eaResultCurrent,
            drawingVisible && !eaPanelOpen && workTicketRequested);
}
