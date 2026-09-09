using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Domain.Devices;

public sealed class Transformer : Device
{
    public const string HvTerminalRole = "HvTerminal";

    public const string TenKilovolts = "10kV";

    public Transformer(Guid id, TransformerKind transformerKind, Guid hvTerminalId)
        : base(id, DeviceType.Transformer, voltageLevel: TenKilovolts)
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
    }

    public TransformerKind TransformerKind { get; }

    public Guid HvTerminalId { get; }

    public ConnectionType AllowedConnectionType => TransformerKind switch
    {
        TransformerKind.PublicPoleMounted => ConnectionType.OverheadLine,
        TransformerKind.DedicatedPoleMounted => ConnectionType.OverheadLine,
        TransformerKind.PublicIndoor => ConnectionType.Cable,
        _ => throw new InvalidOperationException(
            $"Unsupported transformer kind '{TransformerKind}'.")
    };

    public bool OwnsTerminal(Guid terminalId) => HvTerminalId == terminalId;
}
