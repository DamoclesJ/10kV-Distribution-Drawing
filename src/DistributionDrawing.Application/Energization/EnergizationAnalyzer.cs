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
        return Analyze(drawing, scenario, CurrentSwitchStateView.Instance);
    }

    public EnergizationResult Analyze(
        DrawingDocument drawing,
        EnergizationScenario scenario,
        ISwitchStateView switchStateView)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(switchStateView);

        var diagnostics = new List<EnergizationDiagnostic>();
        if (scenario.Seeds.Count == 0)
            return Failure(EnergizationValidity.NoSeeds,
                [new EnergizationDiagnostic(EnergizationDiagnosticCode.EmptyScenario)]);

        ElectricalConnectivityGraph? graph;
        try
        {
            ValidateTopology(drawing);
            graph = _graphBuilder.Build(drawing, switchStateView);
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        {
            diagnostics.Add(new EnergizationDiagnostic(
                EnergizationDiagnosticCode.InvalidTopology, Detail: error.Message));
            return Failure(EnergizationValidity.Failed, diagnostics);
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
                switchStateView.GetSwitchState(device) == SwitchState.Closed)
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
        foreach (EnergizedSeed seed in scenario.Seeds)
        {
            if (!_boundaryPolicy.TryResolve(drawing, seed, out Guid terminalId,
                out EnergizationDiagnosticCode issue))
            {
                diagnostics.Add(new EnergizationDiagnostic(issue, seed.Id));
                continue;
            }

            if (sources[terminalId].Add(seed.Id)) pending.Enqueue((terminalId, seed.Id));
        }

        // A declared source cannot be silently discarded or produce partial states.
        if (diagnostics.Count > 0) return Failure(EnergizationValidity.Failed, diagnostics);

        while (pending.TryDequeue(out (Guid terminalId, Guid seedId) current))
        {
            foreach (Guid adjacentId in adjacency[current.terminalId])
                if (sources[adjacentId].Add(current.seedId))
                    pending.Enqueue((adjacentId, current.seedId));
        }

        return CreateResult(drawing, sources, conductingEdges, groundingConnections,
            diagnostics);
    }

    private static EnergizationResult Failure(EnergizationValidity validity,
        IEnumerable<EnergizationDiagnostic> diagnostics) =>
        new(validity, new Dictionary<Guid, EnergizationPointResult>(),
            new Dictionary<Guid, EnergizationPointResult>(), diagnostics, [], []);

    private static void ValidateTopology(DrawingDocument drawing)
    {
        Dictionary<Guid, Terminal> terminals = drawing.Terminals.ToDictionary(item => item.Id);
        Dictionary<Guid, ElectricalNode> nodes = drawing.ElectricalNodes.ToDictionary(item => item.Id);
        foreach (Terminal terminal in terminals.Values)
            if (terminal.ElectricalNodeId is Guid nodeId &&
                (!nodes.TryGetValue(nodeId, out ElectricalNode? node) ||
                    !node.TerminalIds.Contains(terminal.Id)))
                throw new InvalidOperationException($"Terminal '{terminal.Id}' has an inconsistent electrical node.");
        foreach (ElectricalNode node in nodes.Values)
            foreach (Guid terminalId in node.TerminalIds)
                if (!terminals.TryGetValue(terminalId, out Terminal? terminal) ||
                    terminal.ElectricalNodeId != node.Id)
                    throw new InvalidOperationException($"Node '{node.Id}' has an inconsistent terminal membership.");
        foreach (CableTermination termination in drawing.Devices.OfType<CableTermination>())
            if (!nodes.TryGetValue(termination.InternalNodeId, out ElectricalNode? node) ||
                node.OwnerId != termination.Id ||
                !terminals.TryGetValue(termination.CableSideTerminalId, out Terminal? cable) ||
                cable.ElectricalNodeId != node.Id ||
                !terminals.ContainsKey(termination.OverheadSideTerminalId))
                throw new InvalidOperationException($"Cable termination '{termination.Id}' is missing its required internal path.");
    }

    private static EnergizationResult CreateResult(
        DrawingDocument drawing,
        IReadOnlyDictionary<Guid, HashSet<Guid>> sources,
        IEnumerable<ElectricalConnectivityEdge> conductingEdges,
        IEnumerable<GroundingSwitchConnection> groundingConnections,
        IEnumerable<EnergizationDiagnostic> diagnostics)
    {
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
                    EnergizationState.Deenergized,
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
                    node.Type != ElectricalNodeType.Earth && sourceIds.Length > 0
                        ? EnergizationState.Energized : EnergizationState.Deenergized,
                    sourceIds);
            });
        return new EnergizationResult(EnergizationValidity.Complete, terminalResults, nodeResults,
            diagnostics, conductingEdges, groundingConnections);
    }
}
