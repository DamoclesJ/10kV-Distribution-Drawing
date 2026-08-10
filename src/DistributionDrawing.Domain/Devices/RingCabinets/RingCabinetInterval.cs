using DistributionDrawing.Domain.Devices;

namespace DistributionDrawing.Domain.Devices.RingCabinets;

public sealed class RingCabinetInterval
{
    private readonly IReadOnlyList<SwitchDevice> _switchDevices;

    internal RingCabinetInterval(
        Guid id,
        Guid parentCabinetId,
        int sequence,
        string displayName,
        IntervalKind intervalKind,
        IEnumerable<SwitchDevice> switchDevices,
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

        SwitchDevice[] devices = switchDevices?.ToArray()
            ?? throw new ArgumentNullException(nameof(switchDevices));

        if (intervalKind != IntervalKind.LoadSwitchInterval)
        {
            throw new NotSupportedException(
                "Only normal load-switch intervals are implemented in M1.2-A.");
        }

        if (devices.Length != 2 ||
            devices.Count(device => device.SwitchKind == SwitchKind.LoadSwitch) != 1 ||
            devices.Count(device => device.SwitchKind == SwitchKind.GroundSwitch) != 1)
        {
            throw new ArgumentException(
                "A load-switch interval requires one load switch and one ground switch.",
                nameof(switchDevices));
        }

        IntervalId = id;
        ParentCabinetId = parentCabinetId;
        Sequence = sequence;
        DisplayName = displayName.Trim();
        IntervalKind = intervalKind;
        _switchDevices = Array.AsReadOnly(devices);
        CircuitNodeId = circuitNodeId;
        EarthNodeId = earthNodeId;
        ExternalTerminalId = externalTerminalId;
    }

    public Guid IntervalId { get; }

    public Guid ParentCabinetId { get; }

    public int Sequence { get; }

    public string DisplayName { get; }

    public IntervalKind IntervalKind { get; }

    public IReadOnlyList<SwitchDevice> SwitchDevices => _switchDevices;

    public Guid CircuitNodeId { get; }

    public Guid EarthNodeId { get; }

    public Guid ExternalTerminalId { get; }
}
