namespace DistributionDrawing.Domain.Devices;

public sealed class SwitchDevice : Device
{
    private readonly Guid[] _terminalIds;

    internal SwitchDevice(
        Guid id,
        SwitchKind switchKind,
        SwitchInstallationType installationType,
        Guid firstTerminalId,
        Guid secondTerminalId,
        SwitchState switchState,
        string displayName,
        string voltageLevel,
        Guid? parentIntervalId = null,
        string? dispatchNumber = null)
        : base(
            id,
            DeviceType.Switch,
            displayName,
            voltageLevel,
            switchState,
            parentIntervalId)
    {
        if (firstTerminalId == Guid.Empty)
        {
            throw new ArgumentException("First terminal ID cannot be empty.", nameof(firstTerminalId));
        }

        if (secondTerminalId == Guid.Empty)
        {
            throw new ArgumentException("Second terminal ID cannot be empty.", nameof(secondTerminalId));
        }

        if (firstTerminalId == secondTerminalId)
        {
            throw new ArgumentException("A switch requires two different terminals.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Switch display name is required.", nameof(displayName));
        }

        if (string.IsNullOrWhiteSpace(voltageLevel))
        {
            throw new ArgumentException("Switch voltage level is required.", nameof(voltageLevel));
        }

        if (installationType == SwitchInstallationType.CabinetInterval &&
            (parentIntervalId is null || parentIntervalId == Guid.Empty))
        {
            throw new ArgumentException(
                "A cabinet switch requires a valid parent interval ID.",
                nameof(parentIntervalId));
        }

        if (installationType == SwitchInstallationType.Pole && parentIntervalId is not null)
        {
            throw new ArgumentException(
                "A pole switch cannot have a parent interval ID.",
                nameof(parentIntervalId));
        }

        SwitchKind = switchKind;
        InstallationType = installationType;
        _terminalIds = [firstTerminalId, secondTerminalId];
        DispatchNumber = NormalizeOptionalText(dispatchNumber);
    }

    public SwitchKind SwitchKind { get; }

    public SwitchInstallationType InstallationType { get; }

    public IReadOnlyList<Guid> TerminalIds => _terminalIds;

    public string? DispatchNumber { get; private set; }

    public bool OwnsTerminal(Guid terminalId)
    {
        return _terminalIds.Contains(terminalId);
    }

    public void SetDispatchNumber(string? dispatchNumber)
    {
        DispatchNumber = NormalizeOptionalText(dispatchNumber);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
