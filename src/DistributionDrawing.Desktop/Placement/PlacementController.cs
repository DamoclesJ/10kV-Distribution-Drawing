using System.Windows.Media;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols;

namespace DistributionDrawing.Desktop.Placement;

public sealed class PlacementController
{
    private readonly Func<ProjectRuntimeSession?> _getSession;
    private readonly DeviceCommandFactory _commandFactory;
    private readonly DrawingSceneBuilder _sceneBuilder = new();
    private RingCabinetCreationConfiguration? _pendingRingCabinetConfiguration;
    private Pole? _previewPole;
    private PoleLayout? _previewPoleLayout;
    private RingCabinet? _previewCabinet;
    private RingCabinetLayout? _previewCabinetLayout;
    private DocumentPoint? _previewPosition;
    private const double PlacementGridSpacing = 10;

    public PlacementController(
        Func<ProjectRuntimeSession?> getSession,
        DeviceCommandFactory? commandFactory = null)
    {
        _getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
        _commandFactory = commandFactory ?? new DeviceCommandFactory();
    }

    public PlacementMode Mode { get; private set; }

    public event EventHandler? SceneChanged;

    public void BeginPole()
    {
        _pendingRingCabinetConfiguration = null;
        bool clearedVisiblePreview = ClearPreview();
        ProjectRuntimeSession session = RequireSession();
        AddPoleCommand preview = _commandFactory.CreateAddPole(
            session.PersistenceSession.Domain,
            session.Layout,
            new DocumentPoint(0, 0));
        _previewPole = preview.Pole;
        _previewPoleLayout = preview.Layout;
        Mode = PlacementMode.PlacingPole;
        if (clearedVisiblePreview)
        {
            SceneChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void BeginRingCabinet(RingCabinetCreationConfiguration configuration)
    {
        _pendingRingCabinetConfiguration = configuration
            ?? throw new ArgumentNullException(nameof(configuration));
        bool clearedVisiblePreview = ClearPreview();
        ProjectRuntimeSession session = RequireSession();
        AddRingCabinetCommand preview = _commandFactory.CreateAddRingCabinet(
            session.PersistenceSession.Domain,
            session.Layout,
            configuration,
            new DocumentPoint(0, 0));
        _previewCabinet = preview.Cabinet;
        _previewCabinetLayout = preview.Layout;
        Mode = PlacementMode.PlacingRingCabinet;
        if (clearedVisiblePreview)
        {
            SceneChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Cancel()
    {
        _pendingRingCabinetConfiguration = null;
        Mode = PlacementMode.Idle;
        if (ClearPreview())
        {
            SceneChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool Place(DocumentPoint position, bool snapEnabled = false)
    {
        ProjectRuntimeSession session = RequireSession();
        position = ResolvePlacementPosition(position, snapEnabled);
        ICommand command;
        SelectionReference selection;
        switch (Mode)
        {
            case PlacementMode.PlacingPole:
                AddPoleCommand pole = _commandFactory.CreateAddPole(
                    session.PersistenceSession.Domain,
                    session.Layout,
                    position);
                command = pole;
                selection = new SelectionReference(SelectionTargetKind.Device, pole.Pole.Id);
                break;
            case PlacementMode.PlacingRingCabinet:
                RingCabinetCreationConfiguration configuration =
                    _pendingRingCabinetConfiguration
                    ?? throw new InvalidOperationException(
                        "Ring cabinet placement has no creation configuration.");
                AddRingCabinetCommand cabinet = _commandFactory.CreateAddRingCabinet(
                    session.PersistenceSession.Domain,
                    session.Layout,
                    configuration,
                    position);
                command = cabinet;
                selection = new SelectionReference(
                    SelectionTargetKind.RingCabinet,
                    cabinet.Cabinet.Id);
                break;
            default:
                return false;
        }

        session.CommandStack.ExecuteCommand(command);
        if (Mode == PlacementMode.PlacingRingCabinet)
        {
            _pendingRingCabinetConfiguration = null;
            Mode = PlacementMode.Idle;
        }
        _previewPosition = null;
        session.RebuildScene();
        session.SelectionManager.Select(selection);
        SceneChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void UpdatePointer(DocumentPoint position, bool snapEnabled)
    {
        if (Mode == PlacementMode.Idle)
        {
            return;
        }

        DocumentPoint resolved = ResolvePlacementPosition(position, snapEnabled);
        if (_previewPosition == resolved)
        {
            return;
        }

        _previewPosition = resolved;
        SceneChanged?.Invoke(this, EventArgs.Empty);
    }

    public void HidePreview()
    {
        if (_previewPosition is null)
        {
            return;
        }

        _previewPosition = null;
        SceneChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<SceneElement> CreatePreviewElements()
    {
        if (_previewPosition is not DocumentPoint position)
        {
            return [];
        }

        IEnumerable<SceneElement> elements = Mode switch
        {
            PlacementMode.PlacingPole when _previewPole is not null &&
                _previewPoleLayout is not null =>
                new PoleSymbol().CreateElements(
                    _previewPole,
                    _previewPoleLayout.MoveTo(position),
                    includeLabel: false),
            PlacementMode.PlacingRingCabinet when _previewCabinet is not null &&
                _previewCabinetLayout is not null =>
                _sceneBuilder.Build(
                    _previewCabinet,
                    _previewCabinetLayout.MoveTo(position)).Elements,
            _ => []
        };
        return elements
            .Where(element => element is not SceneLogicalBounds)
            .Select(ProjectGhost)
            .ToArray();
    }

    public DocumentPoint ResolvePlacementPosition(DocumentPoint candidate, bool snapEnabled)
    {
        if (!snapEnabled)
        {
            return candidate;
        }

        return new DocumentPoint(
            Math.Round(candidate.XMillimeters / PlacementGridSpacing) * PlacementGridSpacing,
            Math.Round(candidate.YMillimeters / PlacementGridSpacing) * PlacementGridSpacing);
    }

    private bool ClearPreview()
    {
        bool changed = _previewPosition is not null;
        _previewPosition = null;
        _previewPole = null;
        _previewPoleLayout = null;
        _previewCabinet = null;
        _previewCabinetLayout = null;
        return changed;
    }

    private static SceneElement ProjectGhost(SceneElement element) => element switch
    {
        SceneLine line => new SceneLine(
            line.Start, line.End, GhostColor(line.Stroke), line.ThicknessMillimeters,
            line.StrokeStyle),
        SceneRectangle rectangle => new SceneRectangle(
            rectangle.Bounds, GhostColor(rectangle.Stroke), rectangle.ThicknessMillimeters,
            rectangle.Fill is Color fill ? GhostColor(fill) : null, rectangle.StrokeStyle),
        SceneEllipse ellipse => new SceneEllipse(
            ellipse.Bounds, GhostColor(ellipse.Stroke), ellipse.ThicknessMillimeters,
            ellipse.Fill is Color fill ? GhostColor(fill) : null, ellipse.StrokeStyle),
        ScenePolyline polyline => new ScenePolyline(
            polyline.Points, polyline.IsClosed, GhostColor(polyline.Stroke),
            polyline.ThicknessMillimeters,
            polyline.Fill is Color fill ? GhostColor(fill) : null, polyline.StrokeStyle),
        SceneArc arc => new SceneArc(
            arc.Center, arc.RadiusMillimeters, arc.StartAngleDegrees,
            arc.SweepAngleDegrees, GhostColor(arc.Stroke), arc.ThicknessMillimeters,
            arc.StrokeStyle),
        SceneText text => new SceneText(
            text.Origin, text.Text, GhostColor(text.Foreground), text.FontSizeMillimeters),
        _ => element
    };

    private static Color GhostColor(Color color) =>
        Color.FromArgb((byte)Math.Min((int)color.A, 105), color.R, color.G, color.B);

    public void RemoveSelected()
    {
        ProjectRuntimeSession session = RequireSession();
        if (!session.SelectionManager.HasSingleSelection)
        {
            throw new InvalidOperationException("当前版本暂不支持批量删除。");
        }

        SelectionReference selected = session.SelectionManager.Selected
            ?? throw new InvalidOperationException("No device is selected.");
        if (selected.Kind is not (SelectionTargetKind.Device or SelectionTargetKind.RingCabinet))
        {
            throw new InvalidOperationException("The selected object is not a removable device.");
        }

        ICommand command = _commandFactory.CreateRemove(
            session.PersistenceSession.Domain,
            session.Layout,
            selected.ObjectId);
        session.CommandStack.ExecuteCommand(command);
        session.SelectionManager.Clear();
        session.RebuildScene();
        SceneChanged?.Invoke(this, EventArgs.Empty);
    }

    private ProjectRuntimeSession RequireSession()
    {
        return _getSession()
            ?? throw new InvalidOperationException("No project is currently open.");
    }
}
