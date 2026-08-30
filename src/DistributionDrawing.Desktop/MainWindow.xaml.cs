using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.ComponentModel;
using System.Globalization;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Professional;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.PropertyInspector;
using DistributionDrawing.Desktop.DrawingTypography;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Desktop.Workspace;
using DistributionDrawing.Desktop.Selection;
using DistributionDrawing.Desktop.Placement;
using DistributionDrawing.Desktop.ConnectionEditing;
using DistributionDrawing.Desktop.CableConnection;
using DistributionDrawing.Desktop.Clipboard;
using DistributionDrawing.Desktop.CableTerminationCreation;
using DistributionDrawing.Desktop.PoleSwitchCreation;
using DistributionDrawing.Desktop.PoleAttachmentManagement;
using DistributionDrawing.Desktop.Demo;
using DistributionDrawing.Desktop.DrawingTools;
using DistributionDrawing.Desktop.RingCabinetCreation;
using DistributionDrawing.Desktop.Viewport;
using DistributionDrawing.Desktop.Services;
using DistributionDrawing.Desktop.ViewModels;
using DistributionDrawing.Desktop.SwitchOperation;
using DistributionDrawing.Desktop.Actions;
using DistributionDrawing.Desktop.Export;
using System.Windows.Threading;

namespace DistributionDrawing.Desktop;

public partial class MainWindow : Window
{
    private sealed record InstalledDeviceItem(Guid AttachmentId, string DisplayText);
    private readonly DrawingSceneRenderer _renderer = new();
    private readonly DrawingSceneBuilder _sceneBuilder = new();
    private readonly CanvasViewportController _viewport = new();
    private SelectionManager _selectionManager = new();
    private CommandStack _commandStack = new();
    private PropertyEditor _propertyEditor;
    private readonly ProfessionalCommandFactory _professionalCommandFactory = new();
    private readonly DeviceDragController _deviceDrag = new();
    private readonly CableRouteDragController _cableRouteDrag = new();
    private SelectionRectangleController _selectionRectangle;
    private readonly SceneSelectionQuery _sceneSelectionQuery = new();
    private SelectionObjectResolver _selectionResolver = new();
    private PropertyProjector _propertyProjector = new();
    private PropertyInspectorViewModel _propertyInspector = new();
    private readonly ProjectWorkspaceController _workspace;
    private readonly PlacementController _placement;
    private readonly OverheadLineConnectionController _overheadLineConnection;
    private readonly CableTerminationAttachmentController _cableTerminationAttachment;
    private readonly PoleSwitchAttachmentController _poleSwitchAttachment;
    private readonly PoleAttachmentManagementController _poleAttachmentManagement;
    private readonly SwitchOperationController _switchOperation;
    private readonly CableConnectionController _cableConnection;
    private readonly CableReconnectController _cableReconnect;
    private readonly DrawingClipboardController _clipboard;
    private readonly DrawingToolCoordinator _drawingTools;
    private readonly DesktopUserActions _actions;
    private readonly IDesktopMessageService _messageService;
    private readonly DesktopContextMenuResolver _contextMenuResolver = new();
    private readonly ExportDrawingController _exportDrawing;
    private readonly DispatcherTimer _statusFeedbackTimer = new()
    {
        Interval = TimeSpan.FromSeconds(2.5)
    };
    private DocumentSession? _boundDocumentSession;
    private MainWindowViewModel _shellViewModel = null!;
    private bool _gridVisible;
    private DrawingScene? _currentScene;
    private PropertyInspectionSource? _activeSource;
    private bool _groundingPointPickMode;
    private Guid? _pendingGroundingPointTerminalId;
    private WorkScopePickState _workScopePickState;
    private BoundaryPointCommandValue? _pendingWorkScopeStartBoundary;
    private BoundaryPointCommandValue? _pendingWorkScopeEndBoundary;
    private Guid? _pendingWorkScopeTerminalId;
    private Guid? _pendingWorkScopeDeviceId;
    private static readonly IReadOnlyList<IntervalKind> SupportedIntervalKinds =
        Array.AsReadOnly(Enum.GetValues<IntervalKind>());
    private static readonly IReadOnlyList<GroundingStructureKind> SupportedGroundingStructures =
        Array.AsReadOnly(Enum.GetValues<GroundingStructureKind>());

    public MainWindow()
    {
        InitializeComponent();
        _messageService = new DesktopMessageService(this);
        _propertyEditor = new(_selectionResolver, _commandStack);
        _selectionRectangle = new SelectionRectangleController(_selectionManager);
        PropertyInspectorPanel.DataContext = _propertyInspector;
        _selectionManager.SelectionChanged += OnSelectionChanged;
        _workspace = new ProjectWorkspaceController(
            new WpfProjectWorkspaceDialogs(this, _messageService),
            _sceneBuilder,
            EnsureTransientEditsCommitted);
        _workspace.Workspace.ActiveSessionChanging += OnActiveDocumentSessionChanging;
        _workspace.Workspace.SessionsChanged += OnWorkspaceSessionsChanged;
        _workspace.SessionChanged += OnWorkspaceSessionChanged;
        _placement = new PlacementController(() => _workspace.CurrentSession);
        _overheadLineConnection = new OverheadLineConnectionController(
            () => _workspace.CurrentSession);
        _cableConnection = new CableConnectionController(
            () => _workspace.CurrentSession);
        _cableReconnect = new CableReconnectController(
            () => _workspace.CurrentSession);
        _clipboard = new DrawingClipboardController(
            () => _workspace.CurrentSession);
        _cableTerminationAttachment = new CableTerminationAttachmentController(
            () => _workspace.CurrentSession);
        _poleSwitchAttachment = new PoleSwitchAttachmentController(
            () => _workspace.CurrentSession);
        _poleAttachmentManagement = new PoleAttachmentManagementController(
            () => _workspace.CurrentSession);
        _switchOperation = new SwitchOperationController(
            () => _workspace.CurrentSession);
        _drawingTools = new DrawingToolCoordinator(
            _placement,
            _overheadLineConnection,
            _cableTerminationAttachment,
            _cableConnection,
            _cableReconnect,
            _poleSwitchAttachment,
            () => _workspace.CurrentSession);
        _exportDrawing = new ExportDrawingController(
            () => _workspace.CurrentSession,
            () => _workspace.ActiveDocumentSession?.DocumentName ?? "图纸",
            new WpfExportDrawingDialog(this),
            _messageService);
        _actions = new DesktopUserActions(
            new DesktopActionContext
            {
                ActiveSession = () => _workspace.CurrentSession,
                HasClipboardContent = () => _clipboard.HasContent,
                IsInteractionIdle = IsInteractionIdle,
                CanRotateSelection = CanRotateCurrentSelection,
                CanOperateSwitch = CanOperateCurrentSwitch,
                CanReconnectCable = CanReconnectCurrentCable,
                CanAddPoleAttachment = CanAddAttachmentToCurrentPole
            },
            new DesktopUserActionHandlers
            {
                New = () => _workspace.NewProject(),
                Open = () => _workspace.OpenProject(),
                Save = ExecuteSaveAction,
                SaveAs = ExecuteSaveAsAction,
                CloseDocument = () => _workspace.CloseCurrentProject(),
                Exit = Close,
                ExportPng = ExecuteExportPngAction,
                Undo = OnUndoRequested,
                Redo = OnRedoRequested,
                Copy = ExecuteCopyAction,
                Paste = ExecutePasteAction,
                SelectAll = OnSelectAllRequested,
                Delete = OnDeleteRequested,
                CancelCurrentOperation = CancelCurrentOperation,
                Select = OnSelectModeRequested,
                CreatePole = () => OnBeginPlacePole(this, new RoutedEventArgs()),
                CreateRingCabinet = () => OnBeginPlaceRingCabinet(this, new RoutedEventArgs()),
                CreateOverheadLine = () => OnBeginOverheadLine(this, new RoutedEventArgs()),
                CreateCable = () => OnBeginCable(this, new RoutedEventArgs()),
                AddCableTermination = () => OnAddCableTermination(this, new RoutedEventArgs()),
                AddPoleSwitch = () => OnAddPoleSwitch(this, new RoutedEventArgs()),
                AddGroundingPoint = () => OnBeginAddGroundingPoint(this, new RoutedEventArgs()),
                AddWorkScope = () => OnBeginAddWorkScope(this, new RoutedEventArgs()),
                ZoomIn = () => OnZoomIn(this, new RoutedEventArgs()),
                ZoomOut = () => OnZoomOut(this, new RoutedEventArgs()),
                FitDrawing = () => OnFitDrawing(this, new RoutedEventArgs()),
                ToggleGrid = ToggleGrid,
                TypographySettings = () => OnDrawingTypographySettings(this, new RoutedEventArgs()),
                RotateLeft = () => RotateSelectedPoleAttachment(-1),
                RotateRight = () => RotateSelectedPoleAttachment(1),
                SwitchOperation = ExecuteSwitchOperation,
                ReconnectCableStart = () => BeginCableReconnect(_cableReconnect.BeginStart),
                ReconnectCableEnd = () => BeginCableReconnect(_cableReconnect.BeginEnd)
            },
            _messageService);
        _placement.SceneChanged += OnDrawingToolVisualChanged;
        _overheadLineConnection.VisualChanged += OnDrawingToolVisualChanged;
        _cableConnection.VisualChanged += OnDrawingToolVisualChanged;
        _cableReconnect.VisualChanged += OnDrawingToolVisualChanged;
        _cableConnection.ParametersRequired += OnCableParametersRequired;
        _cableTerminationAttachment.SceneChanged += OnDrawingToolVisualChanged;
        _poleSwitchAttachment.SceneChanged += OnDrawingToolVisualChanged;
        _poleAttachmentManagement.SceneChanged += OnDrawingToolVisualChanged;
        _switchOperation.SceneChanged += OnSwitchOperationSceneChanged;
        _clipboard.ContentChanged += (_, _) => _actions.RefreshCanExecute();
        _viewport.ViewChanged += OnViewportChanged;
        DrawingSurface.SetViewTransform(_viewport.Transform);
        _shellViewModel = new MainWindowViewModel(
            new DesktopShellService(),
            _actions);
        _shellViewModel.Toolbox.PropertyChanged += OnToolboxPropertyChanged;
        _statusFeedbackTimer.Tick += OnStatusFeedbackTimerTick;
        DataContext = _shellViewModel;
        UpdateCanvasStatus();
    }

    private void OnNewProject(object sender, RoutedEventArgs e) => _shellViewModel.NewProjectCommand.Execute(null);

    private void OnOpenProject(object sender, RoutedEventArgs e) => _shellViewModel.OpenProjectCommand.Execute(null);

    private void OnSaveProject(object sender, RoutedEventArgs e) => _shellViewModel.SaveProjectCommand.Execute(null);

    private void OnSaveProjectAs(object sender, RoutedEventArgs e) => _workspace.SaveProjectAs();

    private void OnCloseProject(object sender, RoutedEventArgs e) => _workspace.CloseCurrentProject();

    private void OnBeginPlacePole(object sender, RoutedEventArgs e)
    {
        CancelDeviceDrag();
        CancelProfessionalPicking();
        _drawingTools.BeginPole();
        _shellViewModel.Toolbox.SetSelectedMode(DesktopToolMode.CreatePole);
        UpdateCanvasStatus();
    }

    private void OnBeginPlaceRingCabinet(object sender, RoutedEventArgs e)
    {
        var dialog = new RingCabinetCreationDialog { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Configuration is null)
        {
            return;
        }

        CancelDeviceDrag();
        CancelProfessionalPicking();
        _drawingTools.BeginRingCabinet(dialog.Configuration);
        _shellViewModel.Toolbox.SetSelectedMode(DesktopToolMode.CreateRingCabinet);
        UpdateCanvasStatus();
    }

    private void OnAddCableTermination(object sender, RoutedEventArgs e)
    {
        _shellViewModel.Toolbox.SetSelectedMode(DesktopToolMode.AddCableTermination);
        var dialog = new CableTerminationCreationDialog { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            _shellViewModel.Toolbox.SetSelectedMode(DesktopToolMode.Select);
            return;
        }

        CancelDeviceDrag();
        CancelProfessionalPicking();
        _drawingTools.Cancel();

        try
        {
            _cableTerminationAttachment.AddToSelectedPole(dialog.DisplayName);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _messageService.ShowError("无法添加电缆终端", exception.Message);
        }
        finally
        {
            _shellViewModel.Toolbox.SetSelectedMode(DesktopToolMode.Select);
            UpdateCanvasStatus();
        }
    }

    private void OnAddPoleSwitch(object sender, RoutedEventArgs e)
    {
        _shellViewModel.Toolbox.SetSelectedMode(DesktopToolMode.AddPoleSwitch);
        var dialog = new PoleSwitchCreationDialog { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            _shellViewModel.Toolbox.SetSelectedMode(DesktopToolMode.Select);
            return;
        }

        try
        {
            _drawingTools.AddSwitchAttachment(dialog.SwitchKind);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _messageService.ShowError("无法添加柱上开关", exception.Message);
        }
        finally
        {
            SyncToolboxModeFromInteraction();
            UpdateCanvasStatus();
        }
    }

    private void OnBeginOverheadLine(object sender, RoutedEventArgs e)
    {
        try
        {
            CancelDeviceDrag();
            CancelProfessionalPicking();
            _drawingTools.BeginOverheadLine();
            _shellViewModel.Toolbox.SetSelectedMode(DesktopToolMode.CreateOverheadLine);
            UpdateCanvasStatus();
        }
        catch (InvalidOperationException exception)
        {
            _messageService.ShowError("无法绘制架空线", exception.Message);
        }
    }

    private void OnBeginCable(object sender, RoutedEventArgs e)
    {
        try
        {
            CancelDeviceDrag();
            CancelProfessionalPicking();
            _drawingTools.BeginCable();
            _shellViewModel.Toolbox.SetSelectedMode(DesktopToolMode.CreateCable);
            UpdateCanvasStatus();
        }
        catch (InvalidOperationException exception)
        {
            _messageService.ShowError("无法绘制电缆", exception.Message);
        }
    }

    private void OnCableParametersRequired(object? sender, EventArgs e)
    {
        var dialog = new CableCreationDialog { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            _cableConnection.Cancel();
            return;
        }

        try
        {
            _cableConnection.Complete(dialog.CableType, dialog.Length);
        }
        catch (ArgumentException exception)
        {
            ShowCommandError("电缆创建失败", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ShowCommandError("电缆创建失败", exception.Message);
        }
    }

    private void OnCancelPlacement(object sender, RoutedEventArgs e)
    {
        _drawingTools.Cancel();
    }

    private void OnSwitchOperation(object sender, RoutedEventArgs e)
    {
        ExecuteSwitchOperation();
    }

    private void ExecuteSwitchOperation()
    {
        SwitchOperationResult result = _switchOperation.ToggleSelected();
        if (!result.IsSuccess)
        {
            _messageService.ShowError("开关操作失败", result.ErrorMessage!);
        }
    }

    private void OnSwitchOperationSceneChanged(object? sender, EventArgs e)
    {
        if (_workspace.CurrentSession is not { } session)
        {
            return;
        }

        _currentScene = session.Scene;
        _activeSource = session.InspectionSource;
        _selectionResolver.SetSource(_activeSource);
        _propertyInspector.Apply(
            _propertyProjector.Project(
                _selectionResolver.Resolve(_selectionManager.Selected)));
        RenderCurrentScene();
    }

    private void OnRemoveSelectedDevice(object sender, RoutedEventArgs e) => OnDeleteRequested();

    private void OnDeleteRequested()
    {
        try
        {
            CancelDeviceDrag();
            _drawingTools.RemoveSelected();
            OnDrawingToolVisualChanged(this, EventArgs.Empty);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _messageService.ShowError("删除对象失败", exception.Message);
        }
    }

    private void ExecuteCopyAction()
    {
        ClipboardActionResult result = _clipboard.Copy();
        if (!result.IsSuccess)
        {
            _messageService.ShowError("无法复制对象", result.Message);
        }
        else if (result.HasWarning)
        {
            _messageService.ShowWarning("部分对象未复制", result.Message);
        }
        else
        {
            ShowTransientFeedback(result.Message);
        }
    }

    private void ExecutePasteAction()
    {
        ClipboardActionResult result = _clipboard.Paste();
        if (!result.IsSuccess)
        {
            _messageService.ShowError("无法粘贴对象", result.Message);
            return;
        }

        OnDrawingToolVisualChanged(this, EventArgs.Empty);
        ShowTransientFeedback(result.Message);
    }

    private void ExecuteSaveAction()
    {
        if (_workspace.SaveProject())
        {
            ShowTransientFeedback("已保存");
        }
    }

    private void ExecuteSaveAsAction()
    {
        if (_workspace.SaveProjectAs())
        {
            ShowTransientFeedback("已保存");
        }
    }

    private void ExecuteExportPngAction()
    {
        if (_exportDrawing.ExportPng())
        {
            ShowTransientFeedback("PNG 已导出");
        }
    }

    private void ShowTransientFeedback(string message)
    {
        _shellViewModel.ShowFeedback(message);
        _statusFeedbackTimer.Stop();
        _statusFeedbackTimer.Start();
    }

    private void OnStatusFeedbackTimerTick(object? sender, EventArgs e)
    {
        _statusFeedbackTimer.Stop();
        _shellViewModel.ClearFeedback();
    }

    private void CancelCurrentOperation()
    {
        if (_selectionRectangle.Cancel())
        {
            DrawingSurface.ReleaseMouseCapture();
            RenderCurrentScene();
            return;
        }

        if (_deviceDrag.IsActive || _cableRouteDrag.IsActive)
        {
            CancelDeviceDrag();
            return;
        }

        if (_viewport.IsPanning)
        {
            EndCanvasPan();
            return;
        }

        _drawingTools.Cancel();
        CancelProfessionalPicking();
        _shellViewModel.Toolbox.SetSelectedMode(DesktopToolMode.Select);
        UpdateCanvasStatus();
        _actions.RefreshCanExecute();
    }

    private void OnWindowPreviewKeyDown(
        object sender,
        System.Windows.Input.KeyEventArgs e)
    {
        DesktopShortcutAction shortcut = DesktopShortcutPolicy.Resolve(
            e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key,
            System.Windows.Input.Keyboard.Modifiers,
            IsTextInputFocused(),
            IsInteractionIdle());
        DesktopAction? action = shortcut switch
        {
            DesktopShortcutAction.Undo => _actions.Undo,
            DesktopShortcutAction.Redo => _actions.Redo,
            DesktopShortcutAction.Copy => _actions.Copy,
            DesktopShortcutAction.Paste => _actions.Paste,
            DesktopShortcutAction.SelectAll => _actions.SelectAll,
            DesktopShortcutAction.Delete => _actions.Delete,
            DesktopShortcutAction.Cancel => _actions.CancelCurrentOperation,
            _ => null
        };
        if (action?.CanExecute(null) != true)
        {
            return;
        }

        action.Execute(null);
        e.Handled = true;
    }

    private static bool IsTextInputFocused()
    {
        return System.Windows.Input.Keyboard.FocusedElement is
            System.Windows.Controls.Primitives.TextBoxBase or
            PasswordBox or
            ComboBox { IsEditable: true };
    }

    private bool IsInteractionIdle()
    {
        return !_drawingTools.IsActive &&
               _placement.Mode == PlacementMode.Idle &&
               !_deviceDrag.IsActive &&
               !_cableRouteDrag.IsActive &&
               !_selectionRectangle.IsActive &&
               !_viewport.IsPanning &&
               !_groundingPointPickMode &&
               _workScopePickState == WorkScopePickState.Idle;
    }

    private bool CanRotateCurrentSelection()
    {
        if (!_selectionManager.HasSingleSelection)
        {
            return false;
        }

        ResolvedSelection? selection = _selectionResolver.Resolve(
            _selectionManager.Selected);
        return selection?.PoleAttachment is not null &&
               selection.AttachedDevice is SwitchDevice;
    }

    private bool CanOperateCurrentSwitch()
    {
        return _selectionManager.HasSingleSelection &&
               _selectionResolver.Resolve(_selectionManager.Selected)?.SwitchDevice is not null;
    }

    private bool CanReconnectCurrentCable()
    {
        return _selectionManager.HasSingleSelection &&
               _selectionResolver.Resolve(_selectionManager.Selected)?.CableSegment is not null;
    }

    private bool CanAddAttachmentToCurrentPole()
    {
        return _selectionManager.HasSingleSelection &&
               _selectionResolver.Resolve(_selectionManager.Selected)?.Pole is not null;
    }

    private void OnSelectAllRequested()
    {
        if (_currentScene is null || _drawingTools.IsActive ||
            _deviceDrag.IsActive || _cableRouteDrag.IsActive ||
            _viewport.IsPanning || _selectionRectangle.IsActive)
        {
            return;
        }

        _selectionManager.Replace(_sceneSelectionQuery.SelectAll(_currentScene.HitTestIndex));
    }

    private void OnSelectModeRequested()
    {
        _drawingTools.Cancel();
        CancelProfessionalPicking();
        _shellViewModel.Toolbox.SetSelectedMode(DesktopToolMode.Select);
        UpdateCanvasStatus();
    }

    private void OnCreateRingCabinetModeRequested()
    {
        OnBeginPlaceRingCabinet(this, new RoutedEventArgs());
        UpdateCanvasStatus();
    }

    private void OnCreatePoleModeRequested()
    {
        OnBeginPlacePole(this, new RoutedEventArgs());
        UpdateCanvasStatus();
    }

    private void OnUndoRequested() => OnUndo(this, new RoutedEventArgs());

    private void OnRedoRequested() => OnRedo(this, new RoutedEventArgs());

    private void OnDrawingToolVisualChanged(object? sender, EventArgs e)
    {
        if (_workspace.CurrentSession is not { } session)
        {
            return;
        }

        _currentScene = session.Scene;
        _activeSource = session.InspectionSource;
        _selectionResolver.SetSource(_activeSource);
        _propertyInspector.Apply(
            _propertyProjector.Project(
                _selectionResolver.Resolve(_selectionManager.Selected)));
        UpdateRingCabinetEditor();
        UpdatePoleNumberEditor();
        UpdatePoleInstalledDevicesEditor();
        UpdateIntervalEditor();
        UpdateAttachmentOffsetEditor();
        UpdateAttachmentLayoutEditor();
        UpdateCableTerminationDisplayNameEditor();
        UpdateGroundingPointEditor();
        UpdateWorkScopeEditor();
        SyncToolboxModeFromInteraction();
        UpdateCanvasStatus();
        _actions.RefreshCanExecute();
        RenderCurrentScene();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_workspace.CanCloseApplication())
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _statusFeedbackTimer.Stop();
        _workspace.Dispose();
        base.OnClosed(e);
    }

    private bool EnsureTransientEditsCommitted()
    {
        EndCanvasPan();

        if (_overheadLineConnection.IsActive)
        {
            MessageBoxResult connectionResult = MessageBox.Show(
                this,
                "当前正在绘制架空线。取消本次绘制并继续吗？",
                "未完成的架空线",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);
            if (connectionResult != MessageBoxResult.OK)
            {
                return false;
            }

            _overheadLineConnection.Cancel();
        }

        if (_cableConnection.IsActive)
        {
            MessageBoxResult connectionResult = MessageBox.Show(
                this,
                "当前正在绘制电缆。取消本次绘制并继续吗？",
                "未完成的电缆",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);
            if (connectionResult != MessageBoxResult.OK)
            {
                return false;
            }

            _cableConnection.Cancel();
        }

        if (!_deviceDrag.IsActive && !_cableRouteDrag.IsActive)
        {
            return true;
        }

        MessageBoxResult result = MessageBox.Show(
            this,
            "当前有未提交的拖动预览，是否提交后继续？选择“否”将取消预览。",
            "未提交的布局预览",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);
        if (result == MessageBoxResult.Cancel)
        {
            return false;
        }

        if (result == MessageBoxResult.No)
        {
            CancelDeviceDrag();
            return true;
        }

        try
        {
            ICommand? command = _cableRouteDrag.IsActive
                ? _cableRouteDrag.Commit()
                : _deviceDrag.Commit();
            DrawingSurface.ReleaseMouseCapture();
            if (command is not null)
            {
                _commandStack.ExecuteCommand(command);
            }

            RefreshDrawingScene();
            return true;
        }
        catch (Exception exception)
        {
            ShowCommandError("提交拖动失败", exception.Message);
            return false;
        }
    }

    private void OnWorkspaceSessionChanged(object? sender, EventArgs e)
    {
        _shellViewModel.RefreshCommandStates();
        _viewport.SetViewportSize(
            new Size(DrawingSurface.ActualWidth, DrawingSurface.ActualHeight));
        BindActiveSession(_workspace.ActiveDocumentSession);
        RefreshDocumentTabs();
        UpdateWindowTitle();
    }

    private void OnWorkspaceSessionsChanged(object? sender, EventArgs e) =>
        RefreshDocumentTabs();

    private void RefreshDocumentTabs()
    {
        DocumentTabs.ItemsSource = null;
        DocumentTabs.ItemsSource = _workspace.Workspace.Sessions;
        DocumentTabs.SelectedItem = _workspace.ActiveDocumentSession;
        UpdatePresentationState();
    }

    private void OnDocumentTabSelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DocumentTabs.SelectedItem is DocumentSession session &&
            !ReferenceEquals(session, _workspace.ActiveDocumentSession))
        {
            _workspace.Workspace.ActivateSession(session);
        }
    }

    private void UpdateWindowTitle()
    {
        Title = _workspace.ActiveDocumentSession is { } session
            ? $"{session.TabTitle} - 10kV 配电工作票绘图"
            : "10kV 配电工作票绘图";
    }

    private void OnActiveDocumentSessionChanging(
        object? sender,
        ActiveDocumentSessionChangedEventArgs e)
    {
        CancelTransientInteraction();
        if (e.Previous is not null &&
            ReferenceEquals(_boundDocumentSession, e.Previous))
        {
            e.Previous.UpdateViewState(_viewport.CaptureState());
        }
    }

    private void BindActiveSession(DocumentSession? documentSession)
    {
        if (ReferenceEquals(_boundDocumentSession, documentSession) &&
            documentSession is not null)
        {
            ProjectRuntimeSession current = documentSession.RuntimeSession;
            _currentScene = current.Scene;
            _activeSource = current.InspectionSource;
            _selectionResolver.SetSource(_activeSource);
            OnSelectionChanged(this, EventArgs.Empty);
            return;
        }

        _selectionManager.SelectionChanged -= OnSelectionChanged;
        if (_boundDocumentSession is not null)
        {
            _boundDocumentSession.StateChanged -= OnBoundDocumentSessionStateChanged;
        }

        _boundDocumentSession = documentSession;
        if (documentSession is null)
        {
            _selectionManager = new SelectionManager();
            _selectionRectangle = new SelectionRectangleController(_selectionManager);
            _selectionManager.SelectionChanged += OnSelectionChanged;
            _commandStack = new CommandStack();
            _selectionResolver = new SelectionObjectResolver();
            _propertyProjector = new PropertyProjector();
            _propertyInspector = new PropertyInspectorViewModel();
            _propertyEditor = new PropertyEditor(_selectionResolver, _commandStack);
            PropertyInspectorPanel.DataContext = _propertyInspector;
            _viewport.Reset();
            OnClearDrawing(this, new RoutedEventArgs());
            _shellViewModel.RefreshCommandStates();
            return;
        }

        ProjectRuntimeSession session = documentSession.RuntimeSession;
        documentSession.StateChanged += OnBoundDocumentSessionStateChanged;
        _selectionManager = session.SelectionManager;
        _selectionRectangle = new SelectionRectangleController(_selectionManager);
        _commandStack = session.CommandStack;
        _selectionResolver = session.SelectionResolver;
        _propertyProjector = session.PropertyProjector;
        _propertyInspector = session.PropertyInspector;
        _propertyEditor = new(_selectionResolver, _commandStack, session.Layout);
        _currentScene = session.Scene;
        _activeSource = session.InspectionSource;
        _selectionManager.SelectionChanged += OnSelectionChanged;
        PropertyInspectorPanel.DataContext = _propertyInspector;
        _viewport.RestoreState(documentSession.ViewState);
        OnSelectionChanged(this, EventArgs.Empty);
    }

    private void OnBoundDocumentSessionStateChanged(object? sender, EventArgs e) =>
        RefreshBoundSessionState();

    private void RefreshBoundSessionState()
    {
        _actions.RefreshCanExecute();
        DocumentTabs.Items.Refresh();
        UpdateWindowTitle();
    }

    private void CancelTransientInteraction()
    {
        bool layoutChanged = _deviceDrag.Cancel();
        layoutChanged |= _cableRouteDrag.Cancel();
        if (layoutChanged && _workspace.CurrentSession is { } session)
        {
            session.RebuildScene();
        }

        _selectionRectangle.Cancel();
        EndCanvasPan();
        DrawingSurface.ReleaseMouseCapture();
        _drawingTools.Cancel();
        CancelProfessionalPicking();
        _shellViewModel.Toolbox.SetSelectedMode(DesktopToolMode.Select);
        UpdateCanvasStatus();
    }

    private void OnDrawTestContent(object sender, RoutedEventArgs e)
    {
        _drawingTools.Cancel();
        var firstPole = new Pole(Guid.NewGuid(), "P-01");
        var secondPole = new Pole(Guid.NewGuid(), "P-02");
        var firstAnchor = firstPole.CreateOverheadAnchorTerminal(Guid.NewGuid());
        var secondAnchor = secondPole.CreateOverheadAnchorTerminal(Guid.NewGuid());
        var cableTermination = new CableTermination(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "柱上电缆终端");
        var attachment = new PoleAttachment(
            Guid.NewGuid(),
            firstPole.Id,
            cableTermination.Id);
        var overheadLine = new OverheadLine(
            Guid.NewGuid(),
            "JKLYJ-10kV",
            [firstPole.Id, secondPole.Id]);
        var connection = new Connection(
            overheadLine.ConnectionId,
            ConnectionType.OverheadLine,
            firstAnchor.Id,
            secondAnchor.Id,
            "架空线路",
            "10kV");

        var layout = new DrawingLayout();
        layout.Add(new PoleLayout(firstPole.Id, new DocumentPoint(50, 65)));
        layout.Add(new PoleLayout(secondPole.Id, new DocumentPoint(170, 65)));
        layout.Add(new AttachmentLayout(
            attachment.AttachmentId,
            new DocumentPoint(9, 12)));
        layout.Add(new OverheadLineLayout(
            overheadLine.ConnectionId,
            new DocumentPoint(52, 72),
            new DocumentPoint(172, 72)));

        DrawingScene scene = _sceneBuilder.Build(
            layout,
            [firstPole, secondPole],
            [attachment],
            [cableTermination],
            [connection],
            [overheadLine]);

        ShowScene(
            scene,
            new PropertyInspectionSource
            {
                DrawingLayout = layout,
                Poles = [firstPole, secondPole],
                Devices = [cableTermination],
                PoleAttachments = [attachment],
                Connections = [connection],
                OverheadLines = [overheadLine],
                HitTestIndex = scene.HitTestIndex
            });
    }

    private void OnClearDrawing(object sender, RoutedEventArgs e)
    {
        _deviceDrag.Cancel();
        _cableRouteDrag.Cancel();
        EndCanvasPan();
        _drawingTools.Cancel();
        DrawingSurface.ReleaseMouseCapture();
        _currentScene = null;
        _activeSource = null;
        _groundingPointPickMode = false;
        _pendingGroundingPointTerminalId = null;
        ResetWorkScopePick();
        _selectionResolver.SetSource(null);
        _selectionManager.Clear();
        _propertyInspector.Clear();
        IntervalEditorPanel.Visibility = Visibility.Collapsed;
        PoleNumberEditorPanel.Visibility = Visibility.Collapsed;
        GroundingPointEditorPanel.Visibility = Visibility.Collapsed;
        WorkScopeCreationPanel.Visibility = Visibility.Collapsed;
        WorkScopeEditorPanel.Visibility = Visibility.Collapsed;
        DrawingSurface.Clear();
        _viewport.Reset();
    }

    private void OnDrawingTypographySettings(object sender, RoutedEventArgs e)
    {
        var dialog = new DrawingTypographyDialog
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true)
        {
            RefreshDrawingScene();
        }
    }

    private void OnZoomIn(object sender, RoutedEventArgs e)
    {
        if (!_deviceDrag.IsActive && !_cableRouteDrag.IsActive)
        {
            _viewport.ZoomIn();
            UpdateConnectionPointerFromCurrentMouse();
        }
    }

    private void OnZoomOut(object sender, RoutedEventArgs e)
    {
        if (!_deviceDrag.IsActive && !_cableRouteDrag.IsActive)
        {
            _viewport.ZoomOut();
            UpdateConnectionPointerFromCurrentMouse();
        }
    }

    private void OnFitDrawing(object sender, RoutedEventArgs e)
    {
        if (!_deviceDrag.IsActive && !_cableRouteDrag.IsActive)
        {
            _viewport.Fit(_currentScene);
            UpdateConnectionPointerFromCurrentMouse();
        }
    }

    private void OnViewportChanged(object? sender, EventArgs e)
    {
        DrawingSurface.SetViewTransform(_viewport.Transform);
        UpdateCanvasStatus();
    }

    private void OnToggleGrid(object sender, RoutedEventArgs e)
    {
        ToggleGrid();
    }

    private void ToggleGrid()
    {
        _gridVisible = !_gridVisible;
        DrawingSurface.ShowGrid = _gridVisible;
        UpdateCanvasStatus();
        _actions.RefreshCanExecute();
    }

    private void UpdateCanvasStatus()
    {
        _shellViewModel.UpdateCanvasState(
            _viewport.Transform.Scale,
            _gridVisible,
            _shellViewModel.Toolbox.SelectedMode switch
            {
                _ when _overheadLineConnection.IsActive =>
                    _overheadLineConnection.State ==
                    OverheadLineToolState.PickingStartTerminal
                        ? "绘制架空线：请选择起点杆塔或设备端子"
                        : "绘制架空线：请选择下一杆塔或设备端子，Esc 结束",
                _ when _cableConnection.IsActive => _cableConnection.State ==
                    CableConnectionToolState.PickingStartTerminal
                        ? "绘制电缆：请选择起点"
                        : "绘制电缆：请选择终点",
                _ when _cableReconnect.IsActive => _cableReconnect.Endpoint ==
                    CableReconnectEndpoint.Start
                        ? "修改电缆：请选择新的起点端子"
                        : "修改电缆：请选择新的终点端子",
                _ when _poleSwitchAttachment.IsSelectingControlledConnection =>
                    _poleSwitchAttachment.StatusText,
                _ when _groundingPointPickMode => "添加工作地线：请选择端子",
                _ when _workScopePickState is WorkScopePickState.PickingBoundaryA =>
                    "添加工作范围：请选择边界 A",
                _ when _workScopePickState is WorkScopePickState.PickingBoundaryB =>
                    "添加工作范围：请选择边界 B",
                _ when _workScopePickState is WorkScopePickState.BoundaryAReady =>
                    "添加工作范围：请确认边界 A",
                _ when _workScopePickState is WorkScopePickState.BoundaryBReady =>
                    "添加工作范围：请确认边界 B",
                DesktopToolMode.CreateRingCabinet =>
                    "环网柜：单击图面放置，Esc 或右键退出",
                DesktopToolMode.CreatePole =>
                    "杆塔：单击图面连续放置，Esc 或右键退出",
                _ => "选择对象"
            },
            _selectionManager.SelectionCount);
        UpdatePresentationState();
        _actions.RefreshCanExecute();
    }

    private void UpdatePresentationState()
    {
        bool hasSession = _workspace.ActiveDocumentSession is not null;
        bool hasDrawingContent = _currentScene?.Elements.Any(element =>
            element.TargetId is not null) == true;
        _shellViewModel.UpdatePresentationState(hasSession, hasDrawingContent);
    }

    private void SyncToolboxModeFromInteraction()
    {
        DesktopToolMode mode = _placement.Mode switch
        {
            PlacementMode.PlacingPole => DesktopToolMode.CreatePole,
            PlacementMode.PlacingRingCabinet => DesktopToolMode.CreateRingCabinet,
            _ when _overheadLineConnection.IsActive => DesktopToolMode.CreateOverheadLine,
            _ when _cableConnection.IsActive => DesktopToolMode.CreateCable,
            _ when _poleSwitchAttachment.IsSelectingControlledConnection =>
                DesktopToolMode.AddPoleSwitch,
            _ when _groundingPointPickMode => DesktopToolMode.AddGroundingPoint,
            _ when _workScopePickState != WorkScopePickState.Idle => DesktopToolMode.AddWorkScope,
            _ => DesktopToolMode.Select
        };
        _shellViewModel.Toolbox.SetSelectedMode(mode);
    }

    private void OnDrawingSurfaceSizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        _viewport.SetViewportSize(e.NewSize);
    }

    private void OnDrawingSurfaceMouseWheel(
        object sender,
        System.Windows.Input.MouseWheelEventArgs e)
    {
        if (_deviceDrag.IsActive || _cableRouteDrag.IsActive)
        {
            return;
        }

        _viewport.ZoomFromWheel(e.GetPosition(DrawingSurface), e.Delta);
        UpdateConnectionPointerFromCurrentMouse();
        e.Handled = true;
    }

    private void OnDrawingSurfaceMouseDown(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Middle ||
            _deviceDrag.IsActive ||
            _cableRouteDrag.IsActive)
        {
            return;
        }

        _viewport.BeginPan(e.GetPosition(DrawingSurface));
        if (!DrawingSurface.CaptureMouse())
        {
            _viewport.CancelPan();
            return;
        }

        e.Handled = true;
    }

    private void OnDrawingSurfaceMouseRightButtonDown(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_selectionRectangle.Cancel())
        {
            DrawingSurface.ReleaseMouseCapture();
            RenderCurrentScene();
            e.Handled = true;
            return;
        }

        if (_deviceDrag.IsActive || _cableRouteDrag.IsActive)
        {
            CancelDeviceDrag();
            e.Handled = true;
            return;
        }

        if (_drawingTools.IsActive || _placement.Mode != PlacementMode.Idle)
        {
            _drawingTools.Cancel();
            SyncToolboxModeFromInteraction();
            UpdateCanvasStatus();
            e.Handled = true;
            return;
        }

        if (_groundingPointPickMode || _workScopePickState != WorkScopePickState.Idle)
        {
            CancelProfessionalPicking();
            SyncToolboxModeFromInteraction();
            UpdateCanvasStatus();
            e.Handled = true;
            return;
        }

        if (!IsInteractionIdle() || _currentScene is null)
        {
            e.Handled = true;
            return;
        }

        DocumentPoint point = _viewport.Transform.ViewToDocument(
            e.GetPosition(DrawingSurface));
        SelectionReference? target = _currentScene.HitTestIndex.HitTest(
            point,
            _viewport.Transform.ViewDistanceToDocument(4));
        if (target is not null && !_selectionManager.SelectionSet.Contains(target))
        {
            _selectionManager.Select(target);
        }

        ContextMenu menu = CreateDrawingContextMenu(target is null);
        if (menu.Items.Count > 0)
        {
            DrawingSurface.ContextMenu = menu;
            menu.PlacementTarget = DrawingSurface;
            menu.IsOpen = true;
        }

        e.Handled = true;
    }

    private ContextMenu CreateDrawingContextMenu(bool isBlank)
    {
        IReadOnlyList<DesktopContextActionKind> actionKinds =
            _contextMenuResolver.Resolve(
                IsInteractionIdle(),
                isBlank,
                _selectionManager.SelectionCount,
                CanRotateCurrentSelection(),
                CanOperateCurrentSwitch(),
                CanReconnectCurrentCable());
        var menu = new ContextMenu();
        DesktopContextActionKind? previous = null;
        foreach (DesktopContextActionKind actionKind in actionKinds)
        {
            if (previous is DesktopContextActionKind previousKind &&
                ContextActionGroup(previousKind) != ContextActionGroup(actionKind))
            {
                menu.Items.Add(new Separator());
            }

            menu.Items.Add(CreateContextMenuItem(actionKind));
            previous = actionKind;
        }

        return menu;
    }

    private MenuItem CreateContextMenuItem(DesktopContextActionKind actionKind)
    {
        (string header, System.Windows.Input.ICommand command) = actionKind switch
        {
            DesktopContextActionKind.Paste => ("粘贴", _actions.Paste),
            DesktopContextActionKind.SelectAll => ("全选", _actions.SelectAll),
            DesktopContextActionKind.FitDrawing => ("适合图形", _actions.FitDrawing),
            DesktopContextActionKind.ToggleGrid => ("显示网格", _actions.ToggleGrid),
            DesktopContextActionKind.Copy => ("复制", _actions.Copy),
            DesktopContextActionKind.Delete => ("删除", _actions.Delete),
            DesktopContextActionKind.RotateLeft => ("左转 90°", _actions.RotateLeft),
            DesktopContextActionKind.RotateRight => ("右转 90°", _actions.RotateRight),
            DesktopContextActionKind.SwitchOperation => ("开关分/合", _actions.SwitchOperation),
            DesktopContextActionKind.ReconnectCableStart =>
                ("修改电缆起点", _actions.ReconnectCableStart),
            DesktopContextActionKind.ReconnectCableEnd =>
                ("修改电缆终点", _actions.ReconnectCableEnd),
            _ => throw new ArgumentOutOfRangeException(nameof(actionKind))
        };
        var item = new MenuItem
        {
            Header = header,
            Command = command,
            IsCheckable = actionKind == DesktopContextActionKind.ToggleGrid,
            IsChecked = actionKind == DesktopContextActionKind.ToggleGrid && _gridVisible
        };
        string? iconKey = actionKind switch
        {
            DesktopContextActionKind.Paste => "Icon.Paste",
            DesktopContextActionKind.Copy => "Icon.Copy",
            DesktopContextActionKind.Delete => "Icon.Delete",
            DesktopContextActionKind.FitDrawing => "Icon.Fit",
            _ => null
        };
        if (iconKey is not null && TryFindResource(iconKey) is Geometry geometry)
        {
            var icon = new System.Windows.Shapes.Path
            {
                Data = geometry,
                Width = 16,
                Height = 16,
                Stretch = Stretch.Uniform,
                StrokeThickness = 1.7,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };
            icon.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "TextPrimaryBrush");
            item.Icon = icon;
        }

        return item;
    }

    private static int ContextActionGroup(DesktopContextActionKind actionKind) =>
        actionKind switch
        {
            DesktopContextActionKind.Paste or
            DesktopContextActionKind.SelectAll or
            DesktopContextActionKind.Copy or
            DesktopContextActionKind.Delete => 0,
            DesktopContextActionKind.FitDrawing or
            DesktopContextActionKind.ToggleGrid => 1,
            DesktopContextActionKind.RotateLeft or
            DesktopContextActionKind.RotateRight => 2,
            DesktopContextActionKind.SwitchOperation => 3,
            DesktopContextActionKind.ReconnectCableStart or
            DesktopContextActionKind.ReconnectCableEnd => 4,
            _ => 5
        };

    private void OnDrawingSurfaceMouseUp(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Middle ||
            !_viewport.IsPanning)
        {
            return;
        }

        EndCanvasPan();
        if (_overheadLineConnection.IsActive)
        {
            _drawingTools.UpdatePointer(
                _viewport.Transform.ViewToDocument(
                    e.GetPosition(DrawingSurface)));
        }

        e.Handled = true;
    }

    private void OnDrawingSurfaceLostMouseCapture(
        object sender,
        System.Windows.Input.MouseEventArgs e)
    {
        bool changed = _deviceDrag.Cancel();
        changed |= _cableRouteDrag.Cancel();
        bool selectionRectangleCanceled = _selectionRectangle.Cancel();
        if (changed)
        {
            RefreshDrawingScene();
        }
        else if (selectionRectangleCanceled)
        {
            RenderCurrentScene();
        }

        if (_viewport.IsPanning)
        {
            _viewport.CancelPan();
        }
    }

    private void CancelDeviceDrag()
    {
        bool changed = _deviceDrag.Cancel();
        changed |= _cableRouteDrag.Cancel();
        if (!changed)
        {
            return;
        }

        if (DrawingSurface.IsMouseCaptured)
        {
            DrawingSurface.ReleaseMouseCapture();
        }

        RefreshDrawingScene();
    }

    private void OnApplyIntervalConfiguration(object sender, RoutedEventArgs e)
    {
        if (_selectionManager.Selected is not
                { Kind: SelectionTargetKind.RingCabinetInterval } target ||
            !Enum.TryParse(IntervalTypeInput.SelectedItem?.ToString(), out IntervalKind intervalKind))
        {
            ShowCommandError("间隔修改失败", "请先选择一个有效的环网柜间隔。");
            return;
        }

        GroundingStructureKind? groundingStructure = intervalKind == IntervalKind.IntegratedFeederInterval
            ? Enum.TryParse(
                IntervalGroundingStructureInput.SelectedItem?.ToString(),
                out GroundingStructureKind parsed)
                ? parsed
                : null
            : null;
        PropertyEditResult result = _propertyEditor.TryChangeIntervalType(
            target,
            intervalKind,
            groundingStructure);
        if (!result.IsSuccess)
        {
            ShowCommandError("间隔修改失败", result.ErrorMessage ?? "间隔配置未能应用。");
            return;
        }

        RefreshDrawingScene();
    }

    private void OnIntervalTypeSelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        bool isIntegrated =
            IntervalTypeInput.SelectedItem is IntervalKind.IntegratedFeederInterval;
        IntervalGroundingStructureInput.IsEnabled = isIntegrated;
        if (isIntegrated && IntervalGroundingStructureInput.SelectedItem is null)
        {
            IntervalGroundingStructureInput.SelectedItem =
                GroundingStructureKind.UpperIsolationGrounding;
        }
    }

    private void OnApplyIntervalDisplayName(object sender, RoutedEventArgs e)
    {
        if (_selectionManager.Selected is not
            { Kind: SelectionTargetKind.RingCabinetInterval } target)
        {
            ShowCommandError("间隔修改失败", "请先选择一个环网柜间隔。");
            return;
        }

        PropertyEditResult result = _propertyEditor.TryEdit(
            target,
            PropertyCommandFactory.IntervalDisplayNamePropertyKey,
            IntervalDisplayNameInput.Text);
        if (!result.IsSuccess)
        {
            ShowCommandError("间隔修改失败", result.ErrorMessage ?? "间隔名称未能应用。");
            return;
        }

        RefreshDrawingScene();
    }

    private void EndCanvasPan()
    {
        if (!_viewport.IsPanning)
        {
            return;
        }

        _viewport.EndPan();
        if (DrawingSurface.IsMouseCaptured)
        {
            DrawingSurface.ReleaseMouseCapture();
        }
    }

    private void UpdateConnectionPointerFromCurrentMouse()
    {
        if (!_overheadLineConnection.IsActive && !_cableConnection.IsActive)
        {
            return;
        }

        _drawingTools.UpdatePointer(
            _viewport.Transform.ViewToDocument(
                System.Windows.Input.Mouse.GetPosition(DrawingSurface)));
    }

    private void OnUndo(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_commandStack.CanUndo)
            {
                return;
            }

            CancelDeviceDrag();

            bool hasTransition = false;
            SelectionReference? selection = null;
            if (_workspace.CurrentSession is { } session)
            {
                ICommand command = _commandStack.History[_commandStack.CurrentIndex - 1];
                hasTransition = session.SelectionTransitions.TryGetUndoSelection(
                    command,
                    out selection);
            }

            if (_commandStack.Undo())
            {
                RefreshDrawingScene();
                ApplySelectionTransition(hasTransition, selection);
            }
        }
        catch (ArgumentException exception)
        {
            _messageService.ShowError("撤销失败", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            _messageService.ShowError("撤销失败", exception.Message);
        }
    }

    private void OnRedo(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_commandStack.CanRedo)
            {
                return;
            }

            CancelDeviceDrag();

            bool hasTransition = false;
            SelectionReference? selection = null;
            if (_workspace.CurrentSession is { } session)
            {
                ICommand command = _commandStack.History[_commandStack.CurrentIndex];
                hasTransition = session.SelectionTransitions.TryGetRedoSelection(
                    command,
                    out selection);
            }

            if (_commandStack.Redo())
            {
                RefreshDrawingScene();
                ApplySelectionTransition(hasTransition, selection);
            }
        }
        catch (ArgumentException exception)
        {
            _messageService.ShowError("重做失败", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            _messageService.ShowError("重做失败", exception.Message);
        }
    }

    private void ApplySelectionTransition(
        bool hasTransition,
        SelectionReference? selection)
    {
        if (!hasTransition)
        {
            return;
        }

        if (selection is null)
        {
            _selectionManager.Clear();
            return;
        }

        if (_selectionResolver.Resolve(selection) is not null)
        {
            _selectionManager.Select(selection);
            return;
        }

        _selectionManager.Clear();
    }

    private void OnBeginAddGroundingPoint(object sender, RoutedEventArgs e)
    {
        if (_activeSource?.Document is null || _activeSource.DrawingLayout is null)
        {
            _messageService.ShowError(
                "无法添加工作地线",
                "当前场景没有可编辑的 DrawingDocument 工程。");
            return;
        }

        CancelDeviceDrag();
        _drawingTools.Cancel();
        ResetWorkScopePick();
        _groundingPointPickMode = true;
        _shellViewModel.Toolbox.SetSelectedMode(DesktopToolMode.AddGroundingPoint);
        _pendingGroundingPointTerminalId = null;
        _selectionManager.Clear();
        GroundingPointEditorPanel.Visibility = Visibility.Visible;
        GroundingPointTerminalText.Text = "请在图面中点击一个有效端子";
        GroundingPointLocationInput.Text = string.Empty;
        GroundingPointNumberInput.Text = string.Empty;
        GroundingPointNoteInput.Text = string.Empty;
        UpdateCanvasStatus();
    }

    private void OnBeginAddWorkScope(object sender, RoutedEventArgs e)
    {
        if (_activeSource?.Document is null || _activeSource.DrawingLayout is null)
        {
            _messageService.ShowError(
                "无法添加工作范围",
                "当前场景没有可编辑的 DrawingDocument 工程。");
            return;
        }

        CancelDeviceDrag();
        _drawingTools.Cancel();
        _groundingPointPickMode = false;
        _pendingGroundingPointTerminalId = null;
        ResetWorkScopePick();
        _workScopePickState = WorkScopePickState.PickingBoundaryA;
        _shellViewModel.Toolbox.SetSelectedMode(DesktopToolMode.AddWorkScope);
        WorkScopeBoundaryASideInput.Text = string.Empty;
        WorkScopeBoundaryBSideInput.Text = string.Empty;
        WorkScopeDescriptionInput.Text = string.Empty;
        WorkScopeGroundingPointIdsInput.Text = string.Empty;
        _selectionManager.Clear();
        UpdateWorkScopeEditor();
        UpdateCanvasStatus();
    }

    private void OnDrawRingCabinetComposition(object sender, RoutedEventArgs e)
    {
        _drawingTools.Cancel();
        (RingCabinet cabinet, RingCabinetLayout layout) =
            RingCabinetCompositionDemoFactory.Create();
        DrawingScene scene = _sceneBuilder.Build(cabinet, layout);

        ShowScene(
            scene,
            new PropertyInspectionSource
            {
                RingCabinet = cabinet,
                RingCabinetLayout = layout,
                HitTestIndex = scene.HitTestIndex
        });
    }

    private void OnToolboxPropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ToolboxViewModel.SelectedMode))
        {
            UpdateCanvasStatus();
        }
    }

    private void OnConfirmWorkScopeBoundaryA(object sender, RoutedEventArgs e)
    {
        if (_workScopePickState != WorkScopePickState.BoundaryAReady ||
            _pendingWorkScopeTerminalId is not Guid terminalId ||
            _pendingWorkScopeDeviceId is not Guid deviceId)
        {
            ShowCommandError("边界 A 未就绪", "请先在图面中选择边界 A 端子。");
            return;
        }

        if (string.IsNullOrWhiteSpace(WorkScopeBoundaryASideInput.Text))
        {
            ShowCommandError("边界 A 无效", "Side 必须由用户明确输入。");
            return;
        }

        _pendingWorkScopeStartBoundary = new BoundaryPointCommandValue(
            deviceId,
            terminalId,
            WorkScopeBoundaryASideInput.Text.Trim());
        _pendingWorkScopeTerminalId = null;
        _pendingWorkScopeDeviceId = null;
        _workScopePickState = WorkScopePickState.PickingBoundaryB;
        UpdateWorkScopeEditor();
    }

    private void OnConfirmWorkScopeBoundaryB(object sender, RoutedEventArgs e)
    {
        if (_workScopePickState != WorkScopePickState.BoundaryBReady ||
            _pendingWorkScopeTerminalId is not Guid terminalId ||
            _pendingWorkScopeDeviceId is not Guid deviceId)
        {
            ShowCommandError("边界 B 未就绪", "请先在图面中选择边界 B 端子。");
            return;
        }

        if (string.IsNullOrWhiteSpace(WorkScopeBoundaryBSideInput.Text))
        {
            ShowCommandError("边界 B 无效", "Side 必须由用户明确输入。");
            return;
        }

        _pendingWorkScopeEndBoundary = new BoundaryPointCommandValue(
            deviceId,
            terminalId,
            WorkScopeBoundaryBSideInput.Text.Trim());
        _pendingWorkScopeTerminalId = null;
        _pendingWorkScopeDeviceId = null;
        _workScopePickState = WorkScopePickState.ReadyToCommit;
        UpdateWorkScopeEditor();
    }

    private void OnApplyWorkScope(object sender, RoutedEventArgs e)
    {
        if (_selectionManager.Selected is { Kind: SelectionTargetKind.WorkScope } target)
        {
            if (!TryParseGroundingPointIds(
                    WorkScopeEditorGroundingPointIdsInput.Text,
                    out Guid[] groundingPointIds,
                    out string parseError))
            {
                ShowCommandError("工作范围修改失败", parseError);
                return;
            }

            PropertyEditResult result = _propertyEditor.TryEditWorkScope(
                target,
                WorkScopeEditorDescriptionInput.Text,
                groundingPointIds);
            if (!result.IsSuccess)
            {
                ShowCommandError(
                    "工作范围修改失败",
                    result.ErrorMessage ?? "输入无效。");
                return;
            }

            RefreshDrawingScene();
            return;
        }

        if (_workScopePickState != WorkScopePickState.ReadyToCommit ||
            _activeSource?.Document is null ||
            _pendingWorkScopeStartBoundary is not { } startBoundary ||
            _pendingWorkScopeEndBoundary is not { } endBoundary)
        {
            ShowCommandError(
                "无法创建工作范围",
                "请先分别选择并确认两个边界端子。");
            return;
        }

        if (!TryParseGroundingPointIds(
                WorkScopeGroundingPointIdsInput.Text,
                out Guid[] creationGroundingPointIds,
                out string error))
        {
            ShowCommandError("工作范围创建失败", error);
            return;
        }

        try
        {
            ICommand command = _professionalCommandFactory.CreateAddWorkScope(
                _activeSource.Document,
                startBoundary,
                endBoundary,
                WorkScopeDescriptionInput.Text,
                creationGroundingPointIds);
            AddWorkScopeCommand addCommand = (AddWorkScopeCommand)command;
            _commandStack.ExecuteCommand(addCommand);
            ResetWorkScopePick();
            RefreshDrawingScene();
            _selectionManager.Select(
                new SelectionReference(
                    SelectionTargetKind.WorkScope,
                    addCommand.After.WorkScopeId));
        }
        catch (ArgumentException exception)
        {
            ShowCommandError("工作范围创建失败", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ShowCommandError("工作范围创建失败", exception.Message);
        }
    }

    private void OnRemoveWorkScope(object sender, RoutedEventArgs e)
    {
        if (_activeSource?.Document is null ||
            _selectionManager.Selected is not
            { Kind: SelectionTargetKind.WorkScope, ObjectId: var workScopeId })
        {
            ShowCommandError("无法删除工作范围", "请先选择一个工作范围。");
            return;
        }

        try
        {
            ICommand command = _professionalCommandFactory.CreateRemoveWorkScope(
                _activeSource.Document,
                workScopeId);
            _commandStack.ExecuteCommand(command);
            _selectionManager.Clear();
            RefreshDrawingScene();
        }
        catch (ArgumentException exception)
        {
            ShowCommandError("工作范围删除失败", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ShowCommandError("工作范围删除失败", exception.Message);
        }
    }

    private void OnCancelWorkScope(object sender, RoutedEventArgs e)
    {
        ResetWorkScopePick();
        UpdateWorkScopeEditor();
    }

    private void OnDrawingSurfaceMouseLeftButtonDown(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_currentScene is null)
        {
            return;
        }

        System.Windows.Point point = e.GetPosition(DrawingSurface);
        DocumentPoint documentPoint = _viewport.Transform.ViewToDocument(point);

        if (_drawingTools.IsActive)
        {
            try
            {
                SelectionReference? activeHitTarget = _currentScene.HitTestIndex.HitTest(
                    documentPoint,
                    _viewport.Transform.ViewDistanceToDocument(4));
                _drawingTools.HandleClick(
                    documentPoint,
                    _viewport.Transform.ViewDistanceToDocument(8),
                    activeHitTarget);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                ShowCommandError("绘图操作失败", exception.Message);
            }

            e.Handled = true;
            return;
        }

        if (_groundingPointPickMode)
        {
            Guid? terminalId = HitTestTerminal(
                documentPoint,
                _viewport.Transform.ViewDistanceToDocument(8));
            if (terminalId is null)
            {
                ShowCommandError("端子选择失败", "点击位置没有可解析的端子。");
                e.Handled = true;
                return;
            }

            _pendingGroundingPointTerminalId = terminalId;
            _selectionManager.Select(
                new SelectionReference(SelectionTargetKind.Terminal, terminalId.Value));
            GroundingPointTerminalText.Text = $"已选择端子：{terminalId.Value}";
            e.Handled = true;
            return;
        }

        if (_workScopePickState is WorkScopePickState.PickingBoundaryA or
            WorkScopePickState.PickingBoundaryB)
        {
            Guid? terminalId = HitTestTerminal(
                documentPoint,
                _viewport.Transform.ViewDistanceToDocument(8));
            if (terminalId is null)
            {
                ShowCommandError("端子选择失败", "点击位置没有可解析的端子。");
                e.Handled = true;
                return;
            }

            Guid? deviceId = ResolveBoundaryDeviceId(terminalId.Value);
            if (deviceId is null)
            {
                ShowCommandError(
                    "边界选择失败",
                    "无法根据当前工程聚合关系解析端子所属设备。");
                e.Handled = true;
                return;
            }

            if (_workScopePickState == WorkScopePickState.PickingBoundaryB &&
                _pendingWorkScopeStartBoundary?.TerminalId == terminalId.Value)
            {
                ShowCommandError("边界选择失败", "两个边界不能引用同一个端子。");
                e.Handled = true;
                return;
            }

            _pendingWorkScopeTerminalId = terminalId;
            _pendingWorkScopeDeviceId = deviceId;
            _workScopePickState = _workScopePickState == WorkScopePickState.PickingBoundaryA
                ? WorkScopePickState.BoundaryAReady
                : WorkScopePickState.BoundaryBReady;
            _selectionManager.Select(
                new SelectionReference(SelectionTargetKind.Terminal, terminalId.Value));
            UpdateWorkScopeEditor();
            e.Handled = true;
            return;
        }

        if (e.ClickCount == 2)
        {
            SelectionReference? doubleClickTarget = _currentScene.HitTestIndex.HitTest(
                documentPoint,
                _viewport.Transform.ViewDistanceToDocument(4));
            if (doubleClickTarget?.Kind == SelectionTargetKind.Device &&
                _selectionResolver.Resolve(doubleClickTarget)?.SwitchDevice is not null)
            {
                _selectionManager.Select(doubleClickTarget);
                SwitchOperationResult result = _switchOperation.ToggleSelected();
                if (!result.IsSuccess)
                {
                    ShowCommandError("开关操作失败", result.ErrorMessage!);
                }

                e.Handled = true;
                return;
            }
        }

        SelectionHitTestEntry? hit = _currentScene.HitTestIndex.HitTestEntry(
            documentPoint,
            _viewport.Transform.ViewDistanceToDocument(4));
        SelectionReference? target = hit?.Target;
        bool shiftPressed =
            (System.Windows.Input.Keyboard.Modifiers &
             System.Windows.Input.ModifierKeys.Shift) != 0;
        if (target is null)
        {
            _selectionRectangle.Begin(documentPoint, shiftPressed);
            if (!DrawingSurface.CaptureMouse())
            {
                _selectionRectangle.Cancel();
            }

            e.Handled = true;
            return;
        }

        if (shiftPressed)
        {
            _selectionManager.Toggle(target);
            e.Handled = true;
            return;
        }

        bool targetWasSelected = _selectionManager.SelectionSet.Contains(target);
        if (_workspace.CurrentSession is not { } session)
        {
            e.Handled = true;
            return;
        }

        bool dragStarted;
        if (_selectionManager.SelectionCount > 1 && targetWasSelected)
        {
            dragStarted = _deviceDrag.TryBeginGroupDrag(
                _selectionManager.SelectionSet,
                target,
                documentPoint,
                session.PersistenceSession.Domain,
                session.Layout);
        }
        else
        {
            _selectionManager.Select(target);
            ResolvedSelection? dragSelection = _selectionResolver.Resolve(target);
            PoleAttachment? attachment = dragSelection?.PoleAttachment;
            Device? attachedDevice = attachment is null
                ? null
                : session.PersistenceSession.Domain.Devices.SingleOrDefault(device =>
                    device.Id == attachment.AttachedDeviceId);
            dragStarted = attachment is not null &&
                          attachedDevice is CableTermination or SwitchDevice
                ? _deviceDrag.TryBeginAttachmentDrag(
                    target,
                    attachment.AttachmentId,
                    documentPoint,
                    session.Layout,
                    attachment.PoleId,
                    attachedDevice is CableTermination)
                : (hit is not null && _cableRouteDrag.TryBeginDrag(
                       hit,
                       _currentScene.HitTestIndex.FindAll(target),
                       session.Layout)) ||
                  _deviceDrag.TryBeginDrag(
                      target,
                      documentPoint,
                      session.Layout);
        }

        if (dragStarted)
        {
            if (!DrawingSurface.CaptureMouse())
            {
                _deviceDrag.Cancel();
                _cableRouteDrag.Cancel();
            }
        }

        e.Handled = true;
    }

    private void CancelProfessionalPicking()
    {
        _groundingPointPickMode = false;
        _pendingGroundingPointTerminalId = null;
        ResetWorkScopePick();
    }

    private void OnDrawingSurfaceMouseMove(
        object sender,
        System.Windows.Input.MouseEventArgs e)
    {
        System.Windows.Point point = e.GetPosition(DrawingSurface);
        if (_viewport.IsPanning)
        {
            _viewport.UpdatePan(point);
            if (_overheadLineConnection.IsActive)
            {
                _drawingTools.UpdatePointer(
                    _viewport.Transform.ViewToDocument(point));
            }

            e.Handled = true;
            return;
        }

        if (_overheadLineConnection.IsActive)
        {
            _drawingTools.UpdatePointer(
                _viewport.Transform.ViewToDocument(point));
            e.Handled = true;
            return;
        }

        if (_cableConnection.IsActive)
        {
            _drawingTools.UpdatePointer(
                _viewport.Transform.ViewToDocument(point));
            e.Handled = true;
            return;
        }

        if (_selectionRectangle.IsActive)
        {
            _selectionRectangle.Update(
                _viewport.Transform.ViewToDocument(point));
            RenderCurrentScene();
            e.Handled = true;
            return;
        }

        if ((!_deviceDrag.IsActive && !_cableRouteDrag.IsActive) ||
            e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            return;
        }

        DocumentPoint documentPoint = _viewport.Transform.ViewToDocument(point);
        try
        {
            if (_cableRouteDrag.IsActive
                    ? _cableRouteDrag.UpdatePreview(documentPoint)
                    : _deviceDrag.UpdatePreview(documentPoint))
            {
                RefreshDrawingScene();
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            CancelDeviceDrag();
            ShowCommandError("拖动预览失败", exception.Message);
        }

        e.Handled = true;
    }

    private void OnDrawingSurfaceMouseLeftButtonUp(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_selectionRectangle.IsActive)
        {
            _selectionRectangle.Update(
                _viewport.Transform.ViewToDocument(e.GetPosition(DrawingSurface)));
            _selectionRectangle.Complete(_currentScene!.HitTestIndex);
            DrawingSurface.ReleaseMouseCapture();
            RenderCurrentScene();
            e.Handled = true;
            return;
        }

        if (!_deviceDrag.IsActive && !_cableRouteDrag.IsActive)
        {
            return;
        }

        ICommand? command = null;
        bool commandRecorded = false;
        try
        {
            command = _cableRouteDrag.IsActive
                ? _cableRouteDrag.Commit()
                : _deviceDrag.Commit();
            DrawingSurface.ReleaseMouseCapture();
            if (command is not null)
            {
                _commandStack.ExecuteCommand(command);
                commandRecorded = true;
                RefreshDrawingScene();
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            try
            {
                if (!commandRecorded)
                {
                    command?.Undo();
                }
                if (_workspace.CurrentSession is not null)
                {
                    RefreshDrawingScene();
                }
            }
            catch (Exception recoveryException) when (
                recoveryException is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                ShowCommandError("拖动恢复失败", recoveryException.Message);
            }

            ShowCommandError("提交拖动失败", exception.Message);
        }

        e.Handled = true;
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        _shellViewModel.RefreshCommandStates();
        UpdateCanvasStatus();
        if (_selectionManager.SelectionCount > 1)
        {
            _propertyInspector.Apply(
                new PropertyInspectorSnapshot(
                    null,
                    $"已选择 {_selectionManager.SelectionCount} 个对象",
                    "批量属性编辑将在后续版本提供。",
                    []));
            CollapseSingleSelectionEditors();
            RenderCurrentScene();
            return;
        }

        _propertyInspector.Apply(
            _propertyProjector.Project(
                _selectionResolver.Resolve(_selectionManager.Selected)));
        UpdateRingCabinetEditor();
        UpdatePoleNumberEditor();
        UpdatePoleInstalledDevicesEditor();
        UpdateIntervalEditor();
        UpdateAttachmentOffsetEditor();
        UpdateAttachmentLayoutEditor();
        UpdateCableTerminationDisplayNameEditor();
        UpdateGroundingPointEditor();
        UpdateWorkScopeEditor();
        RenderCurrentScene();
    }

    private void CollapseSingleSelectionEditors()
    {
        RingCabinetEditorPanel.Visibility = Visibility.Collapsed;
        PoleNumberEditorPanel.Visibility = Visibility.Collapsed;
        PoleInstalledDevicesPanel.Visibility = Visibility.Collapsed;
        IntervalEditorPanel.Visibility = Visibility.Collapsed;
        AttachmentOffsetEditorPanel.Visibility = Visibility.Collapsed;
        AttachmentLayoutEditorPanel.Visibility = Visibility.Collapsed;
        CableTerminationDisplayNameEditorPanel.Visibility = Visibility.Collapsed;
        GroundingPointEditorPanel.Visibility = Visibility.Collapsed;
        WorkScopeCreationPanel.Visibility = Visibility.Collapsed;
        WorkScopeEditorPanel.Visibility = Visibility.Collapsed;
        CablePropertyEditorPanel.Visibility = Visibility.Collapsed;
        SwitchOperationPanel.Visibility = Visibility.Collapsed;
    }

    private void OnApplyPoleNumber(object sender, RoutedEventArgs e)
    {
        if (_selectionManager.Selected is not { } target)
        {
            return;
        }

        PropertyEditResult result = _propertyEditor.TryEdit(
            target,
            PropertyCommandFactory.PoleNumberPropertyKey,
            PoleNumberInput.Text);
        if (!result.IsSuccess)
        {
            MessageBox.Show(
                result.ErrorMessage,
                "属性修改失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        RefreshDrawingScene();
    }

    private void OnApplyRingCabinetDisplayName(object sender, RoutedEventArgs e)
    {
        ApplyRingCabinetProperty(
            PropertyCommandFactory.RingCabinetDisplayNamePropertyKey,
            RingCabinetDisplayNameInput.Text);
    }

    private void OnApplyRingCabinetLineName(object sender, RoutedEventArgs e)
    {
        ApplyRingCabinetProperty(
            PropertyCommandFactory.RingCabinetLineNamePropertyKey,
            RingCabinetLineNameInput.Text);
    }

    private void ApplyRingCabinetProperty(string propertyKey, string input)
    {
        if (_selectionManager.Selected is not
            { Kind: SelectionTargetKind.RingCabinet } target)
        {
            ShowCommandError("环网柜属性修改失败", "请先选择一个环网柜。");
            return;
        }

        PropertyEditResult result = _propertyEditor.TryEdit(target, propertyKey, input);
        if (!result.IsSuccess)
        {
            ShowCommandError(
                "环网柜属性修改失败",
                result.ErrorMessage ?? "环网柜属性未能应用。");
            return;
        }

        RefreshDrawingScene();
    }

    private void OnApplyCableType(object sender, RoutedEventArgs e)
    {
        ApplyCableProperty(
            PropertyCommandFactory.CableTypePropertyKey,
            CableTypeInput.Text);
    }

    private void OnApplyCableLength(object sender, RoutedEventArgs e)
    {
        ApplyCableProperty(
            PropertyCommandFactory.CableLengthPropertyKey,
            CableLengthInput.Text);
    }

    private void ApplyCableProperty(string propertyKey, string input)
    {
        if (_selectionManager.Selected is not
            { Kind: SelectionTargetKind.CableSegment } target)
        {
            ShowCommandError("电缆属性修改失败", "请先选择一条电缆。");
            return;
        }

        PropertyEditResult result = _propertyEditor.TryEdit(
            target,
            propertyKey,
            input);
        if (!result.IsSuccess)
        {
            ShowCommandError(
                "电缆属性修改失败",
                result.ErrorMessage ?? "电缆属性未能应用。");
            return;
        }

        RefreshDrawingScene();
    }

    private void OnBeginCableReconnectStart(object sender, RoutedEventArgs e)
    {
        BeginCableReconnect(_cableReconnect.BeginStart);
    }

    private void OnBeginCableReconnectEnd(object sender, RoutedEventArgs e)
    {
        BeginCableReconnect(_cableReconnect.BeginEnd);
    }

    private void OnCancelCableReconnect(object sender, RoutedEventArgs e)
    {
        _cableReconnect.Cancel();
        UpdateCanvasStatus();
    }

    private void BeginCableReconnect(Action begin)
    {
        try
        {
            CancelProfessionalPicking();
            _drawingTools.Cancel();
            begin();
            UpdateCanvasStatus();
        }
        catch (InvalidOperationException exception)
        {
            _messageService.ShowError("电缆端点修改失败", exception.Message);
        }
    }

    private void OnApplyAttachmentOffset(object sender, RoutedEventArgs e)
    {
        if (_workspace.CurrentSession is not { } session ||
            _selectionManager.Selected is not
                { Kind: SelectionTargetKind.PoleAttachment } target)
        {
            ShowCommandError("附属设备位置修改失败", "请先选择一个杆塔附属设备。");
            return;
        }

        SelectionReference? beforeSelection = target;
        PropertyEditResult result = _propertyEditor.TryEditAttachmentOffset(
            session.Layout,
            target,
            AttachmentOffsetXInput.Text,
            AttachmentOffsetYInput.Text,
            out ICommand? executedCommand);
        if (!result.IsSuccess)
        {
            ShowCommandError(
                "附属设备位置修改失败",
                result.ErrorMessage ?? "输入无效。");
            return;
        }

        if (executedCommand is not null)
        {
            session.SelectionTransitions.RecordExecuted(
                executedCommand,
                SelectionTransition.Preserve(beforeSelection));
            session.SelectionTransitions.Prune(session.CommandStack.History);
        }

        RefreshDrawingScene();
    }

    private void OnApplyCableTerminationDisplayName(object sender, RoutedEventArgs e)
    {
        if (_workspace.CurrentSession is not { } session ||
            _selectionManager.Selected is not
                { Kind: SelectionTargetKind.PoleAttachment } target)
        {
            ShowCommandError("电缆终端名称修改失败", "请先选择一个电缆终端附属设备。");
            return;
        }

        SelectionReference? beforeSelection = target;
        PropertyEditResult result = _propertyEditor.TryEditCableTerminationDisplayName(
            target,
            CableTerminationDisplayNameInput.Text,
            out ICommand? executedCommand);
        if (!result.IsSuccess)
        {
            ShowCommandError(
                "电缆终端名称修改失败",
                result.ErrorMessage ?? "输入无效。");
            return;
        }

        if (executedCommand is not null)
        {
            session.SelectionTransitions.RecordExecuted(
                executedCommand,
                SelectionTransition.Preserve(beforeSelection));
            session.SelectionTransitions.Prune(session.CommandStack.History);
        }

        RefreshDrawingScene();
    }

    private void OnApplyAttachmentLayout(object sender, RoutedEventArgs e)
    {
        if (_workspace.CurrentSession is not { } session ||
            _selectionManager.Selected is not
                { Kind: SelectionTargetKind.PoleAttachment } target)
        {
            ShowCommandError("附属设备布局修改失败", "请先选择一个杆塔附属设备。");
            return;
        }

        SelectionReference? beforeSelection = target;
        PropertyEditResult result = _propertyEditor.TryEditAttachmentLayout(
            session.Layout,
            target,
            AttachmentWidthInput.Text,
            AttachmentHeightInput.Text,
            AttachmentLabelOffsetXInput.Text,
            AttachmentLabelOffsetYInput.Text,
            out ICommand? executedCommand);
        if (!result.IsSuccess)
        {
            ShowCommandError(
                "附属设备布局修改失败",
                result.ErrorMessage ?? "输入无效。");
            return;
        }

        if (executedCommand is not null)
        {
            session.SelectionTransitions.RecordExecuted(
                executedCommand,
                SelectionTransition.Preserve(beforeSelection));
            session.SelectionTransitions.Prune(session.CommandStack.History);
        }

        RefreshDrawingScene();
    }

    private void OnApplyGroundingPoint(object sender, RoutedEventArgs e)
    {
        if (_selectionManager.Selected is { Kind: SelectionTargetKind.GroundingPoint } target)
        {
            PropertyEditResult result = _propertyEditor.TryEditGroundingPoint(
                target,
                GroundingPointLocationInput.Text,
                GroundingPointNumberInput.Text,
                GroundingPointNoteInput.Text);
            if (!result.IsSuccess)
            {
                ShowCommandError("工作地线修改失败", result.ErrorMessage ?? "输入无效。");
                return;
            }

            RefreshDrawingScene();
            return;
        }

        if (_activeSource?.Document is null ||
            _pendingGroundingPointTerminalId is not Guid terminalId)
        {
            ShowCommandError("无法创建工作地线", "请先点击一个有效端子。");
            return;
        }

        try
        {
            ICommand command = _professionalCommandFactory.CreateAddGroundingPoint(
                _activeSource.Document,
                terminalId,
                GroundingPointLocationInput.Text,
                GroundingPointNumberInput.Text,
                GroundingPointNoteInput.Text);
            AddGroundingPointCommand addCommand = (AddGroundingPointCommand)command;
            _commandStack.ExecuteCommand(addCommand);
            _groundingPointPickMode = false;
            _pendingGroundingPointTerminalId = null;
            RefreshDrawingScene();
            _selectionManager.Select(
                new SelectionReference(
                    SelectionTargetKind.GroundingPoint,
                    addCommand.After.GroundingPointId));
        }
        catch (ArgumentException exception)
        {
            ShowCommandError("工作地线创建失败", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ShowCommandError("工作地线创建失败", exception.Message);
        }
    }

    private void OnRemoveGroundingPoint(object sender, RoutedEventArgs e)
    {
        if (_activeSource?.Document is null ||
            _selectionManager.Selected is not
            { Kind: SelectionTargetKind.GroundingPoint, ObjectId: var groundingPointId })
        {
            ShowCommandError("无法删除工作地线", "请先选择一个工作地线。");
            return;
        }

        try
        {
            ICommand command = _professionalCommandFactory.CreateRemoveGroundingPoint(
                _activeSource.Document,
                groundingPointId);
            _commandStack.ExecuteCommand(command);
            _selectionManager.Clear();
            RefreshDrawingScene();
        }
        catch (ArgumentException exception)
        {
            ShowCommandError("工作地线删除失败", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ShowCommandError("工作地线删除失败", exception.Message);
        }
    }

    private void UpdatePoleNumberEditor()
    {
        ResolvedSelection? selection = _selectionResolver.Resolve(_selectionManager.Selected);
        if (selection?.Pole is not { } pole)
        {
            PoleNumberEditorPanel.Visibility = Visibility.Collapsed;
            return;
        }

        PoleNumberEditorPanel.Visibility = Visibility.Visible;
        PoleNumberInput.Text = pole.PoleNumber;
    }

    private void UpdatePoleInstalledDevicesEditor()
    {
        ResolvedSelection? selection = _selectionResolver.Resolve(_selectionManager.Selected);
        Pole? pole = selection?.Pole;
        if (pole is null && selection?.PoleAttachment is { } selectedAttachment &&
            _activeSource?.Document is { } selectedDocument)
        {
            pole = selectedDocument.Devices.OfType<Pole>().SingleOrDefault(item =>
                item.Id == selectedAttachment.PoleId);
        }
        if (pole is null)
        {
            PoleInstalledDevicesPanel.Visibility = Visibility.Collapsed;
            return;
        }

        PoleInstalledDevicesPanel.Visibility = Visibility.Visible;
        if (_activeSource?.Document is not { } document)
        {
            PoleInstalledDevicesText.Text = "暂无安装设备";
            return;
        }

        var items = document.PoleAttachments
            .Where(attachment => attachment.PoleId == pole.Id)
            .Select(attachment =>
            {
                Device? device = document.Devices.SingleOrDefault(item =>
                    item.Id == attachment.AttachedDeviceId);
                return new InstalledDeviceItem(
                    attachment.AttachmentId,
                    device is null
                        ? attachment.AttachedDeviceId.ToString()
                        : $"{device.GetType().Name}  {device.DisplayName ?? device.Id.ToString()}");
            })
            .ToArray();
        PoleInstalledDevicesText.Text = items.Length == 0
            ? "暂无安装设备"
            : "请选择下方安装设备进行管理";
        PoleInstalledDevicesList.ItemsSource = items;
        Guid? selectedAttachmentId = selection?.PoleAttachment?.AttachmentId;
        PoleInstalledDevicesList.SelectedItem = selectedAttachmentId is Guid attachmentId
            ? items.SingleOrDefault(item => item.AttachmentId == attachmentId)
            : null;
    }

    private void OnPoleInstalledDeviceSelected(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (PoleInstalledDevicesList.SelectedItem is not InstalledDeviceItem item ||
            _selectionManager.Selected?.ObjectId == item.AttachmentId)
        {
            return;
        }

        _selectionManager.Select(new SelectionReference(
            SelectionTargetKind.PoleAttachment,
            item.AttachmentId));
    }

    private void OnRotatePoleAttachmentLeft(object sender, RoutedEventArgs e) =>
        RotateSelectedPoleAttachment(-1);

    private void OnRotatePoleAttachmentRight(object sender, RoutedEventArgs e) =>
        RotateSelectedPoleAttachment(1);

    private void RotateSelectedPoleAttachment(int quarterTurns)
    {
        try
        {
            _poleAttachmentManagement.RotateCurrent(
                _selectionManager.Selected,
                quarterTurns,
                GetSelectedPoleAttachmentIdFromList());
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _messageService.ShowError("旋转失败", exception.Message);
        }
    }

    private void OnRemovePoleAttachment(object sender, RoutedEventArgs e)
    {
        try
        {
            _poleAttachmentManagement.RemoveCurrent(
                _selectionManager.Selected,
                GetSelectedPoleAttachmentIdFromList());
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ShowCommandError("删除失败", exception.Message);
        }
    }

    private Guid? GetSelectedPoleAttachmentIdFromList()
    {
        return PoleInstalledDevicesList.SelectedItem is InstalledDeviceItem item
            ? item.AttachmentId
            : null;
    }

    private void UpdateRingCabinetEditor()
    {
        ResolvedSelection? selection = _selectionResolver.Resolve(
            _selectionManager.Selected);
        if (selection?.Reference.Kind != SelectionTargetKind.RingCabinet ||
            selection.RingCabinet is not { } cabinet)
        {
            RingCabinetEditorPanel.Visibility = Visibility.Collapsed;
            return;
        }

        RingCabinetEditorPanel.Visibility = Visibility.Visible;
        RingCabinetDisplayNameInput.Text = cabinet.DisplayName ?? string.Empty;
        RingCabinetLineNameInput.Text = cabinet.LineName;
    }

    private void UpdateCablePropertyEditor()
    {
        if (!_selectionManager.HasSingleSelection)
        {
            CablePropertyEditorPanel.Visibility = Visibility.Collapsed;
            return;
        }

        ResolvedSelection? selection = _selectionResolver.Resolve(
            _selectionManager.Selected);
        if (selection?.Reference.Kind != SelectionTargetKind.CableSegment ||
            selection.CableSegment is not { } cable)
        {
            CablePropertyEditorPanel.Visibility = Visibility.Collapsed;
            return;
        }

        CablePropertyEditorPanel.Visibility = Visibility.Visible;
        CableTypeInput.Text = cable.CableType;
        CableLengthInput.Text = cable.Length.ToString(
            "R",
            CultureInfo.InvariantCulture);
    }

    private void UpdateIntervalEditor()
    {
        ResolvedSelection? selection = _selectionResolver.Resolve(_selectionManager.Selected);
        if (selection?.Reference.Kind != SelectionTargetKind.RingCabinetInterval ||
            selection.RingCabinetInterval is not { } interval)
        {
            IntervalEditorPanel.Visibility = Visibility.Collapsed;
            return;
        }

        IntervalEditorPanel.Visibility = Visibility.Visible;
        IntervalDisplayNameInput.Text = interval.DisplayName;
        IntervalTypeInput.ItemsSource = SupportedIntervalKinds;
        IntervalGroundingStructureInput.ItemsSource = SupportedGroundingStructures;
        IntervalTypeInput.SelectedItem = interval.IntervalKind;
        IntervalGroundingStructureInput.SelectedItem = interval.GroundingStructureKind;
        IntervalGroundingStructureInput.IsEnabled =
            interval.IntervalKind == IntervalKind.IntegratedFeederInterval;
    }

    private void UpdateAttachmentOffsetEditor()
    {
        ResolvedSelection? selection = _selectionResolver.Resolve(_selectionManager.Selected);
        if (selection?.Reference.Kind != SelectionTargetKind.PoleAttachment ||
            selection.AttachmentLayout is not { } layout)
        {
            AttachmentOffsetEditorPanel.Visibility = Visibility.Collapsed;
            return;
        }

        AttachmentOffsetEditorPanel.Visibility = Visibility.Visible;
        AttachmentOffsetXInput.Text = layout.Offset.XMillimeters.ToString(
            "R",
            CultureInfo.InvariantCulture);
        AttachmentOffsetYInput.Text = layout.Offset.YMillimeters.ToString(
            "R",
            CultureInfo.InvariantCulture);
    }

    private void UpdateAttachmentLayoutEditor()
    {
        ResolvedSelection? selection = _selectionResolver.Resolve(_selectionManager.Selected);
        if (selection?.Reference.Kind != SelectionTargetKind.PoleAttachment ||
            selection.AttachmentLayout is not { } layout)
        {
            AttachmentLayoutEditorPanel.Visibility = Visibility.Collapsed;
            return;
        }

        AttachmentLayoutEditorPanel.Visibility = Visibility.Visible;
        AttachmentWidthInput.Text = layout.WidthMillimeters.ToString(
            "R",
            CultureInfo.InvariantCulture);
        AttachmentHeightInput.Text = layout.HeightMillimeters.ToString(
            "R",
            CultureInfo.InvariantCulture);
        AttachmentLabelOffsetXInput.Text = layout.LabelOffset.XMillimeters.ToString(
            "R",
            CultureInfo.InvariantCulture);
        AttachmentLabelOffsetYInput.Text = layout.LabelOffset.YMillimeters.ToString(
            "R",
            CultureInfo.InvariantCulture);
    }

    private void UpdateCableTerminationDisplayNameEditor()
    {
        ResolvedSelection? selection = _selectionResolver.Resolve(_selectionManager.Selected);
        if (selection?.Reference.Kind != SelectionTargetKind.PoleAttachment ||
            selection.AttachedDevice is not CableTermination cableTermination)
        {
            CableTerminationDisplayNameEditorPanel.Visibility = Visibility.Collapsed;
            return;
        }

        CableTerminationDisplayNameEditorPanel.Visibility = Visibility.Visible;
        CableTerminationDisplayNameInput.Text = cableTermination.DisplayName ?? string.Empty;
    }

    private void UpdateGroundingPointEditor()
    {
        ResolvedSelection? selection = _selectionResolver.Resolve(_selectionManager.Selected);
        if (selection?.GroundingPoint is { } groundingPoint)
        {
            GroundingPointEditorPanel.Visibility = Visibility.Visible;
            GroundingPointTerminalText.Text = "已绑定到图面端子";
            GroundingPointLocationInput.Text = groundingPoint.Location;
            GroundingPointNumberInput.Text = groundingPoint.Number ?? string.Empty;
            GroundingPointNoteInput.Text = groundingPoint.Note ?? string.Empty;
            return;
        }

        if (_groundingPointPickMode)
        {
            GroundingPointEditorPanel.Visibility = Visibility.Visible;
            return;
        }

        GroundingPointEditorPanel.Visibility = Visibility.Collapsed;
    }

    private void UpdateWorkScopeEditor()
    {
        if (_selectionResolver.Resolve(_selectionManager.Selected) is
            { WorkScope: { } workScope })
        {
            WorkScopeCreationPanel.Visibility = Visibility.Collapsed;
            WorkScopeEditorPanel.Visibility = Visibility.Visible;
            WorkScopeEditorBoundaryText.Text =
                $"边界 A：{workScope.StartBoundary.Side}\n" +
                $"边界 B：{workScope.EndBoundary.Side}";
            WorkScopeEditorDescriptionInput.Text = workScope.Description;
            WorkScopeEditorGroundingPointIdsInput.Text =
                string.Join(", ", workScope.GroundingPointIds);
            return;
        }

        WorkScopeEditorPanel.Visibility = Visibility.Collapsed;
        if (_workScopePickState == WorkScopePickState.Idle)
        {
            WorkScopeCreationPanel.Visibility = Visibility.Collapsed;
            return;
        }

        WorkScopeCreationPanel.Visibility = Visibility.Visible;
        WorkScopePickStateText.Text = _workScopePickState.ToString();
        WorkScopeBoundaryAText.Text = _pendingWorkScopeStartBoundary is { } start
            ? FormatBoundary(start)
            : _workScopePickState is WorkScopePickState.BoundaryAReady
                ? FormatPendingBoundary()
                : "未选择";
        WorkScopeBoundaryBText.Text = _pendingWorkScopeEndBoundary is { } end
            ? FormatBoundary(end)
            : _workScopePickState is WorkScopePickState.BoundaryBReady
                ? FormatPendingBoundary()
                : "未选择";
    }

    private string FormatPendingBoundary()
    {
        if (_pendingWorkScopeTerminalId is not Guid terminalId ||
            _pendingWorkScopeDeviceId is not Guid deviceId)
        {
            return "未选择";
        }

        return "已选择图面端子";
    }

    private static string FormatBoundary(BoundaryPointCommandValue boundary)
    {
        return $"已选择 · 侧别：{boundary.Side}";
    }

    private Guid? ResolveBoundaryDeviceId(Guid terminalId)
    {
        if (_activeSource?.Document is not { } document)
        {
            return null;
        }

        Terminal? terminal = document.Terminals
            .SingleOrDefault(candidate => candidate.Id == terminalId);
        if (terminal is null)
        {
            return null;
        }

        if (terminal.OwnerType == TopologyOwnerType.Device &&
            document.Devices.Any(device => device.Id == terminal.OwnerId))
        {
            return terminal.OwnerId;
        }

        if (terminal.OwnerType == TopologyOwnerType.InternalAggregate)
        {
            Guid parentCabinetId = document.Devices
                .OfType<RingCabinet>()
                .SelectMany(cabinet => cabinet.Intervals
                    .Where(interval => interval.IntervalId == terminal.OwnerId)
                    .Select(interval => cabinet.Id))
                .SingleOrDefault();
            return parentCabinetId == Guid.Empty ? null : parentCabinetId;
        }

        return null;
    }

    private static bool TryParseGroundingPointIds(
        string input,
        out Guid[] ids,
        out string error)
    {
        string[] tokens = input.Split(
            [',', ';', ' ', '\r', '\n', '\t'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parsed = new List<Guid>(tokens.Length);
        foreach (string token in tokens)
        {
            if (!Guid.TryParse(token, out Guid id) || id == Guid.Empty)
            {
                ids = [];
                error = $"GroundingPointId '{token}' 不是有效的稳定 ID。";
                return false;
            }

            parsed.Add(id);
        }

        if (parsed.Distinct().Count() != parsed.Count)
        {
            ids = [];
            error = "GroundingPointId 不能重复。";
            return false;
        }

        ids = parsed.ToArray();
        error = string.Empty;
        return true;
    }

    private void ResetWorkScopePick()
    {
        _workScopePickState = WorkScopePickState.Idle;
        _pendingWorkScopeStartBoundary = null;
        _pendingWorkScopeEndBoundary = null;
        _pendingWorkScopeTerminalId = null;
        _pendingWorkScopeDeviceId = null;
        WorkScopeCreationPanel.Visibility = Visibility.Collapsed;
    }

    private void ShowScene(DrawingScene scene, PropertyInspectionSource source)
    {
        _groundingPointPickMode = false;
        _pendingGroundingPointTerminalId = null;
        ResetWorkScopePick();
        _currentScene = scene;
        _activeSource = source;
        _selectionResolver.SetSource(source);
        _selectionManager.Clear();
        _propertyInspector.Clear();
        RenderCurrentScene();
    }

    private void RefreshDrawingScene()
    {
        if (_workspace.CurrentSession is { } runtimeSession &&
            ReferenceEquals(runtimeSession.PersistenceSession.Domain, _activeSource?.Document))
        {
            runtimeSession.RebuildScene();
            _currentScene = runtimeSession.Scene;
            _activeSource = runtimeSession.InspectionSource;
            _selectionResolver.SetSource(_activeSource);
            OnSelectionChanged(this, EventArgs.Empty);
            return;
        }

        if (_activeSource?.DrawingLayout is not { } layout)
        {
            RenderCurrentScene();
            return;
        }

        PropertyInspectionSource source = _activeSource;
        DrawingScene scene = source.Document is not null
            ? _sceneBuilder.Build(
                source.Document,
                new RuntimeLayoutDocument(layout, source.RingCabinetLayouts))
            : _sceneBuilder.Build(
                layout,
                source.Poles,
                source.PoleAttachments,
                source.Devices,
                source.Connections,
                source.OverheadLines);
        _currentScene = scene;
        _activeSource = new PropertyInspectionSource
        {
            Document = source.Document,
            DrawingLayout = layout,
            RingCabinetLayouts = source.RingCabinetLayouts,
            Poles = source.Poles,
            Devices = source.Devices,
            PoleAttachments = source.PoleAttachments,
            Connections = source.Connections,
            OverheadLines = source.OverheadLines,
            WorkScopes = source.WorkScopes,
            GroundingPoints = source.GroundingPoints,
            Terminals = source.Terminals,
            HitTestIndex = scene.HitTestIndex
        };
        _selectionResolver.SetSource(_activeSource);
        _selectionManager.Retain(reference =>
            _selectionResolver.Resolve(reference) is not null);
        OnSelectionChanged(this, EventArgs.Empty);
    }

    private Guid? HitTestTerminal(
        DocumentPoint point,
        double toleranceMillimeters)
    {
        if (_activeSource?.Document is not { } document ||
            _activeSource.DrawingLayout is not { } layout)
        {
            return null;
        }

        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            layout,
            _activeSource.RingCabinetLayouts,
            document.Connections,
            document.CableSegments);
        return anchors.Anchors
            .Where(anchor =>
                Math.Pow(anchor.Position.XMillimeters - point.XMillimeters, 2) +
                Math.Pow(anchor.Position.YMillimeters - point.YMillimeters, 2) <=
                toleranceMillimeters * toleranceMillimeters)
            .OrderBy(anchor =>
                Math.Pow(anchor.Position.XMillimeters - point.XMillimeters, 2) +
                Math.Pow(anchor.Position.YMillimeters - point.YMillimeters, 2))
            .Select(anchor => (Guid?)anchor.TerminalId)
            .FirstOrDefault();
    }

    private static void ShowCommandError(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void RenderCurrentScene()
    {
        if (_currentScene is null)
        {
            return;
        }

        UpdateSwitchOperationEditor();
        UpdateCablePropertyEditor();

        var elements = _currentScene.Elements.ToList();
        elements.AddRange(_drawingTools.CreateTransientElements());
        elements.AddRange(
            SelectionOverlayBuilder.CreateElements(
                _currentScene.HitTestIndex,
                _selectionManager.SelectionSet));
        elements.AddRange(_selectionRectangle.CreateOverlayElements());
        double pixelsPerDip = VisualTreeHelper.GetDpi(DrawingSurface).PixelsPerDip;
        DrawingSurface.Show(_renderer.Render(new DrawingScene(elements), pixelsPerDip));
        DrawingSurface.SetViewTransform(_viewport.Transform);
    }

    private void UpdateSwitchOperationEditor()
    {
        if (!_selectionManager.HasSingleSelection)
        {
            SwitchOperationPanel.Visibility = Visibility.Collapsed;
            return;
        }

        ResolvedSelection? selection = _selectionResolver.Resolve(
            _selectionManager.Selected);
        if (selection?.SwitchDevice is not { } switchDevice)
        {
            SwitchOperationPanel.Visibility = Visibility.Collapsed;
            return;
        }

        SwitchOperationPanel.Visibility = Visibility.Visible;
        bool isClosed = switchDevice.SwitchState == SwitchState.Closed;
        SwitchOperationStateText.Text = isClosed ? "当前状态：合" : "当前状态：分";
        SwitchOperationButton.Content = isClosed ? "分闸" : "合闸";
    }

}
