using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.SwitchAssemblies;

namespace DistributionDrawing.Domain.Devices.RingCabinets;

public sealed class RingCabinetInterval
{
    private readonly IReadOnlyList<SwitchDevice> _switchDevices;

    internal RingCabinetInterval(
        Guid id,
        Guid parentCabinetId,
        int sequence,
        int bayIndex,
        string displayName,
        IntervalKind intervalKind,
        IEnumerable<SwitchDevice> switchDevices,
        SwitchAssembly switchAssembly,
        GroundingStructureKind? groundingStructureKind,
        Guid? intermediateNodeId,
        Guid circuitNodeId,
        Guid earthNodeId,
        Guid externalTerminalId)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Interval ID cannot be empty.", nameof(id));
        }

        if (parentCabinetId == Guid.Empty)
        {
            throw new ArgumentException("Parent cabinet ID cannot be empty.", nameof(parentCabinetId));
        }

        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Sequence must be positive.");
        }

        if (bayIndex < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(bayIndex), "Bay index must be positive.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Interval display name is required.", nameof(displayName));
        }

        if (circuitNodeId == Guid.Empty)
        {
            throw new ArgumentException("Circuit node ID cannot be empty.", nameof(circuitNodeId));
        }

        if (earthNodeId == Guid.Empty)
        {
            throw new ArgumentException("Earth node ID cannot be empty.", nameof(earthNodeId));
        }

        if (externalTerminalId == Guid.Empty)
        {
            throw new ArgumentException(
                "External terminal ID cannot be empty.",
                nameof(externalTerminalId));
        }

        if (intermediateNodeId == Guid.Empty)
        {
            throw new ArgumentException(
                "Intermediate node ID cannot be empty when specified.",
                nameof(intermediateNodeId));
        }

        SwitchDevice[] devices = switchDevices?.ToArray()
            ?? throw new ArgumentNullException(nameof(switchDevices));
        ArgumentNullException.ThrowIfNull(switchAssembly);

        switch (intervalKind)
        {
            case IntervalKind.LoadSwitchInterval:
                if (groundingStructureKind is not null)
                {
                    throw new ArgumentException(
                        "A load-switch interval cannot have a grounding structure.",
                        nameof(groundingStructureKind));
                }

                if (intermediateNodeId is not null)
                {
                    throw new ArgumentException(
                        "A load-switch interval cannot have an intermediate node.",
                        nameof(intermediateNodeId));
                }

                EnsureSwitchStructure(
                    devices,
                    switchAssembly,
                    id,
                    SwitchAssemblyType.LoadSwitchThreePosition,
                    [SwitchKind.LoadSwitch, SwitchKind.GroundSwitch]);
                break;

            case IntervalKind.IntegratedFeederInterval:
                if (groundingStructureKind is not GroundingStructureKind structureKind)
                {
                    throw new ArgumentException(
                        "An integrated-feeder interval requires a grounding structure.",
                        nameof(groundingStructureKind));
                }

                if (!Enum.IsDefined(structureKind))
                {
                    throw new ArgumentOutOfRangeException(nameof(groundingStructureKind));
                }

                if (intermediateNodeId is null)
                {
                    throw new ArgumentException(
                        "An integrated-feeder interval requires an intermediate node.",
                        nameof(intermediateNodeId));
                }

                EnsureSwitchStructure(
                    devices,
                    switchAssembly,
                    id,
                    SwitchAssemblyType.IntegratedFeeder,
                    [
                        SwitchKind.IsolationSwitch,
                        SwitchKind.CircuitBreaker,
                        SwitchKind.GroundSwitch
                    ]);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(intervalKind));
        }

        IntervalId = id;
        ParentCabinetId = parentCabinetId;
        Sequence = sequence;
        BayIndex = bayIndex;
        DisplayName = displayName.Trim();
        IntervalKind = intervalKind;
        _switchDevices = Array.AsReadOnly(devices);
        SwitchAssembly = switchAssembly;
        GroundingStructureKind = groundingStructureKind;
        IntermediateNodeId = intermediateNodeId;
        CircuitNodeId = circuitNodeId;
        EarthNodeId = earthNodeId;
        ExternalTerminalId = externalTerminalId;
    }

    public Guid IntervalId { get; }

    public Guid ParentCabinetId { get; }

    public int Sequence { get; }

    public int BayIndex { get; }

    public string DisplayName { get; }

    public IntervalKind IntervalKind { get; }

    public IReadOnlyList<SwitchDevice> SwitchDevices => _switchDevices;

    public SwitchAssembly SwitchAssembly { get; }

    public GroundingStructureKind? GroundingStructureKind { get; }

    public Guid? IntermediateNodeId { get; }

    public Guid CircuitNodeId { get; }

    public Guid EarthNodeId { get; }

    public Guid ExternalTerminalId { get; }

    private static void EnsureSwitchStructure(
        IReadOnlyCollection<SwitchDevice> devices,
        SwitchAssembly switchAssembly,
        Guid intervalId,
        SwitchAssemblyType expectedAssemblyType,
        IReadOnlyCollection<SwitchKind> expectedSwitchKinds)
    {
        if (devices.Count != expectedSwitchKinds.Count ||
            expectedSwitchKinds.Any(expectedKind =>
                devices.Count(device => device.SwitchKind == expectedKind) != 1))
        {
            throw new ArgumentException(
                "The interval does not contain the required switch devices.",
                nameof(devices));
        }

        if (switchAssembly.ParentIntervalId != intervalId ||
            switchAssembly.AssemblyType != expectedAssemblyType ||
            !switchAssembly.MemberSwitchIds.ToHashSet().SetEquals(
                devices.Select(device => device.Id)))
        {
            throw new ArgumentException(
                "The switch assembly must contain exactly the switches owned by this interval.",
                nameof(switchAssembly));
        }
    }
}
