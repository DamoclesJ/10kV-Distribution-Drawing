using DistributionDrawing.Desktop.Services;
using System.Windows.Input;

namespace DistributionDrawing.Desktop.ViewModels;

public sealed class MainWindowViewModel
{
    private readonly RelayCommand _undoCommand;
    private readonly RelayCommand _redoCommand;
    private readonly RelayCommand _deleteCommand;

    public MainWindowViewModel(
        DesktopShellService shellService,
        Action newProject,
        Action openProject,
        Action saveProject,
        Action undo,
        Action redo,
        Action delete,
        Action cancel,
        Func<bool>? canUndo = null,
        Func<bool>? canRedo = null,
        Func<bool>? canDelete = null)
    {
        ArgumentNullException.ThrowIfNull(shellService);
        ArgumentNullException.ThrowIfNull(newProject);
        ArgumentNullException.ThrowIfNull(openProject);
        ArgumentNullException.ThrowIfNull(saveProject);
        ArgumentNullException.ThrowIfNull(undo);
        ArgumentNullException.ThrowIfNull(redo);
        ArgumentNullException.ThrowIfNull(delete);
        ArgumentNullException.ThrowIfNull(cancel);
        StatusText = shellService.InitialStatus;
        NewProjectCommand = new RelayCommand(newProject);
        OpenProjectCommand = new RelayCommand(openProject);
        SaveProjectCommand = new RelayCommand(saveProject);
        _undoCommand = new RelayCommand(undo, canUndo);
        _redoCommand = new RelayCommand(redo, canRedo);
        _deleteCommand = new RelayCommand(delete, canDelete);
        UndoCommand = _undoCommand;
        RedoCommand = _redoCommand;
        DeleteCommand = _deleteCommand;
        CancelCommand = new RelayCommand(cancel);
    }

    public string CanvasTitle => "绘图区";

    public string InspectorTitle => "属性检查器";

    public string StatusText { get; }

    public ICommand NewProjectCommand { get; }

    public ICommand OpenProjectCommand { get; }

    public ICommand SaveProjectCommand { get; }

    public ICommand UndoCommand { get; }

    public ICommand RedoCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand CancelCommand { get; }

    public void RefreshCommandStates()
    {
        _undoCommand.Refresh();
        _redoCommand.Refresh();
        _deleteCommand.Refresh();
    }

    private sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();

        public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
