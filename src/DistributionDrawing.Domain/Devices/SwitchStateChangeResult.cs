namespace DistributionDrawing.Domain.Devices;

public sealed record SwitchStateChangeResult(
    SwitchDevice SwitchDevice,
    SwitchState PreviousState,
    SwitchState CurrentState);
