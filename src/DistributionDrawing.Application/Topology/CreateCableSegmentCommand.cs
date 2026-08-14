using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Application.Topology;

public sealed class CreateCableSegmentCommand
{
    private readonly DrawingDocument _document;

    public CreateCableSegmentCommand(
        DrawingDocument document,
        CableSegmentCreationResult result)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public CableSegmentCreationResult Result { get; }

    public void Execute()
    {
        _document.AddCableSegment(Result.CableSegment, Result.Connection);
    }

    public void Undo()
    {
        _document.RemoveCableSegment(Result.CableSegment.Id);
    }

    public void Redo()
    {
        Execute();
    }
}
