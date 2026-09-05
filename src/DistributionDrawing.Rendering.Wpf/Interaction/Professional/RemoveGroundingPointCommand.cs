using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Professional;

public sealed class RemoveGroundingPointCommand : ICommand
{
    private readonly DrawingDocument _document;

    public RemoveGroundingPointCommand(
        DrawingDocument document,
        GroundingPointCommandSnapshot before)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(before);

        _document = document;
        Before = before;
    }

    public GroundingPointCommandSnapshot Before { get; }

    public void Execute()
    {
        _document.RemoveGroundingPoint(Before.GroundingPointId);
    }

    public void Undo()
    {
        _document.CreateGroundingPoint(
            Before.GroundingPointId,
            Before.Target,
            Before.Location,
            Before.Number,
            Before.Note);
    }

    public void Redo()
    {
        Execute();
    }
}
