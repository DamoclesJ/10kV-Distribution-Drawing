namespace DistributionDrawing.Domain.Professional;

public enum GroundingAccessLineSide
{
    SmallerNumberSide,
    LargerNumberSide,
    TransformerSide
}

public enum GroundingAccessPlacementSide
{
    PoleSide,
    AdjacentEndpointSide
}

public enum GroundingAdjacentEndpointKind
{
    Pole,
    Terminal
}

public sealed record GroundingAdjacentEndpoint
{
    public GroundingAdjacentEndpoint(GroundingAdjacentEndpointKind kind, Guid targetId)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (targetId == Guid.Empty)
        {
            throw new ArgumentException("Adjacent endpoint target ID cannot be empty.", nameof(targetId));
        }

        Kind = kind;
        TargetId = targetId;
    }

    public GroundingAdjacentEndpointKind Kind { get; }

    public Guid TargetId { get; }

    public static GroundingAdjacentEndpoint ForPole(Guid poleId) =>
        new(GroundingAdjacentEndpointKind.Pole, poleId);

    public static GroundingAdjacentEndpoint ForTerminal(Guid terminalId) =>
        new(GroundingAdjacentEndpointKind.Terminal, terminalId);
}

public sealed class GroundingAccessPoint
{
    public GroundingAccessPoint(
        Guid groundingAccessPointId,
        Guid connectionId,
        Guid poleId,
        Guid adjacentPoleId,
        GroundingAccessLineSide lineSide)
        : this(
            groundingAccessPointId,
            connectionId,
            poleId,
            GroundingAdjacentEndpoint.ForPole(adjacentPoleId),
            lineSide,
            GroundingAccessPlacementSide.PoleSide)
    {
    }

    public GroundingAccessPoint(
        Guid groundingAccessPointId,
        Guid connectionId,
        Guid poleId,
        GroundingAdjacentEndpoint adjacentEndpoint,
        GroundingAccessLineSide lineSide,
        GroundingAccessPlacementSide placementSide = GroundingAccessPlacementSide.PoleSide)
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

        ArgumentNullException.ThrowIfNull(adjacentEndpoint);
        if (adjacentEndpoint.Kind == GroundingAdjacentEndpointKind.Pole &&
            poleId == adjacentEndpoint.TargetId)
        {
            throw new ArgumentException("A grounding access point requires a different adjacent pole.");
        }

        if (!Enum.IsDefined(lineSide))
        {
            throw new ArgumentOutOfRangeException(nameof(lineSide));
        }
        if (!Enum.IsDefined(placementSide))
        {
            throw new ArgumentOutOfRangeException(nameof(placementSide));
        }
        if (adjacentEndpoint.Kind == GroundingAdjacentEndpointKind.Pole &&
            (lineSide == GroundingAccessLineSide.TransformerSide ||
             placementSide != GroundingAccessPlacementSide.PoleSide))
        {
            throw new ArgumentException(
                "A pole adjacent endpoint requires a numbered line side and pole-side placement.");
        }
        if (adjacentEndpoint.Kind == GroundingAdjacentEndpointKind.Terminal &&
            lineSide != GroundingAccessLineSide.TransformerSide)
        {
            throw new ArgumentException(
                "A terminal adjacent endpoint requires the transformer line side.");
        }

        GroundingAccessPointId = groundingAccessPointId;
        ConnectionId = connectionId;
        PoleId = poleId;
        AdjacentEndpoint = adjacentEndpoint;
        LineSide = lineSide;
        PlacementSide = placementSide;
    }

    public Guid GroundingAccessPointId { get; }

    public Guid ConnectionId { get; }

    public Guid PoleId { get; }

    public GroundingAdjacentEndpoint AdjacentEndpoint { get; }

    public Guid AdjacentPoleId => AdjacentEndpoint.Kind == GroundingAdjacentEndpointKind.Pole
        ? AdjacentEndpoint.TargetId
        : throw new InvalidOperationException("This grounding access point has a terminal adjacent endpoint.");

    public GroundingAccessLineSide LineSide { get; }

    public GroundingAccessPlacementSide PlacementSide { get; }
}
