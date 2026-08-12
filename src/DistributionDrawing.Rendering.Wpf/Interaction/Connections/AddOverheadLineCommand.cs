using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Connections;

public sealed class AddOverheadLineCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly RuntimeLayoutDocument _runtimeLayout;

    public AddOverheadLineCommand(
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
        if (_runtimeLayout.DrawingLayout.OverheadLines.ContainsKey(Connection.Id))
        {
            throw new InvalidOperationException(
                $"Overhead-line layout '{Connection.Id}' already exists.");
        }

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

    public void Undo()
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

    public void Redo() => Execute();
}
