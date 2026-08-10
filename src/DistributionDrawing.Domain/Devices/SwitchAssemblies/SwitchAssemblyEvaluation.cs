namespace DistributionDrawing.Domain.Devices.SwitchAssemblies;

public sealed class SwitchAssemblyEvaluation
{
    internal SwitchAssemblyEvaluation(
        bool isValid,
        OperationalState operationalState,
        bool isEffectivelyGrounded,
        IEnumerable<string> violatedRuleCodes)
    {
        IsValid = isValid;
        OperationalState = operationalState;
        IsEffectivelyGrounded = isEffectivelyGrounded;
        ViolatedRuleCodes = Array.AsReadOnly(violatedRuleCodes.ToArray());
    }

    public bool IsValid { get; }

    public OperationalState OperationalState { get; }

    public bool IsEffectivelyGrounded { get; }

    public IReadOnlyList<string> ViolatedRuleCodes { get; }
}
