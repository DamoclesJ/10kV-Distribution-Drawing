using DistributionDrawing.Domain.Devices;

namespace DistributionDrawing.Domain.Devices.RingCabinets;

/// <summary>
/// Stable-ID input for rebuilding a RingCabinet aggregate from persistence.
/// This is intentionally separate from the JSON DTO contract.
/// </summary>
public sealed record RingCabinetRestoreDefinition(
    Guid CabinetId,
    string DisplayName,
    Guid MainBusNodeId,
    IReadOnlyList<RingCabinetIntervalRestoreDefinition> Intervals,
    string? LineName = null);

public sealed record RingCabinetIntervalRestoreDefinition(
    Guid IntervalId,
    Guid ParentCabinetId,
    int Sequence,
    int BayIndex,
    string DisplayName,
    IntervalKind IntervalKind,
    GroundingStructureKind? GroundingStructureKind,
    Guid? IntermediateNodeId,
    Guid CircuitNodeId,
    Guid EarthNodeId,
    Guid? CableTerminalId,
    Guid SwitchAssemblyId,
    IReadOnlyList<SwitchDeviceRestoreDefinition> Switches);

public sealed record SwitchDeviceRestoreDefinition(
    Guid Id,
    SwitchKind SwitchKind,
    SwitchInstallationType InstallationType,
    Guid FirstTerminalId,
    Guid SecondTerminalId,
    SwitchState SwitchState,
    string DisplayName,
    string VoltageLevel,
    string? DispatchNumber);
