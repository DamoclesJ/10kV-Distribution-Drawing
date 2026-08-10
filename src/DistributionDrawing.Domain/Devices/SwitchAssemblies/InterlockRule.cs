using System.Collections.ObjectModel;
using DistributionDrawing.Domain.Devices;

namespace DistributionDrawing.Domain.Devices.SwitchAssemblies;

public sealed class InterlockRule
{
    private readonly IReadOnlyDictionary<SwitchKind, SwitchState> _requiredStates;

    internal InterlockRule(
        string code,
        InterlockRuleType ruleType,
        IReadOnlyDictionary<SwitchKind, SwitchState> requiredStates,
        string description,
        OperationalState? operationalState = null,
        bool? isEffectivelyGrounded = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Interlock rule code is required.", nameof(code));
        }

        ArgumentNullException.ThrowIfNull(requiredStates);

        if (requiredStates.Count == 0)
        {
            throw new ArgumentException(
                "An interlock rule requires at least one switch-state condition.",
                nameof(requiredStates));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException(
                "Interlock rule description is required.",
                nameof(description));
        }

        if (ruleType == InterlockRuleType.OperationalStateMapping && operationalState is null)
        {
            throw new ArgumentException(
                "An operational-state mapping requires a result state.",
                nameof(operationalState));
        }

        if (ruleType == InterlockRuleType.EffectiveGrounding && isEffectivelyGrounded is null)
        {
            throw new ArgumentException(
                "An effective-grounding rule requires a grounding result.",
                nameof(isEffectivelyGrounded));
        }

        Code = code.Trim();
        RuleType = ruleType;
        _requiredStates = new ReadOnlyDictionary<SwitchKind, SwitchState>(
            requiredStates.ToDictionary(item => item.Key, item => item.Value));
        Description = description.Trim();
        OperationalState = operationalState;
        IsEffectivelyGrounded = isEffectivelyGrounded;
    }

    public string Code { get; }

    public InterlockRuleType RuleType { get; }

    public IReadOnlyDictionary<SwitchKind, SwitchState> RequiredStates => _requiredStates;

    public string Description { get; }

    public OperationalState? OperationalState { get; }

    public bool? IsEffectivelyGrounded { get; }

    internal bool Matches(IReadOnlyDictionary<SwitchKind, SwitchState> states)
    {
        return _requiredStates.All(
            required => states.TryGetValue(required.Key, out SwitchState actual) &&
                        actual == required.Value);
    }
}
