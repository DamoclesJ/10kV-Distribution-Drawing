namespace DistributionDrawing.Rendering.Wpf.Interaction;

public enum CommandTransactionFailureStage
{
    Application,
    Validation
}

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

    public event EventHandler? StateChanged;

    public event EventHandler? DirtyChanged;

    public void ExecuteCommand(ICommand command)
    {
        ExecuteCommand(command, null);
    }

    public void ExecuteCommand(ICommand command, Action? validateAfterExecute)
    {
        ArgumentNullException.ThrowIfNull(command);

        bool wasDirty = IsDirty;

        command.Execute();
        try
        {
            validateAfterExecute?.Invoke();
        }
        catch
        {
            command.Undo();
            throw;
        }

        RecordSuccessfulExecution(command, wasDirty);
    }

    public void ExecuteCommand(
        ICommand command,
        Action validateAfterExecute,
        Action<CommandTransactionFailureStage> restoreAfterFailure)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(validateAfterExecute);
        ArgumentNullException.ThrowIfNull(restoreAfterFailure);

        bool wasDirty = IsDirty;
        CommandTransactionFailureStage failureStage =
            CommandTransactionFailureStage.Application;
        try
        {
            command.Execute();
            failureStage = CommandTransactionFailureStage.Validation;
            validateAfterExecute();
        }
        catch
        {
            restoreAfterFailure(failureStage);
            throw;
        }

        RecordSuccessfulExecution(command, wasDirty);
    }

    public bool Undo() => Undo(null);

    public bool Undo(Action? validateAfterUndo) => Undo(validateAfterUndo, null);

    public bool Undo(Action? validateAfterUndo, Action? restoreSceneAfterFailure)
    {
        if (!CanUndo)
        {
            return false;
        }

        bool wasDirty = IsDirty;
        ICommand command = _history[CurrentIndex - 1];
        command.Undo();
        try
        {
            validateAfterUndo?.Invoke();
        }
        catch
        {
            command.Redo();
            restoreSceneAfterFailure?.Invoke();
            throw;
        }
        CurrentIndex--;
        NotifyStateChanged(wasDirty);
        return true;
    }

    public bool Redo() => Redo(null);

    public bool Redo(Action? validateAfterRedo) => Redo(validateAfterRedo, null);

    public bool Redo(Action? validateAfterRedo, Action? restoreSceneAfterFailure)
    {
        if (!CanRedo)
        {
            return false;
        }

        bool wasDirty = IsDirty;
        ICommand command = _history[CurrentIndex];
        command.Redo();
        try
        {
            validateAfterRedo?.Invoke();
        }
        catch
        {
            command.Undo();
            restoreSceneAfterFailure?.Invoke();
            throw;
        }
        CurrentIndex++;
        NotifyStateChanged(wasDirty);
        return true;
    }

    public void MarkSaved()
    {
        bool wasDirty = IsDirty;
        _savedStateId = CurrentStateId;
        NotifyStateChanged(wasDirty);
    }

    private void NotifyStateChanged(bool wasDirty)
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
        if (wasDirty != IsDirty)
        {
            DirtyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RecordSuccessfulExecution(ICommand command, bool wasDirty)
    {
        if (CurrentIndex < _history.Count)
        {
            _history.RemoveRange(CurrentIndex, _history.Count - CurrentIndex);
            _afterStateIds.RemoveRange(CurrentIndex, _afterStateIds.Count - CurrentIndex);
        }

        _history.Add(command);
        _afterStateIds.Add(_nextStateId++);
        CurrentIndex++;
        TrimHistory();
        NotifyStateChanged(wasDirty);
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
