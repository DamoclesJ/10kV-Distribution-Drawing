using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Interaction;

namespace DistributionDrawing.Desktop.DrawingTools;

internal sealed class WorkTicketGuardedDeleteCommand(
    ICommand inner, DrawingDocument drawing, WorkTicketDataRoot tickets) : ICommand
{
    public void Execute() => Run(inner.Execute);
    public void Undo() => inner.Undo();
    public void Redo() => Run(inner.Redo);

    private void Run(Action apply)
    {
        apply();
        try
        {
            WorkTicketReferenceGuard.Validate(drawing, tickets);
        }
        catch
        {
            inner.Undo();
            throw;
        }
    }
}
