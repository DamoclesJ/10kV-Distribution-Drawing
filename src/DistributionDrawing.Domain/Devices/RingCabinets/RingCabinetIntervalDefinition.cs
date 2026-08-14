using DistributionDrawing.Domain.Devices;

namespace DistributionDrawing.Domain.Devices.RingCabinets;

public sealed class RingCabinetIntervalDefinition
{
    private RingCabinetIntervalDefinition(
        int bayIndex,
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

        BayIndex = bayIndex;
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

    public string? DisplayName { get; }

    public SwitchState? InitialLoadSwitchState { get; }

    public SwitchState? InitialIsolationSwitchState { get; }

    public SwitchState? InitialCircuitBreakerState { get; }

    public SwitchState InitialGroundSwitchState { get; }

    public GroundingStructureKind? GroundingStructureKind { get; }

    public static RingCabinetIntervalDefinition CreateLoadSwitch(
        int bayIndex,
        SwitchState initialLoadSwitchState,
        SwitchState initialGroundSwitchState,
        string? displayName = null)
    {
        EnsureCreatableBayIndex(bayIndex);
        EnsureDefined(initialLoadSwitchState, nameof(initialLoadSwitchState));
        EnsureDefined(initialGroundSwitchState, nameof(initialGroundSwitchState));

        return new RingCabinetIntervalDefinition(
            bayIndex,
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
        GroundingStructureKind groundingStructureKind,
        SwitchState initialIsolationSwitchState,
        SwitchState initialCircuitBreakerState,
        SwitchState initialGroundSwitchState,
        string? displayName = null)
    {
        EnsureCreatableBayIndex(bayIndex);
        EnsureDefined(groundingStructureKind, nameof(groundingStructureKind));
        EnsureDefined(initialIsolationSwitchState, nameof(initialIsolationSwitchState));
        EnsureDefined(initialCircuitBreakerState, nameof(initialCircuitBreakerState));
        EnsureDefined(initialGroundSwitchState, nameof(initialGroundSwitchState));

        return new RingCabinetIntervalDefinition(
            bayIndex,
            IntervalKind.IntegratedFeederInterval,
            displayName,
            null,
            initialIsolationSwitchState,
            initialCircuitBreakerState,
            initialGroundSwitchState,
            groundingStructureKind);
    }

    public static RingCabinetIntervalDefinition CreatePT(
        int bayIndex,
        SwitchState initialIsolationSwitchState,
        SwitchState initialGroundSwitchState,
        string? displayName = null)
    {
        EnsureCreatableBayIndex(bayIndex);
        EnsureDefined(initialIsolationSwitchState, nameof(initialIsolationSwitchState));
        EnsureDefined(initialGroundSwitchState, nameof(initialGroundSwitchState));

        return new RingCabinetIntervalDefinition(
            bayIndex,
            IntervalKind.PTInterval,
            displayName,
            null,
            initialIsolationSwitchState,
            null,
            initialGroundSwitchState,
            null);
    }

    private static void EnsureCreatableBayIndex(int bayIndex)
    {
        if (bayIndex < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(bayIndex), "Bay index must be positive.");
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
