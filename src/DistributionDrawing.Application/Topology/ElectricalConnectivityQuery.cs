using System.Collections.Frozen;

namespace DistributionDrawing.Application.Topology;

public sealed class ElectricalConnectivityQuery
{
    private readonly ElectricalConnectivityGraph _graph;
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> _adjacency;

    public ElectricalConnectivityQuery(ElectricalConnectivityGraph graph)
    {
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));

        Dictionary<Guid, List<Guid>> adjacency = graph.TerminalIds
            .ToDictionary(terminalId => terminalId, _ => new List<Guid>());
        foreach (ElectricalConnectivityEdge edge in graph.Edges)
        {
            adjacency[edge.FirstTerminalId].Add(edge.SecondTerminalId);
            adjacency[edge.SecondTerminalId].Add(edge.FirstTerminalId);
        }

        _adjacency = adjacency.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<Guid>)Array.AsReadOnly(pair.Value.ToArray()));
    }

    public bool IsConnected(Guid startTerminalId, Guid endTerminalId)
    {
        EnsureKnownTerminal(startTerminalId);
        EnsureKnownTerminal(endTerminalId);

        if (startTerminalId == endTerminalId)
        {
            return true;
        }

        HashSet<Guid> visited = [startTerminalId];
        Queue<Guid> pending = new([startTerminalId]);

        while (pending.TryDequeue(out Guid currentTerminalId))
        {
            foreach (Guid adjacentTerminalId in _adjacency[currentTerminalId])
            {
                if (!visited.Add(adjacentTerminalId))
                {
                    continue;
                }

                if (adjacentTerminalId == endTerminalId)
                {
                    return true;
                }

                pending.Enqueue(adjacentTerminalId);
            }
        }

        return false;
    }

    public IReadOnlySet<Guid> FindConnectedTerminalIds(Guid terminalId)
    {
        EnsureKnownTerminal(terminalId);

        HashSet<Guid> connected = [terminalId];
        Queue<Guid> pending = new([terminalId]);

        while (pending.TryDequeue(out Guid currentTerminalId))
        {
            foreach (Guid adjacentTerminalId in _adjacency[currentTerminalId])
            {
                if (connected.Add(adjacentTerminalId))
                {
                    pending.Enqueue(adjacentTerminalId);
                }
            }
        }

        return connected.ToFrozenSet();
    }

    private void EnsureKnownTerminal(Guid terminalId)
    {
        if (!_graph.ContainsTerminal(terminalId))
        {
            throw new KeyNotFoundException(
                $"Terminal '{terminalId}' does not exist in the connectivity graph.");
        }
    }
}
