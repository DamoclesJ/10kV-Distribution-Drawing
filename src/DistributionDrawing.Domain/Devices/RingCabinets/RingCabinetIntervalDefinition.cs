using DistributionDrawing.Domain.Devices;

namespace DistributionDrawing.Domain.Devices.RingCabinets;

public sealed class RingCabinetIntervalDefinition
{
    private RingCabinetIntervalDefinition(
        int bayIndex,
        BayFunction function,
        IntervalKind intervalKind,
        string? displayName,
        SwitchState? initialLoadSwitchState,
        SwitchState? initialIsolationSwitchState,
        SwitchState? initialCircuitBreakerState,
        SwitchState initialGroundSwitchState,
        GroundingStructureKind? groundingStructureKind)
    {
        if (bayIndex < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(bayIndex), "Bay index must be positive.");
        }

        if (!Enum.IsDefined(function))
        {
            throw new ArgumentOutOfRangeException(nameof(function));
        }

        BayIndex = bayIndex;
        Function = function;
        IntervalKind = intervalKind;
        DisplayName = NormalizeOptionalText(displayName);
        InitialLoadSwitchState = initialLoadSwitchState;
        InitialIsolationSwitchState = initialIsolationSwitchState;
        InitialCircuitBreakerState = initialCircuitBreakerState;
        InitialGroundSwitchState = initialGroundSwitchState;
        GroundingStructureKind = groundingStructureKind;
    }

    public IntervalKind IntervalKind { get; }

    public int BayIndex { get; }

    public BayFunction Function { get; }

    public string? DisplayName { get; }

    public SwitchState? InitialLoadSwitchState { get; }

    public SwitchState? InitialIsolationSwitchState { get; }

    public SwitchState? InitialCircuitBreakerState { get; }

    public SwitchState InitialGroundSwitchState { get; }

    public GroundingStructureKind? GroundingStructureKind { get; }

    public static RingCabinetIntervalDefinition CreateLoadSwitch(
        int bayIndex,
        BayFunction function,
        SwitchState initialLoadSwitchState,
        SwitchState initialGroundSwitchState,
        string? displayName = null)
    {
        EnsureCreatableBayMetadata(bayIndex, function);
        EnsureDefined(initialLoadSwitchState, nameof(initialLoadSwitchState));
        EnsureDefined(initialGroundSwitchState, nameof(initialGroundSwitchState));

        return new RingCabinetIntervalDefinition(
            bayIndex,
            function,
            IntervalKind.LoadSwitchInterval,
            displayName,
            initialLoadSwitchState,
            null,
            null,
            initialGroundSwitchState,
            null);
    }

    public static RingCabinetIntervalDefinition CreateIntegratedFeeder(
        int bayIndex,
        BayFunction function,
        GroundingStructureKind groundingStructureKind,
        SwitchState initialIsolationSwitchState,
        SwitchState initialCircuitBreakerState,
        SwitchState initialGroundSwitchState,
        string? displayName = null)
    {
        EnsureCreatableBayMetadata(bayIndex, function);
        EnsureDefined(groundingStructureKind, nameof(groundingStructureKind));
        EnsureDefined(initialIsolationSwitchState, nameof(initialIsolationSwitchState));
        EnsureDefined(initialCircuitBreakerState, nameof(initialCircuitBreakerState));
        EnsureDefined(initialGroundSwitchState, nameof(initialGroundSwitchState));

        return new RingCabinetIntervalDefinition(
            bayIndex,
            function,
            IntervalKind.IntegratedFeederInterval,
            displayName,
            null,
            initialIsolationSwitchState,
            initialCircuitBreakerState,
            initialGroundSwitchState,
            groundingStructureKind);
    }

    private static void EnsureCreatableBayMetadata(int bayIndex, BayFunction function)
    {
        if (bayIndex < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(bayIndex), "Bay index must be positive.");
        }

        if (!Enum.IsDefined(function) ||
            function is BayFunction.Unknown or BayFunction.PT)
        {
            throw new ArgumentOutOfRangeException(
                nameof(function),
                "A new interval requires a supported, known bay function.");
        }
    }

    private static void EnsureDefined(
        GroundingStructureKind kind,
        string parameterName)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void EnsureDefined(SwitchState state, string parameterName)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
