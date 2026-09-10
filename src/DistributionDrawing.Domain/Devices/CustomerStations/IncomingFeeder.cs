using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Domain.Devices.CustomerStations;

public sealed class IncomingFeeder
{
    public const string CableTerminalRole = "CableTerminal";

    public const string StationTerminalRole = "StationTerminal";

    public const string TenKilovolts = "10kV";

    public IncomingFeeder(
        Guid incomingFeederId,
        int sequence,
        string displayName,
        Guid cableTerminalId,
        Guid stationTerminalId,
        Guid electricalNodeId,
        SwitchDevice isolationSwitch,
        Terminal cableTerminal,
        Terminal stationTerminal,
        ElectricalNode electricalNode)
    {
        IncomingFeederId = incomingFeederId;
        Sequence = sequence;
        DisplayName = NormalizeDisplayName(displayName);
        CableTerminalId = cableTerminalId;
        StationTerminalId = stationTerminalId;
        ElectricalNodeId = electricalNodeId;
        IsolationSwitch = isolationSwitch ?? throw new ArgumentNullException(nameof(isolationSwitch));
        CableTerminal = cableTerminal ?? throw new ArgumentNullException(nameof(cableTerminal));
        StationTerminal = stationTerminal ?? throw new ArgumentNullException(nameof(stationTerminal));
        ElectricalNode = electricalNode ?? throw new ArgumentNullException(nameof(electricalNode));

        ValidateStructure();
    }

    public Guid IncomingFeederId { get; }

    public int Sequence { get; }

    public string DisplayName { get; private set; }

    public Guid CableTerminalId { get; }

    public Guid StationTerminalId { get; }

    public Guid ElectricalNodeId { get; }

    public SwitchDevice IsolationSwitch { get; }

    public Terminal CableTerminal { get; }

    public Terminal StationTerminal { get; }

    public ElectricalNode ElectricalNode { get; }

    public void Rename(string displayName)
    {
        DisplayName = NormalizeDisplayName(displayName);
    }

    internal void ValidateStructure()
    {
        if (IncomingFeederId == Guid.Empty)
        {
            throw new ArgumentException("Incoming feeder ID cannot be empty.", nameof(IncomingFeederId));
        }

        if (Sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Sequence));
        }

        Guid[] stableIds =
        [
            IncomingFeederId,
            CableTerminalId,
            StationTerminalId,
            ElectricalNodeId,
            IsolationSwitch.Id
        ];
        if (stableIds.Any(id => id == Guid.Empty) ||
            stableIds.Distinct().Count() != stableIds.Length)
        {
            throw new InvalidOperationException(
                "Incoming feeder aggregate IDs must be non-empty and unique.");
        }

        if (IsolationSwitch.SwitchKind != SwitchKind.IsolationSwitch)
        {
            throw new InvalidOperationException(
                "An incoming feeder switch must be an isolation switch.");
        }

        if (IsolationSwitch.InstallationType !=
                SwitchInstallationType.CustomerStationIncomingFeeder ||
            IsolationSwitch.ParentId != IncomingFeederId)
        {
            throw new InvalidOperationException(
                "The isolation switch owner must be its customer-station incoming feeder.");
        }

        if (IsolationSwitch.FirstTerminalId != CableTerminalId ||
            IsolationSwitch.SecondTerminalId != StationTerminalId)
        {
            throw new InvalidOperationException(
                "Incoming feeder terminal IDs do not match isolation-switch terminal order.");
        }

        ValidateCableTerminal();
        ValidateStationTerminal();
        ValidateElectricalNode();
    }

    private void ValidateCableTerminal()
    {
        if (CableTerminal.Id != CableTerminalId ||
            CableTerminal.OwnerType != TopologyOwnerType.Device ||
            CableTerminal.OwnerId != IsolationSwitch.Id ||
            !string.Equals(CableTerminal.Role, CableTerminalRole, StringComparison.Ordinal) ||
            !string.Equals(CableTerminal.VoltageLevel, TenKilovolts, StringComparison.OrdinalIgnoreCase) ||
            !CableTerminal.IsExternal ||
            CableTerminal.AllowsMultipleConnections ||
            CableTerminal.ElectricalNodeId is not null ||
            !CableTerminal.AllowedConnectionTypes.SetEquals([ConnectionType.Cable]))
        {
            throw new InvalidOperationException(
                "Incoming feeder cable terminal is inconsistent with its contract.");
        }
    }

    private void ValidateStationTerminal()
    {
        if (StationTerminal.Id != StationTerminalId ||
            StationTerminal.OwnerType != TopologyOwnerType.Device ||
            StationTerminal.OwnerId != IsolationSwitch.Id ||
            !string.Equals(StationTerminal.Role, StationTerminalRole, StringComparison.Ordinal) ||
            !string.Equals(StationTerminal.VoltageLevel, TenKilovolts, StringComparison.OrdinalIgnoreCase) ||
            StationTerminal.IsExternal ||
            StationTerminal.AllowsMultipleConnections ||
            StationTerminal.ElectricalNodeId != ElectricalNodeId ||
            StationTerminal.AllowedConnectionTypes.Count != 0)
        {
            throw new InvalidOperationException(
                "Incoming feeder station terminal is inconsistent with its contract.");
        }
    }

    private void ValidateElectricalNode()
    {
        if (ElectricalNode.Id != ElectricalNodeId ||
            ElectricalNode.Type != ElectricalNodeType.Circuit ||
            ElectricalNode.OwnerType != TopologyOwnerType.InternalAggregate ||
            ElectricalNode.OwnerId != IncomingFeederId ||
            (ElectricalNode.TerminalIds.Count != 0 &&
             !ElectricalNode.TerminalIds.SetEquals([StationTerminalId])))
        {
            throw new InvalidOperationException(
                "Incoming feeder electrical node is inconsistent with its contract.");
        }
    }

    private static string NormalizeDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException(
                "Incoming feeder display name is required.",
                nameof(displayName));
        }

        return displayName.Trim();
    }
}
