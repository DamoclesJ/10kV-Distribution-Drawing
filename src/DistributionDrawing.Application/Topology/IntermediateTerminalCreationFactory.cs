using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.Topology;

public sealed class IntermediateTerminalCreationFactory
{
    public IntermediateTerminalCreationResult Create(string displayName)
    {
        Guid intermediateTerminalId = Guid.NewGuid();
        Guid terminalId = Guid.NewGuid();
        var intermediateTerminal = new IntermediateTerminal(
            intermediateTerminalId,
            displayName,
            terminalId);
        var terminal = new Terminal(
            terminalId,
            TopologyOwnerType.IntermediateTerminal,
            intermediateTerminalId,
            "IntermediateTerminal",
            "10kV",
            isExternal: true,
            allowsMultipleConnections: true,
            allowedConnectionTypes: [ConnectionType.Cable]);

        return new IntermediateTerminalCreationResult(intermediateTerminal, terminal);
    }
}
