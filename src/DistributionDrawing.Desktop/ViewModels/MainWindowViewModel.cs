using System.ComponentModel;
using System.Windows.Input;
using DistributionDrawing.Desktop.Actions;
using DistributionDrawing.Desktop.Services;

namespace DistributionDrawing.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private double _zoom = 1.0;
    private bool _gridVisible;
    private string _modeText = "就绪";
    private int _selectionCount;

    public MainWindowViewModel(
        DesktopShellService shellService,
        DesktopUserActions actions)
    {
        ArgumentNullException.ThrowIfNull(shellService);
        Actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _modeText = shellService.InitialStatus;
        Toolbox = new ToolboxViewModel();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DesktopUserActions Actions { get; }

    public string CanvasTitle => "绘图区";

    public string InspectorTitle => "属性检查器";

    public string StatusText => _modeText;

    public string SelectionStatus => $"已选择: {_selectionCount}";

    public string ZoomText => $"缩放: {_zoom:0.00}x";

    public string GridText => $"网格: {(_gridVisible ? "开" : "关")}";

    public string SnapText => "吸附: 开";

    public bool GridVisible => _gridVisible;

    public ICommand NewProjectCommand => Actions.New;
    public ICommand OpenProjectCommand => Actions.Open;
    public ICommand SaveProjectCommand => Actions.Save;
    public ICommand SaveProjectAsCommand => Actions.SaveAs;
    public ICommand CloseDocumentCommand => Actions.CloseDocument;
    public ICommand ExitCommand => Actions.Exit;
    public ICommand ExportPngCommand => Actions.ExportPng;
    public ICommand UndoCommand => Actions.Undo;
    public ICommand RedoCommand => Actions.Redo;
    public ICommand CopyCommand => Actions.Copy;
    public ICommand PasteCommand => Actions.Paste;
    public ICommand SelectAllCommand => Actions.SelectAll;
    public ICommand DeleteCommand => Actions.Delete;
    public ICommand CancelCommand => Actions.CancelCurrentOperation;
    public ICommand SelectCommand => Actions.Select;
    public ICommand CreatePoleCommand => Actions.CreatePole;
    public ICommand CreateRingCabinetCommand => Actions.CreateRingCabinet;
    public ICommand CreateOverheadLineCommand => Actions.CreateOverheadLine;
    public ICommand CreateCableCommand => Actions.CreateCable;
    public ICommand AddCableTerminationCommand => Actions.AddCableTermination;
    public ICommand AddPoleSwitchCommand => Actions.AddPoleSwitch;
    public ICommand AddGroundingPointCommand => Actions.AddGroundingPoint;
    public ICommand AddWorkScopeCommand => Actions.AddWorkScope;
    public ICommand ZoomInCommand => Actions.ZoomIn;
    public ICommand ZoomOutCommand => Actions.ZoomOut;
    public ICommand FitDrawingCommand => Actions.FitDrawing;
    public ICommand ToggleGridCommand => Actions.ToggleGrid;
    public ICommand TypographySettingsCommand => Actions.TypographySettings;
    public ICommand RotateLeftCommand => Actions.RotateLeft;
    public ICommand RotateRightCommand => Actions.RotateRight;
    public ICommand SwitchOperationCommand => Actions.SwitchOperation;
    public ICommand ReconnectCableStartCommand => Actions.ReconnectCableStart;
    public ICommand ReconnectCableEndCommand => Actions.ReconnectCableEnd;

    public ToolboxViewModel Toolbox { get; }

    public void UpdateCanvasState(
        double zoom,
        bool gridVisible,
        string modeText,
        int selectionCount = 0)
    {
        _zoom = zoom;
        _gridVisible = gridVisible;
        _modeText = string.IsNullOrWhiteSpace(modeText) ? "选择" : modeText;
        _selectionCount = Math.Max(0, selectionCount);
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(SelectionStatus));
        OnPropertyChanged(nameof(ZoomText));
        OnPropertyChanged(nameof(GridText));
        OnPropertyChanged(nameof(GridVisible));
    }

    public void RefreshCommandStates() => Actions.RefreshCanExecute();

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
