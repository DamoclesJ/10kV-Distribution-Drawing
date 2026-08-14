using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.Topology;

public sealed class CableReconnectResult
{
    public CableReconnectResult(
        CableSegment beforeCableSegment,
        Connection beforeConnection,
        CableSegment afterCableSegment,
        Connection afterConnection)
    {
        BeforeCableSegment = beforeCableSegment
            ?? throw new ArgumentNullException(nameof(beforeCableSegment));
        BeforeConnection = beforeConnection
            ?? throw new ArgumentNullException(nameof(beforeConnection));
        AfterCableSegment = afterCableSegment
            ?? throw new ArgumentNullException(nameof(afterCableSegment));
        AfterConnection = afterConnection
            ?? throw new ArgumentNullException(nameof(afterConnection));
    }

    public CableSegment BeforeCableSegment { get; }

    public Connection BeforeConnection { get; }

    public CableSegment AfterCableSegment { get; }

    public Connection AfterConnection { get; }
}
