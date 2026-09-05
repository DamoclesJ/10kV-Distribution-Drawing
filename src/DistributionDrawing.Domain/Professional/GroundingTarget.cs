namespace DistributionDrawing.Domain.Professional;

public enum GroundingTargetKind
{
    Terminal,
    GroundingAccessPoint
}

public sealed record GroundingTarget
{
    public GroundingTarget(GroundingTargetKind kind, Guid targetId)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (targetId == Guid.Empty)
        {
            throw new ArgumentException("Grounding target ID cannot be empty.", nameof(targetId));
        }

        Kind = kind;
        TargetId = targetId;
    }

    public GroundingTargetKind Kind { get; }

    public Guid TargetId { get; }

    public static GroundingTarget ForTerminal(Guid terminalId) =>
        new(GroundingTargetKind.Terminal, terminalId);

    public static GroundingTarget ForGroundingAccessPoint(Guid groundingAccessPointId) =>
        new(GroundingTargetKind.GroundingAccessPoint, groundingAccessPointId);
}
