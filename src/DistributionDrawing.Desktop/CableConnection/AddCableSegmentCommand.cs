using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Interaction;

namespace DistributionDrawing.Desktop.CableConnection;

public sealed class AddCableSegmentCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly CableSegmentCreationResult _creation;

    public AddCableSegmentCommand(
        DrawingDocument document,
        CableSegmentCreationResult creation)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _creation = creation ?? throw new ArgumentNullException(nameof(creation));
    }

    public CableSegmentCreationResult Creation => _creation;

    public void Execute()
    {
        _document.AddCableSegment(_creation.CableSegment, _creation.Connection);
    }

    public void Undo()
    {
        _document.RemoveCableSegment(_creation.CableSegment.Id);
    }

    public void Redo() => Execute();
}
