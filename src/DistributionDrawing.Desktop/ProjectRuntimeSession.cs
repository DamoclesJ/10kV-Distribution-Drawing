using System.IO;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.PropertyInspector;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Desktop.Selection;

namespace DistributionDrawing.Desktop;

/// <summary>
/// Runtime projection of a persisted project. Persistence DTOs are converted
/// to millimeter layout objects, then rendered into a fresh scene. Transient
/// WPF and editor state is created here and is never part of the file format.
/// </summary>
public sealed class ProjectRuntimeSession
{
    private readonly DrawingSceneBuilder _sceneBuilder;
    private ProjectRuntimeSession(
        ProjectSession persistenceSession,
        RuntimeLayoutDocument layout,
        DrawingScene scene,
        PropertyInspectionSource inspectionSource,
        DrawingSceneBuilder sceneBuilder)
    {
        _sceneBuilder = sceneBuilder;
        PersistenceSession = persistenceSession;
        Layout = layout;
        Scene = scene;
        InspectionSource = inspectionSource;

        SelectionManager = new SelectionManager();
        SelectionResolver = new SelectionObjectResolver();
        SelectionResolver.SetSource(inspectionSource);
        PropertyInspector = new PropertyInspectorViewModel();
        PropertyProjector = new PropertyProjector();
        CommandStack = new CommandStack();
        SelectionTransitions = new SelectionTransitionCoordinator();
        CommandStack.MarkSaved();
    }

    public ProjectSession PersistenceSession { get; private set; }

    public RuntimeLayoutDocument Layout { get; private set; }

    public DrawingScene Scene { get; private set; }

    public PropertyInspectionSource InspectionSource { get; private set; }

    public SelectionManager SelectionManager { get; }

    public SelectionObjectResolver SelectionResolver { get; }

    public PropertyInspectorViewModel PropertyInspector { get; }

    public PropertyProjector PropertyProjector { get; }

    public CommandStack CommandStack { get; }

    public ISelectionTransitionCoordinator SelectionTransitions { get; }

    public bool IsDirty => PersistenceSession.IsDirty || CommandStack.IsDirty;

    public long SavePoint => CommandStack.SavedStateId;

    public static ProjectRuntimeSession Create(
        ProjectSession persistenceSession,
        DrawingSceneBuilder? sceneBuilder = null)
    {
        ArgumentNullException.ThrowIfNull(persistenceSession);

        DrawingSceneBuilder builder = sceneBuilder ?? new DrawingSceneBuilder();
        RuntimeLayoutDocument layout = ProjectLayoutRuntimeMapper.ToRuntime(
            persistenceSession.Domain,
            persistenceSession.Layout);
        DrawingScene scene = builder.Build(persistenceSession.Domain, layout);
        PropertyInspectionSource source = CreateInspectionSource(
            persistenceSession,
            layout,
            scene);

        return new ProjectRuntimeSession(
            persistenceSession,
            layout,
            scene,
            source,
            builder);
    }

    public static ProjectRuntimeSession CreateEmpty(
        ProjectSession persistenceSession,
        DrawingSceneBuilder? sceneBuilder = null)
    {
        return Create(persistenceSession, sceneBuilder);
    }

    public static ProjectRuntimeSession Load(
        ProjectService projectService,
        string filePath,
        DrawingSceneBuilder? sceneBuilder = null)
    {
        ArgumentNullException.ThrowIfNull(projectService);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return Create(projectService.LoadProject(filePath), sceneBuilder);
    }

    public void RebuildScene()
    {
        Scene = _sceneBuilder.Build(PersistenceSession.Domain, Layout);
        InspectionSource = CreateInspectionSource(PersistenceSession, Layout, Scene);
        SelectionResolver.SetSource(InspectionSource);
    }

    public void AcceptSavedSession(ProjectSession persistenceSession)
    {
        ArgumentNullException.ThrowIfNull(persistenceSession);
        if (!ReferenceEquals(persistenceSession.Domain, PersistenceSession.Domain))
        {
            throw new InvalidOperationException(
                "A saved session must preserve the runtime Domain object graph.");
        }

        PersistenceSession = persistenceSession;
        RebuildScene();
        CommandStack.MarkSaved();
    }

    private static PropertyInspectionSource CreateInspectionSource(
        ProjectSession session,
        RuntimeLayoutDocument layout,
        DrawingScene scene)
    {
        RingCabinet? cabinet = session.Domain.Devices.OfType<RingCabinet>().FirstOrDefault();
        RingCabinetLayout? cabinetLayout = null;
        if (cabinet is not null &&
            layout.RingCabinetLayouts.TryGetValue(cabinet.Id, out RingCabinetLayout? found))
        {
            cabinetLayout = found;
        }

        return new PropertyInspectionSource
        {
            Document = session.Domain,
            RingCabinet = cabinet,
            RingCabinetLayout = cabinetLayout,
            RingCabinetLayouts = layout.RingCabinetLayouts,
            DrawingLayout = layout.DrawingLayout,
            Poles = session.Domain.Devices.OfType<Pole>().ToArray(),
            Devices = session.Domain.Devices,
            PoleAttachments = session.Domain.PoleAttachments,
            Connections = session.Domain.Connections,
            OverheadLines = session.Domain.OverheadLines,
            WorkScopes = session.Domain.WorkScopes,
            GroundingPoints = session.Domain.GroundingPoints,
            Terminals = session.Domain.Terminals,
            HitTestIndex = scene.HitTestIndex
        };
    }
}

internal static class ProjectLayoutRuntimeMapper
{
    public static ProjectLayoutSnapshot ToSnapshot(
        DistributionDrawing.Domain.Documents.DrawingDocument domain,
        RuntimeLayoutDocument runtime)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(runtime);

        var cabinets = runtime.RingCabinetLayouts.Values.Select(layout =>
            new ProjectRingCabinetLayoutDto(
                layout.CabinetId,
                Point(layout.Position),
                layout.WidthMillimeters,
                layout.HeightMillimeters,
                layout.MainBusYMillimeters,
                Point(layout.LabelOffset),
                layout.IntervalLayouts.Values.Select(interval =>
                    new ProjectRingCabinetIntervalLayoutDto(
                        interval.IntervalId,
                        Point(interval.RelativePosition),
                        interval.WidthMillimeters,
                        interval.HeightMillimeters,
                        Point(interval.SequenceLabelOffset),
                        Point(interval.NameLabelOffset),
                        interval.SwitchLayouts.Values.Select(switchLayout =>
                            new ProjectRingCabinetSwitchLayoutDto(
                                switchLayout.SwitchDeviceId,
                                Point(switchLayout.RelativePosition),
                                switchLayout.WidthMillimeters,
                                switchLayout.HeightMillimeters,
                                Point(switchLayout.LabelOffset))).ToArray())).ToArray())).ToArray();
        var poles = runtime.DrawingLayout.Poles.Values.Select(layout =>
            new ProjectPoleLayoutDto(
                layout.PoleId,
                Point(layout.Position),
                layout.WidthMillimeters,
                layout.HeightMillimeters,
                Point(layout.LabelOffset))).ToArray();
        var attachments = runtime.DrawingLayout.Attachments.Values.Select(layout =>
            new ProjectAttachmentLayoutDto(
                layout.AttachmentId,
                Point(layout.Offset),
                layout.WidthMillimeters,
                layout.HeightMillimeters,
                Point(layout.LabelOffset))).ToArray();
        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            domain,
            runtime.DrawingLayout,
            runtime.RingCabinetLayouts,
            domain.Connections,
            domain.CableSegments);
        var overheadLines = runtime.DrawingLayout.OverheadLines.Values.Select(layout =>
        {
            Connection connection = domain.Connections.SingleOrDefault(
                    item => item.Id == layout.ConnectionId)
                ?? throw new InvalidDataException(
                    $"Connection '{layout.ConnectionId}' does not exist for layout snapshot.");
            if (!anchors.TryGet(connection.StartTerminalId, out TerminalAnchor startAnchor) ||
                !anchors.TryGet(connection.EndTerminalId, out TerminalAnchor endAnchor))
            {
                throw new InvalidDataException(
                    $"Terminal anchors are missing for connection '{connection.Id}'.");
            }

            return new ProjectOverheadLineLayoutDto(
                layout.ConnectionId,
                Point(startAnchor.Position),
                Point(endAnchor.Position),
                Point(layout.ContinuationOffset));
        }).ToArray();

        return new ProjectLayoutSnapshot(new ProjectLayoutDto(
            domain.Id,
            "mm",
            cabinets,
            poles,
            attachments,
            overheadLines));
    }

    public static RuntimeLayoutDocument ToRuntime(
        DistributionDrawing.Domain.Documents.DrawingDocument domain,
        ProjectLayoutSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.DocumentId != domain.Id || snapshot.CoordinateUnit != "mm")
        {
            throw new InvalidDataException("The restored layout does not match the document.");
        }

        var drawingLayout = new DrawingLayout();
        foreach (ProjectPoleLayoutDto dto in snapshot.Poles)
        {
            drawingLayout.Add(new PoleLayout(
                dto.PoleId,
                Point(dto.Position),
                dto.WidthMillimeters,
                dto.HeightMillimeters,
                Point(dto.LabelOffset)));
        }

        foreach (ProjectAttachmentLayoutDto dto in snapshot.Attachments)
        {
            drawingLayout.Add(new AttachmentLayout(
                dto.AttachmentId,
                Point(dto.Offset),
                dto.WidthMillimeters,
                dto.HeightMillimeters,
                Point(dto.LabelOffset)));
        }

        foreach (ProjectOverheadLineLayoutDto dto in snapshot.OverheadLines)
        {
            bool isContinued = domain.OverheadLines
                .Single(line => line.ConnectionId == dto.ConnectionId)
                .IsContinued;
            drawingLayout.Add(new OverheadLineLayout(
                dto.ConnectionId,
                Point(dto.Start),
                Point(dto.End),
                isContinued,
                Point(dto.ContinuationOffset)));
        }

        var cabinetLayouts = new Dictionary<Guid, RingCabinetLayout>();
        foreach (ProjectRingCabinetLayoutDto dto in snapshot.RingCabinets)
        {
            var intervals = new List<RingCabinetIntervalLayout>();
            foreach (ProjectRingCabinetIntervalLayoutDto intervalDto in dto.Intervals)
            {
                var switches = intervalDto.Switches
                    .Select(switchDto => new RingCabinetSwitchLayout(
                        switchDto.SwitchDeviceId,
                        Point(switchDto.RelativePosition),
                        switchDto.WidthMillimeters,
                        switchDto.HeightMillimeters,
                        Point(switchDto.LabelOffset)))
                    .ToArray();
                intervals.Add(new RingCabinetIntervalLayout(
                    intervalDto.IntervalId,
                    Point(intervalDto.RelativePosition),
                    intervalDto.WidthMillimeters,
                    intervalDto.HeightMillimeters,
                    Point(intervalDto.SequenceLabelOffset),
                    Point(intervalDto.NameLabelOffset),
                    switches));
            }

            cabinetLayouts.Add(
                dto.CabinetId,
                new RingCabinetLayout(
                    dto.CabinetId,
                    Point(dto.Position),
                    dto.WidthMillimeters,
                    dto.HeightMillimeters,
                    dto.MainBusYMillimeters,
                    intervals,
                    Point(dto.LabelOffset)));
        }

        return new RuntimeLayoutDocument(drawingLayout, cabinetLayouts);
    }

    private static ProjectPointDto Point(DocumentPoint point)
    {
        return new ProjectPointDto(point.XMillimeters, point.YMillimeters);
    }

    private static DocumentPoint Point(ProjectPointDto point)
    {
        return new DocumentPoint(point.XMillimeters, point.YMillimeters);
    }
}
