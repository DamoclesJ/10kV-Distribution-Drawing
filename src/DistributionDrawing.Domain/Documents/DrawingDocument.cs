using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Devices.SwitchAssemblies;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Domain.Documents;

public sealed class DrawingDocument
{
    private readonly List<Device> _devices = [];
    private readonly List<Terminal> _terminals = [];
    private readonly List<ElectricalNode> _electricalNodes = [];
    private readonly List<SwitchAssembly> _switchAssemblies = [];
    private readonly List<Connection> _connections = [];
    private readonly HashSet<Guid> _internalAggregateOwnerIds = [];

    public DrawingDocument(Guid id, string title)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Document ID cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Document title is required.", nameof(title));
        }

        Id = id;
        Title = title.Trim();
    }

    public Guid Id { get; }

    public string Title { get; private set; }

    public IReadOnlyList<Device> Devices => _devices;

    public IReadOnlyList<Terminal> Terminals => _terminals;

    public IReadOnlyList<ElectricalNode> ElectricalNodes => _electricalNodes;

    public IReadOnlyList<SwitchAssembly> SwitchAssemblies => _switchAssemblies;

    public IReadOnlyList<Connection> Connections => _connections;

    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Document title is required.", nameof(title));
        }

        Title = title.Trim();
    }

    public void AddDevice(Device device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (device is RingCabinet ringCabinet)
        {
            AddRingCabinet(ringCabinet);
            return;
        }

        EnsureObjectIdIsAvailable(device.Id, nameof(Device));

        if (device.Type == DeviceType.RingCabinet)
        {
            throw new InvalidOperationException(
                "A ring cabinet device must use the RingCabinet domain type.");
        }

        if (device.Type == DeviceType.Switch && device is not SwitchDevice)
        {
            throw new InvalidOperationException(
                "A switch device must use the SwitchDevice domain type.");
        }

        if (device.ParentId is Guid parentId &&
            !_internalAggregateOwnerIds.Contains(parentId))
        {
            throw new InvalidOperationException(
                $"Parent internal aggregate '{parentId}' does not exist.");
        }

        _devices.Add(device);
    }

    public void AddElectricalNode(ElectricalNode electricalNode)
    {
        ArgumentNullException.ThrowIfNull(electricalNode);

        EnsureObjectIdIsAvailable(electricalNode.Id, nameof(ElectricalNode));
        EnsureTopologyOwnerExists(electricalNode.OwnerType, electricalNode.OwnerId);

        _electricalNodes.Add(electricalNode);
    }

    public void AddTerminal(Terminal terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);

        EnsureObjectIdIsAvailable(terminal.Id, nameof(Terminal));
        EnsureTopologyOwnerExists(terminal.OwnerType, terminal.OwnerId);

        if (terminal.OwnerType == TopologyOwnerType.Device)
        {
            Device owner = _devices.Single(device => device.Id == terminal.OwnerId);

            if (owner is SwitchDevice switchDevice && !switchDevice.OwnsTerminal(terminal.Id))
            {
                throw new InvalidOperationException(
                    $"Terminal '{terminal.Id}' is not declared by switch '{owner.Id}'.");
            }
        }

        ElectricalNode? electricalNode = null;

        if (terminal.ElectricalNodeId is Guid electricalNodeId)
        {
            electricalNode = _electricalNodes.FirstOrDefault(node => node.Id == electricalNodeId)
                ?? throw new InvalidOperationException(
                    $"Electrical node '{electricalNodeId}' does not exist.");
        }

        _terminals.Add(terminal);
        electricalNode?.AttachTerminal(terminal.Id);
    }

    public void AddConnection(Connection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        EnsureObjectIdIsAvailable(connection.Id, nameof(Connection));

        Terminal start = GetTerminal(connection.StartTerminalId);
        Terminal end = GetTerminal(connection.EndTerminalId);

        EnsureTerminalAcceptsConnection(start, connection);
        EnsureTerminalAcceptsConnection(end, connection);

        _connections.Add(connection);
    }

    private Terminal GetTerminal(Guid terminalId)
    {
        return _terminals.FirstOrDefault(terminal => terminal.Id == terminalId)
            ?? throw new InvalidOperationException($"Terminal '{terminalId}' does not exist.");
    }

    private void AddRingCabinet(RingCabinet ringCabinet)
    {
        ringCabinet.ValidateStructure();

        SwitchDevice[] internalSwitches = ringCabinet.InternalSwitchDevices.ToArray();
        SwitchAssembly[] internalAssemblies = ringCabinet.InternalSwitchAssemblies.ToArray();

        EnsureObjectIdIsAvailable(ringCabinet.Id, nameof(RingCabinet));

        foreach (RingCabinetInterval interval in ringCabinet.Intervals)
        {
            EnsureObjectIdIsAvailable(interval.IntervalId, nameof(RingCabinetInterval));
        }

        foreach (SwitchDevice switchDevice in internalSwitches)
        {
            EnsureObjectIdIsAvailable(switchDevice.Id, nameof(SwitchDevice));
        }

        foreach (SwitchAssembly switchAssembly in internalAssemblies)
        {
            EnsureObjectIdIsAvailable(switchAssembly.AssemblyId, nameof(SwitchAssembly));
        }

        foreach (ElectricalNode electricalNode in ringCabinet.ElectricalNodes)
        {
            EnsureObjectIdIsAvailable(electricalNode.Id, nameof(ElectricalNode));
        }

        foreach (Terminal terminal in ringCabinet.Terminals)
        {
            EnsureObjectIdIsAvailable(terminal.Id, nameof(Terminal));
        }

        _devices.Add(ringCabinet);

        foreach (RingCabinetInterval interval in ringCabinet.Intervals)
        {
            _internalAggregateOwnerIds.Add(interval.IntervalId);
        }

        _devices.AddRange(internalSwitches);
        _switchAssemblies.AddRange(internalAssemblies);
        _electricalNodes.AddRange(ringCabinet.ElectricalNodes);
        _terminals.AddRange(ringCabinet.Terminals);
    }

    private void EnsureTopologyOwnerExists(TopologyOwnerType ownerType, Guid ownerId)
    {
        bool ownerExists = ownerType switch
        {
            TopologyOwnerType.Device => _devices.Any(device => device.Id == ownerId),
            TopologyOwnerType.InternalAggregate => _internalAggregateOwnerIds.Contains(ownerId),
            _ => false
        };

        if (!ownerExists)
        {
            throw new InvalidOperationException(
                $"Topology owner '{ownerId}' of type '{ownerType}' does not exist.");
        }
    }

    private void EnsureObjectIdIsAvailable(Guid objectId, string objectName)
    {
        if (_devices.Any(device => device.Id == objectId) ||
            _terminals.Any(terminal => terminal.Id == objectId) ||
            _electricalNodes.Any(node => node.Id == objectId) ||
            _switchAssemblies.Any(assembly => assembly.AssemblyId == objectId) ||
            _connections.Any(connection => connection.Id == objectId) ||
            _internalAggregateOwnerIds.Contains(objectId))
        {
            throw new InvalidOperationException($"{objectName} ID '{objectId}' is already in use.");
        }
    }

    private void EnsureTerminalAcceptsConnection(Terminal terminal, Connection connection)
    {
        if (!terminal.Allows(connection.Type))
        {
            throw new InvalidOperationException(
                $"Terminal '{terminal.Id}' does not allow connection type '{connection.Type}'.");
        }

        if (!string.Equals(
                terminal.VoltageLevel,
                connection.VoltageLevel,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Terminal '{terminal.Id}' voltage level is incompatible with the connection.");
        }

        if (!terminal.AllowsMultipleConnections &&
            _connections.Any(existing => existing.UsesTerminal(terminal.Id)))
        {
            throw new InvalidOperationException(
                $"Terminal '{terminal.Id}' already has a connection.");
        }
    }
}
