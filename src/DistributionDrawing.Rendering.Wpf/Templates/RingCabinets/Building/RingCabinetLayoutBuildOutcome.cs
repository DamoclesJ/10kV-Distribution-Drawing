namespace DistributionDrawing.Rendering.Wpf.Templates.RingCabinets.Building;

public sealed class RingCabinetLayoutBuildOutcome
{
    private RingCabinetLayoutBuildOutcome(
        RingCabinetLayoutBuildResult? result,
        RingCabinetLayoutBuildFailure? failure)
    {
        Result = result;
        Failure = failure;
    }

    public bool IsSuccess => Result is not null;

    public RingCabinetLayoutBuildResult? Result { get; }

    public RingCabinetLayoutBuildFailure? Failure { get; }

    public static RingCabinetLayoutBuildOutcome Success(
        RingCabinetLayoutBuildResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new RingCabinetLayoutBuildOutcome(result, null);
    }

    public static RingCabinetLayoutBuildOutcome Failed(
        RingCabinetLayoutBuildFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new RingCabinetLayoutBuildOutcome(null, failure);
    }
}
