using DistributionDrawing.Application.Templates.RingCabinets;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class RingCabinetCreationConfiguration
{
    public RingCabinetCreationConfiguration(
        string displayName,
        RingCabinetTemplate template)
    {
        DisplayName = displayName;
        Template = template ?? throw new ArgumentNullException(nameof(template));
    }

    public string DisplayName { get; }

    public RingCabinetTemplate Template { get; }
}
