using System.Windows.Media;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Desktop.WorkTickets;

// Presentation only: colors explicit ticket setup facts; it never derives electrical state.
internal static class WorkTicketOverlayBuilder
{
    public static IReadOnlyList<SceneElement> Build(SelectionHitTestIndex index, WorkTicketSession? ticket)
    {
        if (ticket is null) return [];
        List<SceneElement> elements = [];
        foreach (IsolationBoundary boundary in ticket.IsolationBoundaries)
            Add(elements, index, new SelectionReference(SelectionTargetKind.Device, boundary.DeviceId), Colors.SteelBlue);
        foreach (Guid id in ticket.GroundingPointIds)
            Add(elements, index, new SelectionReference(SelectionTargetKind.GroundingPoint, id), Colors.MidnightBlue);
        foreach (UserTicketFact fact in ticket.UserFacts.Where(item => item.Kind == "RetainedLive" && item.Confirmed))
            foreach (TicketReference reference in fact.References)
                if (ToSelection(reference) is { } selected) Add(elements, index, selected, Colors.Firebrick);
        foreach (RetainedLivePart part in ticket.Analysis?.RetainedLiveParts ?? [])
            if (part.Origin == FactOrigin.ModelFact)
                foreach (TicketReference reference in part.References)
                    if (ToSelection(reference) is { } selected) Add(elements, index, selected, Colors.Firebrick);
        return elements;
    }

    internal static SelectionReference? ToSelection(TicketReference reference) => reference.Kind switch
    {
        TicketReferenceKind.Device => new SelectionReference(SelectionTargetKind.Device, reference.Id),
        TicketReferenceKind.Terminal => new SelectionReference(SelectionTargetKind.Terminal, reference.Id),
        TicketReferenceKind.Connection => new SelectionReference(SelectionTargetKind.Connection, reference.Id),
        TicketReferenceKind.RingInterval => new SelectionReference(SelectionTargetKind.RingCabinetInterval, reference.Id),
        TicketReferenceKind.GroundingPoint => new SelectionReference(SelectionTargetKind.GroundingPoint, reference.Id),
        TicketReferenceKind.GroundingAccessPoint => new SelectionReference(SelectionTargetKind.GroundingAccessPoint, reference.Id),
        TicketReferenceKind.WorkScope => new SelectionReference(SelectionTargetKind.WorkScope, reference.Id),
        _ => null
    };

    private static void Add(List<SceneElement> elements, SelectionHitTestIndex index,
        SelectionReference reference, Color color)
    {
        foreach (SelectionHitTestEntry entry in index.FindAll(reference))
        {
            DocumentRect bounds = entry.Bounds;
            elements.Add(new SceneRectangle(new DocumentRect(bounds.XMillimeters - 2,
                bounds.YMillimeters - 2, bounds.WidthMillimeters + 4, bounds.HeightMillimeters + 4),
                color, 1.5));
        }
    }
}
