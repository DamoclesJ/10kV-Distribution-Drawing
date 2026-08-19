namespace DistributionDrawing.Application.Templates.RingCabinets;

public sealed class BayTemplate
{
    public BayTemplate(
        int index,
        BayEquipmentConfiguration equipmentConfiguration,
        string? displayName = null)
    {
        if (index < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Bay index must be positive.");
        }

        Index = index;
        EquipmentConfiguration = equipmentConfiguration ??
            throw new ArgumentNullException(nameof(equipmentConfiguration));
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? null
            : displayName.Trim();
    }

    public int Index { get; }

    public BayEquipmentConfiguration EquipmentConfiguration { get; }

    public string? DisplayName { get; }
}
