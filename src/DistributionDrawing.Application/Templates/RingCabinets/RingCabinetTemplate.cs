using System.Collections.Frozen;

namespace DistributionDrawing.Application.Templates.RingCabinets;

public sealed class RingCabinetTemplate
{
    private readonly IReadOnlyList<BayTemplate> _bays;
    private readonly IReadOnlySet<TemplateCapability> _requiredCapabilities;

    public RingCabinetTemplate(
        TemplateId templateId,
        string name,
        RingCabinetTemplateType cabinetType,
        IEnumerable<BayTemplate> bays,
        RingCabinetLayoutRule layoutRule,
        SecondaryConfiguration secondaryConfiguration)
    {
        TemplateId = templateId ?? throw new ArgumentNullException(nameof(templateId));

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Template name is required.", nameof(name));
        }

        if (!Enum.IsDefined(cabinetType))
        {
            throw new ArgumentOutOfRangeException(nameof(cabinetType));
        }

        BayTemplate[] bayValues = bays?.ToArray()
            ?? throw new ArgumentNullException(nameof(bays));
        if (bayValues.Length == 0)
        {
            throw new ArgumentException("A ring cabinet template requires at least one bay.", nameof(bays));
        }

        if (bayValues.Any(bay => bay is null))
        {
            throw new ArgumentException("Template bays cannot contain null entries.", nameof(bays));
        }

        int[] indexes = bayValues.Select(bay => bay.Index).ToArray();
        if (indexes.Any(index => index < 1))
        {
            throw new ArgumentException("Every bay index must be positive.", nameof(bays));
        }

        if (indexes.Distinct().Count() != indexes.Length)
        {
            throw new ArgumentException(
                "Bay indexes must be unique within a ring cabinet template.",
                nameof(bays));
        }

        Name = name.Trim();
        CabinetType = cabinetType;
        _bays = Array.AsReadOnly(bayValues);
        LayoutRule = layoutRule ?? throw new ArgumentNullException(nameof(layoutRule));
        SecondaryConfiguration = secondaryConfiguration ??
            throw new ArgumentNullException(nameof(secondaryConfiguration));
        _requiredCapabilities = DeriveCapabilities(
            bayValues,
            secondaryConfiguration).ToFrozenSet();
    }

    public TemplateId TemplateId { get; }

    public string Name { get; }

    public RingCabinetTemplateType CabinetType { get; }

    public IReadOnlyList<BayTemplate> Bays => _bays;

    public RingCabinetLayoutRule LayoutRule { get; }

    public SecondaryConfiguration SecondaryConfiguration { get; }

    public IReadOnlySet<TemplateCapability> RequiredCapabilities => _requiredCapabilities;

    private static IEnumerable<TemplateCapability> DeriveCapabilities(
        IEnumerable<BayTemplate> bays,
        SecondaryConfiguration secondaryConfiguration)
    {
        var capabilities = new HashSet<TemplateCapability>
        {
            TemplateCapability.BasicRingCabinet,
            TemplateCapability.RingCabinetLayout
        };

        foreach (BayTemplate bay in bays)
        {
            capabilities.Add(bay.EquipmentConfiguration switch
            {
                LoadSwitchConfiguration => TemplateCapability.LoadSwitchBay,
                IntegratedFeederConfiguration => TemplateCapability.IntegratedFeederBay,
                _ => throw new InvalidOperationException(
                    $"Unsupported equipment configuration '{bay.EquipmentConfiguration.GetType().Name}'.")
            });
        }

        switch (secondaryConfiguration)
        {
            case NoSecondaryConfiguration:
                break;
            case DtuSecondaryConfiguration:
                capabilities.Add(TemplateCapability.DtuSecondary);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported secondary configuration '{secondaryConfiguration.GetType().Name}'.");
        }

        return capabilities;
    }
}
