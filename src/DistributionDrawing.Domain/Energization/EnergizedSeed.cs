namespace DistributionDrawing.Domain.Energization;

public sealed record EnergizedSeed
{
    public EnergizedSeed(Guid id, Guid boundaryDeviceId, EnergizationSide side)
    {
        if (id == Guid.Empty) throw new ArgumentException("Seed ID is required.", nameof(id));
        if (boundaryDeviceId == Guid.Empty)
            throw new ArgumentException("Boundary device ID is required.", nameof(boundaryDeviceId));
        if (!Enum.IsDefined(side)) throw new ArgumentOutOfRangeException(nameof(side));

        Id = id;
        BoundaryDeviceId = boundaryDeviceId;
        Side = side;
    }

    public Guid Id { get; }
    public Guid BoundaryDeviceId { get; }
    public EnergizationSide Side { get; }
}
