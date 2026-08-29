using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Desktop.Clipboard;

internal sealed class AddCopiedPoleCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly RuntimeLayoutDocument _layout;
    private readonly Pole _pole;
    private readonly IReadOnlyList<ElectricalNode> _nodes;
    private readonly IReadOnlyList<Terminal> _terminals;
    private readonly PoleLayout _poleLayout;

    public AddCopiedPoleCommand(
        DrawingDocument document,
        RuntimeLayoutDocument layout,
        Pole pole,
        IEnumerable<ElectricalNode> nodes,
        IEnumerable<Terminal> terminals,
        PoleLayout poleLayout)
    {
        _document = document;
        _layout = layout;
        _pole = pole;
        _nodes = nodes.ToArray();
        _terminals = terminals.ToArray();
        _poleLayout = poleLayout;
    }

    public void Execute()
    {
        _document.AddDevice(_pole);
        try
        {
            foreach (ElectricalNode node in _nodes) _document.AddElectricalNode(node);
            foreach (Terminal terminal in _terminals) _document.AddTerminal(terminal);
            _layout.DrawingLayout.Add(_poleLayout);
        }
        catch
        {
            _document.RemoveDevice(_pole.Id);
            throw;
        }
    }

    public void Undo()
    {
        _document.RemoveDevice(_pole.Id);
        _layout.DrawingLayout.RemovePole(_pole.Id);
    }

    public void Redo() => Execute();
}

internal sealed class AddCopiedCableSegmentCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly RuntimeLayoutDocument _layout;
    private readonly Connection _connection;
    private readonly CableSegment _segment;
    private readonly CableRouteGuide? _routeGuide;

    public AddCopiedCableSegmentCommand(
        DrawingDocument document,
        RuntimeLayoutDocument layout,
        Connection connection,
        CableSegment segment,
        CableRouteGuide? routeGuide)
    {
        _document = document;
        _layout = layout;
        _connection = connection;
        _segment = segment;
        _routeGuide = routeGuide;
    }

    public void Execute()
    {
        _document.AddCableSegment(_segment, _connection);
        if (_routeGuide is not null)
        {
            _layout.SetCableRouteGuide(_routeGuide);
        }
    }

    public void Undo()
    {
        _layout.RemoveCableRouteGuide(_segment.Id);
        _document.RemoveCableSegment(_segment.Id);
    }

    public void Redo() => Execute();
}

internal sealed class PasteSelectionCommand : ICommand
{
    private readonly IReadOnlyList<ICommand> _commands;
    private readonly SelectionManager _selectionManager;
    private readonly SelectionSet _beforeSelection;
    private readonly IReadOnlyList<SelectionReference> _afterSelection;
    private readonly SelectionReference? _afterPrimary;

    public PasteSelectionCommand(
        IEnumerable<ICommand> commands,
        SelectionManager selectionManager,
        IEnumerable<SelectionReference> afterSelection,
        SelectionReference? afterPrimary)
    {
        _commands = commands.ToArray();
        _selectionManager = selectionManager;
        _beforeSelection = selectionManager.SelectionSet;
        _afterSelection = afterSelection.ToArray();
        _afterPrimary = afterPrimary;
    }

    public void Execute()
    {
        int executed = 0;
        try
        {
            foreach (ICommand command in _commands)
            {
                command.Execute();
                executed++;
            }

            _selectionManager.Replace(_afterSelection, _afterPrimary);
        }
        catch
        {
            foreach (ICommand command in _commands.Take(executed).Reverse())
            {
                command.Undo();
            }

            RestoreBeforeSelection();
            throw;
        }
    }

    public void Undo()
    {
        foreach (ICommand command in _commands.Reverse())
        {
            command.Undo();
        }

        RestoreBeforeSelection();
    }

    public void Redo() => Execute();

    private void RestoreBeforeSelection()
    {
        _selectionManager.Replace(
            _beforeSelection.SelectedReferences,
            _beforeSelection.PrimarySelection);
    }
}
