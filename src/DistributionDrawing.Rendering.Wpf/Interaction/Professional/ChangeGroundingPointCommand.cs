using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Professional;

public sealed class ChangeGroundingPointCommand : ICommand
{
    private readonly DrawingDocument _document;

    public ChangeGroundingPointCommand(
        DrawingDocument document,
        GroundingPointCommandSnapshot before,
        GroundingPointCommandSnapshot after)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        if (before.GroundingPointId != after.GroundingPointId)
        {
            throw new ArgumentException(
                "Grounding point command states must refer to the same object.",
                nameof(after));
        }

        if (before.TerminalId != after.TerminalId)
        {
            throw new ArgumentException(
                "Terminal rebinding is not supported by this command.",
                nameof(after));
        }

        _document = document;
        Before = before;
        After = after;
    }

    public GroundingPointCommandSnapshot Before { get; }

    public GroundingPointCommandSnapshot After { get; }

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

    private void Apply(GroundingPointCommandSnapshot snapshot)
    {
        _document.UpdateGroundingPoint(
            snapshot.GroundingPointId,
            snapshot.TerminalId,
            snapshot.Location,
            snapshot.Number,
            snapshot.Note);
    }
}
