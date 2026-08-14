using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.Topology;

public sealed class IntermediateTerminalCreationResult
{
    public IntermediateTerminalCreationResult(
        IntermediateTerminal intermediateTerminal,
        Terminal terminal)
    {
        IntermediateTerminal = intermediateTerminal
            ?? throw new ArgumentNullException(nameof(intermediateTerminal));
        Terminal = terminal ?? throw new ArgumentNullException(nameof(terminal));
    }

    public IntermediateTerminal IntermediateTerminal { get; }

    public Terminal Terminal { get; }
}
