using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.Topology;

public sealed class ElectricalConnectivityGraphBuilder
{
    public ElectricalConnectivityGraph Build(DrawingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        Guid[] terminalIds = document.Terminals
            .Select(terminal => terminal.Id)
            .ToArray();
        HashSet<Guid> terminalIdSet = terminalIds.ToHashSet();
        var edges = new List<ElectricalConnectivityEdge>();

        foreach (ElectricalNode node in document.ElectricalNodes)
        {
            Guid[] nodeTerminalIds = node.TerminalIds.ToArray();
            EnsureKnownTerminals(nodeTerminalIds, terminalIdSet, node.Id);

            for (int firstIndex = 0; firstIndex < nodeTerminalIds.Length; firstIndex++)
            {
                for (int secondIndex = firstIndex + 1;
                     secondIndex < nodeTerminalIds.Length;
                     secondIndex++)
                {
                    edges.Add(new ElectricalConnectivityEdge(
                        nodeTerminalIds[firstIndex],
                        nodeTerminalIds[secondIndex],
                        ElectricalConnectivityEdgeType.ElectricalNodeInternal,
                        node.Id));
                }
            }
        }

        foreach (Connection connection in document.Connections)
        {
            EnsureKnownTerminals(
                [connection.StartTerminalId, connection.EndTerminalId],
                terminalIdSet,
                connection.Id);
            edges.Add(new ElectricalConnectivityEdge(
                connection.StartTerminalId,
                connection.EndTerminalId,
                ElectricalConnectivityEdgeType.Connection,
                connection.Id));
        }

        foreach (SwitchDevice switchDevice in document.Devices.OfType<SwitchDevice>())
        {
            Guid[] switchTerminalIds = switchDevice.TerminalIds.ToArray();
            EnsureKnownTerminals(switchTerminalIds, terminalIdSet, switchDevice.Id);

            if (switchDevice.SwitchState == SwitchState.Closed)
            {
                if (switchTerminalIds.Length != 2)
                {
                    throw new InvalidOperationException(
                        $"Switch '{switchDevice.Id}' must have exactly two terminals.");
                }

                edges.Add(new ElectricalConnectivityEdge(
                    switchTerminalIds[0],
                    switchTerminalIds[1],
                    ElectricalConnectivityEdgeType.ClosedSwitch,
                    switchDevice.Id));
            }
        }

        return new ElectricalConnectivityGraph(terminalIds, edges);
    }

    private static void EnsureKnownTerminals(
        IEnumerable<Guid> terminalIds,
        IReadOnlySet<Guid> knownTerminalIds,
        Guid sourceId)
    {
        Guid[] missingTerminalIds = terminalIds
            .Where(terminalId => !knownTerminalIds.Contains(terminalId))
            .ToArray();
        if (missingTerminalIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"Connectivity source '{sourceId}' references an unknown terminal.");
        }
    }
}
