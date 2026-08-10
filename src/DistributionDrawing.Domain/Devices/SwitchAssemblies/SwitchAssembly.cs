using DistributionDrawing.Domain.Devices;

namespace DistributionDrawing.Domain.Devices.SwitchAssemblies;

public sealed class SwitchAssembly
{
    private const string LoadSwitchRuleSet = "load-switch-three-position/v1";

    private readonly IReadOnlyList<SwitchDevice> _memberSwitches;
    private readonly IReadOnlyList<Guid> _memberSwitchIds;
    private readonly IReadOnlyList<InterlockRule> _interlockRules;

    private SwitchAssembly(
        Guid assemblyId,
        Guid parentIntervalId,
        SwitchAssemblyType assemblyType,
        IEnumerable<SwitchDevice> memberSwitches,
        string ruleSetRef,
        IEnumerable<InterlockRule> interlockRules)
    {
        if (assemblyId == Guid.Empty)
        {
            throw new ArgumentException("Switch assembly ID cannot be empty.", nameof(assemblyId));
        }

        if (parentIntervalId == Guid.Empty)
        {
            throw new ArgumentException("Parent interval ID cannot be empty.", nameof(parentIntervalId));
        }

        if (string.IsNullOrWhiteSpace(ruleSetRef))
        {
            throw new ArgumentException("Rule-set reference is required.", nameof(ruleSetRef));
        }

        SwitchDevice[] members = memberSwitches?.ToArray()
            ?? throw new ArgumentNullException(nameof(memberSwitches));
        InterlockRule[] rules = interlockRules?.ToArray()
            ?? throw new ArgumentNullException(nameof(interlockRules));

        if (members.Length == 0)
        {
            throw new ArgumentException(
                "A switch assembly requires member switches.",
                nameof(memberSwitches));
        }

        if (members.Select(member => member.Id).Distinct().Count() != members.Length)
        {
            throw new ArgumentException(
                "A switch assembly cannot contain duplicate member switches.",
                nameof(memberSwitches));
        }

        if (rules.Length == 0)
        {
            throw new ArgumentException(
                "A switch assembly requires interlock rules.",
                nameof(interlockRules));
        }

        if (rules.Select(rule => rule.Code).Distinct(StringComparer.Ordinal).Count() != rules.Length)
        {
            throw new ArgumentException(
                "A switch assembly cannot contain duplicate interlock rule codes.",
                nameof(interlockRules));
        }

        AssemblyId = assemblyId;
        ParentIntervalId = parentIntervalId;
        AssemblyType = assemblyType;
        _memberSwitches = Array.AsReadOnly(members);
        _memberSwitchIds = Array.AsReadOnly(members.Select(member => member.Id).ToArray());
        RuleSetRef = ruleSetRef.Trim();
        _interlockRules = Array.AsReadOnly(rules);
    }

    public Guid AssemblyId { get; }

    public Guid ParentIntervalId { get; }

    public SwitchAssemblyType AssemblyType { get; }

    public IReadOnlyList<Guid> MemberSwitchIds => _memberSwitchIds;

    public string RuleSetRef { get; }

    public IReadOnlyList<InterlockRule> InterlockRules => _interlockRules;

    internal static SwitchAssembly CreateLoadSwitchThreePosition(
        Guid assemblyId,
        Guid parentIntervalId,
        SwitchDevice loadSwitch,
        SwitchDevice groundSwitch)
    {
        ArgumentNullException.ThrowIfNull(loadSwitch);
        ArgumentNullException.ThrowIfNull(groundSwitch);

        if (loadSwitch.SwitchKind != SwitchKind.LoadSwitch)
        {
            throw new ArgumentException(
                "The load-switch role requires a LoadSwitch device.",
                nameof(loadSwitch));
        }

        if (groundSwitch.SwitchKind != SwitchKind.GroundSwitch)
        {
            throw new ArgumentException(
                "The ground-switch role requires a GroundSwitch device.",
                nameof(groundSwitch));
        }

        EnsureCabinetIntervalMember(loadSwitch, parentIntervalId);
        EnsureCabinetIntervalMember(groundSwitch, parentIntervalId);

        var assembly = new SwitchAssembly(
            assemblyId,
            parentIntervalId,
            SwitchAssemblyType.LoadSwitchThreePosition,
            [loadSwitch, groundSwitch],
            LoadSwitchRuleSet,
            CreateLoadSwitchRules());

        SwitchAssemblyEvaluation evaluation = assembly.Evaluate();

        if (!evaluation.IsValid)
        {
            throw new ArgumentException(
                "Initial load-switch and ground-switch states violate the mechanical interlock.");
        }

        return assembly;
    }

    public SwitchAssemblyEvaluation Evaluate()
    {
        return EvaluateStates(GetCurrentStates());
    }

    public SwitchAssemblyEvaluation ChangeSwitchState(Guid switchDeviceId, SwitchState targetState)
    {
        if (!Enum.IsDefined(targetState))
        {
            throw new ArgumentOutOfRangeException(nameof(targetState));
        }

        SwitchDevice switchDevice = _memberSwitches
            .FirstOrDefault(member => member.Id == switchDeviceId)
            ?? throw new InvalidOperationException(
                $"Switch '{switchDeviceId}' is not a member of assembly '{AssemblyId}'.");

        Dictionary<SwitchKind, SwitchState> targetStates = GetCurrentStates();
        targetStates[switchDevice.SwitchKind] = targetState;

        SwitchAssemblyEvaluation evaluation = EvaluateStates(targetStates);

        if (!evaluation.IsValid)
        {
            throw new InvalidOperationException(
                $"Switch state change violates interlock rule(s): " +
                string.Join(", ", evaluation.ViolatedRuleCodes));
        }

        switchDevice.SetSwitchState(targetState);
        return evaluation;
    }

    private SwitchAssemblyEvaluation EvaluateStates(
        IReadOnlyDictionary<SwitchKind, SwitchState> states)
    {
        string[] violations = _interlockRules
            .Where(rule =>
                (rule.RuleType == InterlockRuleType.MutualExclusion ||
                 rule.RuleType == InterlockRuleType.InvalidCombination) &&
                rule.Matches(states))
            .Select(rule => rule.Code)
            .ToArray();

        OperationalState operationalState = _interlockRules
            .Where(rule =>
                rule.RuleType == InterlockRuleType.OperationalStateMapping &&
                rule.Matches(states))
            .Select(rule => rule.OperationalState)
            .FirstOrDefault() ?? OperationalState.Unclassified;

        bool isEffectivelyGrounded = _interlockRules.Any(rule =>
            rule.RuleType == InterlockRuleType.EffectiveGrounding &&
            rule.IsEffectivelyGrounded == true &&
            rule.Matches(states));

        return new SwitchAssemblyEvaluation(
            violations.Length == 0,
            operationalState,
            isEffectivelyGrounded,
            violations);
    }

    private Dictionary<SwitchKind, SwitchState> GetCurrentStates()
    {
        return _memberSwitches.ToDictionary(
            member => member.SwitchKind,
            member => member.SwitchState
                ?? throw new InvalidOperationException(
                    $"Switch '{member.Id}' does not have a switch state."));
    }

    private static IReadOnlyList<InterlockRule> CreateLoadSwitchRules()
    {
        return
        [
            new InterlockRule(
                "LS-GS-MUTUAL-EXCLUSION",
                InterlockRuleType.MutualExclusion,
                new Dictionary<SwitchKind, SwitchState>
                {
                    [SwitchKind.LoadSwitch] = SwitchState.Closed,
                    [SwitchKind.GroundSwitch] = SwitchState.Closed
                },
                "Load switch and ground switch cannot both be closed."),
            new InterlockRule(
                "LS-ASSEMBLY-RUNNING",
                InterlockRuleType.OperationalStateMapping,
                new Dictionary<SwitchKind, SwitchState>
                {
                    [SwitchKind.LoadSwitch] = SwitchState.Closed,
                    [SwitchKind.GroundSwitch] = SwitchState.Open
                },
                "Load circuit is connected.",
                OperationalState.Running),
            new InterlockRule(
                "LS-ASSEMBLY-DISCONNECTED",
                InterlockRuleType.OperationalStateMapping,
                new Dictionary<SwitchKind, SwitchState>
                {
                    [SwitchKind.LoadSwitch] = SwitchState.Open,
                    [SwitchKind.GroundSwitch] = SwitchState.Open
                },
                "Load circuit is disconnected and not grounded.",
                OperationalState.Disconnected),
            new InterlockRule(
                "LS-ASSEMBLY-GROUNDED",
                InterlockRuleType.OperationalStateMapping,
                new Dictionary<SwitchKind, SwitchState>
                {
                    [SwitchKind.LoadSwitch] = SwitchState.Open,
                    [SwitchKind.GroundSwitch] = SwitchState.Closed
                },
                "Load circuit is grounded.",
                OperationalState.Grounded),
            new InterlockRule(
                "LS-ASSEMBLY-EFFECTIVE-GROUNDING",
                InterlockRuleType.EffectiveGrounding,
                new Dictionary<SwitchKind, SwitchState>
                {
                    [SwitchKind.LoadSwitch] = SwitchState.Open,
                    [SwitchKind.GroundSwitch] = SwitchState.Closed
                },
                "The external circuit is effectively grounded.",
                isEffectivelyGrounded: true)
        ];
    }

    private static void EnsureCabinetIntervalMember(
        SwitchDevice switchDevice,
        Guid parentIntervalId)
    {
        if (switchDevice.InstallationType != SwitchInstallationType.CabinetInterval ||
            switchDevice.ParentId != parentIntervalId)
        {
            throw new ArgumentException(
                $"Switch '{switchDevice.Id}' is not owned by interval '{parentIntervalId}'.");
        }
    }
}
