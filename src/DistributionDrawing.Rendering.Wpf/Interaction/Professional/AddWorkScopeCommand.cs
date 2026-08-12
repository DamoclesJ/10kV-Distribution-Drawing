using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Professional;

public sealed class AddWorkScopeCommand : ICommand
{
    private readonly DrawingDocument _document;

    public AddWorkScopeCommand(
        DrawingDocument document,
        WorkScopeCommandSnapshot after)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(after);

        _document = document;
        After = after;
    }

    public WorkScopeCommandSnapshot After { get; }

    public void Execute()
    {
        _document.CreateWorkScope(
            After.WorkScopeId,
            After.StartBoundary.ToDomain(),
            After.EndBoundary.ToDomain(),
            After.Description,
            After.CopyGroundingPointIds());
    }

    public void Undo()
    {
        _document.RemoveWorkScope(After.WorkScopeId);
    }

    public void Redo()
    {
        Execute();
    }
}
