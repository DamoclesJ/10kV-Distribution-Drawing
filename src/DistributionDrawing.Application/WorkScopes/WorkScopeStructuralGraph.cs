using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.WorkScopes;

/// <summary>Physical terminal relationships with declared source crossings removed.</summary>
public sealed class WorkScopeStructuralGraph
{
    public sealed record Edge(Guid FirstTerminalId, Guid SecondTerminalId,
        Guid? ConnectionId = null);

    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> _adjacency;

    private WorkScopeStructuralGraph(IReadOnlyList<Edge> edges,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> adjacency)
    {
        Edges = edges;
        _adjacency = adjacency;
    }

    public IReadOnlyList<Edge> Edges { get; }

    public static WorkScopeStructuralGraph Build(DrawingDocument drawing,
        IReadOnlySet<Guid> boundaryDeviceIds)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        ArgumentNullException.ThrowIfNull(boundaryDeviceIds);
        ElectricalConnectivityGraph conducting = new ElectricalConnectivityGraphBuilder().Build(drawing);
        HashSet<Guid> earthTerminals = drawing.ElectricalNodes
            .Where(node => node.Type == ElectricalNodeType.Earth)
            .SelectMany(node => node.TerminalIds).ToHashSet();
        List<Edge> edges = conducting.Edges
            .Where(edge => edge.Type != ElectricalConnectivityEdgeType.ClosedSwitch &&
                !earthTerminals.Contains(edge.FirstTerminalId) &&
                !earthTerminals.Contains(edge.SecondTerminalId))
            .Select(edge => new Edge(edge.FirstTerminalId, edge.SecondTerminalId,
                edge.Type == ElectricalConnectivityEdgeType.Connection ? edge.SourceId : null))
            .ToList();
        foreach (SwitchDevice device in drawing.Devices.OfType<SwitchDevice>())
        {
            if (boundaryDeviceIds.Contains(device.Id) ||
                device.SwitchKind == SwitchKind.GroundSwitch) continue;
            if (device.SwitchKind is not (SwitchKind.LoadSwitch or SwitchKind.IsolationSwitch or
                SwitchKind.CircuitBreaker or SwitchKind.DropoutFuse)) continue;
            if (earthTerminals.Contains(device.FirstTerminalId) ||
                earthTerminals.Contains(device.SecondTerminalId)) continue;
            edges.Add(new Edge(device.FirstTerminalId, device.SecondTerminalId));
        }
        Dictionary<Guid, List<Guid>> adjacency = conducting.TerminalIds
            .ToDictionary(id => id, _ => new List<Guid>());
        foreach (Edge edge in edges)
        {
            adjacency[edge.FirstTerminalId].Add(edge.SecondTerminalId);
            adjacency[edge.SecondTerminalId].Add(edge.FirstTerminalId);
        }
        return new WorkScopeStructuralGraph(edges.AsReadOnly(), adjacency.ToDictionary(
            pair => pair.Key, pair => (IReadOnlyList<Guid>)pair.Value.AsReadOnly()));
    }

    public IReadOnlySet<Guid> FindRegion(Guid terminalId)
    {
        if (!_adjacency.ContainsKey(terminalId))
            throw new KeyNotFoundException($"Terminal '{terminalId}' is not in the graph.");
        HashSet<Guid> region = [terminalId];
        Queue<Guid> pending = new([terminalId]);
        while (pending.TryDequeue(out Guid current))
            foreach (Guid adjacent in _adjacency[current])
                if (region.Add(adjacent)) pending.Enqueue(adjacent);
        return region;
    }
}
