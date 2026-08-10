using DistributionDrawing.Domain.Devices;

namespace DistributionDrawing.Domain.Devices.RingCabinets;

public sealed class RingCabinetIntervalDefinition
{
    private RingCabinetIntervalDefinition(
        IntervalKind intervalKind,
        string? displayName,
        SwitchState? initialLoadSwitchState,
        SwitchState? initialIsolationSwitchState,
        SwitchState? initialCircuitBreakerState,
        SwitchState initialGroundSwitchState)
    {
        IntervalKind = intervalKind;
        DisplayName = NormalizeOptionalText(displayName);
        InitialLoadSwitchState = initialLoadSwitchState;
        InitialIsolationSwitchState = initialIsolationSwitchState;
        InitialCircuitBreakerState = initialCircuitBreakerState;
        InitialGroundSwitchState = initialGroundSwitchState;
    }

    public IntervalKind IntervalKind { get; }

    public string? DisplayName { get; }

    public SwitchState? InitialLoadSwitchState { get; }

    public SwitchState? InitialIsolationSwitchState { get; }

    public SwitchState? InitialCircuitBreakerState { get; }

    public SwitchState InitialGroundSwitchState { get; }

    public static RingCabinetIntervalDefinition CreateLoadSwitch(
        SwitchState initialLoadSwitchState,
        SwitchState initialGroundSwitchState,
        string? displayName = null)
    {
        EnsureDefined(initialLoadSwitchState, nameof(initialLoadSwitchState));
        EnsureDefined(initialGroundSwitchState, nameof(initialGroundSwitchState));

        return new RingCabinetIntervalDefinition(
            IntervalKind.LoadSwitchInterval,
            displayName,
            initialLoadSwitchState,
            null,
            null,
            initialGroundSwitchState);
    }

    public static RingCabinetIntervalDefinition CreateIntegratedFeeder(
        SwitchState initialIsolationSwitchState,
        SwitchState initialCircuitBreakerState,
        SwitchState initialGroundSwitchState,
        string? displayName = null)
    {
        EnsureDefined(initialIsolationSwitchState, nameof(initialIsolationSwitchState));
        EnsureDefined(initialCircuitBreakerState, nameof(initialCircuitBreakerState));
        EnsureDefined(initialGroundSwitchState, nameof(initialGroundSwitchState));

        return new RingCabinetIntervalDefinition(
            IntervalKind.IntegratedFeederInterval,
            displayName,
            null,
            initialIsolationSwitchState,
            initialCircuitBreakerState,
            initialGroundSwitchState);
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
