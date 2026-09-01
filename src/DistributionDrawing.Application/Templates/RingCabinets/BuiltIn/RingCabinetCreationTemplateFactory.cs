using DistributionDrawing.Domain.Devices.RingCabinets;

namespace DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;

public sealed class RingCabinetCreationTemplateFactory
{
    public const int MinimumIntervalCount = 2;
    public const int MaximumIntervalCount = 24;

    public RingCabinetTemplate Create(
        RingCabinetTemplateType cabinetType,
        int businessIntervalCount,
        GroundingStructureKind integratedGroundingStructureKind =
            GroundingStructureKind.UpperIsolationGrounding,
        bool includePTInterval = false,
        RingCabinetPTPlacement ptPlacement = RingCabinetPTPlacement.Right)
    {
        if (cabinetType is not (RingCabinetTemplateType.Conventional or
            RingCabinetTemplateType.PrimarySecondaryIntegrated))
        {
            throw new ArgumentOutOfRangeException(
                nameof(cabinetType),
                "Only conventional and primary-secondary integrated cabinets are supported.");
        }

        if (businessIntervalCount is < MinimumIntervalCount or > MaximumIntervalCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(businessIntervalCount),
                $"Interval count must be between {MinimumIntervalCount} and {MaximumIntervalCount}.");
        }

        if (!Enum.IsDefined(integratedGroundingStructureKind))
        {
            throw new ArgumentOutOfRangeException(nameof(integratedGroundingStructureKind));
        }

        if (!Enum.IsDefined(ptPlacement))
        {
            throw new ArgumentOutOfRangeException(nameof(ptPlacement));
        }

        int ptIndex = ptPlacement == RingCabinetPTPlacement.Left
            ? 1
            : businessIntervalCount;
        var bays = new List<BayTemplate>(businessIntervalCount);
        for (int index = 1; index <= businessIntervalCount; index++)
        {
            bool isPT = includePTInterval && index == ptIndex;
            BayEquipmentConfiguration equipment = isPT
                ? new PTConfiguration()
                : cabinetType switch
            {
                RingCabinetTemplateType.Conventional => new LoadSwitchConfiguration(),
                RingCabinetTemplateType.PrimarySecondaryIntegrated =>
                    new IntegratedFeederConfiguration(integratedGroundingStructureKind),
                _ => throw new InvalidOperationException()
            };
            bays.Add(new BayTemplate(index, equipment, isPT ? "PT" : $"负{index}"));
        }

        string typeId = cabinetType == RingCabinetTemplateType.Conventional
            ? "conventional"
            : "primary-secondary-integrated";
        string ptSuffix = includePTInterval
            ? $"+pt-{ptPlacement.ToString().ToLowerInvariant()}"
            : string.Empty;
        return new RingCabinetTemplate(
            new TemplateId($"builtin:ring-cabinet/{typeId}/{businessIntervalCount}-business{ptSuffix}"),
            $"{cabinetType} {businessIntervalCount} business intervals{ptSuffix}",
            cabinetType,
            bays,
            RingCabinetLayoutRule.Default,
            NoSecondaryConfiguration.Instance);
    }
}
