using System.Windows;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Desktop;

public partial class MainWindow : Window
{
    private readonly DrawingSceneRenderer _renderer = new();
    private readonly DrawingSceneBuilder _sceneBuilder = new();
    private readonly DocumentCoordinateSystem _coordinates = new();
    private readonly SelectionManager _selectionManager = new();
    private DrawingScene? _currentScene;

    public MainWindow()
    {
        InitializeComponent();
        _selectionManager.SelectionChanged += OnSelectionChanged;
    }

    private void OnDrawTestContent(object sender, RoutedEventArgs e)
    {
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

        ShowScene(scene);
    }

    private void OnClearDrawing(object sender, RoutedEventArgs e)
    {
        _currentScene = null;
        _selectionManager.Clear();
        DrawingSurface.Clear();
    }

    private void OnDrawRingCabinetComposition(object sender, RoutedEventArgs e)
    {
        RingCabinet cabinet = CreateMixedRingCabinet();
        RingCabinetLayout layout = CreateMixedRingCabinetLayout(cabinet);
        DrawingScene scene = _sceneBuilder.Build(cabinet, layout);

        ShowScene(scene);
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
        var documentPoint = new DocumentPoint(
            _coordinates.DipToMillimeters(point.X),
            _coordinates.DipToMillimeters(point.Y));
        _selectionManager.Select(_currentScene.HitTestIndex.HitTest(documentPoint));
        e.Handled = true;
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        RenderCurrentScene();
    }

    private void ShowScene(DrawingScene scene)
    {
        _currentScene = scene;
        _selectionManager.Clear();
        RenderCurrentScene();
    }

    private void RenderCurrentScene()
    {
        if (_currentScene is null)
        {
            return;
        }

        var elements = _currentScene.Elements.ToList();
        elements.AddRange(
            SelectionOverlayBuilder.CreateElements(
                _currentScene.HitTestIndex,
                _selectionManager.Selected));
        double pixelsPerDip = VisualTreeHelper.GetDpi(DrawingSurface).PixelsPerDip;
        DrawingSurface.Show(_renderer.Render(new DrawingScene(elements), pixelsPerDip));
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
