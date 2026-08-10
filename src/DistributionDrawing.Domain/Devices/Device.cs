namespace DistributionDrawing.Domain.Devices;

public sealed class Device
{
    public Device(
        Guid id,
        DeviceType type,
        string? displayName = null,
        string? voltageLevel = null,
        SwitchState? switchState = null,
        Guid? parentId = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Device ID cannot be empty.", nameof(id));
        }

        if (type == DeviceType.Switch && switchState is null)
        {
            throw new ArgumentException("A switch device requires a switch state.", nameof(switchState));
        }

        if (type != DeviceType.Switch && switchState is not null)
        {
            throw new ArgumentException("Only switch devices can have a switch state.", nameof(switchState));
        }

        Id = id;
        Type = type;
        DisplayName = NormalizeOptionalText(displayName);
        VoltageLevel = NormalizeOptionalText(voltageLevel);
        SwitchState = switchState;
        ParentId = parentId;
    }

    public Guid Id { get; }

    public DeviceType Type { get; }

    public string? DisplayName { get; private set; }

    public string? VoltageLevel { get; }

    public SwitchState? SwitchState { get; private set; }

    public Guid? ParentId { get; }

    public void Rename(string? displayName)
    {
        DisplayName = NormalizeOptionalText(displayName);
    }

    public void SetSwitchState(SwitchState state)
    {
        if (Type != DeviceType.Switch)
        {
            throw new InvalidOperationException("Only switch devices can change switch state.");
        }

        SwitchState = state;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
