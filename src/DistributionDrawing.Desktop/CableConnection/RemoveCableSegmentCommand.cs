using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;

namespace DistributionDrawing.Desktop.CableConnection;

public sealed class RemoveCableSegmentCommand : ICommand
{
    private readonly DrawingDocument _document;

    public RemoveCableSegmentCommand(
        DrawingDocument document,
        CableSegment cableSegment,
        Connection connection)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        CableSegment = cableSegment ?? throw new ArgumentNullException(nameof(cableSegment));
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        if (CableSegment.ConnectionId != Connection.Id ||
            CableSegment.StartTerminalId != Connection.StartTerminalId ||
            CableSegment.EndTerminalId != Connection.EndTerminalId)
        {
            throw new ArgumentException(
                "Cable segment and connection facts must match.",
                nameof(connection));
        }
    }

    public CableSegment CableSegment { get; }

    public Connection Connection { get; }

    public void Execute()
    {
        _document.RemoveCableSegment(CableSegment.Id);
    }

    public void Undo()
    {
        _document.AddCableSegment(CableSegment, Connection);
    }

    public void Redo() => Execute();
}
