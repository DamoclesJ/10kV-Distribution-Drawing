using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Professional;

public sealed class AddGroundingPointCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly Action<GroundingPointCommandSnapshot>? _beforeExecute;

    public AddGroundingPointCommand(
        DrawingDocument document,
        GroundingPointCommandSnapshot after,
        Action<GroundingPointCommandSnapshot>? beforeExecute = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(after);

        _document = document;
        After = after;
        _beforeExecute = beforeExecute;
    }

    public GroundingPointCommandSnapshot After { get; }

    public void Execute()
    {
        _beforeExecute?.Invoke(After);
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
