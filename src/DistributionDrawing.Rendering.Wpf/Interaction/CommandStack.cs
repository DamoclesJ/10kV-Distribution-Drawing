namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class CommandStack
{
    private readonly List<ICommand> _history = [];
    private readonly List<long> _afterStateIds = [];
    private readonly int _maximumCapacity;
    private long _nextStateId = 1;
    private long _savedStateId;

    public CommandStack(int maximumCapacity = 100)
    {
        if (maximumCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCapacity),
                "Command history capacity must be greater than zero.");
        }

        _maximumCapacity = maximumCapacity;
    }

    public IReadOnlyList<ICommand> History => _history.AsReadOnly();

    public int CurrentIndex { get; private set; }

    public bool CanUndo => CurrentIndex > 0;

    public bool CanRedo => CurrentIndex < _history.Count;

    public long CurrentStateId => CurrentIndex == 0
        ? 0
        : _afterStateIds[CurrentIndex - 1];

    public long SavedStateId => _savedStateId;

    public bool IsDirty => CurrentStateId != SavedStateId;

    public void ExecuteCommand(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.Execute();

        if (CurrentIndex < _history.Count)
        {
            _history.RemoveRange(CurrentIndex, _history.Count - CurrentIndex);
            _afterStateIds.RemoveRange(CurrentIndex, _afterStateIds.Count - CurrentIndex);
        }

        _history.Add(command);
        _afterStateIds.Add(_nextStateId++);
        CurrentIndex++;
        TrimHistory();
    }

    public bool Undo()
    {
        if (!CanUndo)
        {
            return false;
        }

        ICommand command = _history[CurrentIndex - 1];
        command.Undo();
        CurrentIndex--;
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo)
        {
            return false;
        }

        ICommand command = _history[CurrentIndex];
        command.Redo();
        CurrentIndex++;
        return true;
    }

    public void MarkSaved()
    {
        _savedStateId = CurrentStateId;
    }

    private void TrimHistory()
    {
        int excess = _history.Count - _maximumCapacity;
        if (excess <= 0)
        {
            return;
        }

        _history.RemoveRange(0, excess);
        _afterStateIds.RemoveRange(0, excess);
        CurrentIndex -= excess;
        if (CurrentIndex < 0)
        {
            CurrentIndex = 0;
        }
    }
}
