using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Domain.Devices;

public sealed class Pole : Device
{
    private const string TenKilovolts = "10kV";
    private const string OverheadAnchorRole = "OverheadAnchor";
    private readonly HashSet<Guid> _overheadAnchorTerminalIds = [];

    public Pole(
        Guid id,
        string poleNumber,
        string? displayName = null,
        PoleType poleType = PoleType.Cement,
        IEnumerable<Guid>? overheadAnchorTerminalIds = null)
        : base(id, DeviceType.Pole, displayName)
    {
        if (string.IsNullOrWhiteSpace(poleNumber))
        {
            throw new ArgumentException("Pole number is required.", nameof(poleNumber));
        }

        if (!Enum.IsDefined(poleType))
        {
            throw new ArgumentOutOfRangeException(nameof(poleType));
        }

        PoleNumber = poleNumber.Trim();
        PoleType = poleType;

        if (overheadAnchorTerminalIds is not null)
        {
            foreach (Guid terminalId in overheadAnchorTerminalIds)
            {
                RegisterOverheadAnchorTerminal(terminalId);
            }
        }
    }

    public Pole(
        Guid id,
        string poleNumber,
        PoleType poleType,
        string? displayName = null,
        IEnumerable<Guid>? overheadAnchorTerminalIds = null)
        : this(id, poleNumber, displayName, poleType, overheadAnchorTerminalIds)
    {
    }

    public string PoleNumber { get; private set; }

    public PoleType PoleType { get; }

    public void RenamePoleNumber(string poleNumber)
    {
        if (string.IsNullOrWhiteSpace(poleNumber))
        {
            throw new ArgumentException("Pole number is required.", nameof(poleNumber));
        }

        PoleNumber = poleNumber.Trim();
    }

    public IReadOnlySet<Guid> OverheadAnchorTerminalIds => _overheadAnchorTerminalIds;

    public bool OwnsTerminal(Guid terminalId)
    {
        return _overheadAnchorTerminalIds.Contains(terminalId);
    }

    public void RegisterOverheadAnchorTerminal(Guid terminalId)
    {
        if (terminalId == Guid.Empty)
        {
            throw new ArgumentException(
                "Overhead anchor terminal ID cannot be empty.",
                nameof(terminalId));
        }

        if (!_overheadAnchorTerminalIds.Add(terminalId))
        {
            throw new InvalidOperationException(
                $"Pole '{Id}' already owns overhead anchor terminal '{terminalId}'.");
        }
    }

    public Terminal CreateOverheadAnchorTerminal(
        Guid terminalId,
        bool allowsMultipleConnections = false,
        Guid? electricalNodeId = null)
    {
        Terminal terminal = new(
            terminalId,
            TopologyOwnerType.Device,
            Id,
            OverheadAnchorRole,
            TenKilovolts,
            true,
            allowsMultipleConnections,
            electricalNodeId,
            [ConnectionType.OverheadLine]);

        RegisterOverheadAnchorTerminal(terminalId);
        return terminal;
    }
}
