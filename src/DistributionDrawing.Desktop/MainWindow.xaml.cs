using System.Windows;
using System.ComponentModel;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Professional;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.PropertyInspector;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Desktop.Workspace;
using DistributionDrawing.Desktop.Placement;
using DistributionDrawing.Desktop.ConnectionEditing;
using DistributionDrawing.Desktop.DrawingTools;
using DistributionDrawing.Desktop.Viewport;

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
    private readonly PoleLayoutEditor _poleLayoutEditor = new();
    private SelectionObjectResolver _selectionResolver = new();
    private PropertyProjector _propertyProjector = new();
    private PropertyInspectorViewModel _propertyInspector = new();
    private readonly ProjectWorkspaceController _workspace;
    private readonly PlacementController _placement;
    private readonly OverheadLineConnectionController _overheadLineConnection;
    private readonly DrawingToolCoordinator _drawingTools;
    private DrawingScene? _currentScene;
    private PropertyInspectionSource? _activeSource;
    private bool _groundingPointPickMode;
    private Guid? _pendingGroundingPointTerminalId;
    private WorkScopePickState _workScopePickState;
    private BoundaryPointCommandValue? _pendingWorkScopeStartBoundary;
    private BoundaryPointCommandValue? _pendingWorkScopeEndBoundary;
    private Guid? _pendingWorkScopeTerminalId;
    private Guid? _pendingWorkScopeDeviceId;

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
        _drawingTools = new DrawingToolCoordinator(_placement, _overheadLineConnection);
        _placement.SceneChanged += OnDrawingToolVisualChanged;
        _overheadLineConnection.VisualChanged += OnDrawingToolVisualChanged;
        _viewport.ViewChanged += OnViewportChanged;
        DrawingSurface.SetViewTransform(_viewport.Transform);
    }

    private void OnNewProject(object sender, RoutedEventArgs e) => _workspace.NewProject();

    private void OnOpenProject(object sender, RoutedEventArgs e) => _workspace.OpenProject();

    private void OnSaveProject(object sender, RoutedEventArgs e) => _workspace.SaveProject();

    private void OnSaveProjectAs(object sender, RoutedEventArgs e) => _workspace.SaveProjectAs();

    private void OnCloseProject(object sender, RoutedEventArgs e) => _workspace.CloseCurrentProject();

    private void OnBeginPlacePole(object sender, RoutedEventArgs e)
    {
        CancelProfessionalPicking();
        _drawingTools.BeginPole();
    }

    private void OnBeginPlaceRingCabinet(object sender, RoutedEventArgs e)
    {
        CancelProfessionalPicking();
        _drawingTools.BeginRingCabinet();
    }

    private void OnBeginOverheadLine(object sender, RoutedEventArgs e)
    {
        try
        {
            CancelProfessionalPicking();
            _drawingTools.BeginOverheadLine();
        }
        catch (InvalidOperationException exception)
        {
            ShowCommandError("无法绘制架空线", exception.Message);
        }
    }

    private void OnCancelPlacement(object sender, RoutedEventArgs e)
    {
        _drawingTools.Cancel();
    }

    private void OnRemoveSelectedDevice(object sender, RoutedEventArgs e)
    {
        try
        {
            _drawingTools.RemoveSelected();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ShowCommandError("删除对象失败", exception.Message);
        }
    }

    private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            if (_viewport.IsPanning)
            {
                EndCanvasPan();
                e.Handled = true;
                return;
            }

            _drawingTools.Cancel();
            e.Handled = true;
        }
    }

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
        UpdatePoleNumberEditor();
        UpdateGroundingPointEditor();
        UpdateWorkScopeEditor();
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

        if (!_poleLayoutEditor.IsActive)
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
            _poleLayoutEditor.Cancel();
            DrawingSurface.ReleaseMouseCapture();
            return true;
        }

        if (_poleLayoutEditor.Commit() is not { } command)
        {
            return false;
        }

        try
        {
            _commandStack.ExecuteCommand(command);
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
        _propertyEditor = new(_selectionResolver, _commandStack);
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
        EndCanvasPan();
        _drawingTools.Cancel();
        _poleLayoutEditor.Cancel();
        DrawingSurface.ReleaseMouseCapture();
        _currentScene = null;
        _activeSource = null;
        _groundingPointPickMode = false;
        _pendingGroundingPointTerminalId = null;
        ResetWorkScopePick();
        _selectionResolver.SetSource(null);
        _selectionManager.Clear();
        _propertyInspector.Clear();
        PoleNumberEditorPanel.Visibility = Visibility.Collapsed;
        GroundingPointEditorPanel.Visibility = Visibility.Collapsed;
        WorkScopeCreationPanel.Visibility = Visibility.Collapsed;
        WorkScopeEditorPanel.Visibility = Visibility.Collapsed;
        DrawingSurface.Clear();
        _viewport.Reset();
    }

    private void OnZoomIn(object sender, RoutedEventArgs e)
    {
        if (!_poleLayoutEditor.IsActive)
        {
            _viewport.ZoomIn();
            UpdateConnectionPointerFromCurrentMouse();
        }
    }

    private void OnZoomOut(object sender, RoutedEventArgs e)
    {
        if (!_poleLayoutEditor.IsActive)
        {
            _viewport.ZoomOut();
            UpdateConnectionPointerFromCurrentMouse();
        }
    }

    private void OnFitDrawing(object sender, RoutedEventArgs e)
    {
        if (!_poleLayoutEditor.IsActive)
        {
            _viewport.Fit(_currentScene);
            UpdateConnectionPointerFromCurrentMouse();
        }
    }

    private void OnViewportChanged(object? sender, EventArgs e)
    {
        DrawingSurface.SetViewTransform(_viewport.Transform);
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
        if (_poleLayoutEditor.IsActive)
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
            _poleLayoutEditor.IsActive)
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
        if (_viewport.IsPanning)
        {
            _viewport.CancelPan();
        }
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
        if (!_overheadLineConnection.IsActive)
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
            if (_commandStack.Undo())
            {
                RefreshDrawingScene();
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
            if (_commandStack.Redo())
            {
                RefreshDrawingScene();
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

    private void OnBeginAddGroundingPoint(object sender, RoutedEventArgs e)
    {
        if (_activeSource?.Document is null || _activeSource.DrawingLayout is null)
        {
            ShowCommandError(
                "无法添加工作地线",
                "当前场景没有可编辑的 DrawingDocument 工程。");
            return;
        }

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
        RingCabinet cabinet = CreateMixedRingCabinet();
        RingCabinetLayout layout = CreateMixedRingCabinetLayout(cabinet);
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
                out Guid[] groundingPointIds,
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
                groundingPointIds);
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

        SelectionReference? target = _currentScene.HitTestIndex.HitTest(
            documentPoint,
            _viewport.Transform.ViewDistanceToDocument(4));
        _selectionManager.Select(target);

        if (target is not null &&
            target.Kind == SelectionTargetKind.Device &&
            _activeSource?.DrawingLayout is { } layout &&
            layout.Poles.TryGetValue(target.ObjectId, out PoleLayout poleLayout))
        {
            _poleLayoutEditor.BeginDrag(target, documentPoint, poleLayout, layout);
            DrawingSurface.CaptureMouse();
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

        if (!_poleLayoutEditor.IsActive ||
            e.LeftButton != System.Windows.Input.MouseButtonState.Pressed ||
            _activeSource?.DrawingLayout is not { } layout)
        {
            return;
        }

        DocumentPoint documentPoint = _viewport.Transform.ViewToDocument(point);
        PoleLayout preview = _poleLayoutEditor.UpdatePreview(documentPoint);
        layout.Replace(preview);
        RefreshDrawingScene();
        e.Handled = true;
    }

    private void OnDrawingSurfaceMouseLeftButtonUp(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_poleLayoutEditor.IsActive ||
            _activeSource?.DrawingLayout is not { } layout)
        {
            return;
        }

        MoveCommand? command = _poleLayoutEditor.Commit();
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
        _propertyInspector.Apply(
            _propertyProjector.Project(
                _selectionResolver.Resolve(_selectionManager.Selected)));
        UpdatePoleNumberEditor();
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
            UpdatePoleNumberEditor();
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
        UpdatePoleNumberEditor();
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
            _activeSource.RingCabinetLayouts);
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

    private static RingCabinet CreateMixedRingCabinet()
    {
        RingCabinetIntervalDefinition[] definitions =
        [
            RingCabinetIntervalDefinition.CreateLoadSwitch(
                SwitchState.Closed,
                SwitchState.Open,
                "进线负荷开关"),
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                GroundingStructureKind.UpperIsolationGrounding,
                SwitchState.Closed,
                SwitchState.Open,
                SwitchState.Open,
                "一二次融合馈线"),
            RingCabinetIntervalDefinition.CreateLoadSwitch(
                SwitchState.Open,
                SwitchState.Open,
                "出线负荷开关"),
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                GroundingStructureKind.LowerLowerGrounding,
                SwitchState.Closed,
                SwitchState.Closed,
                SwitchState.Open,
                "融合联络馈线")
        ];

        return RingCabinet.Create(
            RingCabinetDefinition.Create(
                Guid.NewGuid(),
                "混合型环网柜演示",
                definitions));
    }

    private static RingCabinetLayout CreateMixedRingCabinetLayout(RingCabinet cabinet)
    {
        var intervalLayouts = new List<RingCabinetIntervalLayout>();
        const double intervalWidth = 65;
        const double intervalHeight = 125;

        foreach (RingCabinetInterval interval in cabinet.Intervals)
        {
            double x = 10 + (interval.Sequence - 1) * intervalWidth;
            var switches = new List<RingCabinetSwitchLayout>();

            if (interval.IntervalKind == IntervalKind.LoadSwitchInterval)
            {
                switches.Add(CreateSwitchLayout(
                    interval,
                    SwitchKind.LoadSwitch,
                    new DocumentPoint(23, 35)));
                switches.Add(CreateSwitchLayout(
                    interval,
                    SwitchKind.GroundSwitch,
                    new DocumentPoint(23, 72)));
            }
            else
            {
                GroundingStructureKind structure = interval.GroundingStructureKind!.Value;
                SwitchKind upperKind = structure == GroundingStructureKind.LowerLowerGrounding
                    ? SwitchKind.CircuitBreaker
                    : SwitchKind.IsolationSwitch;
                SwitchKind lowerKind = structure == GroundingStructureKind.LowerLowerGrounding
                    ? SwitchKind.IsolationSwitch
                    : SwitchKind.CircuitBreaker;

                switches.Add(CreateSwitchLayout(
                    interval,
                    upperKind,
                    new DocumentPoint(18, 28)));
                switches.Add(CreateSwitchLayout(
                    interval,
                    lowerKind,
                    new DocumentPoint(18, 70)));
                switches.Add(CreateSwitchLayout(
                    interval,
                    SwitchKind.GroundSwitch,
                    new DocumentPoint(42, structure == GroundingStructureKind.UpperIsolationGrounding ? 49 : 84)));
            }

            intervalLayouts.Add(
                new RingCabinetIntervalLayout(
                    interval.IntervalId,
                    new DocumentPoint(x, 10),
                    intervalWidth - 5,
                    intervalHeight,
                    switchLayouts: switches));
        }

        return new RingCabinetLayout(
            cabinet.Id,
            new DocumentPoint(45, 80),
            275,
            145,
            25,
            intervalLayouts);
    }

    private static RingCabinetSwitchLayout CreateSwitchLayout(
        RingCabinetInterval interval,
        SwitchKind switchKind,
        DocumentPoint position)
    {
        SwitchDevice switchDevice = interval.SwitchDevices.Single(
            candidate => candidate.SwitchKind == switchKind);
        return new RingCabinetSwitchLayout(
            switchDevice.Id,
            position,
            widthMillimeters: 16,
            heightMillimeters: 10);
    }
}
