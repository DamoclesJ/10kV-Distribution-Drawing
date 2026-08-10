namespace DistributionDrawing.Domain.Devices.RingCabinets;

public sealed class RingCabinetDefinition
{
    private readonly IReadOnlyList<RingCabinetIntervalDefinition> _intervalDefinitions;

    private RingCabinetDefinition(
        Guid cabinetId,
        string displayName,
        IEnumerable<RingCabinetIntervalDefinition> intervalDefinitions)
    {
        if (cabinetId == Guid.Empty)
        {
            throw new ArgumentException("Cabinet ID cannot be empty.", nameof(cabinetId));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Cabinet display name is required.", nameof(displayName));
        }

        RingCabinetIntervalDefinition[] definitions = intervalDefinitions?.ToArray()
            ?? throw new ArgumentNullException(nameof(intervalDefinitions));

        if (definitions.Length == 0)
        {
            throw new ArgumentException(
                "A ring cabinet requires at least one interval definition.",
                nameof(intervalDefinitions));
        }

        if (definitions.Any(definition => definition is null))
        {
            throw new ArgumentException(
                "Interval definitions cannot contain null entries.",
                nameof(intervalDefinitions));
        }

        CabinetId = cabinetId;
        DisplayName = displayName.Trim();
        _intervalDefinitions = Array.AsReadOnly(definitions);
    }

    public Guid CabinetId { get; }

    public string DisplayName { get; }

    public IReadOnlyList<RingCabinetIntervalDefinition> IntervalDefinitions =>
        _intervalDefinitions;

    public static RingCabinetDefinition Create(
        Guid cabinetId,
        string displayName,
        IEnumerable<RingCabinetIntervalDefinition> intervalDefinitions)
    {
        return new RingCabinetDefinition(cabinetId, displayName, intervalDefinitions);
    }
}
