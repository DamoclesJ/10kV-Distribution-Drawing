namespace DistributionDrawing.Domain.Professional;

public enum GroundingAccessLineSide
{
    SmallerNumberSide,
    LargerNumberSide
}

public sealed class GroundingAccessPoint
{
    public GroundingAccessPoint(
        Guid groundingAccessPointId,
        Guid connectionId,
        Guid poleId,
        Guid adjacentPoleId,
        GroundingAccessLineSide lineSide)
    {
        if (groundingAccessPointId == Guid.Empty)
        {
            throw new ArgumentException(
                "Grounding access point ID cannot be empty.",
                nameof(groundingAccessPointId));
        }

        if (connectionId == Guid.Empty)
        {
            throw new ArgumentException("Connection ID cannot be empty.", nameof(connectionId));
        }

        if (poleId == Guid.Empty)
        {
            throw new ArgumentException("Pole ID cannot be empty.", nameof(poleId));
        }

        if (adjacentPoleId == Guid.Empty)
        {
            throw new ArgumentException("Adjacent pole ID cannot be empty.", nameof(adjacentPoleId));
        }

        if (poleId == adjacentPoleId)
        {
            throw new ArgumentException("A grounding access point requires a different adjacent pole.");
        }

        if (!Enum.IsDefined(lineSide))
        {
            throw new ArgumentOutOfRangeException(nameof(lineSide));
        }

        GroundingAccessPointId = groundingAccessPointId;
        ConnectionId = connectionId;
        PoleId = poleId;
        AdjacentPoleId = adjacentPoleId;
        LineSide = lineSide;
    }

    public Guid GroundingAccessPointId { get; }

    public Guid ConnectionId { get; }

    public Guid PoleId { get; }

    public Guid AdjacentPoleId { get; }

    public GroundingAccessLineSide LineSide { get; }
}
