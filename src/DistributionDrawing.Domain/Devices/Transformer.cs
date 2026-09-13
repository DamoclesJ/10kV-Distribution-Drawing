using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Domain.Devices;

public sealed class Transformer : Device
{
    private readonly bool _canRestoreLegacyNamingIncomplete;

    public const string HvTerminalRole = "HvTerminal";

    public const string TenKilovolts = "10kV";

    public Transformer(
        Guid id,
        TransformerKind transformerKind,
        Guid hvTerminalId,
        string displayName)
        : this(
            id,
            transformerKind,
            hvTerminalId,
            RequireDisplayName(displayName),
            isLegacyNamingIncomplete: false)
    {
    }

    private Transformer(
        Guid id,
        TransformerKind transformerKind,
        Guid hvTerminalId,
        string? displayName,
        bool isLegacyNamingIncomplete)
        : base(id, DeviceType.Transformer, displayName, TenKilovolts)
    {
        if (!Enum.IsDefined(transformerKind))
        {
            throw new ArgumentOutOfRangeException(nameof(transformerKind));
        }

        if (hvTerminalId == Guid.Empty)
        {
            throw new ArgumentException(
                "Transformer HV terminal ID cannot be empty.",
                nameof(hvTerminalId));
        }

        if (hvTerminalId == id)
        {
            throw new ArgumentException(
                "Transformer and HV terminal IDs must be different.",
                nameof(hvTerminalId));
        }

        TransformerKind = transformerKind;
        HvTerminalId = hvTerminalId;
        IsLegacyNamingIncomplete = isLegacyNamingIncomplete;
        _canRestoreLegacyNamingIncomplete = isLegacyNamingIncomplete;
    }

    public TransformerKind TransformerKind { get; }

    public Guid HvTerminalId { get; }

    public bool IsLegacyNamingIncomplete { get; private set; }

    public ConnectionType AllowedConnectionType => TransformerKind switch
    {
        TransformerKind.PublicPoleMounted => ConnectionType.OverheadLine,
        TransformerKind.DedicatedPoleMounted => ConnectionType.OverheadLine,
        TransformerKind.PublicIndoor => ConnectionType.Cable,
        _ => throw new InvalidOperationException(
            $"Unsupported transformer kind '{TransformerKind}'.")
    };

    public bool OwnsTerminal(Guid terminalId) => HvTerminalId == terminalId;

    public override void Rename(string? displayName)
    {
        base.Rename(RequireDisplayName(displayName));
        IsLegacyNamingIncomplete = false;
    }

    internal static Transformer RestoreLegacy(
        Guid id,
        TransformerKind transformerKind,
        Guid hvTerminalId,
        string? displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return new Transformer(id, transformerKind, hvTerminalId, displayName);
        }

        return new Transformer(
            id,
            transformerKind,
            hvTerminalId,
            displayName: null,
            isLegacyNamingIncomplete: true);
    }

    internal void RestoreLegacyNamingIncomplete()
    {
        if (!_canRestoreLegacyNamingIncomplete)
        {
            throw new InvalidOperationException(
                "Only a Transformer restored from legacy incomplete data can return to that state.");
        }

        base.Rename(null);
        IsLegacyNamingIncomplete = true;
    }

    private static string RequireDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException(
                "Transformer display name cannot be empty.",
                nameof(displayName));
        }

        return displayName.Trim();
    }
}
