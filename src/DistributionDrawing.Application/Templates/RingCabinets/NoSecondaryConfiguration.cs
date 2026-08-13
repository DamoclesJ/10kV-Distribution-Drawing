namespace DistributionDrawing.Application.Templates.RingCabinets;

public sealed class NoSecondaryConfiguration : SecondaryConfiguration
{
    private NoSecondaryConfiguration()
    {
    }

    public static NoSecondaryConfiguration Instance { get; } = new();
}
