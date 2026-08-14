using DistributionDrawing.Desktop.Services;
using System.Windows.Input;
using System.ComponentModel;

namespace DistributionDrawing.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly RelayCommand _undoCommand;
    private readonly RelayCommand _redoCommand;
    private readonly RelayCommand _deleteCommand;
    private double _zoom = 1.0;
    private bool _gridVisible;
    private string _modeText = "选择";

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
        Func<bool>? canDelete = null,
        Action? selectMode = null,
        Action? createRingCabinetMode = null,
        Action? createPoleMode = null)
    {
        ArgumentNullException.ThrowIfNull(shellService);
        ArgumentNullException.ThrowIfNull(newProject);
        ArgumentNullException.ThrowIfNull(openProject);
        ArgumentNullException.ThrowIfNull(saveProject);
        ArgumentNullException.ThrowIfNull(undo);
        ArgumentNullException.ThrowIfNull(redo);
        ArgumentNullException.ThrowIfNull(delete);
        ArgumentNullException.ThrowIfNull(cancel);
        _modeText = shellService.InitialStatus;
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
        Toolbox = new ToolboxViewModel(
            selectMode ?? (() => { }),
            createRingCabinetMode ?? (() => { }),
            createPoleMode ?? (() => { }));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CanvasTitle => "绘图区";

    public string InspectorTitle => "属性检查器";

    public string StatusText => $"缩放 {_zoom:0.00}x · 网格 {(_gridVisible ? "开" : "关")} · 模式 {_modeText}";

    public string ZoomText => $"缩放: {_zoom:0.00}x";

    public string GridText => $"网格: {(_gridVisible ? "开" : "关")}";

    public string ModeText => $"模式: {_modeText}";

    public ICommand NewProjectCommand { get; }

    public ICommand OpenProjectCommand { get; }

    public ICommand SaveProjectCommand { get; }

    public ICommand UndoCommand { get; }

    public ICommand RedoCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand CancelCommand { get; }

    public ToolboxViewModel Toolbox { get; }

    public void UpdateCanvasState(double zoom, bool gridVisible, string modeText)
    {
        _zoom = zoom;
        _gridVisible = gridVisible;
        _modeText = string.IsNullOrWhiteSpace(modeText) ? "选择" : modeText;
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ZoomText));
        OnPropertyChanged(nameof(GridText));
        OnPropertyChanged(nameof(ModeText));
    }

    public void RefreshCommandStates()
    {
        _undoCommand.Refresh();
        _redoCommand.Refresh();
        _deleteCommand.Refresh();
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

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
