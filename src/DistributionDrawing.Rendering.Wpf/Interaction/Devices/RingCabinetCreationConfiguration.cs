using DistributionDrawing.Domain.Devices.RingCabinets;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class RingCabinetCreationConfiguration
{
    private readonly IReadOnlyList<RingCabinetIntervalCreationConfiguration> _intervals;

    public RingCabinetCreationConfiguration(
        string displayName,
        IEnumerable<RingCabinetIntervalCreationConfiguration> intervals)
    {
        DisplayName = displayName;
        RingCabinetIntervalCreationConfiguration[] values = intervals?.ToArray()
            ?? throw new ArgumentNullException(nameof(intervals));
        _intervals = Array.AsReadOnly(values);
    }

    public string DisplayName { get; }

    public IReadOnlyList<RingCabinetIntervalCreationConfiguration> Intervals => _intervals;
}

public sealed record RingCabinetIntervalCreationConfiguration(
    string DisplayName,
    IntervalKind IntervalKind,
    GroundingStructureKind? GroundingStructureKind);
