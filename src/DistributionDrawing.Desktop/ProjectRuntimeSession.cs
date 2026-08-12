using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.PropertyInspector;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Desktop;

/// <summary>
/// Runtime projection of a persisted project. Persistence DTOs are converted
/// to millimeter layout objects, then rendered into a fresh scene. Transient
/// WPF and editor state is created here and is never part of the file format.
/// </summary>
public sealed class ProjectRuntimeSession
{
    private ProjectRuntimeSession(
        ProjectSession persistenceSession,
        RuntimeLayoutDocument layout,
        DrawingScene scene,
        PropertyInspectionSource inspectionSource)
    {
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
        CommandStack.MarkSaved();
    }

    public ProjectSession PersistenceSession { get; }

    public RuntimeLayoutDocument Layout { get; }

    public DrawingScene Scene { get; }

    public PropertyInspectionSource InspectionSource { get; }

    public SelectionManager SelectionManager { get; }

    public SelectionObjectResolver SelectionResolver { get; }

    public PropertyInspectorViewModel PropertyInspector { get; }

    public PropertyProjector PropertyProjector { get; }

    public CommandStack CommandStack { get; }

    public bool IsDirty => PersistenceSession.IsDirty || CommandStack.IsDirty;

    public long SavePoint => CommandStack.SavedStateId;

    public static ProjectRuntimeSession Load(
        ProjectService projectService,
        string filePath,
        DrawingSceneBuilder? sceneBuilder = null)
    {
        ArgumentNullException.ThrowIfNull(projectService);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        ProjectSession persistenceSession = projectService.LoadProject(filePath);
        RuntimeLayoutDocument layout = ProjectLayoutRuntimeMapper.ToRuntime(
            persistenceSession.Domain,
            persistenceSession.Layout);
        DrawingSceneBuilder builder = sceneBuilder ?? new DrawingSceneBuilder();
        DrawingScene scene = builder.Build(persistenceSession.Domain, layout);
        PropertyInspectionSource source = CreateInspectionSource(
            persistenceSession,
            layout,
            scene);

        return new ProjectRuntimeSession(
            persistenceSession,
            layout,
            scene,
            source);
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

    private static DocumentPoint Point(ProjectPointDto point)
    {
        return new DocumentPoint(point.XMillimeters, point.YMillimeters);
    }
}
