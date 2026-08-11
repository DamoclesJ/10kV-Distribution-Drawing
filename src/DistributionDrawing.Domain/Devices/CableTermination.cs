namespace DistributionDrawing.Domain.Devices;

public sealed class CableTermination : Device
{
    private const string TenKilovolts = "10kV";
    private readonly Guid[] _terminalIds;

    public CableTermination(
        Guid id,
        Guid cableSideTerminalId,
        Guid overheadSideTerminalId,
        Guid internalNodeId,
        string? displayName = null,
        string voltageLevel = TenKilovolts)
        : base(id, DeviceType.CableTermination, displayName, voltageLevel)
    {
        if (cableSideTerminalId == Guid.Empty)
        {
            throw new ArgumentException(
                "Cable-side terminal ID cannot be empty.",
                nameof(cableSideTerminalId));
        }

        if (overheadSideTerminalId == Guid.Empty)
        {
            throw new ArgumentException(
                "Overhead-side terminal ID cannot be empty.",
                nameof(overheadSideTerminalId));
        }

        if (cableSideTerminalId == overheadSideTerminalId)
        {
            throw new ArgumentException(
                "A cable termination requires two different terminals.");
        }

        if (internalNodeId == Guid.Empty)
        {
            throw new ArgumentException(
                "Internal node ID cannot be empty.",
                nameof(internalNodeId));
        }

        if (string.IsNullOrWhiteSpace(voltageLevel))
        {
            throw new ArgumentException(
                "Cable termination voltage level is required.",
                nameof(voltageLevel));
        }

        if (!string.Equals(voltageLevel.Trim(), TenKilovolts, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Cable termination voltage level must be 10kV.",
                nameof(voltageLevel));
        }

        CableSideTerminalId = cableSideTerminalId;
        OverheadSideTerminalId = overheadSideTerminalId;
        InternalNodeId = internalNodeId;
        _terminalIds = [cableSideTerminalId, overheadSideTerminalId];
    }

    public Guid CableSideTerminalId { get; }

    public Guid OverheadSideTerminalId { get; }

    public Guid InternalNodeId { get; }

    public IReadOnlyList<Guid> TerminalIds => _terminalIds;

    public bool OwnsTerminal(Guid terminalId)
    {
        return _terminalIds.Contains(terminalId);
    }

    public bool OwnsInternalNode(Guid nodeId)
    {
        return InternalNodeId == nodeId;
    }
}
