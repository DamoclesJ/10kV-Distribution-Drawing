using System.Windows;
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
using DistributionDrawing.Desktop.CableTerminationCreation;
using DistributionDrawing.Desktop.PoleSwitchCreation;
using DistributionDrawing.Desktop.Demo;
using DistributionDrawing.Desktop.DrawingTools;
using DistributionDrawing.Desktop.RingCabinetCreation;
using DistributionDrawing.Desktop.Viewport;
using DistributionDrawing.Desktop.Services;
using DistributionDrawing.Desktop.ViewModels;
using DistributionDrawing.Desktop.SwitchOperation;

namespace DistributionDrawing.Desktop;

public partial class MainWindow : Window
{
    private readonly DrawingSceneRenderer _renderer = new();
    private readonly DrawingSceneBuilder _sceneBuilder = new();
    private readonly CanvasViewportController _viewport = new();
    private SelectionManager _selectionManager = new();
    private CommandStack _commandStack = new();
    private PropertyEditor _propertyEditor;
    private readonly ProfessionalCommandFactory _professionalCommandFactory = new();
    private readonly DeviceDragController _deviceDrag = new();
    private readonly CableRouteDragController _cableRouteDrag = new();
    private SelectionObjectResolver _selectionResolver = new();
    private PropertyProjector _propertyProjector = new();
    private PropertyInspectorViewModel _propertyInspector = new();
    private readonly ProjectWorkspaceController _workspace;
    private readonly PlacementController _placement;
    private readonly OverheadLineConnectionController _overheadLineConnection;
    private readonly CableTerminationAttachmentController _cableTerminationAttachment;
    private readonly PoleSwitchAttachmentController _poleSwitchAttachment;
    private readonly SwitchOperationController _switchOperation;
    private readonly CableConnectionController _cableConnection;
    private readonly CableReconnectController _cableReconnect;
    private readonly DrawingToolCoordinator _drawingTools;
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
        _propertyEditor = new(_selectionResolver, _commandStack);
        PropertyInspectorPanel.DataContext = _propertyInspector;
        _selectionManager.SelectionChanged += OnSelectionChanged;
        _workspace = new ProjectWorkspaceController(
            new WpfProjectWorkspaceDialogs(this),
            _sceneBuilder,
            EnsureTransientEditsCommitted);
        _workspace.SessionChanged += OnWorkspaceSessionChanged;
        _placement = new PlacementController(() => _workspace.CurrentSession);
        _overheadLineConnection = new OverheadLineConnectionController(
            () => _workspace.CurrentSession);
        _cableConnection = new CableConnectionController(
            () => _workspace.CurrentSession);
        _cableReconnect = new CableReconnectController(
            () => _workspace.CurrentSession);
        _cableTerminationAttachment = new CableTerminationAttachmentController(
            () => _workspace.CurrentSession);
        _poleSwitchAttachment = new PoleSwitchAttachmentController(
            () => _workspace.CurrentSession);
        _switchOperation = new SwitchOperationController(
            () => _workspace.CurrentSession);
        _drawingTools = new DrawingToolCoordinator(
            _placement,
            _overheadLineConnection,
            _cableTerminationAttachment,
            _cableConnection,
            _cableReconnect,
            _poleSwitchAttachment);
        _placement.SceneChanged += OnDrawingToolVisualChanged;
        _overheadLineConnection.VisualChanged += OnDrawingToolVisualChanged;
        _cableConnection.VisualChanged += OnDrawingToolVisualChanged;
        _cableReconnect.VisualChanged += OnDrawingToolVisualChanged;
        _cableConnection.ParametersRequired += OnCableParametersRequired;
        _cableTerminationAttachment.SceneChanged += OnDrawingToolVisualChanged;
        _switchOperation.SceneChanged += OnSwitchOperationSceneChanged;
        _viewport.ViewChanged += OnViewportChanged;
        DrawingSurface.SetViewTransform(_viewport.Transform);
        _shellViewModel = new MainWindowViewModel(
            new DesktopShellService(),
            () => _workspace.NewProject(),
            () => _workspace.OpenProject(),
            () => _workspace.SaveProject(),
            OnUndoRequested,
            OnRedoRequested,
            OnDeleteRequested,
            OnCancelRequested,
            () => _commandStack.CanUndo,
            () => _commandStack.CanRedo,
            () => _selectionManager.Selected is not null,
            OnSelectModeRequested,
            OnCreateRingCabinetModeRequested,
            OnCreatePoleModeRequested);
        _shellViewModel.Toolbox.PropertyChanged += OnToolboxPropertyChanged;
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
    }

    private void OnAddCableTermination(object sender, RoutedEventArgs e)
    {
        var dialog = new CableTerminationCreationDialog { Owner = this };
        if (dialog.ShowDialog() != true)
        {
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
            ShowCommandError("无法添加电缆终端", exception.Message);
        }
    }

    private void OnAddPoleSwitch(object sender, RoutedEventArgs e)
    {
        var dialog = new PoleSwitchCreationDialog { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _drawingTools.AddSwitchAttachment(dialog.SwitchKind);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ShowCommandError("无法添加柱上开关", exception.Message);
        }
    }

    private void OnBeginOverheadLine(object sender, RoutedEventArgs e)
    {
        try
        {
            CancelDeviceDrag();
            CancelProfessionalPicking();
            _drawingTools.BeginOverheadLine();
        }
        catch (InvalidOperationException exception)
        {
            ShowCommandError("无法绘制架空线", exception.Message);
        }
    }

    private void OnBeginCable(object sender, RoutedEventArgs e)
    {
        try
        {
            CancelDeviceDrag();
            CancelProfessionalPicking();
            _drawingTools.BeginCable();
            UpdateCanvasStatus();
        }
        catch (InvalidOperationException exception)
        {
            ShowCommandError("无法绘制电缆", exception.Message);
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
        SwitchOperationResult result = _switchOperation.ToggleSelected();
        if (!result.IsSuccess)
        {
            ShowCommandError("开关操作失败", result.ErrorMessage!);
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
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ShowCommandError("删除对象失败", exception.Message);
        }
    }

    private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
        {
            if (e.Key == System.Windows.Input.Key.Z)
            {
                _shellViewModel.UndoCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (e.Key == System.Windows.Input.Key.Y)
            {
                _shellViewModel.RedoCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (e.Key == System.Windows.Input.Key.S)
            {
                _shellViewModel.SaveProjectCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == System.Windows.Input.Key.Delete)
        {
            _shellViewModel.DeleteCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == System.Windows.Input.Key.Escape)
        {
            if (_deviceDrag.IsActive || _cableRouteDrag.IsActive)
            {
                CancelDeviceDrag();
                e.Handled = true;
                return;
            }

            if (_viewport.IsPanning)
            {
                EndCanvasPan();
                e.Handled = true;
                return;
            }

            _shellViewModel.CancelCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnCancelRequested() => _drawingTools.Cancel();

    private void OnSelectModeRequested()
    {
        _drawingTools.Cancel();
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
        UpdateIntervalEditor();
        UpdateAttachmentOffsetEditor();
        UpdateAttachmentLayoutEditor();
        UpdateCableTerminationDisplayNameEditor();
        UpdateGroundingPointEditor();
        UpdateWorkScopeEditor();
        UpdateCanvasStatus();
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
        _deviceDrag.Cancel();
        _cableRouteDrag.Cancel();
        DrawingSurface.ReleaseMouseCapture();
        EndCanvasPan();
        _drawingTools.Cancel();
        _viewport.SetViewportSize(
            new Size(DrawingSurface.ActualWidth, DrawingSurface.ActualHeight));
        _viewport.Reset();
        if (_workspace.CurrentSession is not { } session)
        {
            OnClearDrawing(this, new RoutedEventArgs());
            return;
        }

        _selectionManager.SelectionChanged -= OnSelectionChanged;
        _selectionManager = session.SelectionManager;
        _commandStack = session.CommandStack;
        _selectionResolver = session.SelectionResolver;
        _propertyProjector = session.PropertyProjector;
        _propertyInspector = session.PropertyInspector;
        _propertyEditor = new(_selectionResolver, _commandStack, session.Layout);
        _currentScene = session.Scene;
        _activeSource = session.InspectionSource;
        _selectionManager.SelectionChanged += OnSelectionChanged;
        PropertyInspectorPanel.DataContext = _propertyInspector;
        RenderCurrentScene();
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
        _gridVisible = sender is System.Windows.Controls.MenuItem menuItem && menuItem.IsChecked;
        DrawingSurface.ShowGrid = _gridVisible;
        UpdateCanvasStatus();
    }

    private void UpdateCanvasStatus()
    {
        _shellViewModel.UpdateCanvasState(
            _viewport.Transform.Scale,
            _gridVisible,
            _shellViewModel.Toolbox.SelectedMode switch
            {
                DesktopToolMode.CreateRingCabinet => "创建环网柜",
                DesktopToolMode.CreatePole => "创建杆塔",
                _ when _cableConnection.IsActive => _cableConnection.State ==
                    CableConnectionToolState.PickingStartTerminal
                        ? "绘制电缆：请选择起点"
                        : "绘制电缆：请选择终点",
                _ when _cableReconnect.IsActive => _cableReconnect.Endpoint ==
                    CableReconnectEndpoint.Start
                        ? "修改电缆：请选择新的起点端子"
                        : "修改电缆：请选择新的终点端子",
                _ => "选择"
            });
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
        if (changed)
        {
            RefreshDrawingScene();
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
            ShowCommandError("撤销失败", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ShowCommandError("撤销失败", exception.Message);
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
            ShowCommandError("重做失败", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ShowCommandError("重做失败", exception.Message);
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
            ShowCommandError(
                "无法添加工作地线",
                "当前场景没有可编辑的 DrawingDocument 工程。");
            return;
        }

        CancelDeviceDrag();
        _drawingTools.Cancel();
        ResetWorkScopePick();
        _groundingPointPickMode = true;
        _pendingGroundingPointTerminalId = null;
        _selectionManager.Clear();
        GroundingPointEditorPanel.Visibility = Visibility.Visible;
        GroundingPointTerminalText.Text = "请在图面中点击一个有效端子";
        GroundingPointLocationInput.Text = string.Empty;
        GroundingPointNumberInput.Text = string.Empty;
        GroundingPointNoteInput.Text = string.Empty;
    }

    private void OnBeginAddWorkScope(object sender, RoutedEventArgs e)
    {
        if (_activeSource?.Document is null || _activeSource.DrawingLayout is null)
        {
            ShowCommandError(
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
        WorkScopeBoundaryASideInput.Text = string.Empty;
        WorkScopeBoundaryBSideInput.Text = string.Empty;
        WorkScopeDescriptionInput.Text = string.Empty;
        WorkScopeGroundingPointIdsInput.Text = string.Empty;
        _selectionManager.Clear();
        UpdateWorkScopeEditor();
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
                _drawingTools.HandleClick(
                    documentPoint,
                    _viewport.Transform.ViewDistanceToDocument(8));
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
        _selectionManager.Select(target);

        ResolvedSelection? dragSelection = target is null
            ? null
            : _selectionResolver.Resolve(target);
        Guid? orbitParentPoleId = dragSelection?.PoleAttachment is { } attachment &&
                                  _workspace.CurrentSession?.PersistenceSession.Domain.Devices
                                      .SingleOrDefault(device =>
                                          device.Id == attachment.AttachedDeviceId) is CableTermination
            ? attachment.PoleId
            : null;
        if (target is not null &&
            _workspace.CurrentSession is { } session &&
            ((hit is not null && _cableRouteDrag.TryBeginDrag(
                hit,
                _currentScene.HitTestIndex.FindAll(target),
                session.Layout)) ||
            _deviceDrag.TryBeginDrag(
                target,
                documentPoint,
                session.Layout,
                orbitParentPoleId)))
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

        if ((!_deviceDrag.IsActive && !_cableRouteDrag.IsActive) ||
            e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            return;
        }

        DocumentPoint documentPoint = _viewport.Transform.ViewToDocument(point);
        if (_cableRouteDrag.IsActive
                ? _cableRouteDrag.UpdatePreview(documentPoint)
                : _deviceDrag.UpdatePreview(documentPoint))
        {
            RefreshDrawingScene();
        }

        e.Handled = true;
    }

    private void OnDrawingSurfaceMouseLeftButtonUp(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_deviceDrag.IsActive && !_cableRouteDrag.IsActive)
        {
            return;
        }

        ICommand? command = _cableRouteDrag.IsActive
            ? _cableRouteDrag.Commit()
            : _deviceDrag.Commit();
        DrawingSurface.ReleaseMouseCapture();
        if (command is not null)
        {
            _commandStack.ExecuteCommand(command);
            RefreshDrawingScene();
        }

        e.Handled = true;
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        _shellViewModel.RefreshCommandStates();
        _propertyInspector.Apply(
            _propertyProjector.Project(
                _selectionResolver.Resolve(_selectionManager.Selected)));
        UpdateRingCabinetEditor();
        UpdatePoleNumberEditor();
        UpdateIntervalEditor();
        UpdateAttachmentOffsetEditor();
        UpdateAttachmentLayoutEditor();
        UpdateCableTerminationDisplayNameEditor();
        UpdateGroundingPointEditor();
        UpdateWorkScopeEditor();
        RenderCurrentScene();
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
            ShowCommandError("电缆端点修改失败", exception.Message);
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
            GroundingPointTerminalText.Text = $"端子：{groundingPoint.TerminalId}";
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
                $"A: {workScope.StartBoundary.DeviceId} / {workScope.StartBoundary.TerminalId} / {workScope.StartBoundary.Side}\n" +
                $"B: {workScope.EndBoundary.DeviceId} / {workScope.EndBoundary.TerminalId} / {workScope.EndBoundary.Side}";
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

        return $"设备：{deviceId}\n端子：{terminalId}";
    }

    private static string FormatBoundary(BoundaryPointCommandValue boundary)
    {
        return $"设备：{boundary.DeviceId}\n端子：{boundary.TerminalId}\n侧别：{boundary.Side}";
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
            if (_selectionManager.Selected is { } runtimeSelected &&
                _selectionResolver.Resolve(runtimeSelected) is null)
            {
                _selectionManager.Clear();
            }

            _propertyInspector.Apply(
                _propertyProjector.Project(
                    _selectionResolver.Resolve(_selectionManager.Selected)));
            UpdateRingCabinetEditor();
            UpdatePoleNumberEditor();
            UpdateIntervalEditor();
            UpdateAttachmentOffsetEditor();
            UpdateAttachmentLayoutEditor();
            UpdateCableTerminationDisplayNameEditor();
            UpdateGroundingPointEditor();
            UpdateWorkScopeEditor();
            RenderCurrentScene();
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
        if (_selectionManager.Selected is { } selected &&
            _selectionResolver.Resolve(selected) is null)
        {
            _selectionManager.Clear();
        }
        _propertyInspector.Apply(
            _propertyProjector.Project(
                _selectionResolver.Resolve(_selectionManager.Selected)));
        UpdateRingCabinetEditor();
        UpdatePoleNumberEditor();
        UpdateIntervalEditor();
        UpdateAttachmentOffsetEditor();
        UpdateAttachmentLayoutEditor();
        UpdateCableTerminationDisplayNameEditor();
        UpdateGroundingPointEditor();
        UpdateWorkScopeEditor();
        RenderCurrentScene();
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
                _selectionManager.Selected));
        double pixelsPerDip = VisualTreeHelper.GetDpi(DrawingSurface).PixelsPerDip;
        DrawingSurface.Show(_renderer.Render(new DrawingScene(elements), pixelsPerDip));
        DrawingSurface.SetViewTransform(_viewport.Transform);
    }

    private void UpdateSwitchOperationEditor()
    {
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
