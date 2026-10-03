namespace DistributionDrawing.Domain.Professional;

/// <summary>Immutable structural boundary; EA transition validation belongs to projection.</summary>
public sealed record WorkScopeBoundary
{
    public WorkScopeBoundary(Guid deviceId, BoundarySide side,
        Guid? terminalId = null, Guid? connectionId = null)
    {
        if (deviceId == Guid.Empty || terminalId == Guid.Empty || connectionId == Guid.Empty)
            throw new ArgumentException("Boundary identities cannot be empty.");
        if (!Enum.IsDefined(side) || side == BoundarySide.Unknown)
            throw new ArgumentOutOfRangeException(nameof(side));
        DeviceId = deviceId;
        Side = side;
        TerminalId = terminalId;
        ConnectionId = connectionId;
    }

    public Guid DeviceId { get; }
    public BoundarySide Side { get; }
    public Guid? TerminalId { get; }
    public Guid? ConnectionId { get; }
}
