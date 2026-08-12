using DistributionDrawing.Domain.Professional;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Professional;

/// <summary>
/// Immutable command value for a BoundaryPoint. It deliberately contains
/// stable IDs and the user-confirmed side only; it does not retain Domain
/// object references.
/// </summary>
public sealed record BoundaryPointCommandValue(
    Guid DeviceId,
    Guid TerminalId,
    string Side)
{
    public static BoundaryPointCommandValue From(BoundaryPoint boundaryPoint)
    {
        ArgumentNullException.ThrowIfNull(boundaryPoint);
        return new BoundaryPointCommandValue(
            boundaryPoint.DeviceId,
            boundaryPoint.TerminalId,
            boundaryPoint.Side);
    }

    public BoundaryPoint ToDomain()
    {
        return new BoundaryPoint(DeviceId, TerminalId, Side);
    }
}
