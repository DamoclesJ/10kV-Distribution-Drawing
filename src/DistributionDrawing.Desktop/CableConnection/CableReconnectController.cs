using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Desktop.CableConnection;

public enum CableReconnectEndpoint
{
    Start,
    End
}

public sealed class CableReconnectController
{
    private readonly Func<ProjectRuntimeSession?> _getSession;

    public CableReconnectController(Func<ProjectRuntimeSession?> getSession)
    {
        _getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
    }

    public CableReconnectEndpoint? Endpoint { get; private set; }

    public bool IsActive => Endpoint is not null;

    public event EventHandler? VisualChanged;

    public void BeginStart() => Begin(CableReconnectEndpoint.Start);

    public void BeginEnd() => Begin(CableReconnectEndpoint.End);

    public void Cancel()
    {
        if (!IsActive)
        {
            return;
        }

        Endpoint = null;
        VisualChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pick(DocumentPoint pointer, double toleranceMillimeters)
    {
        CableReconnectEndpoint endpoint = Endpoint
            ?? throw new InvalidOperationException("当前没有进行电缆端点重连。");
        ProjectRuntimeSession session = RequireSession();
        DrawingDocument document = session.PersistenceSession.Domain;
        SelectionReference selected = session.SelectionManager.Selected
            ?? throw new InvalidOperationException("请先选择一条电缆。");
        CableSegment cable = document.CableSegments.SingleOrDefault(
                candidate => candidate.Id == selected.ObjectId)
            ?? throw new InvalidOperationException("所选电缆不存在。");

        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            session.Layout.DrawingLayout,
            session.Layout.RingCabinetLayouts,
            document.Connections,
            document.CableSegments);
        TerminalAnchor picked = PickAnchor(
            document,
            cable,
            endpoint,
            anchors,
            pointer,
            toleranceMillimeters);

        Guid startTerminalId = endpoint == CableReconnectEndpoint.Start
            ? picked.TerminalId
            : cable.StartTerminalId;
        Guid endTerminalId = endpoint == CableReconnectEndpoint.End
            ? picked.TerminalId
            : cable.EndTerminalId;

        if (startTerminalId == endTerminalId)
        {
            throw new InvalidOperationException("新的电缆起点和终点不能相同。");
        }

        try
        {
            var applicationCommand = new ReconnectCableCommand(
                document,
                cable.Id,
                startTerminalId,
                endTerminalId);
            session.CommandStack.ExecuteCommand(
                new ReconnectCableCommandAdapter(applicationCommand));
            session.RebuildScene();
            session.SelectionManager.Select(
                new SelectionReference(SelectionTargetKind.CableSegment, cable.Id));
            Endpoint = null;
            VisualChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException("电缆端点修改失败，工程数据未改变。", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException("电缆端点修改失败，工程数据未改变。", exception);
        }
    }

    private void Begin(CableReconnectEndpoint endpoint)
    {
        ProjectRuntimeSession session = RequireSession();
        if (session.SelectionManager.Selected is not
            { Kind: SelectionTargetKind.CableSegment })
        {
            throw new InvalidOperationException("请先选择一条电缆。");
        }

        Endpoint = endpoint;
        VisualChanged?.Invoke(this, EventArgs.Empty);
    }

    private static TerminalAnchor PickAnchor(
        DrawingDocument document,
        CableSegment cable,
        CableReconnectEndpoint endpoint,
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

        Guid otherTerminalId = endpoint == CableReconnectEndpoint.Start
            ? cable.EndTerminalId
            : cable.StartTerminalId;
        var terminals = document.Terminals.ToDictionary(terminal => terminal.Id);
        var candidates = anchors.Anchors
            .Where(anchor => anchor.TerminalId != otherTerminalId)
            .Where(anchor => terminals.TryGetValue(anchor.TerminalId, out Terminal? terminal) &&
                IsAvailable(document, terminal, cable.ConnectionId))
            .Select(anchor => (Anchor: anchor, Distance: Distance(anchor.Position, pointer)))
            .Where(candidate => candidate.Distance <= toleranceMillimeters)
            .OrderBy(candidate => candidate.Distance)
            .ToArray();
        if (candidates.Length == 0)
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

    private static bool IsAvailable(
        DrawingDocument document,
        Terminal terminal,
        Guid currentConnectionId)
    {
        if (!terminal.IsExternal || !terminal.Allows(ConnectionType.Cable))
        {
            return false;
        }

        return terminal.AllowsMultipleConnections || document.Connections.All(
            connection => connection.Id == currentConnectionId ||
                !connection.UsesTerminal(terminal.Id));
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

internal sealed class ReconnectCableCommandAdapter : ICommand
{
    private readonly ReconnectCableCommand _command;

    public ReconnectCableCommandAdapter(ReconnectCableCommand command)
    {
        _command = command ?? throw new ArgumentNullException(nameof(command));
    }

    public void Execute() => _command.Execute();

    public void Undo() => _command.Undo();

    public void Redo() => _command.Redo();
}
