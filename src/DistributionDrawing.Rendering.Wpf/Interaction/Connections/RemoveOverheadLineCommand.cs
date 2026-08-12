using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
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
        OverheadLineLayout layout)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _runtimeLayout = runtimeLayout ?? throw new ArgumentNullException(nameof(runtimeLayout));
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        OverheadLine = overheadLine ?? throw new ArgumentNullException(nameof(overheadLine));
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
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
                _runtimeLayout.DrawingLayout.Add(Layout);
            }
            catch
            {
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
