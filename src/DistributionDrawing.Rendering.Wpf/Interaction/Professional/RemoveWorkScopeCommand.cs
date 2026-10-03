using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Professional;

public sealed class RemoveWorkScopeCommand : ICommand
{
    private readonly DrawingDocument _document;

    public RemoveWorkScopeCommand(
        DrawingDocument document,
        WorkScopeCommandSnapshot before)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(before);

        _document = document;
        Before = before;
    }

    public WorkScopeCommandSnapshot Before { get; }

    public void Execute()
    {
        _document.RemoveWorkScope(Before.WorkScopeId);
    }

    public void Undo()
    {
        _document.CreateWorkScope(
            Before.WorkScopeId,
            Before.Regions,
            Before.Boundaries,
            Before.Description);
    }

    public void Redo()
    {
        Execute();
    }
}
