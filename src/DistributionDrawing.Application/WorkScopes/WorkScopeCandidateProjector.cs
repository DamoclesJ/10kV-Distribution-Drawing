using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.WorkScopes;

/// <summary>Projects the current complete EA result into transient work-scope regions.</summary>
public sealed class WorkScopeCandidateProjector
{
    private readonly ElectricalConnectivityGraphBuilder _graphBuilder = new();

    public WorkScopeCandidateProjection Project(
        DrawingDocument drawing,
        EnergizationAnalysisState analysisState)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        ArgumentNullException.ThrowIfNull(analysisState);

        EnergizationResult? result = analysisState.CurrentResult;
        if (result is null)
        {
            WorkScopeCandidateDiagnosticCode code = analysisState.Freshness switch
            {
                EnergizationFreshness.Stale => WorkScopeCandidateDiagnosticCode.StaleAnalysis,
                _ when analysisState.LatestResult?.Validity == EnergizationValidity.NoSeeds =>
                    WorkScopeCandidateDiagnosticCode.NoSeeds,
                _ when analysisState.LatestResult?.Validity == EnergizationValidity.Failed =>
                    WorkScopeCandidateDiagnosticCode.FailedAnalysis,
                _ => WorkScopeCandidateDiagnosticCode.CurrentResultUnavailable
            };
            return Invalid(code);
        }

        ElectricalConnectivityGraph graph;
        try
        {
            graph = _graphBuilder.Build(drawing);
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        {
            return Invalid(WorkScopeCandidateDiagnosticCode.IdentityMismatch, detail: error.Message);
        }

        if (!HasConsistentIdentities(drawing, graph, result, out string? identityError))
            return Invalid(WorkScopeCandidateDiagnosticCode.IdentityMismatch, detail: identityError);

        HashSet<Guid> earthNodeIds = drawing.ElectricalNodes
            .Where(node => node.Type == ElectricalNodeType.Earth)
            .Select(node => node.Id)
            .ToHashSet();
        HashSet<Guid> earthTerminalIds = drawing.Terminals
            .Where(terminal => terminal.ElectricalNodeId is Guid nodeId && earthNodeIds.Contains(nodeId))
            .Select(terminal => terminal.Id)
            .Concat(result.GroundingSwitchConnections.Select(connection => connection.EarthSideTerminalId))
            .ToHashSet();

        HashSet<Guid> deenergizedTerminalIds = result.Terminals
            .Where(pair => pair.Value.State == EnergizationState.Deenergized &&
                !earthTerminalIds.Contains(pair.Key))
            .Select(pair => pair.Key)
            .ToHashSet();

        HashSet<Guid> deenergizedNodeIds = result.Nodes
            .Where(pair => pair.Value.State == EnergizationState.Deenergized &&
                !earthNodeIds.Contains(pair.Key))
            .Select(pair => pair.Key)
            .ToHashSet();

        var adjacency = deenergizedTerminalIds.ToDictionary(id => id, _ => new SortedSet<Guid>());
        var diagnostics = new List<WorkScopeCandidateDiagnostic>();
        foreach (ElectricalConnectivityEdge edge in result.ConductingEdges)
        {
            bool firstEarth = earthTerminalIds.Contains(edge.FirstTerminalId);
            bool secondEarth = earthTerminalIds.Contains(edge.SecondTerminalId);
            if (firstEarth || secondEarth) continue;

            bool firstDeenergized = deenergizedTerminalIds.Contains(edge.FirstTerminalId);
            bool secondDeenergized = deenergizedTerminalIds.Contains(edge.SecondTerminalId);
            bool firstEnergized = result.Terminals[edge.FirstTerminalId].State == EnergizationState.Energized;
            bool secondEnergized = result.Terminals[edge.SecondTerminalId].State == EnergizationState.Energized;
            if (firstDeenergized && secondEnergized || secondDeenergized && firstEnergized)
            {
                diagnostics.Add(new WorkScopeCandidateDiagnostic(
                    WorkScopeCandidateDiagnosticCode.ConductingEdgeStateMismatch, edge.SourceId));
                continue;
            }

            if (firstDeenergized && secondDeenergized)
            {
                adjacency[edge.FirstTerminalId].Add(edge.SecondTerminalId);
                adjacency[edge.SecondTerminalId].Add(edge.FirstTerminalId);
            }
        }

        if (diagnostics.Count > 0)
            return Invalid(diagnostics);

        List<HashSet<Guid>> components = FindComponents(adjacency);
        Dictionary<Guid, Terminal> terminals = drawing.Terminals.ToDictionary(item => item.Id);
        var regions = components.Select(component =>
        {
            Guid[] nodeIds = component.Select(id => terminals[id].ElectricalNodeId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Where(deenergizedNodeIds.Contains)
                .Distinct()
                .Order()
                .ToArray();
            return new WorkScopeCandidateRegion(component, nodeIds);
        }).ToList();

        WorkScopeCandidateBoundary[] boundaries = FindBoundaries(
            drawing, result, deenergizedTerminalIds, earthTerminalIds, diagnostics);
        if (diagnostics.Any(item => item.Code is WorkScopeCandidateDiagnosticCode.IdentityMismatch or
                WorkScopeCandidateDiagnosticCode.BoundaryTransitionAmbiguous or
                WorkScopeCandidateDiagnosticCode.UnsupportedStructure))
            return Invalid(diagnostics);

        regions = regions
            .OrderBy(region => region.TerminalIds, GuidListComparer.Instance)
            .ThenBy(region => region.ElectricalNodeIds, GuidListComparer.Instance)
            .ToList();

        if (regions.Count == 0)
            diagnostics.Add(new WorkScopeCandidateDiagnostic(WorkScopeCandidateDiagnosticCode.EmptyCandidate));

        WorkScopeCandidate candidate = new(regions, boundaries, SortDiagnostics(diagnostics));
        return new WorkScopeCandidateProjection(candidate, candidate.Diagnostics);
    }

    private static bool HasConsistentIdentities(
        DrawingDocument drawing,
        ElectricalConnectivityGraph graph,
        EnergizationResult result,
        out string? error)
    {
        error = null;
        HashSet<Guid> terminalIds = drawing.Terminals.Select(item => item.Id).ToHashSet();
        HashSet<Guid> nodeIds = drawing.ElectricalNodes.Select(item => item.Id).ToHashSet();
        if (!terminalIds.SetEquals(result.Terminals.Keys) || !nodeIds.SetEquals(result.Nodes.Keys))
        {
            error = "EA point identities do not match the current drawing.";
            return false;
        }

        Dictionary<Guid, Terminal> terminals = drawing.Terminals.ToDictionary(item => item.Id);
        Dictionary<Guid, ElectricalNode> nodes = drawing.ElectricalNodes.ToDictionary(item => item.Id);
        foreach (Terminal terminal in terminals.Values)
        {
            if (terminal.ElectricalNodeId is not Guid nodeId) continue;
            if (!nodes.TryGetValue(nodeId, out ElectricalNode? node) || !node.TerminalIds.Contains(terminal.Id))
            {
                error = $"Terminal '{terminal.Id}' has inconsistent electrical-node identity.";
                return false;
            }
            if (result.Terminals[terminal.Id].State != result.Nodes[nodeId].State)
            {
                error = $"Terminal '{terminal.Id}' and node '{nodeId}' have inconsistent EA states.";
                return false;
            }
        }

        foreach (ElectricalNode node in nodes.Values)
        foreach (Guid terminalId in node.TerminalIds)
        {
            if (!terminals.TryGetValue(terminalId, out Terminal? terminal) || terminal.ElectricalNodeId != node.Id)
            {
                error = $"Node '{node.Id}' has inconsistent terminal identity.";
                return false;
            }
        }

        HashSet<Guid> earthTerminalIds = terminals.Values
            .Where(terminal => terminal.ElectricalNodeId is Guid id && nodes[id].Type == ElectricalNodeType.Earth)
            .Select(terminal => terminal.Id)
            .ToHashSet();
        HashSet<Guid> groundSwitchIds = drawing.Devices.OfType<SwitchDevice>()
            .Where(device => device.SwitchKind == SwitchKind.GroundSwitch)
            .Select(device => device.Id)
            .ToHashSet();
        static string EdgeKey(ElectricalConnectivityEdge edge) =>
            $"{(edge.FirstTerminalId.CompareTo(edge.SecondTerminalId) < 0 ? edge.FirstTerminalId : edge.SecondTerminalId):N}:" +
            $"{(edge.FirstTerminalId.CompareTo(edge.SecondTerminalId) < 0 ? edge.SecondTerminalId : edge.FirstTerminalId):N}:" +
            $"{edge.Type}:{edge.SourceId:N}";
        HashSet<string> expectedEdges = graph.Edges
            .Where(edge => !earthTerminalIds.Contains(edge.FirstTerminalId) &&
                !earthTerminalIds.Contains(edge.SecondTerminalId) &&
                !(edge.Type == ElectricalConnectivityEdgeType.ClosedSwitch && groundSwitchIds.Contains(edge.SourceId)))
            .Select(EdgeKey)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> resultEdges = result.ConductingEdges.Select(EdgeKey).ToHashSet(StringComparer.Ordinal);
        if (expectedEdges.Count != graph.Edges.Count(edge =>
                !earthTerminalIds.Contains(edge.FirstTerminalId) &&
                !earthTerminalIds.Contains(edge.SecondTerminalId) &&
                !(edge.Type == ElectricalConnectivityEdgeType.ClosedSwitch && groundSwitchIds.Contains(edge.SourceId))) ||
            resultEdges.Count != result.ConductingEdges.Count || !expectedEdges.SetEquals(resultEdges))
        {
            error = "EA conducting edges do not match current electrical topology.";
            return false;
        }

        if (result.Terminals.Values.Any(point => !Enum.IsDefined(point.State) ||
                point.State == EnergizationState.Unknown) ||
            result.Nodes.Values.Any(point => !Enum.IsDefined(point.State) ||
                point.State == EnergizationState.Unknown))
        {
            error = "EA result contains incomplete point states.";
            return false;
        }

        return true;
    }

    private static List<HashSet<Guid>> FindComponents(
        IReadOnlyDictionary<Guid, SortedSet<Guid>> adjacency)
    {
        var remaining = adjacency.Keys.ToHashSet();
        var components = new List<HashSet<Guid>>();
        foreach (Guid start in adjacency.Keys.Order())
        {
            if (!remaining.Remove(start)) continue;
            var component = new HashSet<Guid> { start };
            var queue = new Queue<Guid>();
            queue.Enqueue(start);
            while (queue.TryDequeue(out Guid current))
            foreach (Guid next in adjacency[current])
                if (remaining.Remove(next))
                {
                    component.Add(next);
                    queue.Enqueue(next);
                }
            components.Add(component);
        }
        return components;
    }

    private static WorkScopeCandidateBoundary[] FindBoundaries(
        DrawingDocument drawing,
        EnergizationResult result,
        IReadOnlySet<Guid> deenergizedTerminalIds,
        IReadOnlySet<Guid> earthTerminalIds,
        ICollection<WorkScopeCandidateDiagnostic> diagnostics)
    {
        var boundaries = new Dictionary<(Guid SwitchId, Guid Deenergized, Guid Energized), WorkScopeCandidateBoundary>();
        HashSet<Guid> terminalIds = drawing.Terminals.Select(item => item.Id).ToHashSet();
        foreach (SwitchDevice device in drawing.Devices.OfType<SwitchDevice>().OrderBy(item => item.Id))
        {
            if (device.SwitchKind == SwitchKind.GroundSwitch) continue;
            if (device.TerminalIds.Count != 2 || device.TerminalIds.Any(id => !terminalIds.Contains(id)))
            {
                diagnostics.Add(new WorkScopeCandidateDiagnostic(
                    WorkScopeCandidateDiagnosticCode.UnsupportedStructure, device.Id));
                continue;
            }

            Guid first = device.FirstTerminalId;
            Guid second = device.SecondTerminalId;
            if (earthTerminalIds.Contains(first) || earthTerminalIds.Contains(second)) continue;
            EnergizationState firstState = result.Terminals[first].State;
            EnergizationState secondState = result.Terminals[second].State;
            if (firstState == secondState) continue;
            if (firstState == EnergizationState.Unknown || secondState == EnergizationState.Unknown)
            {
                diagnostics.Add(new WorkScopeCandidateDiagnostic(
                    WorkScopeCandidateDiagnosticCode.BoundaryTransitionAmbiguous, device.Id));
                continue;
            }

            Guid deenergized = firstState == EnergizationState.Deenergized ? first : second;
            Guid energized = firstState == EnergizationState.Energized ? first : second;
            if (!deenergizedTerminalIds.Contains(deenergized)) continue;
            if (device.SwitchState != SwitchState.Open || result.ConductingEdges.Any(edge =>
                    edge.Type == ElectricalConnectivityEdgeType.ClosedSwitch && edge.SourceId == device.Id))
            {
                diagnostics.Add(new WorkScopeCandidateDiagnostic(
                    WorkScopeCandidateDiagnosticCode.ConductingEdgeStateMismatch, device.Id));
                continue;
            }

            Guid[] relatedConnections = drawing.Connections
                .Where(connection => device.TerminalIds.Any(connection.UsesTerminal))
                .Select(connection => connection.Id)
                .Order()
                .ToArray();
            Guid[] attachedPoles = drawing.PoleAttachments
                .Where(attachment => attachment.AttachedDeviceId == device.Id)
                .Select(attachment => attachment.PoleId)
                .Distinct()
                .Order()
                .ToArray();
            if (attachedPoles.Length > 1)
            {
                diagnostics.Add(new WorkScopeCandidateDiagnostic(
                    WorkScopeCandidateDiagnosticCode.BoundaryTransitionAmbiguous, device.Id,
                    "Switch is attached to multiple poles."));
                continue;
            }

            boundaries[(device.Id, deenergized, energized)] = new WorkScopeCandidateBoundary(
                device.Id, device.SwitchKind, device.InstallationType,
                deenergized, energized, device.ParentId,
                attachedPoles.Length == 1 ? attachedPoles[0] : null, relatedConnections);
        }
        return boundaries.Values
            .OrderBy(item => item.SwitchDeviceId)
            .ThenBy(item => item.DeenergizedTerminalId)
            .ThenBy(item => item.EnergizedTerminalId)
            .ToArray();
    }

    private static WorkScopeCandidateProjection Invalid(WorkScopeCandidateDiagnosticCode code, Guid? id = null, string? detail = null) =>
        Invalid([new WorkScopeCandidateDiagnostic(code, id, detail)]);

    private static WorkScopeCandidateProjection Invalid(IEnumerable<WorkScopeCandidateDiagnostic> diagnostics)
    {
        WorkScopeCandidateDiagnostic[] sorted = SortDiagnostics(diagnostics);
        return new WorkScopeCandidateProjection(null, sorted);
    }

    private static WorkScopeCandidateDiagnostic[] SortDiagnostics(IEnumerable<WorkScopeCandidateDiagnostic> diagnostics) =>
        diagnostics.OrderBy(item => item.Code).ThenBy(item => item.Identity).ThenBy(item => item.Detail, StringComparer.Ordinal).ToArray();

    private sealed class GuidListComparer : IComparer<IReadOnlyList<Guid>>
    {
        public static GuidListComparer Instance { get; } = new();
        public int Compare(IReadOnlyList<Guid>? x, IReadOnlyList<Guid>? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            for (int i = 0; i < Math.Min(x.Count, y.Count); i++)
            {
                int comparison = x[i].CompareTo(y[i]);
                if (comparison != 0) return comparison;
            }
            return x.Count.CompareTo(y.Count);
        }
    }
}
