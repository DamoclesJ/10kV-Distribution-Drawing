namespace DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;

public static class Conventional10kVRingCabinetTemplate
{
    private static readonly IReadOnlyList<BayTemplate> Intervals = Array.AsReadOnly(
        new BayTemplate[]
        {
            new(1, new LoadSwitchConfiguration()),
            new(2, new LoadSwitchConfiguration()),
            new(3, new LoadSwitchConfiguration())
        });

    public const string TemplateIdValue = "builtin:ring-cabinet/conventional/3-bay";

    public const string DisplayName = "10kV 常规三间隔环网柜";

    public const int SchemaVersion = 1;

    public const int DefaultIntervalCount = 3;

    public static TemplateId TemplateId { get; } = new(TemplateIdValue);

    public static RingCabinetTemplateType DefaultCabinetType =>
        RingCabinetTemplateType.Conventional;

    public static IReadOnlyList<BayTemplate> DefaultIntervals => Intervals;

    public static SecondaryConfiguration DefaultSecondaryConfiguration =>
        NoSecondaryConfiguration.Instance;

    public static RingCabinetLayoutRule LayoutReference =>
        RingCabinetLayoutRule.Default;

    public static RingCabinetTemplate Create()
    {
        return new RingCabinetTemplate(
            TemplateId,
            DisplayName,
            DefaultCabinetType,
            DefaultIntervals,
            LayoutReference,
            DefaultSecondaryConfiguration);
    }
}
