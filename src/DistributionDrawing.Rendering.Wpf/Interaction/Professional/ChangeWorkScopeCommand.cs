using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Professional;

public sealed class ChangeWorkScopeCommand : ICommand
{
    private readonly DrawingDocument _document;

    public ChangeWorkScopeCommand(
        DrawingDocument document,
        WorkScopeCommandSnapshot before,
        WorkScopeCommandSnapshot after)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        if (before.WorkScopeId != after.WorkScopeId)
        {
            throw new ArgumentException(
                "WorkScope command states must refer to the same object.",
                nameof(after));
        }

        _document = document;
        Before = before;
        After = after;
    }

    public WorkScopeCommandSnapshot Before { get; }

    public WorkScopeCommandSnapshot After { get; }

    public void Execute()
    {
        Apply(After);
    }

    public void Undo()
    {
        Apply(Before);
    }

    public void Redo()
    {
        Execute();
    }

    private void Apply(WorkScopeCommandSnapshot snapshot)
    {
        _document.UpdateWorkScope(
            snapshot.WorkScopeId,
            snapshot.StartBoundary.ToDomain(),
            snapshot.EndBoundary.ToDomain(),
            snapshot.Description,
            snapshot.CopyGroundingPointIds());
    }
}
