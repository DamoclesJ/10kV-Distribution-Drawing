namespace DistributionDrawing.Domain.Professional;

/// <summary>
/// Immutable value object identifying one end of a work scope.
/// </summary>
public sealed record BoundaryPoint
{
    public BoundaryPoint(Guid deviceId, Guid terminalId, string side)
    {
        if (deviceId == Guid.Empty)
        {
            throw new ArgumentException("Boundary device ID cannot be empty.", nameof(deviceId));
        }

        if (terminalId == Guid.Empty)
        {
            throw new ArgumentException("Boundary terminal ID cannot be empty.", nameof(terminalId));
        }

        if (string.IsNullOrWhiteSpace(side))
        {
            throw new ArgumentException("Boundary side is required.", nameof(side));
        }

        DeviceId = deviceId;
        TerminalId = terminalId;
        Side = side.Trim();
    }

    public Guid DeviceId { get; }

    public Guid TerminalId { get; }

    public string Side { get; }
}
