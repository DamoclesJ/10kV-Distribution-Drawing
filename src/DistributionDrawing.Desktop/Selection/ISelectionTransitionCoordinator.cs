using DistributionDrawing.Rendering.Wpf.Interaction;

namespace DistributionDrawing.Desktop.Selection;

public interface ISelectionTransitionCoordinator
{
    void RecordExecuted(ICommand command, SelectionTransition transition);

    bool TryGetUndoSelection(
        ICommand command,
        out SelectionReference? selection);

    bool TryGetRedoSelection(
        ICommand command,
        out SelectionReference? selection);

    void Prune(IReadOnlyCollection<ICommand> activeCommands);

    void Clear();
}
