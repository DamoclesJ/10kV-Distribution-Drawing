namespace DistributionDrawing.Domain.Devices;

public interface ISwitchStateView
{
    SwitchState? GetSwitchState(SwitchDevice switchDevice);
}

public sealed class CurrentSwitchStateView : ISwitchStateView
{
    public static CurrentSwitchStateView Instance { get; } = new();

    private CurrentSwitchStateView()
    {
    }

    public SwitchState? GetSwitchState(SwitchDevice switchDevice)
    {
        ArgumentNullException.ThrowIfNull(switchDevice);
        return switchDevice.SwitchState;
    }
}
