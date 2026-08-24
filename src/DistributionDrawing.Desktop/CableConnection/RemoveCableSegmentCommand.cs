using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Desktop.CableConnection;

public sealed class RemoveCableSegmentCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly RuntimeLayoutDocument? _layout;
    private readonly CableRouteGuide? _routeGuide;

    public RemoveCableSegmentCommand(
        DrawingDocument document,
        CableSegment cableSegment,
        Connection connection,
        RuntimeLayoutDocument? layout = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        CableSegment = cableSegment ?? throw new ArgumentNullException(nameof(cableSegment));
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _layout = layout;
        _routeGuide = layout?.CableRouteGuides.GetValueOrDefault(cableSegment.Id);
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
        _layout?.RemoveCableRouteGuide(CableSegment.Id);
    }

    public void Undo()
    {
        _document.AddCableSegment(CableSegment, Connection);
        if (_routeGuide is not null)
        {
            _layout?.SetCableRouteGuide(_routeGuide);
        }
    }

    public void Redo() => Execute();
}
