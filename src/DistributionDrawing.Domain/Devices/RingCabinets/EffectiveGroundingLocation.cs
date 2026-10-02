namespace DistributionDrawing.Domain.Devices.RingCabinets;

public sealed record EffectiveGroundingLocation(
    Guid CabinetId,
    Guid IntervalId,
    Guid CircuitNodeId,
    Guid? CableTerminalId);
