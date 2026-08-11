using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Devices.SwitchAssemblies;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Domain.Documents;

public sealed class DrawingDocument
{
    private readonly List<Device> _devices = [];
    private readonly List<Terminal> _terminals = [];
    private readonly List<ElectricalNode> _electricalNodes = [];
    private readonly List<SwitchAssembly> _switchAssemblies = [];
    private readonly List<Connection> _connections = [];
    private readonly List<PoleAttachment> _poleAttachments = [];
    private readonly List<OverheadLine> _overheadLines = [];
    private readonly List<WorkScope> _workScopes = [];
    private readonly List<GroundingPoint> _groundingPoints = [];
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

    public IReadOnlyList<PoleAttachment> PoleAttachments => _poleAttachments;

    public IReadOnlyList<OverheadLine> OverheadLines => _overheadLines;

    public IReadOnlyList<WorkScope> WorkScopes => _workScopes;

    public IReadOnlyList<GroundingPoint> GroundingPoints => _groundingPoints;

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

        if (device.Type == DeviceType.Pole && device is not Pole)
        {
            throw new InvalidOperationException(
                "A pole device must use the Pole domain type.");
        }

        if (device.Type == DeviceType.CableTermination && device is not CableTermination)
        {
            throw new InvalidOperationException(
                "A cable termination device must use the CableTermination domain type.");
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

        if (electricalNode.OwnerType == TopologyOwnerType.Device &&
            _devices.Single(device => device.Id == electricalNode.OwnerId) is CableTermination termination &&
            !termination.OwnsInternalNode(electricalNode.Id))
        {
            throw new InvalidOperationException(
                $"Electrical node '{electricalNode.Id}' is not declared by cable termination '{termination.Id}'.");
        }

        if (electricalNode.OwnerType == TopologyOwnerType.Device &&
            _devices.Single(device => device.Id == electricalNode.OwnerId) is CableTermination cableTermination &&
            electricalNode.Type != ElectricalNodeType.Intermediate)
        {
            throw new InvalidOperationException(
                $"Cable termination '{cableTermination.Id}' internal node must be an intermediate node.");
        }

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

            if (owner is Pole pole && !pole.OwnsTerminal(terminal.Id))
            {
                throw new InvalidOperationException(
                    $"Terminal '{terminal.Id}' is not declared by pole '{owner.Id}'.");
            }

            if (owner is CableTermination cableTermination &&
                !cableTermination.OwnsTerminal(terminal.Id))
            {
                throw new InvalidOperationException(
                    $"Terminal '{terminal.Id}' is not declared by cable termination '{owner.Id}'.");
            }

            if (owner is CableTermination termination &&
                terminal.ElectricalNodeId != termination.InternalNodeId)
            {
                throw new InvalidOperationException(
                    $"Cable termination terminal '{terminal.Id}' must reference its internal node.");
            }

            if (owner is CableTermination cableTermination &&
                terminal.Id == cableTermination.CableSideTerminalId)
            {
                EnsureTerminalPolicy(
                    terminal,
                    ConnectionType.Cable,
                    "Cable termination cable-side terminal");
            }

            if (owner is CableTermination overheadTermination &&
                terminal.Id == overheadTermination.OverheadSideTerminalId)
            {
                EnsureTerminalPolicy(
                    terminal,
                    ConnectionType.OverheadLine,
                    "Cable termination overhead-side terminal");
            }

            if (owner is Pole)
            {
                EnsureTerminalPolicy(
                    terminal,
                    ConnectionType.OverheadLine,
                    "Pole anchor terminal",
                    allowMultipleConnections: true);
            }
        }

        ElectricalNode? electricalNode = null;

        if (terminal.ElectricalNodeId is Guid electricalNodeId)
        {
            electricalNode = _electricalNodes.FirstOrDefault(node => node.Id == electricalNodeId)
                ?? throw new InvalidOperationException(
                    $"Electrical node '{electricalNodeId}' does not exist.");

            if (terminal.OwnerType == TopologyOwnerType.Device &&
                _devices.Single(device => device.Id == terminal.OwnerId) is Pole pole &&
                (electricalNode.OwnerType != TopologyOwnerType.Device ||
                 electricalNode.OwnerId != pole.Id))
            {
                throw new InvalidOperationException(
                    $"Pole terminal '{terminal.Id}' must reference a node owned by pole '{pole.Id}'.");
            }

            if (terminal.OwnerType == TopologyOwnerType.Device &&
                _devices.Single(device => device.Id == terminal.OwnerId) is CableTermination termination &&
                (electricalNode.OwnerType != TopologyOwnerType.Device ||
                 electricalNode.OwnerId != termination.Id))
            {
                throw new InvalidOperationException(
                    $"Cable termination terminal '{terminal.Id}' must reference a node owned by cable termination '{termination.Id}'.");
            }
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

    public void AddPoleAttachment(PoleAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        EnsureObjectIdIsAvailable(attachment.AttachmentId, nameof(PoleAttachment));

        if (_devices.FirstOrDefault(device => device.Id == attachment.PoleId) is not Pole)
        {
            throw new InvalidOperationException(
                $"Pole '{attachment.PoleId}' does not exist.");
        }

        Device attachedDevice = _devices.FirstOrDefault(
                device => device.Id == attachment.AttachedDeviceId)
            ?? throw new InvalidOperationException(
                $"Attached device '{attachment.AttachedDeviceId}' does not exist.");

        if (attachedDevice is not SwitchDevice and not CableTermination)
        {
            throw new InvalidOperationException(
                "Only pole SwitchDevice or CableTermination can be attached to a pole.");
        }

        if (attachedDevice is SwitchDevice switchDevice &&
            switchDevice.InstallationType != SwitchInstallationType.Pole)
        {
            throw new InvalidOperationException(
                $"Switch '{switchDevice.Id}' is not a pole-installed switch.");
        }

        if (_poleAttachments.Any(existing =>
                existing.AttachedDeviceId == attachment.AttachedDeviceId))
        {
            throw new InvalidOperationException(
                $"Device '{attachment.AttachedDeviceId}' is already attached to a pole.");
        }

        _poleAttachments.Add(attachment);
    }

    public void AddOverheadLine(OverheadLine overheadLine)
    {
        ArgumentNullException.ThrowIfNull(overheadLine);

        if (_overheadLines.Any(existing => existing.ConnectionId == overheadLine.ConnectionId))
        {
            throw new InvalidOperationException(
                $"Connection '{overheadLine.ConnectionId}' already has an overhead line detail.");
        }

        Connection connection = _connections.FirstOrDefault(
                existing => existing.Id == overheadLine.ConnectionId)
            ?? throw new InvalidOperationException(
                $"Connection '{overheadLine.ConnectionId}' does not exist.");

        overheadLine.ValidateAgainst(connection);

        foreach (Guid poleId in overheadLine.SupportPoleIds)
        {
            if (_devices.FirstOrDefault(device => device.Id == poleId) is not Pole)
            {
                throw new InvalidOperationException(
                    $"Support pole '{poleId}' does not exist.");
            }
        }

        ValidateOverheadEndpoint(connection.StartTerminalId, overheadLine.SupportPoleIds[0]);
        ValidateOverheadEndpoint(
            connection.EndTerminalId,
            overheadLine.SupportPoleIds[^1]);

        _overheadLines.Add(overheadLine);
    }

    public WorkScope CreateWorkScope(
        Guid workScopeId,
        BoundaryPoint startBoundary,
        BoundaryPoint endBoundary,
        string description,
        IEnumerable<Guid>? groundingPointIds = null)
    {
        WorkScope workScope = WorkScope.Create(
            workScopeId,
            startBoundary,
            endBoundary,
            description,
            groundingPointIds);
        AddWorkScope(workScope);
        return workScope;
    }

    public void AddWorkScope(WorkScope workScope)
    {
        ArgumentNullException.ThrowIfNull(workScope);

        EnsureObjectIdIsAvailable(workScope.WorkScopeId, nameof(WorkScope));
        ValidateBoundaryPoint(workScope.StartBoundary);
        ValidateBoundaryPoint(workScope.EndBoundary);
        EnsureGroundingPointReferencesExist(workScope.GroundingPointIds);

        _workScopes.Add(workScope);
    }

    public void UpdateWorkScope(
        Guid workScopeId,
        BoundaryPoint startBoundary,
        BoundaryPoint endBoundary,
        string description,
        IEnumerable<Guid>? groundingPointIds = null)
    {
        WorkScope workScope = GetWorkScope(workScopeId);
        WorkScope replacement = WorkScope.Create(
            workScopeId,
            startBoundary,
            endBoundary,
            description,
            groundingPointIds);

        ValidateBoundaryPoint(replacement.StartBoundary);
        ValidateBoundaryPoint(replacement.EndBoundary);
        EnsureGroundingPointReferencesExist(replacement.GroundingPointIds);
        workScope.Update(
            replacement.StartBoundary,
            replacement.EndBoundary,
            replacement.Description,
            replacement.GroundingPointIds);
    }

    public void RemoveWorkScope(Guid workScopeId)
    {
        WorkScope workScope = GetWorkScope(workScopeId);
        _workScopes.Remove(workScope);
    }

    public GroundingPoint CreateGroundingPoint(
        Guid groundingPointId,
        Guid terminalId,
        string location,
        string? number = null,
        string? note = null)
    {
        GroundingPoint groundingPoint = GroundingPoint.Create(
            groundingPointId,
            terminalId,
            location,
            number,
            note);
        AddGroundingPoint(groundingPoint);
        return groundingPoint;
    }

    public void AddGroundingPoint(GroundingPoint groundingPoint)
    {
        ArgumentNullException.ThrowIfNull(groundingPoint);

        EnsureObjectIdIsAvailable(
            groundingPoint.GroundingPointId,
            nameof(GroundingPoint));
        ValidateGroundingPointTerminal(groundingPoint.TerminalId);

        if (_groundingPoints.Any(existing =>
                existing.TerminalId == groundingPoint.TerminalId))
        {
            throw new InvalidOperationException(
                $"Terminal '{groundingPoint.TerminalId}' already has a grounding point.");
        }

        _groundingPoints.Add(groundingPoint);
    }

    public void UpdateGroundingPoint(
        Guid groundingPointId,
        Guid terminalId,
        string location,
        string? number = null,
        string? note = null)
    {
        GroundingPoint groundingPoint = GetGroundingPoint(groundingPointId);
        GroundingPoint replacement = GroundingPoint.Create(
            groundingPointId,
            terminalId,
            location,
            number,
            note);

        ValidateGroundingPointTerminal(replacement.TerminalId);
        if (_groundingPoints.Any(existing =>
                existing.GroundingPointId != groundingPointId &&
                existing.TerminalId == replacement.TerminalId))
        {
            throw new InvalidOperationException(
                $"Terminal '{replacement.TerminalId}' already has a grounding point.");
        }

        groundingPoint.Update(
            replacement.TerminalId,
            replacement.Location,
            replacement.Number,
            replacement.Note);
    }

    public void RemoveGroundingPoint(Guid groundingPointId)
    {
        GroundingPoint groundingPoint = GetGroundingPoint(groundingPointId);
        if (_workScopes.Any(workScope =>
                workScope.GroundingPointIds.Contains(groundingPointId)))
        {
            throw new InvalidOperationException(
                $"Grounding point '{groundingPointId}' is still referenced by a work scope.");
        }

        _groundingPoints.Remove(groundingPoint);
    }

    private Terminal GetTerminal(Guid terminalId)
    {
        return _terminals.FirstOrDefault(terminal => terminal.Id == terminalId)
            ?? throw new InvalidOperationException($"Terminal '{terminalId}' does not exist.");
    }

    public WorkScope GetWorkScope(Guid workScopeId)
    {
        return _workScopes.FirstOrDefault(workScope => workScope.WorkScopeId == workScopeId)
            ?? throw new InvalidOperationException(
                $"Work scope '{workScopeId}' does not exist.");
    }

    public GroundingPoint GetGroundingPoint(Guid groundingPointId)
    {
        return _groundingPoints.FirstOrDefault(
                groundingPoint => groundingPoint.GroundingPointId == groundingPointId)
            ?? throw new InvalidOperationException(
                $"Grounding point '{groundingPointId}' does not exist.");
    }

    private void ValidateBoundaryPoint(BoundaryPoint boundaryPoint)
    {
        ArgumentNullException.ThrowIfNull(boundaryPoint);

        Device device = _devices.FirstOrDefault(candidate =>
                candidate.Id == boundaryPoint.DeviceId)
            ?? throw new InvalidOperationException(
                $"Boundary device '{boundaryPoint.DeviceId}' does not exist.");

        Terminal terminal = GetTerminal(boundaryPoint.TerminalId);
        if (terminal.OwnerType == TopologyOwnerType.Device)
        {
            if (terminal.OwnerId != device.Id)
            {
                throw new InvalidOperationException(
                    $"Boundary terminal '{terminal.Id}' is not owned by device '{device.Id}'.");
            }

            return;
        }

        if (terminal.OwnerType == TopologyOwnerType.InternalAggregate)
        {
            RingCabinetInterval? interval = _devices
                .OfType<RingCabinet>()
                .SelectMany(cabinet => cabinet.Intervals)
                .SingleOrDefault(candidate => candidate.IntervalId == terminal.OwnerId);

            if (interval is not null && interval.ParentCabinetId == device.Id)
            {
                return;
            }
        }

        throw new InvalidOperationException(
            $"Boundary terminal '{terminal.Id}' is not owned by or contained in device '{device.Id}'.");
    }

    private void ValidateGroundingPointTerminal(Guid terminalId)
    {
        _ = GetTerminal(terminalId);
    }

    private void EnsureGroundingPointReferencesExist(IEnumerable<Guid> groundingPointIds)
    {
        ArgumentNullException.ThrowIfNull(groundingPointIds);

        Guid[] ids = groundingPointIds.ToArray();
        if (ids.Distinct().Count() != ids.Length)
        {
            throw new InvalidOperationException(
                "A work scope cannot reference the same grounding point more than once.");
        }

        foreach (Guid groundingPointId in ids)
        {
            if (!_groundingPoints.Any(point =>
                    point.GroundingPointId == groundingPointId))
            {
                throw new InvalidOperationException(
                    $"Grounding point '{groundingPointId}' does not exist.");
            }
        }
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
            _poleAttachments.Any(attachment => attachment.AttachmentId == objectId) ||
            _workScopes.Any(workScope => workScope.WorkScopeId == objectId) ||
            _groundingPoints.Any(point => point.GroundingPointId == objectId) ||
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

    private static void EnsureTerminalPolicy(
        Terminal terminal,
        ConnectionType allowedConnectionType,
        string terminalDescription,
        bool allowMultipleConnections = false)
    {
        if (!terminal.IsExternal ||
            !allowMultipleConnections && terminal.AllowsMultipleConnections ||
            !terminal.AllowedConnectionTypes.SetEquals([allowedConnectionType]))
        {
            throw new InvalidOperationException(
                $"{terminalDescription} '{terminal.Id}' has an invalid connection policy.");
        }
    }

    private void ValidateOverheadEndpoint(Guid terminalId, Guid expectedPoleId)
    {
        Terminal terminal = GetTerminal(terminalId);

        if (terminal.OwnerType != TopologyOwnerType.Device)
        {
            return;
        }

        Device owner = _devices.Single(device => device.Id == terminal.OwnerId);

        Guid? physicalPoleId = owner switch
        {
            Pole pole => pole.Id,
            CableTermination termination => GetAttachedPoleId(termination.Id),
            SwitchDevice switchDevice when switchDevice.InstallationType == SwitchInstallationType.Pole =>
                GetAttachedPoleId(switchDevice.Id),
            _ => null
        };

        if (physicalPoleId is Guid poleId && poleId != expectedPoleId)
        {
            throw new InvalidOperationException(
                $"Overhead line endpoint '{terminalId}' is not physically located at support pole '{expectedPoleId}'.");
        }
    }

    private Guid GetAttachedPoleId(Guid deviceId)
    {
        return _poleAttachments.FirstOrDefault(
                attachment => attachment.AttachedDeviceId == deviceId)?.PoleId
            ?? throw new InvalidOperationException(
                $"Device '{deviceId}' must be attached to a pole before it is used by an overhead line.");
    }
}
