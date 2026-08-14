using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.Topology;

public sealed class CableSplitResult
{
    public CableSplitResult(
        CableSegment originalCableSegment,
        Connection originalConnection,
        IntermediateTerminalCreationResult intermediateTerminal,
        CableSegment firstCableSegment,
        Connection firstConnection,
        CableSegment secondCableSegment,
        Connection secondConnection)
    {
        OriginalCableSegment = originalCableSegment
            ?? throw new ArgumentNullException(nameof(originalCableSegment));
        OriginalConnection = originalConnection
            ?? throw new ArgumentNullException(nameof(originalConnection));
        IntermediateTerminal = intermediateTerminal
            ?? throw new ArgumentNullException(nameof(intermediateTerminal));
        FirstCableSegment = firstCableSegment
            ?? throw new ArgumentNullException(nameof(firstCableSegment));
        FirstConnection = firstConnection
            ?? throw new ArgumentNullException(nameof(firstConnection));
        SecondCableSegment = secondCableSegment
            ?? throw new ArgumentNullException(nameof(secondCableSegment));
        SecondConnection = secondConnection
            ?? throw new ArgumentNullException(nameof(secondConnection));
    }

    public CableSegment OriginalCableSegment { get; }

    public Connection OriginalConnection { get; }

    public IntermediateTerminalCreationResult IntermediateTerminal { get; }

    public CableSegment FirstCableSegment { get; }

    public Connection FirstConnection { get; }

    public CableSegment SecondCableSegment { get; }

    public Connection SecondConnection { get; }
}
