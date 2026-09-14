using DistributionDrawing.Domain.Devices.RingCabinets;

namespace DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;

public sealed class RingCabinetCreationTemplateFactory
{
    private static readonly IReadOnlySet<int> ConventionalCounts =
        new HashSet<int> { 3, 4, 5, 6 };
    private static readonly IReadOnlySet<int> IntegratedCounts =
        new HashSet<int> { 4, 6 };

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

        IReadOnlySet<int> supportedCounts = cabinetType == RingCabinetTemplateType.Conventional
            ? ConventionalCounts
            : IntegratedCounts;
        if (!supportedCounts.Contains(businessIntervalCount))
        {
            throw new ArgumentOutOfRangeException(
                nameof(businessIntervalCount),
                $"Unsupported business interval count '{businessIntervalCount}' for '{cabinetType}'.");
        }

        if (cabinetType == RingCabinetTemplateType.Conventional && includePTInterval)
        {
            throw new ArgumentException(
                "PT intervals are currently available only for primary-secondary integrated creation.",
                nameof(includePTInterval));
        }

        if (!Enum.IsDefined(integratedGroundingStructureKind))
        {
            throw new ArgumentOutOfRangeException(nameof(integratedGroundingStructureKind));
        }

        if (!Enum.IsDefined(ptPlacement))
        {
            throw new ArgumentOutOfRangeException(nameof(ptPlacement));
        }

        int totalIntervalCount = businessIntervalCount + (includePTInterval ? 1 : 0);
        int ptIndex = ptPlacement == RingCabinetPTPlacement.Left ? 1 : totalIntervalCount;
        var bays = new List<BayTemplate>(totalIntervalCount);
        for (int index = 1; index <= totalIntervalCount; index++)
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
