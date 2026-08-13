namespace DistributionDrawing.Rendering.Wpf.Templates.RingCabinets.Building;

public sealed class RingCabinetTemplateBuildOutcome
{
    private RingCabinetTemplateBuildOutcome(
        RingCabinetTemplateBuildResult? result,
        RingCabinetTemplateBuildFailure? failure)
    {
        Result = result;
        Failure = failure;
    }

    public bool IsSuccess => Result is not null;

    public RingCabinetTemplateBuildResult? Result { get; }

    public RingCabinetTemplateBuildFailure? Failure { get; }

    public static RingCabinetTemplateBuildOutcome Success(
        RingCabinetTemplateBuildResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new RingCabinetTemplateBuildOutcome(result, null);
    }

    public static RingCabinetTemplateBuildOutcome Failed(
        RingCabinetTemplateBuildFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new RingCabinetTemplateBuildOutcome(null, failure);
    }
}
