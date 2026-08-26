using System.Windows.Media;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Connections;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Desktop.ConnectionEditing;

public sealed class OverheadLineConnectionController
{
    private readonly Func<ProjectRuntimeSession?> _getSession;
    private readonly OverheadLineCommandFactory _commandFactory;
    private readonly OrthogonalRouter _previewRouter = new();
    private Guid? _startTerminalId;
    private DocumentPoint? _previewEnd;

    public OverheadLineConnectionController(
        Func<ProjectRuntimeSession?> getSession,
        OverheadLineCommandFactory? commandFactory = null)
    {
        _getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
        _commandFactory = commandFactory ?? new OverheadLineCommandFactory();
    }

    public OverheadLineToolState State { get; private set; }

    public OverheadLineToolOutcome LastOutcome { get; private set; }

    public bool IsActive => State != OverheadLineToolState.Idle;

    public bool HasPreview => State == OverheadLineToolState.PickingEndTerminal;

    public bool IsOverheadLineSelected =>
        _getSession()?.SelectionManager.Selected?.Kind == SelectionTargetKind.Connection;

    public event EventHandler? VisualChanged;

    public void Begin()
    {
        _ = RequireSession();
        _startTerminalId = null;
        _previewEnd = null;
        State = OverheadLineToolState.PickingStartTerminal;
        LastOutcome = OverheadLineToolOutcome.None;
        VisualChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Cancel()
    {
        bool changed = IsActive || _previewEnd is not null;
        _startTerminalId = null;
        _previewEnd = null;
        State = OverheadLineToolState.Idle;
        LastOutcome = OverheadLineToolOutcome.Cancelled;
        if (changed)
        {
            VisualChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void UpdatePointer(DocumentPoint pointer)
    {
        if (State != OverheadLineToolState.PickingEndTerminal)
        {
            return;
        }

        _previewEnd = pointer;
        VisualChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pick(DocumentPoint pointer, double toleranceMillimeters)
    {
        if (!IsActive)
        {
            return;
        }

        try
        {
            ProjectRuntimeSession session = RequireSession();
            TerminalAnchorIndex anchors = BuildAnchors(session);
            TerminalAnchor picked = PickAnchor(
                session,
                anchors,
                pointer,
                toleranceMillimeters);
            if (State == OverheadLineToolState.PickingStartTerminal)
            {
                _startTerminalId = picked.TerminalId;
                _previewEnd = picked.Position;
                State = OverheadLineToolState.PickingEndTerminal;
                LastOutcome = OverheadLineToolOutcome.StartPicked;
                session.SelectionManager.Select(
                    new SelectionReference(SelectionTargetKind.Terminal, picked.TerminalId));
                VisualChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            Guid startTerminalId = _startTerminalId
                ?? throw new InvalidOperationException("The overhead-line start terminal is missing.");
            if (!anchors.TryGet(startTerminalId, out TerminalAnchor startAnchor))
            {
                throw new InvalidOperationException(
                    $"Start terminal anchor '{startTerminalId}' no longer exists.");
            }

            AddOverheadLineCommand command = _commandFactory.CreateAdd(
                session.PersistenceSession.Domain,
                session.Layout,
                startTerminalId,
                picked.TerminalId,
                startAnchor.Position,
                picked.Position);
            session.CommandStack.ExecuteCommand(command, session.RebuildScene);
            Guid? continuationTerminalId = ResolveContinuationTerminalId(
                session,
                picked.TerminalId);
            _startTerminalId = continuationTerminalId;
            _previewEnd = continuationTerminalId is Guid nextTerminalId &&
                BuildAnchors(session).TryGet(nextTerminalId, out TerminalAnchor nextAnchor)
                    ? nextAnchor.Position
                    : null;
            State = continuationTerminalId is null
                ? OverheadLineToolState.Idle
                : OverheadLineToolState.PickingEndTerminal;
            LastOutcome = OverheadLineToolOutcome.Committed;
            session.SelectionManager.Select(
                new SelectionReference(
                    SelectionTargetKind.Connection,
                    command.Connection.Id));
            VisualChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            LastOutcome = OverheadLineToolOutcome.InvalidTarget;
            throw;
        }
    }

    public void RemoveSelected()
    {
        ProjectRuntimeSession session = RequireSession();
        SelectionReference selected = session.SelectionManager.Selected
            ?? throw new InvalidOperationException("No overhead line is selected.");
        if (selected.Kind != SelectionTargetKind.Connection)
        {
            throw new InvalidOperationException("The selected object is not an overhead line.");
        }

        RemoveOverheadLineCommand command = _commandFactory.CreateRemove(
            session.PersistenceSession.Domain,
            session.Layout,
            selected.ObjectId);
        session.CommandStack.ExecuteCommand(command);
        session.SelectionManager.Clear();
        session.RebuildScene();
        VisualChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<SceneElement> CreatePreviewElements()
    {
        if (State != OverheadLineToolState.PickingEndTerminal ||
            _startTerminalId is not Guid startTerminalId ||
            _previewEnd is not DocumentPoint previewEnd ||
            _getSession() is not { } session)
        {
            return [];
        }

        TerminalAnchorIndex anchors = BuildAnchors(session);
        if (!anchors.TryGet(startTerminalId, out TerminalAnchor startAnchor))
        {
            return [];
        }

        IReadOnlyList<DocumentPoint> path = _previewRouter.CreatePreview(startAnchor, previewEnd);
        return path.Zip(path.Skip(1), (start, end) => (SceneElement)new SceneLine(
                start,
                end,
                Colors.DodgerBlue,
                DrawingMetrics.Default.Line.ConnectionThickness,
                SceneStrokeStyle.Solid))
            .ToArray();
    }

    private static TerminalAnchorIndex BuildAnchors(ProjectRuntimeSession session)
    {
        return TerminalAnchorIndex.Build(
            session.PersistenceSession.Domain,
            session.Layout.DrawingLayout,
            session.Layout.RingCabinetLayouts,
            session.PersistenceSession.Domain.Connections,
            session.PersistenceSession.Domain.CableSegments);
    }

    private static TerminalAnchor PickAnchor(
        ProjectRuntimeSession session,
        TerminalAnchorIndex anchors,
        DocumentPoint pointer,
        double toleranceMillimeters)
    {
        if (toleranceMillimeters <= 0 ||
            double.IsNaN(toleranceMillimeters) ||
            double.IsInfinity(toleranceMillimeters))
        {
            throw new ArgumentOutOfRangeException(nameof(toleranceMillimeters));
        }

        var terminals = session.PersistenceSession.Domain.Terminals.ToDictionary(item => item.Id);
        var nearby = anchors.Anchors
            .Where(anchor => terminals.ContainsKey(anchor.TerminalId))
            .Select(anchor => new
            {
                Anchor = anchor,
                Terminal = terminals[anchor.TerminalId],
                Distance = Distance(anchor.Position, pointer)
            })
            .Where(candidate => candidate.Distance <= toleranceMillimeters)
            .ToArray();
        if (nearby.Length == 0)
        {
            throw new InvalidOperationException(
                "未找到架空线端子，请点击有效的架空线连接点。");
        }

        var candidates = nearby
            .Where(candidate => IsAvailable(session, candidate.Terminal))
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => EndpointPriority(session, candidate.Terminal))
            .ThenBy(candidate => candidate.Anchor.TerminalId)
            .ToArray();
        if (candidates.Length == 0)
        {
            throw new InvalidOperationException(
                "所选端子不能连接架空线，或已被其他连接占用。");
        }

        if (candidates.Length > 1 &&
            candidates[0].Anchor.Position == candidates[1].Anchor.Position &&
            EndpointPriority(session, candidates[0].Terminal) ==
            EndpointPriority(session, candidates[1].Terminal))
        {
            throw new InvalidOperationException(
                "多个可用架空线端子重叠，无法确定连接目标。");
        }

        return candidates[0].Anchor;
    }

    private static int EndpointPriority(ProjectRuntimeSession session, Terminal terminal)
    {
        if (terminal.OwnerType != TopologyOwnerType.Device)
        {
            return 0;
        }

        Device? owner = session.PersistenceSession.Domain.Devices
            .SingleOrDefault(device => device.Id == terminal.OwnerId);
        return owner is Pole ? 1 : 0;
    }

    private static Guid? ResolveContinuationTerminalId(
        ProjectRuntimeSession session,
        Guid reachedTerminalId)
    {
        Terminal reachedTerminal = session.PersistenceSession.Domain.Terminals
            .Single(terminal => terminal.Id == reachedTerminalId);
        if (reachedTerminal.AllowsMultipleConnections)
        {
            return reachedTerminal.Id;
        }

        if (reachedTerminal.OwnerType != TopologyOwnerType.Device)
        {
            return null;
        }

        SwitchDevice? switchDevice = session.PersistenceSession.Domain.Devices
            .OfType<SwitchDevice>()
            .SingleOrDefault(device =>
                device.InstallationType == SwitchInstallationType.Pole &&
                device.Id == reachedTerminal.OwnerId);
        return switchDevice?.TerminalIds.Single(id => id != reachedTerminalId);
    }

    private static bool IsAvailable(ProjectRuntimeSession session, Terminal terminal)
    {
        if (!terminal.IsExternal || !terminal.Allows(ConnectionType.OverheadLine))
        {
            return false;
        }

        return terminal.AllowsMultipleConnections ||
            session.PersistenceSession.Domain.Connections.All(
                connection => !connection.UsesTerminal(terminal.Id));
    }

    private static double Distance(DocumentPoint first, DocumentPoint second)
    {
        double x = first.XMillimeters - second.XMillimeters;
        double y = first.YMillimeters - second.YMillimeters;
        return Math.Sqrt(x * x + y * y);
    }

    private ProjectRuntimeSession RequireSession()
    {
        return _getSession()
            ?? throw new InvalidOperationException("No project is currently open.");
    }
}
