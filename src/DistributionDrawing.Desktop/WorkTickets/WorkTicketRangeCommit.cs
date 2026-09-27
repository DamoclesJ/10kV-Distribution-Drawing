using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Interaction;

namespace DistributionDrawing.Desktop.WorkTickets;

internal static class WorkTicketRangeCommit
{
    public static WorkTicketSession Apply(DrawingDocument drawing, WorkTicketDataRoot tickets,
        CommandStack commandStack, Guid? ticketId, IReadOnlyList<IsolationBoundary?> boundaries,
        IReadOnlyList<WorkScopeItem> workScopes, WorkTask task)
    {
        WorkTicketSession? before = tickets.Selected(ticketId);
        WorkTicketSession seed = before ?? WorkTicketSession.Create();
        WorkTicketSession after = WorkTicketRangeSetup.Confirm(drawing, seed, boundaries, workScopes);
        if (after.Task != task) after = (after with { Task = task }).Invalidate();
        if (before is null || !ReferenceEquals(before, after))
            commandStack.ExecuteCommand(new WorkTicketChangeCommand(tickets, before, after));
        return after;
    }
}
