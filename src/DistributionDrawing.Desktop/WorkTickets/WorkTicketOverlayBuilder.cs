using System.Windows.Media;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Desktop.WorkTickets;

internal sealed record WorkTicketRangeOwner(Guid ProjectId, Guid? TicketId)
{
    public bool Matches(Guid? projectId, Guid? ticketId) =>
        projectId == ProjectId && ticketId == TicketId;
}

internal static class WorkRangeCanvasActivation
{
    public static bool ActivateOrdinaryObject(
        TicketRangePickMode pickerMode,
        SelectionReference? target,
        Action showInspector)
    {
        ArgumentNullException.ThrowIfNull(showInspector);
        if (pickerMode != TicketRangePickMode.Idle || target is null) return false;
        showInspector();
        return true;
    }
}

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

    public static IReadOnlyList<SceneElement> BuildBoundarySelection(
        SelectionHitTestIndex index, WorkTicketRangeOwner? owner,
        Guid? currentProjectId, Guid? currentTicketId,
        IReadOnlyList<IsolationBoundary?> boundaries)
    {
        if (owner is null || !owner.Matches(currentProjectId, currentTicketId)) return [];
        List<SceneElement> elements = [];
        for (int slot = 0; slot < boundaries.Count; slot++)
        {
            if (boundaries[slot] is not { } boundary) continue;
            SelectionReference reference = new(SelectionTargetKind.Device, boundary.DeviceId);
            SelectionHitTestEntry? entry = index.FindAll(reference).FirstOrDefault();
            if (entry is null) continue;
            DocumentRect bounds = entry.Bounds;
            elements.Add(new SceneRectangle(new DocumentRect(bounds.XMillimeters - 3,
                bounds.YMillimeters - 3, bounds.WidthMillimeters + 6, bounds.HeightMillimeters + 6),
                Colors.SteelBlue, 1.8));
            elements.Add(new SceneText(new DocumentPoint(bounds.XMillimeters - 3,
                bounds.YMillimeters - 3), $"[{WorkTicketRangeSetup.SlotName(slot)}]",
                Colors.SteelBlue, 4.5));
        }
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
