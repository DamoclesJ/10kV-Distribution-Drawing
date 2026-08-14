namespace DistributionDrawing.Application.Topology;

public sealed class ElectricalConnectivityGraph
{
    private readonly IReadOnlyList<Guid> _terminalIds;
    private readonly IReadOnlyList<ElectricalConnectivityEdge> _edges;

    internal ElectricalConnectivityGraph(
        IEnumerable<Guid> terminalIds,
        IEnumerable<ElectricalConnectivityEdge> edges)
    {
        Guid[] vertices = terminalIds?.ToArray()
            ?? throw new ArgumentNullException(nameof(terminalIds));
        ElectricalConnectivityEdge[] graphEdges = edges?.ToArray()
            ?? throw new ArgumentNullException(nameof(edges));

        if (vertices.Any(id => id == Guid.Empty) ||
            vertices.Distinct().Count() != vertices.Length)
        {
            throw new ArgumentException(
                "Graph vertices must contain unique non-empty terminal IDs.",
                nameof(terminalIds));
        }

        HashSet<Guid> vertexSet = vertices.ToHashSet();
        if (graphEdges.Any(edge =>
                !vertexSet.Contains(edge.FirstTerminalId) ||
                !vertexSet.Contains(edge.SecondTerminalId)))
        {
            throw new ArgumentException(
                "Every graph edge endpoint must be a graph vertex.",
                nameof(edges));
        }

        _terminalIds = Array.AsReadOnly(vertices);
        _edges = Array.AsReadOnly(graphEdges);
    }

    public IReadOnlyList<Guid> TerminalIds => _terminalIds;

    public IReadOnlyList<ElectricalConnectivityEdge> Edges => _edges;

    public bool ContainsTerminal(Guid terminalId)
    {
        return _terminalIds.Contains(terminalId);
    }
}
