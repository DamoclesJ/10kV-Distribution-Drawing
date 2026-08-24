using System.Windows.Media;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Desktop.CableConnection;

public sealed class CableConnectionController
{
    private readonly Func<ProjectRuntimeSession?> _getSession;
    private readonly CableSegmentCreationFactory _creationFactory = new();
    private readonly OrthogonalRouter _previewRouter = new();
    private Guid? _startTerminalId;
    private Guid? _endTerminalId;
    private DocumentPoint? _previewEnd;

    public CableConnectionController(Func<ProjectRuntimeSession?> getSession)
    {
        _getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
    }

    public CableConnectionToolState State { get; private set; }

    public CableConnectionToolOutcome LastOutcome { get; private set; }

    public bool IsActive => State != CableConnectionToolState.Idle;

    public bool IsCableSegmentSelected =>
        _getSession()?.SelectionManager.Selected is
        { Kind: SelectionTargetKind.CableSegment };

    public bool HasPreview => State is CableConnectionToolState.PickingEndTerminal or
        CableConnectionToolState.AwaitingParameters;

    public event EventHandler? VisualChanged;

    public event EventHandler? ParametersRequired;

    public void Begin()
    {
        _ = RequireSession();
        _startTerminalId = null;
        _endTerminalId = null;
        _previewEnd = null;
        State = CableConnectionToolState.PickingStartTerminal;
        LastOutcome = CableConnectionToolOutcome.None;
        VisualChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Cancel()
    {
        bool changed = IsActive || _previewEnd is not null;
        _startTerminalId = null;
        _endTerminalId = null;
        _previewEnd = null;
        State = CableConnectionToolState.Idle;
        LastOutcome = CableConnectionToolOutcome.Cancelled;
        if (changed)
        {
            VisualChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void UpdatePointer(DocumentPoint pointer)
    {
        if (State != CableConnectionToolState.PickingEndTerminal)
        {
            return;
        }

        _previewEnd = pointer;
        VisualChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pick(DocumentPoint pointer, double toleranceMillimeters)
    {
        if (State is not (CableConnectionToolState.PickingStartTerminal or
            CableConnectionToolState.PickingEndTerminal))
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

            if (State == CableConnectionToolState.PickingStartTerminal)
            {
                _startTerminalId = picked.TerminalId;
                _previewEnd = picked.Position;
                State = CableConnectionToolState.PickingEndTerminal;
                LastOutcome = CableConnectionToolOutcome.StartPicked;
                session.SelectionManager.Select(
                    new SelectionReference(SelectionTargetKind.Terminal, picked.TerminalId));
                VisualChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            Guid startTerminalId = _startTerminalId
                ?? throw new InvalidOperationException("电缆起点端子不存在。");
            if (picked.TerminalId == startTerminalId)
            {
                throw new InvalidOperationException("电缆起点和终点不能相同。");
            }

            _endTerminalId = picked.TerminalId;
            _previewEnd = picked.Position;
            State = CableConnectionToolState.AwaitingParameters;
            LastOutcome = CableConnectionToolOutcome.EndPicked;
            ParametersRequired?.Invoke(this, EventArgs.Empty);
            VisualChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            LastOutcome = CableConnectionToolOutcome.InvalidTarget;
            throw;
        }
    }

    public void Complete(string cableType, double length)
    {
        if (State != CableConnectionToolState.AwaitingParameters)
        {
            throw new InvalidOperationException("当前没有待完成的电缆连接。");
        }

        try
        {
            ProjectRuntimeSession session = RequireSession();
            Guid startTerminalId = _startTerminalId
                ?? throw new InvalidOperationException("电缆起点端子不存在。");
            Guid endTerminalId = _endTerminalId
                ?? throw new InvalidOperationException("电缆终点端子不存在。");
            CableSegmentCreationResult creation = _creationFactory.Create(
                session.PersistenceSession.Domain,
                startTerminalId,
                endTerminalId,
                "电缆",
                cableType,
                length);
            var command = new AddCableSegmentCommand(
                session.PersistenceSession.Domain,
                creation);
            session.CommandStack.ExecuteCommand(command);
            session.RebuildScene();
            session.SelectionManager.Select(
                new SelectionReference(
                    SelectionTargetKind.CableSegment,
                    creation.CableSegment.Id));
            _startTerminalId = null;
            _endTerminalId = null;
            _previewEnd = null;
            State = CableConnectionToolState.Idle;
            LastOutcome = CableConnectionToolOutcome.Committed;
            VisualChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            LastOutcome = CableConnectionToolOutcome.InvalidTarget;
            throw;
        }
    }

    public void RemoveSelected()
    {
        ProjectRuntimeSession session = RequireSession();
        SelectionReference selected = session.SelectionManager.Selected
            ?? throw new InvalidOperationException("请先选择一条电缆。");
        if (selected.Kind != SelectionTargetKind.CableSegment)
        {
            throw new InvalidOperationException("当前选择不是电缆。");
        }

        DrawingDocument document = session.PersistenceSession.Domain;
        CableSegment cableSegment = document.CableSegments.SingleOrDefault(
                candidate => candidate.Id == selected.ObjectId)
            ?? throw new InvalidOperationException("所选电缆不存在，未执行删除。");
        Connection connection = document.Connections.SingleOrDefault(
                candidate => candidate.Id == cableSegment.ConnectionId)
            ?? throw new InvalidOperationException("所选电缆的连接不存在，未执行删除。");

        var command = new RemoveCableSegmentCommand(
            document,
            cableSegment,
            connection,
            session.Layout);
        try
        {
            session.CommandStack.ExecuteCommand(command);
            session.SelectionManager.Clear();
            session.RebuildScene();
            LastOutcome = CableConnectionToolOutcome.Committed;
            VisualChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException("电缆删除失败，工程数据未改变。", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException("电缆删除失败，工程数据未改变。", exception);
        }
    }

    public IReadOnlyList<SceneElement> CreatePreviewElements()
    {
        if (!HasPreview ||
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
                Colors.DarkOrange,
                DrawingMetrics.Default.Line.ConnectionThickness,
                SceneStrokeStyle.Dashed))
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
        var candidates = anchors.Anchors
            .Where(anchor => terminals.ContainsKey(anchor.TerminalId))
            .Select(anchor => (Anchor: anchor, Distance: Distance(anchor.Position, pointer)))
            .Where(candidate => candidate.Distance <= toleranceMillimeters)
            .OrderBy(candidate => candidate.Distance)
            .ToArray();
        if (candidates.Length == 0 ||
            !IsAvailable(session, terminals[candidates[0].Anchor.TerminalId]))
        {
            throw new InvalidOperationException("点击位置没有可连接电缆的端子。");
        }

        if (candidates.Length > 1 &&
            candidates[0].Anchor.Position == candidates[1].Anchor.Position)
        {
            throw new InvalidOperationException("多个电缆端子重叠，无法确定连接目标。");
        }

        return candidates[0].Anchor;
    }

    private static bool IsAvailable(ProjectRuntimeSession session, Terminal terminal)
    {
        if (!terminal.IsExternal || !terminal.Allows(ConnectionType.Cable))
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
            ?? throw new InvalidOperationException("当前没有打开工程。");
    }
}
