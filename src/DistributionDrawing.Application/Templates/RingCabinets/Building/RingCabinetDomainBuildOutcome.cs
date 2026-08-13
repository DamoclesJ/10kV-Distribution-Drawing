namespace DistributionDrawing.Application.Templates.RingCabinets.Building;

public sealed class RingCabinetDomainBuildOutcome
{
    private RingCabinetDomainBuildOutcome(
        RingCabinetDomainBuildResult? result,
        RingCabinetDomainBuildFailure? failure)
    {
        Result = result;
        Failure = failure;
    }

    public bool IsSuccess => Result is not null;

    public RingCabinetDomainBuildResult? Result { get; }

    public RingCabinetDomainBuildFailure? Failure { get; }

    public static RingCabinetDomainBuildOutcome Success(
        RingCabinetDomainBuildResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new RingCabinetDomainBuildOutcome(result, null);
    }

    public static RingCabinetDomainBuildOutcome Failed(
        RingCabinetDomainBuildFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new RingCabinetDomainBuildOutcome(null, failure);
    }
}
