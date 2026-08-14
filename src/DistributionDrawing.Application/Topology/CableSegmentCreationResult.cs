using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.Topology;

public sealed class CableSegmentCreationResult
{
    public CableSegmentCreationResult(
        CableSegment cableSegment,
        Connection connection)
    {
        CableSegment = cableSegment ?? throw new ArgumentNullException(nameof(cableSegment));
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public CableSegment CableSegment { get; }

    public Connection Connection { get; }
}
