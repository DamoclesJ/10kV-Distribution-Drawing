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
    private readonly List<CableSegment> _cableSegments = [];
    private readonly List<IntermediateTerminal> _intermediateTerminals = [];
    private readonly List<PoleAttachment> _poleAttachments = [];
    private readonly List<OverheadLine> _overheadLines = [];
    private readonly List<WorkScope> _workScopes = [];
    private readonly List<GroundingPoint> _groundingPoints = [];
    private readonly List<GroundingAccessPoint> _groundingAccessPoints = [];
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

    public IReadOnlyList<CableSegment> CableSegments => _cableSegments;

    public IReadOnlyList<IntermediateTerminal> IntermediateTerminals =>
        _intermediateTerminals;

    public IReadOnlyList<PoleAttachment> PoleAttachments => _poleAttachments;

    public IReadOnlyList<OverheadLine> OverheadLines => _overheadLines;

    public IReadOnlyList<WorkScope> WorkScopes => _workScopes;

    public IReadOnlyList<GroundingPoint> GroundingPoints => _groundingPoints;

    public IReadOnlyList<GroundingAccessPoint> GroundingAccessPoints => _groundingAccessPoints;

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

    public void SynchronizeRingCabinetAggregate(RingCabinet ringCabinet)
    {
        ArgumentNullException.ThrowIfNull(ringCabinet);

        RingCabinet registeredCabinet = _devices.OfType<RingCabinet>()
            .SingleOrDefault(candidate => candidate.Id == ringCabinet.Id)
            ?? throw new InvalidOperationException(
                $"Ring cabinet '{ringCabinet.Id}' is not registered in this document.");
        if (!ReferenceEquals(registeredCabinet, ringCabinet))
        {
            throw new InvalidOperationException(
                "Only the registered ring-cabinet aggregate can be synchronized.");
        }

        ringCabinet.ValidateStructure();
        HashSet<Guid> intervalIds = ringCabinet.Intervals
            .Select(interval => interval.IntervalId)
            .ToHashSet();
        SwitchDevice[] previousSwitches = _devices.OfType<SwitchDevice>()
            .Where(device => device.ParentId is Guid parentId && intervalIds.Contains(parentId))
            .ToArray();
        HashSet<Guid> previousSwitchIds = previousSwitches
            .Select(device => device.Id)
            .ToHashSet();
        SwitchAssembly[] previousAssemblies = _switchAssemblies
            .Where(assembly => intervalIds.Contains(assembly.ParentIntervalId))
            .ToArray();
        ElectricalNode[] previousNodes = _electricalNodes
            .Where(node =>
                (node.OwnerType == TopologyOwnerType.InternalAggregate &&
                 intervalIds.Contains(node.OwnerId)) ||
                (node.OwnerType == TopologyOwnerType.Device &&
                 previousSwitchIds.Contains(node.OwnerId)))
            .ToArray();
        Terminal[] previousTerminals = _terminals
            .Where(terminal =>
                (terminal.OwnerType == TopologyOwnerType.InternalAggregate &&
                 intervalIds.Contains(terminal.OwnerId)) ||
                (terminal.OwnerType == TopologyOwnerType.Device &&
                 previousSwitchIds.Contains(terminal.OwnerId)))
            .ToArray();

        SwitchDevice[] replacementSwitches = ringCabinet.InternalSwitchDevices.ToArray();
        HashSet<Guid> replacementSwitchIds = replacementSwitches
            .Select(device => device.Id)
            .ToHashSet();
        SwitchAssembly[] replacementAssemblies = ringCabinet.InternalSwitchAssemblies.ToArray();
        ElectricalNode[] replacementNodes = ringCabinet.ElectricalNodes
            .Where(node =>
                (node.OwnerType == TopologyOwnerType.InternalAggregate &&
                 intervalIds.Contains(node.OwnerId)) ||
                (node.OwnerType == TopologyOwnerType.Device &&
                 replacementSwitchIds.Contains(node.OwnerId)))
            .ToArray();
        Terminal[] replacementTerminals = ringCabinet.Terminals
            .Where(terminal =>
                (terminal.OwnerType == TopologyOwnerType.InternalAggregate &&
                 intervalIds.Contains(terminal.OwnerId)) ||
                (terminal.OwnerType == TopologyOwnerType.Device &&
                 replacementSwitchIds.Contains(terminal.OwnerId)))
            .ToArray();

        HashSet<Guid> replacementTerminalIds = replacementTerminals
            .Select(terminal => terminal.Id)
            .ToHashSet();
        HashSet<Guid> retiredTerminalIds = previousTerminals
            .Select(terminal => terminal.Id)
            .Where(id => !replacementTerminalIds.Contains(id))
            .ToHashSet();
        if (_connections.Any(connection =>
                retiredTerminalIds.Contains(connection.StartTerminalId) ||
                retiredTerminalIds.Contains(connection.EndTerminalId)) ||
            _groundingPoints.Any(point => TargetsAnyTerminal(point, retiredTerminalIds)) ||
            _workScopes.Any(scope =>
                retiredTerminalIds.Contains(scope.StartBoundary.TerminalId) ||
                retiredTerminalIds.Contains(scope.EndBoundary.TerminalId)))
        {
            throw new InvalidOperationException(
                "The ring-cabinet cable terminal is still referenced and cannot be removed or replaced.");
        }

        Guid[] replacementIds = replacementSwitches.Select(device => device.Id)
            .Concat(replacementAssemblies.Select(assembly => assembly.AssemblyId))
            .Concat(replacementNodes.Select(node => node.Id))
            .Concat(replacementTerminals.Select(terminal => terminal.Id))
            .ToArray();
        if (replacementIds.Distinct().Count() != replacementIds.Length)
        {
            throw new InvalidOperationException(
                "Ring-cabinet replacement objects must have unique stable IDs.");
        }

        EnsureReplacementIdsAreAvailable(
            replacementSwitches.Select(device => device.Id),
            previousSwitches.Select(device => device.Id));
        EnsureReplacementIdsAreAvailable(
            replacementAssemblies.Select(assembly => assembly.AssemblyId),
            previousAssemblies.Select(assembly => assembly.AssemblyId));
        EnsureReplacementIdsAreAvailable(
            replacementNodes.Select(node => node.Id),
            previousNodes.Select(node => node.Id));
        EnsureReplacementIdsAreAvailable(
            replacementTerminals.Select(terminal => terminal.Id),
            previousTerminals.Select(terminal => terminal.Id));

        _devices.RemoveAll(device => previousSwitches.Contains(device));
        _switchAssemblies.RemoveAll(previousAssemblies.Contains);
        _electricalNodes.RemoveAll(previousNodes.Contains);
        _terminals.RemoveAll(previousTerminals.Contains);
        _devices.AddRange(replacementSwitches);
        _switchAssemblies.AddRange(replacementAssemblies);
        _electricalNodes.AddRange(replacementNodes);
        _terminals.AddRange(replacementTerminals);
    }

    private void EnsureReplacementIdsAreAvailable(
        IEnumerable<Guid> replacementIds,
        IEnumerable<Guid> previousIds)
    {
        HashSet<Guid> permittedIds = previousIds.ToHashSet();
        foreach (Guid replacementId in replacementIds.Where(id => !permittedIds.Contains(id)))
        {
            EnsureObjectIdIsAvailable(replacementId, "Ring-cabinet aggregate object");
        }
    }

    public void RemoveDevice(Guid deviceId)
    {
        Device device = _devices.SingleOrDefault(candidate => candidate.Id == deviceId)
            ?? throw new InvalidOperationException($"Device '{deviceId}' does not exist.");
        if (device is not Pole and not RingCabinet)
        {
            throw new InvalidOperationException(
                "Only Pole and RingCabinet removal is supported in this phase.");
        }

        HashSet<Guid> aggregateDeviceIds = [deviceId];
        HashSet<Guid> aggregateOwnerIds = [deviceId];
        if (device is RingCabinet cabinet)
        {
            aggregateDeviceIds.UnionWith(cabinet.InternalSwitchDevices.Select(item => item.Id));
            aggregateOwnerIds.UnionWith(cabinet.Intervals.Select(item => item.IntervalId));
        }

        HashSet<Guid> terminalIds = _terminals
            .Where(terminal => aggregateOwnerIds.Contains(terminal.OwnerId) ||
                aggregateDeviceIds.Contains(terminal.OwnerId))
            .Select(terminal => terminal.Id)
            .ToHashSet();

        if (_connections.Any(connection =>
                terminalIds.Contains(connection.StartTerminalId) ||
                terminalIds.Contains(connection.EndTerminalId)))
        {
            throw new InvalidOperationException(
                $"Device '{deviceId}' is still referenced by a connection.");
        }

        if (_poleAttachments.Any(attachment =>
                attachment.PoleId == deviceId ||
                aggregateDeviceIds.Contains(attachment.AttachedDeviceId)))
        {
            throw new InvalidOperationException(
                $"Device '{deviceId}' is still referenced by a pole attachment.");
        }

        if (device is Pole && _overheadLines.Any(line => line.SupportPoleIds.Contains(deviceId)))
        {
            throw new InvalidOperationException(
                $"Pole '{deviceId}' is still referenced by an overhead line.");
        }

        if (_groundingPoints.Any(point => TargetsAnyTerminal(point, terminalIds)) ||
            _workScopes.Any(scope =>
                aggregateDeviceIds.Contains(scope.StartBoundary.DeviceId) ||
                aggregateDeviceIds.Contains(scope.EndBoundary.DeviceId) ||
                terminalIds.Contains(scope.StartBoundary.TerminalId) ||
                terminalIds.Contains(scope.EndBoundary.TerminalId)))
        {
            throw new InvalidOperationException(
                $"Device '{deviceId}' is still referenced by Professional data.");
        }

        if (device is RingCabinet ringCabinet)
        {
            _terminals.RemoveAll(terminal => terminalIds.Contains(terminal.Id));
            _electricalNodes.RemoveAll(node =>
                aggregateOwnerIds.Contains(node.OwnerId) ||
                aggregateDeviceIds.Contains(node.OwnerId));
            _switchAssemblies.RemoveAll(assembly =>
                ringCabinet.InternalSwitchAssemblies.Any(item => item.AssemblyId == assembly.AssemblyId));
            _devices.RemoveAll(item => aggregateDeviceIds.Contains(item.Id));
            foreach (RingCabinetInterval interval in ringCabinet.Intervals)
            {
                _internalAggregateOwnerIds.Remove(interval.IntervalId);
            }

            return;
        }

        _terminals.RemoveAll(terminal => terminalIds.Contains(terminal.Id));
        _electricalNodes.RemoveAll(node => node.OwnerId == deviceId);
        _devices.Remove(device);
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

    public void AddIntermediateTerminal(
        IntermediateTerminal intermediateTerminal,
        Terminal terminal)
    {
        ArgumentNullException.ThrowIfNull(intermediateTerminal);
        ArgumentNullException.ThrowIfNull(terminal);

        if (intermediateTerminal.TerminalId != terminal.Id ||
            terminal.OwnerType != TopologyOwnerType.IntermediateTerminal ||
            terminal.OwnerId != intermediateTerminal.Id)
        {
            throw new InvalidOperationException(
                $"Intermediate terminal '{intermediateTerminal.Id}' owner relation is inconsistent.");
        }

        if (terminal.ElectricalNodeId is not null)
        {
            throw new InvalidOperationException(
                $"Intermediate terminal '{intermediateTerminal.Id}' cannot own an electrical node.");
        }

        if (!terminal.AllowsMultipleConnections)
        {
            throw new InvalidOperationException(
                $"Intermediate terminal '{intermediateTerminal.Id}' must allow multiple connections.");
        }

        EnsureObjectIdIsAvailable(intermediateTerminal.Id, nameof(IntermediateTerminal));
        EnsureObjectIdIsAvailable(terminal.Id, nameof(Terminal));

        EnsureTerminalPolicy(
            terminal,
            ConnectionType.Cable,
            "Intermediate terminal",
            allowMultipleConnections: true);

        _intermediateTerminals.Add(intermediateTerminal);
        try
        {
            AddTerminal(terminal);
        }
        catch
        {
            _intermediateTerminals.Remove(intermediateTerminal);
            throw;
        }
    }

    public IntermediateTerminal? FindIntermediateTerminal(Guid intermediateTerminalId)
    {
        return _intermediateTerminals.SingleOrDefault(existing =>
            existing.Id == intermediateTerminalId);
    }

    public void RemoveIntermediateTerminal(Guid intermediateTerminalId)
    {
        IntermediateTerminal intermediateTerminal =
            FindIntermediateTerminal(intermediateTerminalId)
            ?? throw new InvalidOperationException(
                $"Intermediate terminal '{intermediateTerminalId}' does not exist.");
        Guid terminalId = intermediateTerminal.TerminalId;
        HashSet<Guid> referencedConnectionIds = _connections
            .Where(connection => connection.UsesTerminal(terminalId))
            .Select(connection => connection.Id)
            .ToHashSet();

        if (referencedConnectionIds.Count > 0 ||
            _cableSegments.Any(segment =>
                referencedConnectionIds.Contains(segment.ConnectionId) ||
                segment.StartTerminalId == terminalId ||
                segment.EndTerminalId == terminalId))
        {
            throw new InvalidOperationException(
                $"Intermediate terminal '{intermediateTerminalId}' is still referenced by a connection or cable segment.");
        }

        Terminal terminal = _terminals.SingleOrDefault(existing => existing.Id == terminalId)
            ?? throw new InvalidOperationException(
                $"Intermediate terminal '{intermediateTerminalId}' child terminal is missing.");
        _terminals.Remove(terminal);
        _intermediateTerminals.Remove(intermediateTerminal);
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
                terminal.Id == termination.CableSideTerminalId &&
                terminal.ElectricalNodeId != termination.InternalNodeId)
            {
                throw new InvalidOperationException(
                    $"Cable termination terminal '{terminal.Id}' must reference its internal node.");
            }

            if (owner is CableTermination cableSideTermination &&
                terminal.Id == cableSideTermination.CableSideTerminalId)
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
                terminal.Id == termination.CableSideTerminalId &&
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

        if (_connections.Any(existing =>
                existing.UsesTerminal(connection.StartTerminalId) &&
                existing.UsesTerminal(connection.EndTerminalId)))
        {
            throw new InvalidOperationException(
                "A connection between the selected terminals already exists.");
        }

        if (start.OwnerType == end.OwnerType && start.OwnerId == end.OwnerId)
        {
            throw new InvalidOperationException(
                "An external connection cannot connect two terminals of the same topology owner.");
        }

        if (start.ElectricalNodeId is Guid startNodeId &&
            end.ElectricalNodeId == startNodeId)
        {
            throw new InvalidOperationException(
                "An external connection cannot reconnect terminals on the same electrical node.");
        }

        EnsureTerminalAcceptsConnection(start, connection);
        EnsureTerminalAcceptsConnection(end, connection);

        _connections.Add(connection);
    }

    public void AddCableSegment(CableSegment cableSegment, Connection connection)
    {
        ArgumentNullException.ThrowIfNull(cableSegment);
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.Type != ConnectionType.Cable)
        {
            throw new InvalidOperationException(
                "A cable segment requires a cable connection.");
        }

        if (cableSegment.ConnectionId != connection.Id ||
            cableSegment.StartTerminalId != connection.StartTerminalId ||
            cableSegment.EndTerminalId != connection.EndTerminalId ||
            !string.Equals(
                cableSegment.VoltageLevel,
                connection.VoltageLevel,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Cable segment and connection facts are inconsistent.");
        }

        EnsureObjectIdIsAvailable(cableSegment.Id, nameof(CableSegment));
        if (_cableSegments.Any(existing => existing.ConnectionId == connection.Id))
        {
            throw new InvalidOperationException(
                $"Connection '{connection.Id}' already has a cable segment.");
        }

        AddConnection(connection);
        try
        {
            _cableSegments.Add(cableSegment);
        }
        catch
        {
            _connections.Remove(connection);
            throw;
        }
    }

    public CableSegment RemoveCableSegment(Guid cableSegmentId)
    {
        CableSegment cableSegment = _cableSegments.SingleOrDefault(existing =>
                existing.Id == cableSegmentId)
            ?? throw new InvalidOperationException(
                $"Cable segment '{cableSegmentId}' does not exist.");

        Connection connection = _connections.SingleOrDefault(existing =>
                existing.Id == cableSegment.ConnectionId)
            ?? throw new InvalidOperationException(
                $"Cable segment '{cableSegmentId}' connection is missing.");

        _cableSegments.Remove(cableSegment);
        try
        {
            RemoveConnection(connection.Id);
        }
        catch
        {
            _cableSegments.Add(cableSegment);
            throw;
        }

        return cableSegment;
    }

    public void ReplaceCableSegmentConnection(
        CableSegment beforeCableSegment,
        Connection beforeConnection,
        CableSegment afterCableSegment,
        Connection afterConnection)
    {
        ArgumentNullException.ThrowIfNull(beforeCableSegment);
        ArgumentNullException.ThrowIfNull(beforeConnection);
        ArgumentNullException.ThrowIfNull(afterCableSegment);
        ArgumentNullException.ThrowIfNull(afterConnection);

        if (beforeCableSegment.Id != afterCableSegment.Id)
        {
            throw new InvalidOperationException(
                "Reconnect must preserve the cable segment ID.");
        }

        if (beforeConnection.Id != afterConnection.Id)
        {
            throw new InvalidOperationException(
                "Reconnect must preserve the connection ID.");
        }

        ValidateCableSegmentConnection(beforeCableSegment, beforeConnection);
        ValidateCableSegmentConnection(afterCableSegment, afterConnection);

        ValidateCableReconnectConnection(afterConnection, beforeConnection.Id);

        CableSegment currentCableSegment = _cableSegments.SingleOrDefault(segment =>
                segment.Id == beforeCableSegment.Id)
            ?? throw new InvalidOperationException(
                $"Cable segment '{beforeCableSegment.Id}' does not exist.");
        Connection currentConnection = _connections.SingleOrDefault(connection =>
                connection.Id == beforeConnection.Id)
            ?? throw new InvalidOperationException(
                $"Connection '{beforeConnection.Id}' does not exist.");

        if (!CableSegmentFactsEqual(currentCableSegment, beforeCableSegment) ||
            !ConnectionFactsEqual(currentConnection, beforeConnection))
        {
            throw new InvalidOperationException(
                "The reconnect before state does not match the document.");
        }

        RemoveCableSegment(beforeCableSegment.Id);
        try
        {
            AddCableSegment(afterCableSegment, afterConnection);
        }
        catch
        {
            AddCableSegment(beforeCableSegment, beforeConnection);
            throw;
        }
    }

    public SwitchStateChangeResult ChangeSwitchState(
        Guid switchDeviceId,
        SwitchState targetState)
    {
        if (!Enum.IsDefined(targetState))
        {
            throw new ArgumentOutOfRangeException(nameof(targetState));
        }

        RingCabinetInterval? cabinetInterval = _devices
            .OfType<RingCabinet>()
            .SelectMany(cabinet => cabinet.Intervals)
            .SingleOrDefault(interval => interval.SwitchDevices.Any(device =>
                device.Id == switchDeviceId));
        SwitchDevice? switchDevice = cabinetInterval?.SwitchDevices.Single(device =>
            device.Id == switchDeviceId);
        switchDevice ??= _devices
            .OfType<SwitchDevice>()
            .SingleOrDefault(device =>
                device.Id == switchDeviceId &&
                device.InstallationType == SwitchInstallationType.Pole);
        if (switchDevice is null)
        {
            throw new InvalidOperationException(
                $"Switch '{switchDeviceId}' does not exist.");
        }

        SwitchState previousState = switchDevice.SwitchState
            ?? throw new InvalidOperationException(
                $"Switch '{switchDeviceId}' has no switch state.");

        switch (switchDevice.InstallationType)
        {
            case SwitchInstallationType.Pole:
                if (_switchAssemblies.Any(assembly =>
                        assembly.MemberSwitchIds.Contains(switchDeviceId)))
                {
                    throw new InvalidOperationException(
                        $"Pole switch '{switchDeviceId}' cannot belong to a switch assembly.");
                }

                switchDevice.SetSwitchState(targetState);
                break;

            case SwitchInstallationType.CabinetInterval:
                SwitchAssembly assembly = cabinetInterval?.SwitchAssembly
                    ?? throw new InvalidOperationException(
                        $"Cabinet switch '{switchDeviceId}' has no switch assembly.");
                assembly.ChangeSwitchState(switchDeviceId, targetState);
                break;

            default:
                throw new InvalidOperationException(
                    $"Switch '{switchDeviceId}' has an unsupported installation type.");
        }

        return new SwitchStateChangeResult(
            switchDevice,
            previousState,
            targetState);
    }

    public Connection RemoveConnection(Guid connectionId)
    {
        Connection connection = _connections.SingleOrDefault(existing => existing.Id == connectionId)
            ?? throw new InvalidOperationException(
                $"Connection '{connectionId}' does not exist.");
        if (_overheadLines.Any(line => line.ConnectionId == connectionId))
        {
            throw new InvalidOperationException(
                $"Connection '{connectionId}' still has an overhead-line detail.");
        }

        if (_cableSegments.Any(segment => segment.ConnectionId == connectionId))
        {
            throw new InvalidOperationException(
                $"Connection '{connectionId}' still has a cable segment.");
        }

        _connections.Remove(connection);
        return connection;
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

    public void AddPoleSwitchAttachment(
        SwitchDevice switchDevice,
        Terminal firstTerminal,
        Terminal secondTerminal,
        PoleAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(switchDevice);
        ArgumentNullException.ThrowIfNull(firstTerminal);
        ArgumentNullException.ThrowIfNull(secondTerminal);
        ArgumentNullException.ThrowIfNull(attachment);

        if (switchDevice.InstallationType != SwitchInstallationType.Pole)
        {
            throw new InvalidOperationException(
                $"Switch '{switchDevice.Id}' is not a pole-installed switch.");
        }

        if (firstTerminal.OwnerId != switchDevice.Id ||
            secondTerminal.OwnerId != switchDevice.Id ||
            !switchDevice.OwnsTerminal(firstTerminal.Id) ||
            !switchDevice.OwnsTerminal(secondTerminal.Id) ||
            firstTerminal.Id == secondTerminal.Id)
        {
            throw new InvalidOperationException(
                $"Switch '{switchDevice.Id}' terminal aggregate is inconsistent.");
        }

        if (attachment.PoleId == attachment.AttachedDeviceId ||
            attachment.AttachedDeviceId != switchDevice.Id)
        {
            throw new InvalidOperationException(
                $"Attachment '{attachment.AttachmentId}' does not reference the switch.");
        }

        if (_devices.FirstOrDefault(device => device.Id == attachment.PoleId) is not Pole)
        {
            throw new InvalidOperationException(
                $"Pole '{attachment.PoleId}' does not exist.");
        }

        if (_poleAttachments.Any(existing =>
                existing.AttachedDeviceId == switchDevice.Id))
        {
            throw new InvalidOperationException(
                $"Device '{switchDevice.Id}' is already attached to a pole.");
        }

        Guid switchRightNodeId = secondTerminal.ElectricalNodeId ?? Guid.NewGuid();
        ElectricalNode switchRightNode = new(
            switchRightNodeId,
            ElectricalNodeType.Intermediate,
            TopologyOwnerType.Device,
            switchDevice.Id);

        EnsureObjectIdIsAvailable(switchDevice.Id, nameof(SwitchDevice));
        EnsureObjectIdIsAvailable(switchRightNode.Id, nameof(ElectricalNode));
        EnsureObjectIdIsAvailable(firstTerminal.Id, nameof(Terminal));
        EnsureObjectIdIsAvailable(secondTerminal.Id, nameof(Terminal));
        EnsureObjectIdIsAvailable(attachment.AttachmentId, nameof(PoleAttachment));

        Pole pole = (Pole)_devices.Single(device => device.Id == attachment.PoleId);
        Guid? poleJunctionNodeId = pole.OverheadAnchorTerminalIds.Count > 0
            ? EnsurePoleJunction(attachment.PoleId)
            : null;

        _devices.Add(switchDevice);
        _electricalNodes.Add(switchRightNode);
        try
        {
            AddTerminal(poleJunctionNodeId is Guid junctionNodeId
                ? BindTerminal(firstTerminal, junctionNodeId)
                : firstTerminal);
            AddTerminal(BindTerminal(secondTerminal, switchRightNodeId));
            AddPoleAttachment(attachment);
        }
        catch
        {
            _poleAttachments.Remove(attachment);
            _terminals.RemoveAll(terminal =>
                terminal.Id == firstTerminal.Id || terminal.Id == secondTerminal.Id);
            _electricalNodes.Remove(switchRightNode);
            _devices.Remove(switchDevice);
            throw;
        }
    }

    public void RemovePoleSwitchAttachment(Guid attachmentId)
    {
        PoleAttachment attachment = _poleAttachments.SingleOrDefault(existing =>
                existing.AttachmentId == attachmentId)
            ?? throw new InvalidOperationException(
                $"Pole attachment '{attachmentId}' does not exist.");
        SwitchDevice switchDevice = _devices.SingleOrDefault(device =>
                device.Id == attachment.AttachedDeviceId) as SwitchDevice
            ?? throw new InvalidOperationException(
                $"Attachment '{attachmentId}' does not reference a switch.");

        Guid[] terminalIds = [.. switchDevice.TerminalIds];
        if (_connections.Any(connection =>
                terminalIds.Contains(connection.StartTerminalId) ||
                terminalIds.Contains(connection.EndTerminalId)))
        {
            throw new InvalidOperationException(
                $"Switch '{switchDevice.Id}' is still referenced by a connection.");
        }

        if (_groundingPoints.Any(point => TargetsAnyTerminal(point, terminalIds)) ||
            _workScopes.Any(scope => terminalIds.Contains(scope.StartBoundary.TerminalId) ||
                terminalIds.Contains(scope.EndBoundary.TerminalId)))
        {
            throw new InvalidOperationException(
                $"Switch '{switchDevice.Id}' is still referenced by professional data.");
        }

        _poleAttachments.Remove(attachment);
        _terminals.RemoveAll(terminal => terminalIds.Contains(terminal.Id));
        _electricalNodes.RemoveAll(node => node.OwnerId == switchDevice.Id);
        _devices.Remove(switchDevice);
    }

    /// <summary>
    /// Removes a pole switch while restoring the overhead connections that it
    /// controls to the pole junction. The operation is atomic and preserves
    /// every connection and overhead-line identity.
    /// </summary>
    public void RemovePoleSwitchAndBypass(Guid attachmentId)
    {
        PoleAttachment attachment = _poleAttachments.SingleOrDefault(item =>
                item.AttachmentId == attachmentId)
            ?? throw new InvalidOperationException(
                $"Pole attachment '{attachmentId}' does not exist.");
        SwitchDevice switchDevice = _devices.SingleOrDefault(item =>
                item.Id == attachment.AttachedDeviceId) as SwitchDevice
            ?? throw new InvalidOperationException(
                $"Attachment '{attachmentId}' does not reference a switch.");
        Pole pole = _devices.SingleOrDefault(item => item.Id == attachment.PoleId) as Pole
            ?? throw new InvalidOperationException(
                $"Pole '{attachment.PoleId}' does not exist.");
        Guid[] switchTerminalIds = [.. switchDevice.TerminalIds];
        Terminal[] poleTerminals = _terminals
            .Where(item => pole.OverheadAnchorTerminalIds.Contains(item.Id))
            .ToArray();
        Guid poleJunctionTerminalId = poleTerminals
            .Where(item => item.ElectricalNodeId is not null)
            .Select(item => item.Id)
            .FirstOrDefault();
        if (poleJunctionTerminalId == Guid.Empty)
        {
            throw new InvalidOperationException("杆塔缺少可用的汇流端子。");
        }

        Connection[] connected = _connections
            .Where(connection => switchTerminalIds.Any(connection.UsesTerminal))
            .ToArray();
        if (connected.Count(connection =>
                connection.UsesTerminal(switchDevice.TerminalIds[1])) > 1)
        {
            throw new InvalidOperationException("当前柱上开关控制了多条线路，暂不能自动旁路删除。");
        }

        var replacements = new List<(Connection Before, Connection After, OverheadLine Line)>();
        foreach (Connection connection in connected)
        {
            if (connection.UsesTerminal(switchDevice.TerminalIds[0]) &&
                connection.UsesTerminal(switchDevice.TerminalIds[1]))
            {
                throw new InvalidOperationException("柱上开关连接状态不一致，不能旁路删除。");
            }

            OverheadLine line = _overheadLines.SingleOrDefault(item =>
                    item.ConnectionId == connection.Id)
                ?? throw new InvalidOperationException("柱上开关关联的架空线明细缺失。");
            Connection after = new(
                connection.Id,
                connection.Type,
                connection.StartTerminalId == switchTerminalIds[0] ||
                    connection.StartTerminalId == switchTerminalIds[1]
                    ? poleJunctionTerminalId
                    : connection.StartTerminalId,
                connection.EndTerminalId == switchTerminalIds[0] ||
                    connection.EndTerminalId == switchTerminalIds[1]
                    ? poleJunctionTerminalId
                    : connection.EndTerminalId,
                connection.DisplayName,
                connection.VoltageLevel);
            replacements.Add((connection, after, line));
        }

        int applied = 0;
        try
        {
            foreach ((Connection before, Connection after, OverheadLine line) in replacements)
            {
                ReplaceOverheadConnection(before, after, line);
                applied++;
            }

            RemovePoleSwitchAttachment(attachmentId);
        }
        catch
        {
            foreach ((Connection before, Connection after, OverheadLine line) in
                     replacements.Take(applied).Reverse())
            {
                ReplaceOverheadConnection(after, before, line);
            }

            throw;
        }
    }

    private void ReplaceOverheadConnection(
        Connection before,
        Connection after,
        OverheadLine line)
    {
        if (before.Id != after.Id || line.ConnectionId != before.Id)
        {
            throw new InvalidOperationException("架空线旁路连接标识不一致。");
        }

        RemoveOverheadLine(before.Id);
        RemoveConnection(before.Id);
        try
        {
            AddConnection(after);
            AddOverheadLine(line);
        }
        catch
        {
            if (_connections.Any(item => item.Id == after.Id))
            {
                RemoveConnection(after.Id);
            }

            AddConnection(before);
            AddOverheadLine(line);
            throw;
        }
    }

    public void AddCableTerminationAttachment(
        CableTermination cableTermination,
        ElectricalNode internalNode,
        Terminal cableSideTerminal,
        Terminal overheadSideTerminal,
        PoleAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(cableTermination);
        ArgumentNullException.ThrowIfNull(internalNode);
        ArgumentNullException.ThrowIfNull(cableSideTerminal);
        ArgumentNullException.ThrowIfNull(overheadSideTerminal);
        ArgumentNullException.ThrowIfNull(attachment);

        ValidateCableTerminationAttachmentAggregate(
            cableTermination,
            internalNode,
            cableSideTerminal,
            overheadSideTerminal,
            attachment);

        Guid[] aggregateIds =
        [
            cableTermination.Id,
            internalNode.Id,
            cableSideTerminal.Id,
            overheadSideTerminal.Id,
            attachment.AttachmentId
        ];
        if (aggregateIds.Distinct().Count() != aggregateIds.Length)
        {
            throw new InvalidOperationException(
                "Cable termination aggregate IDs must be unique.");
        }

        foreach (Guid aggregateId in aggregateIds)
        {
            EnsureObjectIdIsAvailable(aggregateId, "Cable termination aggregate");
        }

        if (_poleAttachments.Any(existing =>
                existing.AttachedDeviceId == cableTermination.Id))
        {
            throw new InvalidOperationException(
                $"Device '{cableTermination.Id}' is already attached to a pole.");
        }

        Terminal boundOverheadSideTerminal = overheadSideTerminal;
        Pole attachedPole = (Pole)_devices.Single(device => device.Id == attachment.PoleId);
        if (attachedPole.OverheadAnchorTerminalIds.Count > 0)
        {
            Guid poleJunctionNodeId = EnsurePoleJunction(attachment.PoleId);
            boundOverheadSideTerminal = BindTerminal(
                overheadSideTerminal,
                poleJunctionNodeId);
        }

        _devices.Add(cableTermination);
        _electricalNodes.Add(internalNode);
        _terminals.Add(cableSideTerminal);
        _terminals.Add(boundOverheadSideTerminal);
        internalNode.AttachTerminal(cableSideTerminal.Id);
        if (boundOverheadSideTerminal.ElectricalNodeId == internalNode.Id)
        {
            internalNode.AttachTerminal(boundOverheadSideTerminal.Id);
        }
        else
        {
            _electricalNodes.Single(node => node.Id == boundOverheadSideTerminal.ElectricalNodeId)
                .AttachTerminal(boundOverheadSideTerminal.Id);
        }
        _poleAttachments.Add(attachment);
    }

    public void RemoveCableTerminationAttachment(Guid attachmentId)
    {
        PoleAttachment attachment = _poleAttachments.SingleOrDefault(existing =>
                existing.AttachmentId == attachmentId)
            ?? throw new InvalidOperationException(
                $"Pole attachment '{attachmentId}' does not exist.");
        CableTermination cableTermination = _devices.SingleOrDefault(device =>
                device.Id == attachment.AttachedDeviceId) as CableTermination
            ?? throw new InvalidOperationException(
                $"Attachment '{attachmentId}' does not reference a cable termination.");
        ElectricalNode internalNode = _electricalNodes.SingleOrDefault(node =>
                node.Id == cableTermination.InternalNodeId)
            ?? throw new InvalidOperationException(
                $"Cable termination '{cableTermination.Id}' internal node is missing.");
        Terminal cableSideTerminal = _terminals.SingleOrDefault(terminal =>
                terminal.Id == cableTermination.CableSideTerminalId)
            ?? throw new InvalidOperationException(
                $"Cable termination '{cableTermination.Id}' cable-side terminal is missing.");
        Terminal overheadSideTerminal = _terminals.SingleOrDefault(terminal =>
                terminal.Id == cableTermination.OverheadSideTerminalId)
            ?? throw new InvalidOperationException(
                $"Cable termination '{cableTermination.Id}' overhead-side terminal is missing.");

        ValidateCableTerminationAttachmentAggregate(
            cableTermination,
            internalNode,
            cableSideTerminal,
            overheadSideTerminal,
            attachment);

        if (_electricalNodes.Count(node => node.OwnerId == cableTermination.Id) != 1 ||
            _terminals.Count(terminal => terminal.OwnerId == cableTermination.Id) != 2 ||
            _poleAttachments.Count(existing =>
                existing.AttachedDeviceId == cableTermination.Id) != 1)
        {
            throw new InvalidOperationException(
                $"Cable termination '{cableTermination.Id}' aggregate is incomplete or inconsistent.");
        }

        HashSet<Guid> terminalIds =
        [
            cableTermination.CableSideTerminalId,
            cableTermination.OverheadSideTerminalId
        ];
        Connection[] referencedConnections = _connections
            .Where(connection =>
                terminalIds.Contains(connection.StartTerminalId) ||
                terminalIds.Contains(connection.EndTerminalId))
            .ToArray();
        if (referencedConnections.Length > 0)
        {
            HashSet<Guid> connectionIds = referencedConnections
                .Select(connection => connection.Id)
                .ToHashSet();
            if (_overheadLines.Any(line => connectionIds.Contains(line.ConnectionId)))
            {
                throw new InvalidOperationException(
                    $"Cable termination '{cableTermination.Id}' is still referenced by an overhead line.");
            }

            throw new InvalidOperationException(
                $"Cable termination '{cableTermination.Id}' is still referenced by a connection.");
        }

        if (_groundingPoints.Any(point => TargetsAnyTerminal(point, terminalIds)))
        {
            throw new InvalidOperationException(
                $"Cable termination '{cableTermination.Id}' is still referenced by a grounding point.");
        }

        if (_workScopes.Any(scope =>
                scope.StartBoundary.DeviceId == cableTermination.Id ||
                scope.EndBoundary.DeviceId == cableTermination.Id ||
                terminalIds.Contains(scope.StartBoundary.TerminalId) ||
                terminalIds.Contains(scope.EndBoundary.TerminalId)))
        {
            throw new InvalidOperationException(
                $"Cable termination '{cableTermination.Id}' is still referenced by a work scope.");
        }

        _poleAttachments.Remove(attachment);
        _terminals.Remove(cableSideTerminal);
        _terminals.Remove(overheadSideTerminal);
        _electricalNodes.Remove(internalNode);
        _devices.Remove(cableTermination);
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

    public OverheadLine RemoveOverheadLine(Guid connectionId)
    {
        OverheadLine overheadLine = _overheadLines.SingleOrDefault(
                existing => existing.ConnectionId == connectionId)
            ?? throw new InvalidOperationException(
                $"Overhead line '{connectionId}' does not exist.");

        GroundingAccessPoint[] accessPoints = _groundingAccessPoints
            .Where(point => point.ConnectionId == connectionId)
            .ToArray();
        GroundingAccessPoint? occupied = accessPoints.FirstOrDefault(point =>
            _groundingPoints.Any(groundingPoint =>
                groundingPoint.Target == GroundingTarget.ForGroundingAccessPoint(
                    point.GroundingAccessPointId)));
        if (occupied is not null)
        {
            throw new InvalidOperationException(
                $"Overhead line '{connectionId}' has an occupied grounding access point '{occupied.GroundingAccessPointId}'.");
        }

        _groundingAccessPoints.RemoveAll(point => point.ConnectionId == connectionId);
        _overheadLines.Remove(overheadLine);
        return overheadLine;
    }

    public GroundingAccessPoint CreateGroundingAccessPoint(
        Guid groundingAccessPointId,
        Guid connectionId,
        Guid poleId,
        Guid adjacentPoleId,
        GroundingAccessLineSide lineSide)
    {
        var point = new GroundingAccessPoint(
            groundingAccessPointId,
            connectionId,
            poleId,
            adjacentPoleId,
            lineSide);
        AddGroundingAccessPoint(point);
        return point;
    }

    public void AddGroundingAccessPoint(GroundingAccessPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);
        EnsureObjectIdIsAvailable(point.GroundingAccessPointId, nameof(GroundingAccessPoint));
        ValidateGroundingAccessPoint(point);

        if (_groundingAccessPoints.Any(existing =>
                existing.ConnectionId == point.ConnectionId &&
                existing.PoleId == point.PoleId &&
                existing.AdjacentPoleId == point.AdjacentPoleId))
        {
            throw new InvalidOperationException(
                "A grounding access point already exists on the selected conductor half-edge.");
        }

        _groundingAccessPoints.Add(point);
    }

    public GroundingAccessPoint GetGroundingAccessPoint(Guid groundingAccessPointId)
    {
        return _groundingAccessPoints.SingleOrDefault(point =>
                point.GroundingAccessPointId == groundingAccessPointId)
            ?? throw new InvalidOperationException(
                $"Grounding access point '{groundingAccessPointId}' does not exist.");
    }

    public void RemoveGroundingAccessPoint(Guid groundingAccessPointId)
    {
        GroundingAccessPoint point = GetGroundingAccessPoint(groundingAccessPointId);
        if (_groundingPoints.Any(groundingPoint =>
                groundingPoint.Target == GroundingTarget.ForGroundingAccessPoint(
                    groundingAccessPointId)))
        {
            throw new InvalidOperationException(
                $"Grounding access point '{groundingAccessPointId}' is occupied by a grounding point.");
        }

        _groundingAccessPoints.Remove(point);
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
        return CreateGroundingPoint(
            groundingPointId,
            GroundingTarget.ForTerminal(terminalId),
            location,
            number,
            note);
    }

    public GroundingPoint CreateGroundingPoint(
        Guid groundingPointId,
        GroundingTarget target,
        string location,
        string? number = null,
        string? note = null)
    {
        GroundingPoint groundingPoint = GroundingPoint.Create(
            groundingPointId,
            target,
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
        ValidateGroundingTarget(groundingPoint.Target);

        if (_groundingPoints.Any(existing =>
                existing.Target == groundingPoint.Target))
        {
            throw new InvalidOperationException(
                $"Grounding target '{groundingPoint.Target.TargetId}' already has a grounding point.");
        }

        if (groundingPoint.Number is not null && _groundingPoints.Any(existing =>
                string.Equals(existing.Number, groundingPoint.Number, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Grounding point number '{groundingPoint.Number}' is already in use.");
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
        UpdateGroundingPoint(
            groundingPointId,
            GroundingTarget.ForTerminal(terminalId),
            location,
            number,
            note);
    }

    public void UpdateGroundingPoint(
        Guid groundingPointId,
        GroundingTarget target,
        string location,
        string? number = null,
        string? note = null)
    {
        GroundingPoint groundingPoint = GetGroundingPoint(groundingPointId);
        if (groundingPoint.Target != target)
        {
            throw new InvalidOperationException("Grounding target rebinding is not supported.");
        }

        GroundingPoint replacement = GroundingPoint.Create(
            groundingPointId,
            target,
            location,
            number,
            note);

        ValidateGroundingTarget(replacement.Target);
        if (_groundingPoints.Any(existing =>
                existing.GroundingPointId != groundingPointId &&
                existing.Target == replacement.Target))
        {
            throw new InvalidOperationException(
                $"Grounding target '{replacement.Target.TargetId}' already has a grounding point.");
        }

        if (string.IsNullOrWhiteSpace(replacement.Number))
        {
            throw new InvalidOperationException("Grounding point number is required when editing.");
        }

        if (_groundingPoints.Any(existing =>
                existing.GroundingPointId != groundingPointId &&
                string.Equals(existing.Number, replacement.Number, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Grounding point number '{replacement.Number}' is already in use.");
        }

        groundingPoint.Update(
            replacement.Target,
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

    private void ValidateGroundingTarget(GroundingTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        switch (target.Kind)
        {
            case GroundingTargetKind.Terminal:
                _ = GetTerminal(target.TargetId);
                break;
            case GroundingTargetKind.GroundingAccessPoint:
                _ = GetGroundingAccessPoint(target.TargetId);
                break;
            default:
                throw new InvalidOperationException($"Unsupported grounding target kind '{target.Kind}'.");
        }
    }

    private void ValidateGroundingAccessPoint(GroundingAccessPoint point)
    {
        Connection connection = _connections.SingleOrDefault(candidate =>
                candidate.Id == point.ConnectionId)
            ?? throw new InvalidOperationException(
                $"Connection '{point.ConnectionId}' does not exist.");
        if (connection.Type != ConnectionType.OverheadLine)
        {
            throw new InvalidOperationException(
                $"Connection '{point.ConnectionId}' is not an overhead line.");
        }

        OverheadLine line = _overheadLines.SingleOrDefault(candidate =>
                candidate.ConnectionId == point.ConnectionId)
            ?? throw new InvalidOperationException(
                $"Overhead line detail '{point.ConnectionId}' does not exist.");
        if (_devices.SingleOrDefault(device => device.Id == point.PoleId) is not Pole)
        {
            throw new InvalidOperationException($"Pole '{point.PoleId}' does not exist.");
        }
        if (_devices.SingleOrDefault(device => device.Id == point.AdjacentPoleId) is not Pole)
        {
            throw new InvalidOperationException(
                $"Adjacent pole '{point.AdjacentPoleId}' does not exist.");
        }

        int poleIndex = line.SupportPoleIds.ToList().IndexOf(point.PoleId);
        if (poleIndex < 0)
        {
            throw new InvalidOperationException(
                $"Pole '{point.PoleId}' is not a support pole of overhead line '{point.ConnectionId}'.");
        }

        bool isPredecessor = poleIndex > 0 &&
                             line.SupportPoleIds[poleIndex - 1] == point.AdjacentPoleId;
        bool isSuccessor = poleIndex + 1 < line.SupportPoleIds.Count &&
                           line.SupportPoleIds[poleIndex + 1] == point.AdjacentPoleId;
        if (!isPredecessor && !isSuccessor)
        {
            throw new InvalidOperationException(
                $"Pole '{point.AdjacentPoleId}' is not directly adjacent to pole '{point.PoleId}' on overhead line '{point.ConnectionId}'.");
        }
    }

    private static bool TargetsAnyTerminal(
        GroundingPoint groundingPoint,
        IReadOnlyCollection<Guid> terminalIds)
    {
        return groundingPoint.Target.Kind == GroundingTargetKind.Terminal &&
               terminalIds.Contains(groundingPoint.Target.TargetId);
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
            TopologyOwnerType.IntermediateTerminal => _intermediateTerminals.Any(
                intermediateTerminal => intermediateTerminal.Id == ownerId),
            _ => false
        };

        if (!ownerExists)
        {
            throw new InvalidOperationException(
                $"Topology owner '{ownerId}' of type '{ownerType}' does not exist.");
        }
    }

    private void ValidateCableTerminationAttachmentAggregate(
        CableTermination cableTermination,
        ElectricalNode internalNode,
        Terminal cableSideTerminal,
        Terminal overheadSideTerminal,
        PoleAttachment attachment)
    {
        if (_devices.FirstOrDefault(device => device.Id == attachment.PoleId) is not Pole)
        {
            throw new InvalidOperationException(
                $"Pole '{attachment.PoleId}' does not exist.");
        }

        if (attachment.AttachedDeviceId != cableTermination.Id)
        {
            throw new InvalidOperationException(
                "Pole attachment must reference the cable termination.");
        }

        if (internalNode.Id != cableTermination.InternalNodeId ||
            internalNode.Type != ElectricalNodeType.Intermediate ||
            internalNode.OwnerType != TopologyOwnerType.Device ||
            internalNode.OwnerId != cableTermination.Id)
        {
            throw new InvalidOperationException(
                "Cable termination internal node is inconsistent with its device.");
        }

        if (internalNode.TerminalIds.Count != 0 &&
            !internalNode.TerminalIds.SetEquals([cableTermination.CableSideTerminalId]) &&
            !internalNode.TerminalIds.SetEquals([
                cableTermination.CableSideTerminalId,
                cableTermination.OverheadSideTerminalId]))
        {
            throw new InvalidOperationException(
                "Cable termination internal node contains inconsistent terminals.");
        }

        ValidateCableTerminationTerminal(
            cableTermination,
            cableSideTerminal,
            cableTermination.CableSideTerminalId,
            CableTermination.CableSideRole,
            ConnectionType.Cable);
        ValidateCableTerminationOverheadTerminal(
            cableTermination,
            overheadSideTerminal,
            cableTermination.OverheadSideTerminalId,
            CableTermination.OverheadSideRole,
            ConnectionType.OverheadLine);
    }

    /// <summary>
    /// Ensures that the pole's existing center terminals participate in one
    /// explicit junction node. The pole remains a physical support device.
    /// </summary>
    public Guid EnsurePoleJunction(Guid poleId)
    {
        Pole pole = _devices.SingleOrDefault(device => device.Id == poleId) as Pole
            ?? throw new InvalidOperationException($"Pole '{poleId}' does not exist.");
        Guid[] terminalIds = pole.OverheadAnchorTerminalIds.ToArray();
        if (terminalIds.Length == 0)
        {
            throw new InvalidOperationException($"Pole '{poleId}' has no overhead terminal.");
        }

        Guid[] existingNodeIds = _terminals
            .Where(terminal => terminalIds.Contains(terminal.Id))
            .Select(terminal => terminal.ElectricalNodeId)
            .OfType<Guid>()
            .Distinct()
            .ToArray();
        if (existingNodeIds.Length > 1)
        {
            throw new InvalidOperationException($"Pole '{poleId}' has inconsistent junction nodes.");
        }

        Guid nodeId = existingNodeIds.SingleOrDefault();
        if (nodeId == Guid.Empty)
        {
            nodeId = Guid.NewGuid();
            _electricalNodes.Add(new ElectricalNode(
                nodeId,
                ElectricalNodeType.Intermediate,
                TopologyOwnerType.Device,
                pole.Id));
        }

        ElectricalNode node = _electricalNodes.SingleOrDefault(candidate => candidate.Id == nodeId)
            ?? throw new InvalidOperationException($"Pole junction node '{nodeId}' does not exist.");
        foreach (Guid terminalId in terminalIds)
        {
            int index = _terminals.FindIndex(terminal => terminal.Id == terminalId);
            if (index < 0)
            {
                continue;
            }

            Terminal current = _terminals[index];
            if (current.ElectricalNodeId != nodeId)
            {
                _terminals[index] = BindTerminal(current, nodeId);
            }

            node.AttachTerminal(terminalId);
        }

        return nodeId;
    }

    private static Terminal BindTerminal(Terminal terminal, Guid electricalNodeId)
    {
        return new Terminal(
            terminal.Id,
            terminal.OwnerType,
            terminal.OwnerId,
            terminal.Role,
            terminal.VoltageLevel,
            terminal.IsExternal,
            terminal.AllowsMultipleConnections,
            electricalNodeId,
            terminal.AllowedConnectionTypes);
    }

    private static void ValidateCableTerminationTerminal(
        CableTermination cableTermination,
        Terminal terminal,
        Guid expectedTerminalId,
        string expectedRole,
        ConnectionType expectedConnectionType)
    {
        if (terminal.Id != expectedTerminalId ||
            terminal.OwnerType != TopologyOwnerType.Device ||
            terminal.OwnerId != cableTermination.Id ||
            terminal.ElectricalNodeId != cableTermination.InternalNodeId ||
            !string.Equals(terminal.Role, expectedRole, StringComparison.Ordinal) ||
            !string.Equals(
                terminal.VoltageLevel,
                cableTermination.VoltageLevel,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cable termination terminal '{terminal.Id}' is inconsistent with its device.");
        }

        EnsureTerminalPolicy(
            terminal,
            expectedConnectionType,
            $"Cable termination {expectedRole} terminal");
    }

    private static void ValidateCableTerminationOverheadTerminal(
        CableTermination cableTermination,
        Terminal terminal,
        Guid expectedTerminalId,
        string expectedRole,
        ConnectionType expectedConnectionType)
    {
        if (terminal.Id != expectedTerminalId ||
            terminal.OwnerType != TopologyOwnerType.Device ||
            terminal.OwnerId != cableTermination.Id ||
            !string.Equals(terminal.Role, expectedRole, StringComparison.Ordinal) ||
            !string.Equals(
                terminal.VoltageLevel,
                cableTermination.VoltageLevel,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cable termination terminal '{terminal.Id}' is inconsistent with its device.");
        }

        EnsureTerminalPolicy(
            terminal,
            expectedConnectionType,
            $"Cable termination {expectedRole} terminal");
    }

    private void EnsureObjectIdIsAvailable(Guid objectId, string objectName)
    {
        if (_devices.Any(device => device.Id == objectId) ||
            _terminals.Any(terminal => terminal.Id == objectId) ||
            _electricalNodes.Any(node => node.Id == objectId) ||
            _switchAssemblies.Any(assembly => assembly.AssemblyId == objectId) ||
            _connections.Any(connection => connection.Id == objectId) ||
            _cableSegments.Any(segment => segment.Id == objectId) ||
            _intermediateTerminals.Any(terminal => terminal.Id == objectId) ||
            _poleAttachments.Any(attachment => attachment.AttachmentId == objectId) ||
            _workScopes.Any(workScope => workScope.WorkScopeId == objectId) ||
            _groundingPoints.Any(point => point.GroundingPointId == objectId) ||
            _groundingAccessPoints.Any(point => point.GroundingAccessPointId == objectId) ||
            _internalAggregateOwnerIds.Contains(objectId))
        {
            throw new InvalidOperationException($"{objectName} ID '{objectId}' is already in use.");
        }
    }

    private void EnsureTerminalAcceptsConnection(Terminal terminal, Connection connection)
    {
        if (!terminal.IsExternal || !terminal.Allows(connection.Type))
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

    private void ValidateCableReconnectConnection(
        Connection connection,
        Guid replacedConnectionId)
    {
        if (connection.Type != ConnectionType.Cable)
        {
            throw new InvalidOperationException(
                "A cable reconnect requires a cable connection.");
        }

        Terminal start = GetTerminal(connection.StartTerminalId);
        Terminal end = GetTerminal(connection.EndTerminalId);
        if (connection.StartTerminalId == connection.EndTerminalId)
        {
            throw new InvalidOperationException(
                "Reconnect requires two different terminals.");
        }

        if (start.OwnerType == end.OwnerType && start.OwnerId == end.OwnerId)
        {
            throw new InvalidOperationException(
                "An external connection cannot connect two terminals of the same topology owner.");
        }

        if (start.ElectricalNodeId is Guid startNodeId &&
            end.ElectricalNodeId == startNodeId)
        {
            throw new InvalidOperationException(
                "An external connection cannot reconnect terminals on the same electrical node.");
        }

        if (_connections.Any(existing =>
                existing.Id != replacedConnectionId &&
                existing.UsesTerminal(connection.StartTerminalId) &&
                existing.UsesTerminal(connection.EndTerminalId)))
        {
            throw new InvalidOperationException(
                "A connection between the selected terminals already exists.");
        }

        EnsureTerminalAcceptsReconnect(start, connection, replacedConnectionId);
        EnsureTerminalAcceptsReconnect(end, connection, replacedConnectionId);
    }

    private void EnsureTerminalAcceptsReconnect(
        Terminal terminal,
        Connection connection,
        Guid replacedConnectionId)
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
            _connections.Any(existing =>
                existing.Id != replacedConnectionId &&
                existing.UsesTerminal(terminal.Id)))
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

    private static void ValidateCableSegmentConnection(
        CableSegment cableSegment,
        Connection connection)
    {
        if (connection.Type != ConnectionType.Cable ||
            cableSegment.ConnectionId != connection.Id ||
            cableSegment.StartTerminalId != connection.StartTerminalId ||
            cableSegment.EndTerminalId != connection.EndTerminalId ||
            !string.Equals(
                cableSegment.VoltageLevel,
                connection.VoltageLevel,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cable segment '{cableSegment.Id}' and connection '{connection.Id}' are inconsistent.");
        }
    }

    private static bool CableSegmentFactsEqual(
        CableSegment first,
        CableSegment second)
    {
        return first.Id == second.Id &&
            string.Equals(first.Name, second.Name, StringComparison.Ordinal) &&
            string.Equals(first.CableType, second.CableType, StringComparison.Ordinal) &&
            first.Length == second.Length &&
            string.Equals(first.VoltageLevel, second.VoltageLevel, StringComparison.Ordinal) &&
            first.ConnectionId == second.ConnectionId &&
            first.StartTerminalId == second.StartTerminalId &&
            first.EndTerminalId == second.EndTerminalId;
    }

    private static bool ConnectionFactsEqual(Connection first, Connection second)
    {
        return first.Id == second.Id &&
            first.Type == second.Type &&
            first.StartTerminalId == second.StartTerminalId &&
            first.EndTerminalId == second.EndTerminalId &&
            string.Equals(first.DisplayName, second.DisplayName, StringComparison.Ordinal) &&
            string.Equals(first.VoltageLevel, second.VoltageLevel, StringComparison.Ordinal);
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
