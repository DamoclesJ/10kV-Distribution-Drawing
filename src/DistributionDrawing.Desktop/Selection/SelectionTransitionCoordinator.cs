using DistributionDrawing.Rendering.Wpf.Interaction;

namespace DistributionDrawing.Desktop.Selection;

public sealed class SelectionTransitionCoordinator : ISelectionTransitionCoordinator
{
    private readonly Dictionary<ICommand, SelectionTransition> _transitions =
        new(ReferenceEqualityComparer.Instance);

    public void RecordExecuted(ICommand command, SelectionTransition transition)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(transition);

        if (!_transitions.TryAdd(command, transition))
        {
            throw new InvalidOperationException(
                "Selection transition has already been recorded for this command.");
        }
    }

    public bool TryGetUndoSelection(
        ICommand command,
        out SelectionReference? selection)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_transitions.TryGetValue(command, out SelectionTransition? transition))
        {
            selection = transition.SelectionBefore;
            return true;
        }

        selection = null;
        return false;
    }

    public bool TryGetRedoSelection(
        ICommand command,
        out SelectionReference? selection)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_transitions.TryGetValue(command, out SelectionTransition? transition))
        {
            selection = transition.SelectionAfter;
            return true;
        }

        selection = null;
        return false;
    }

    public void Prune(IReadOnlyCollection<ICommand> activeCommands)
    {
        ArgumentNullException.ThrowIfNull(activeCommands);

        HashSet<ICommand> active = new(
            activeCommands,
            ReferenceEqualityComparer.Instance);
        foreach (ICommand command in _transitions.Keys.ToArray())
        {
            if (!active.Contains(command))
            {
                _transitions.Remove(command);
            }
        }
    }

    public void Clear()
    {
        _transitions.Clear();
    }
}
