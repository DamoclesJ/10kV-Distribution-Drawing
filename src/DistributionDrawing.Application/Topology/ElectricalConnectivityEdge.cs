namespace DistributionDrawing.Application.Topology;

public sealed class ElectricalConnectivityEdge
{
    public ElectricalConnectivityEdge(
        Guid firstTerminalId,
        Guid secondTerminalId,
        ElectricalConnectivityEdgeType type,
        Guid sourceId)
    {
        if (firstTerminalId == Guid.Empty || secondTerminalId == Guid.Empty)
        {
            throw new ArgumentException("Connectivity edge terminal IDs are required.");
        }

        if (firstTerminalId == secondTerminalId)
        {
            throw new ArgumentException(
                "A connectivity edge requires two different terminals.");
        }

        if (sourceId == Guid.Empty)
        {
            throw new ArgumentException("Connectivity edge source ID is required.");
        }

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        FirstTerminalId = firstTerminalId;
        SecondTerminalId = secondTerminalId;
        Type = type;
        SourceId = sourceId;
    }

    public Guid FirstTerminalId { get; }

    public Guid SecondTerminalId { get; }

    public ElectricalConnectivityEdgeType Type { get; }

    public Guid SourceId { get; }

    public bool Connects(Guid firstTerminalId, Guid secondTerminalId)
    {
        return (FirstTerminalId == firstTerminalId && SecondTerminalId == secondTerminalId) ||
            (FirstTerminalId == secondTerminalId && SecondTerminalId == firstTerminalId);
    }
}
