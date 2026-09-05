using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Professional;

public sealed class AddGroundingPointCommand : ICommand
{
    private readonly DrawingDocument _document;

    public AddGroundingPointCommand(
        DrawingDocument document,
        GroundingPointCommandSnapshot after)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(after);

        _document = document;
        After = after;
    }

    public GroundingPointCommandSnapshot After { get; }

    public void Execute()
    {
        _document.CreateGroundingPoint(
            After.GroundingPointId,
            After.Target,
            After.Location,
            After.Number,
            After.Note);
    }

    public void Undo()
    {
        _document.RemoveGroundingPoint(After.GroundingPointId);
    }

    public void Redo()
    {
        Execute();
    }
}
