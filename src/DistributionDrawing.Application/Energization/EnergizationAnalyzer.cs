using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.Energization;

public sealed class EnergizationAnalyzer
{
    private readonly ElectricalConnectivityGraphBuilder _graphBuilder = new();
    private readonly EnergizationBoundaryPolicy _boundaryPolicy = new();

    public EnergizationResult Analyze(DrawingDocument drawing, EnergizationScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        ArgumentNullException.ThrowIfNull(scenario);

        var diagnostics = new List<EnergizationDiagnostic>();
        if (scenario.Seeds.Count == 0)
            diagnostics.Add(new EnergizationDiagnostic(EnergizationDiagnosticCode.EmptyScenario));
        if (!scenario.IsSourceSetComplete)
            diagnostics.Add(new EnergizationDiagnostic(EnergizationDiagnosticCode.SourceSetUnconfirmed));

        ElectricalConnectivityGraph? graph;
        try
        {
            graph = _graphBuilder.Build(drawing);
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        {
            diagnostics.Add(new EnergizationDiagnostic(
                EnergizationDiagnosticCode.InvalidTopology, Detail: error.Message));
            return CreateResult(drawing, new Dictionary<Guid, HashSet<Guid>>(), [], [],
                diagnostics, EnergizationValidity.Incomplete);
        }

        var switches = drawing.Devices.OfType<SwitchDevice>()
            .ToDictionary(device => device.Id);
        HashSet<Guid> earthTerminalIds = drawing.Terminals
            .Where(terminal => terminal.ElectricalNodeId is Guid nodeId &&
                drawing.ElectricalNodes.Any(node => node.Id == nodeId &&
                    node.Type == ElectricalNodeType.Earth))
            .Select(terminal => terminal.Id).ToHashSet();

        ElectricalConnectivityEdge[] conductingEdges = graph.Edges
            .Where(edge => !earthTerminalIds.Contains(edge.FirstTerminalId) &&
                !earthTerminalIds.Contains(edge.SecondTerminalId) &&
                !(edge.Type == ElectricalConnectivityEdgeType.ClosedSwitch &&
                  switches.TryGetValue(edge.SourceId, out SwitchDevice? device) &&
                  device.SwitchKind == SwitchKind.GroundSwitch))
            .ToArray();
        GroundingSwitchConnection[] groundingConnections = switches.Values
            .Where(device => device.SwitchKind == SwitchKind.GroundSwitch &&
                device.SwitchState == SwitchState.Closed)
            .Select(device => new GroundingSwitchConnection(
                device.Id,
                earthTerminalIds.Contains(device.FirstTerminalId)
                    ? device.SecondTerminalId : device.FirstTerminalId,
                earthTerminalIds.Contains(device.FirstTerminalId)
                    ? device.FirstTerminalId : device.SecondTerminalId))
            .ToArray();

        var sources = graph.TerminalIds.ToDictionary(id => id, _ => new HashSet<Guid>());
        var adjacency = graph.TerminalIds.ToDictionary(id => id, _ => new List<Guid>());
        foreach (ElectricalConnectivityEdge edge in conductingEdges)
        {
            adjacency[edge.FirstTerminalId].Add(edge.SecondTerminalId);
            adjacency[edge.SecondTerminalId].Add(edge.FirstTerminalId);
        }

        var pending = new Queue<(Guid TerminalId, Guid SeedId)>();
        int resolvedSeedCount = 0;
        foreach (EnergizedSeed seed in scenario.Seeds)
        {
            if (!_boundaryPolicy.TryResolve(drawing, seed, out Guid terminalId,
                out EnergizationDiagnosticCode issue))
            {
                diagnostics.Add(new EnergizationDiagnostic(issue, seed.Id));
                continue;
            }

            resolvedSeedCount++;
            if (sources[terminalId].Add(seed.Id)) pending.Enqueue((terminalId, seed.Id));
        }

        while (pending.TryDequeue(out (Guid terminalId, Guid seedId) current))
        {
            foreach (Guid adjacentId in adjacency[current.terminalId])
                if (sources[adjacentId].Add(current.seedId))
                    pending.Enqueue((adjacentId, current.seedId));
        }

        EnergizationValidity validity = scenario.Seeds.Count == 0
            ? EnergizationValidity.NoSeeds
            : resolvedSeedCount != scenario.Seeds.Count
                ? EnergizationValidity.Incomplete
                : scenario.IsSourceSetComplete
                    ? EnergizationValidity.Complete
                    : EnergizationValidity.ForwardOnly;
        return CreateResult(drawing, sources, conductingEdges, groundingConnections,
            diagnostics, validity);
    }

    private static EnergizationResult CreateResult(
        DrawingDocument drawing,
        IReadOnlyDictionary<Guid, HashSet<Guid>> sources,
        IEnumerable<ElectricalConnectivityEdge> conductingEdges,
        IEnumerable<GroundingSwitchConnection> groundingConnections,
        IEnumerable<EnergizationDiagnostic> diagnostics,
        EnergizationValidity validity)
    {
        bool canConcludeDeenergized = validity == EnergizationValidity.Complete;
        HashSet<Guid> earthNodeIds = drawing.ElectricalNodes
            .Where(node => node.Type == ElectricalNodeType.Earth)
            .Select(node => node.Id).ToHashSet();
        Dictionary<Guid, EnergizationPointResult> terminalResults = drawing.Terminals
            .ToDictionary(terminal => terminal.Id, terminal =>
            {
                bool earth = terminal.ElectricalNodeId is Guid nodeId &&
                    earthNodeIds.Contains(nodeId);
                IReadOnlySet<Guid> reached = sources.TryGetValue(terminal.Id, out HashSet<Guid>? ids)
                    ? ids : new HashSet<Guid>();
                return new EnergizationPointResult(
                    !earth && reached.Count > 0 ? EnergizationState.Energized :
                    !earth && canConcludeDeenergized ? EnergizationState.Deenergized :
                    EnergizationState.Unknown,
                    earth ? [] : reached);
            });
        Dictionary<Guid, EnergizationPointResult> nodeResults = drawing.ElectricalNodes
            .ToDictionary(node => node.Id, node =>
            {
                Guid[] sourceIds = node.TerminalIds
                    .Where(terminalResults.ContainsKey)
                    .SelectMany(id => terminalResults[id].EnergizedBy)
                    .Distinct().ToArray();
                return new EnergizationPointResult(
                    node.Type == ElectricalNodeType.Earth || node.TerminalIds.Count == 0
                        ? EnergizationState.Unknown
                        : sourceIds.Length > 0 ? EnergizationState.Energized
                        : canConcludeDeenergized ? EnergizationState.Deenergized
                        : EnergizationState.Unknown,
                    sourceIds);
            });
        return new EnergizationResult(validity, terminalResults, nodeResults,
            diagnostics, conductingEdges, groundingConnections);
    }
}
