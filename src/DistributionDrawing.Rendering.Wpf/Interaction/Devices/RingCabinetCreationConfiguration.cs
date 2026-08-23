using DistributionDrawing.Application.Templates.RingCabinets;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class RingCabinetCreationConfiguration
{
    public RingCabinetCreationConfiguration(
        string displayName,
        RingCabinetTemplate template,
        string? lineName = null)
    {
        DisplayName = displayName;
        Template = template ?? throw new ArgumentNullException(nameof(template));
        LineName = lineName?.Trim() ?? string.Empty;
    }

    public string DisplayName { get; }

    public RingCabinetTemplate Template { get; }

    public string LineName { get; }
}
