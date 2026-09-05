using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Connections;

public sealed class RemoveOverheadLineCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly RuntimeLayoutDocument _runtimeLayout;

    public RemoveOverheadLineCommand(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        Connection connection,
        OverheadLine overheadLine,
        OverheadLineLayout layout,
        IEnumerable<GroundingAccessPoint>? groundingAccessPoints = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _runtimeLayout = runtimeLayout ?? throw new ArgumentNullException(nameof(runtimeLayout));
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        OverheadLine = overheadLine ?? throw new ArgumentNullException(nameof(overheadLine));
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        GroundingAccessPoints = Array.AsReadOnly(
            (groundingAccessPoints ?? []).ToArray());
        if (Connection.Id != OverheadLine.ConnectionId || Connection.Id != Layout.ConnectionId)
        {
            throw new ArgumentException(
                "Connection, overhead line, and layout IDs must match.",
                nameof(layout));
        }
    }

    public Connection Connection { get; }

    public OverheadLine OverheadLine { get; }

    public OverheadLineLayout Layout { get; }

    public IReadOnlyList<GroundingAccessPoint> GroundingAccessPoints { get; }

    public void Execute()
    {
        _ = _runtimeLayout.DrawingLayout.OverheadLines[Connection.Id];
        _runtimeLayout.DrawingLayout.RemoveOverheadLine(Connection.Id);
        try
        {
            _document.RemoveOverheadLine(Connection.Id);
            try
            {
                _document.RemoveConnection(Connection.Id);
            }
            catch
            {
                _document.AddOverheadLine(OverheadLine);
                foreach (GroundingAccessPoint point in GroundingAccessPoints)
                {
                    _document.AddGroundingAccessPoint(point);
                }
                throw;
            }
        }
        catch
        {
            _runtimeLayout.DrawingLayout.Add(Layout);
            throw;
        }
    }

    public void Undo()
    {
        _document.AddConnection(Connection);
        try
        {
            _document.AddOverheadLine(OverheadLine);
            try
            {
                foreach (GroundingAccessPoint point in GroundingAccessPoints)
                {
                    _document.AddGroundingAccessPoint(point);
                }
                _runtimeLayout.DrawingLayout.Add(Layout);
            }
            catch
            {
                foreach (GroundingAccessPoint point in GroundingAccessPoints.Reverse())
                {
                    if (_document.GroundingAccessPoints.Any(candidate =>
                            candidate.GroundingAccessPointId == point.GroundingAccessPointId))
                    {
                        _document.RemoveGroundingAccessPoint(point.GroundingAccessPointId);
                    }
                }
                _document.RemoveOverheadLine(Connection.Id);
                throw;
            }
        }
        catch
        {
            _document.RemoveConnection(Connection.Id);
            throw;
        }
    }

    public void Redo() => Execute();
}
