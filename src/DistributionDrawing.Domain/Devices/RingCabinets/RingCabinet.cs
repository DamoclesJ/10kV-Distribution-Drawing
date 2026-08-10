using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.SwitchAssemblies;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Domain.Devices.RingCabinets;

public sealed class RingCabinet : Device
{
    private const string TenKilovolts = "10kV";
    private const string BusSideRole = "BusSide";
    private const string CircuitSideRole = "CircuitSide";
    private const string DeviceSideRole = "DeviceSide";
    private const string GroundSideRole = "GroundSide";
    private const string ExternalCircuitRole = "ExternalCircuit";
    private const string FirstTerminalRole = "Terminal1";
    private const string SecondTerminalRole = "Terminal2";

    private readonly IReadOnlyList<RingCabinetInterval> _intervals;
    private readonly IReadOnlyList<ElectricalNode> _electricalNodes;
    private readonly IReadOnlyList<Terminal> _terminals;

    private RingCabinet(
        Guid id,
        string displayName,
        CabinetKind cabinetKind,
        Guid mainBusNodeId,
        IEnumerable<RingCabinetInterval> intervals,
        IEnumerable<ElectricalNode> electricalNodes,
        IEnumerable<Terminal> terminals)
        : base(id, DeviceType.RingCabinet, displayName, TenKilovolts)
    {
        CabinetKind = cabinetKind;
        MainBusNodeId = mainBusNodeId;
        _intervals = Array.AsReadOnly(intervals.ToArray());
        _electricalNodes = Array.AsReadOnly(electricalNodes.ToArray());
        _terminals = Array.AsReadOnly(terminals.ToArray());
    }

    public CabinetKind CabinetKind { get; }

    public Guid MainBusNodeId { get; }

    public IReadOnlyList<RingCabinetInterval> Intervals => _intervals;

    public IReadOnlyList<ElectricalNode> ElectricalNodes => _electricalNodes;

    public IReadOnlyList<Terminal> Terminals => _terminals;

    internal IEnumerable<SwitchDevice> InternalSwitchDevices =>
        _intervals.SelectMany(interval => interval.SwitchDevices);

    internal IEnumerable<SwitchAssembly> InternalSwitchAssemblies =>
        _intervals.Select(interval => interval.SwitchAssembly);

    public static RingCabinet CreateNormalLoadSwitchCabinet(
        Guid id,
        string displayName,
        int intervalCount,
        SwitchState initialLoadSwitchState,
        SwitchState initialGroundSwitchState)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Cabinet ID cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Cabinet display name is required.", nameof(displayName));
        }

        if (intervalCount is < 3 or > 6)
        {
            throw new ArgumentOutOfRangeException(
                nameof(intervalCount),
                "A normal load-switch cabinet supports 3, 4, 5, or 6 intervals.");
        }

        Guid mainBusNodeId = Guid.NewGuid();
        var electricalNodes = new List<ElectricalNode>();
        var terminals = new List<Terminal>();
        var intervals = new List<RingCabinetInterval>();

        var mainBusNode = new ElectricalNode(
            mainBusNodeId,
            ElectricalNodeType.MainBus,
            TopologyOwnerType.Device,
            id);

        electricalNodes.Add(mainBusNode);

        for (int sequence = 1; sequence <= intervalCount; sequence++)
        {
            Guid intervalId = Guid.NewGuid();
            Guid circuitNodeId = Guid.NewGuid();
            Guid earthNodeId = Guid.NewGuid();
            Guid externalTerminalId = Guid.NewGuid();
            Guid loadSwitchBusTerminalId = Guid.NewGuid();
            Guid loadSwitchCircuitTerminalId = Guid.NewGuid();
            Guid groundSwitchDeviceTerminalId = Guid.NewGuid();
            Guid groundSwitchGroundTerminalId = Guid.NewGuid();

            var circuitNode = new ElectricalNode(
                circuitNodeId,
                ElectricalNodeType.Circuit,
                TopologyOwnerType.InternalAggregate,
                intervalId);

            var earthNode = new ElectricalNode(
                earthNodeId,
                ElectricalNodeType.Earth,
                TopologyOwnerType.InternalAggregate,
                intervalId);

            var loadSwitch = new SwitchDevice(
                Guid.NewGuid(),
                SwitchKind.LoadSwitch,
                SwitchInstallationType.CabinetInterval,
                loadSwitchBusTerminalId,
                loadSwitchCircuitTerminalId,
                initialLoadSwitchState,
                $"{sequence}号间隔负荷开关",
                TenKilovolts,
                intervalId);

            var groundSwitch = new SwitchDevice(
                Guid.NewGuid(),
                SwitchKind.GroundSwitch,
                SwitchInstallationType.CabinetInterval,
                groundSwitchDeviceTerminalId,
                groundSwitchGroundTerminalId,
                initialGroundSwitchState,
                $"{sequence}号间隔接地刀闸",
                TenKilovolts,
                intervalId);

            SwitchAssembly switchAssembly = SwitchAssembly.CreateLoadSwitchThreePosition(
                Guid.NewGuid(),
                intervalId,
                loadSwitch,
                groundSwitch);

            AddTerminal(
                terminals,
                mainBusNode,
                new Terminal(
                    loadSwitchBusTerminalId,
                    TopologyOwnerType.Device,
                    loadSwitch.Id,
                    BusSideRole,
                    TenKilovolts,
                    false,
                    false,
                    mainBusNodeId));

            AddTerminal(
                terminals,
                circuitNode,
                new Terminal(
                    loadSwitchCircuitTerminalId,
                    TopologyOwnerType.Device,
                    loadSwitch.Id,
                    CircuitSideRole,
                    TenKilovolts,
                    false,
                    false,
                    circuitNodeId));

            AddTerminal(
                terminals,
                circuitNode,
                new Terminal(
                    groundSwitchDeviceTerminalId,
                    TopologyOwnerType.Device,
                    groundSwitch.Id,
                    DeviceSideRole,
                    TenKilovolts,
                    false,
                    false,
                    circuitNodeId));

            AddTerminal(
                terminals,
                earthNode,
                new Terminal(
                    groundSwitchGroundTerminalId,
                    TopologyOwnerType.Device,
                    groundSwitch.Id,
                    GroundSideRole,
                    null,
                    false,
                    false,
                    earthNodeId));

            AddTerminal(
                terminals,
                circuitNode,
                new Terminal(
                    externalTerminalId,
                    TopologyOwnerType.InternalAggregate,
                    intervalId,
                    ExternalCircuitRole,
                    TenKilovolts,
                    true,
                    false,
                    circuitNodeId,
                    [ConnectionType.Cable, ConnectionType.OverheadLine]));

            electricalNodes.Add(circuitNode);
            electricalNodes.Add(earthNode);

            intervals.Add(
                new RingCabinetInterval(
                    intervalId,
                    id,
                    sequence,
                    $"{sequence}号间隔",
                    IntervalKind.LoadSwitchInterval,
                    [loadSwitch, groundSwitch],
                    switchAssembly,
                    null,
                    circuitNodeId,
                    earthNodeId,
                    externalTerminalId));
        }

        var cabinet = new RingCabinet(
            id,
            displayName.Trim(),
            CabinetKind.LoadSwitchType,
            mainBusNodeId,
            intervals,
            electricalNodes,
            terminals);

        cabinet.ValidateStructure();
        return cabinet;
    }

    public static RingCabinet CreatePrimarySecondaryIntegratedCabinetBase(
        Guid id,
        string displayName,
        int intervalCount,
        SwitchState initialIsolationSwitchState,
        SwitchState initialCircuitBreakerState,
        SwitchState initialGroundSwitchState)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Cabinet ID cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Cabinet display name is required.", nameof(displayName));
        }

        if (intervalCount is not (4 or 6))
        {
            throw new ArgumentOutOfRangeException(
                nameof(intervalCount),
                "A primary-secondary integrated cabinet supports 4 or 6 intervals.");
        }

        Guid mainBusNodeId = Guid.NewGuid();
        var electricalNodes = new List<ElectricalNode>();
        var terminals = new List<Terminal>();
        var intervals = new List<RingCabinetInterval>();

        var mainBusNode = new ElectricalNode(
            mainBusNodeId,
            ElectricalNodeType.MainBus,
            TopologyOwnerType.Device,
            id);

        electricalNodes.Add(mainBusNode);

        for (int sequence = 1; sequence <= intervalCount; sequence++)
        {
            Guid intervalId = Guid.NewGuid();
            Guid intermediateNodeId = Guid.NewGuid();
            Guid circuitNodeId = Guid.NewGuid();
            Guid earthNodeId = Guid.NewGuid();
            Guid externalTerminalId = Guid.NewGuid();
            Guid isolationFirstTerminalId = Guid.NewGuid();
            Guid isolationSecondTerminalId = Guid.NewGuid();
            Guid breakerFirstTerminalId = Guid.NewGuid();
            Guid breakerSecondTerminalId = Guid.NewGuid();
            Guid groundSwitchDeviceTerminalId = Guid.NewGuid();
            Guid groundSwitchGroundTerminalId = Guid.NewGuid();

            var intermediateNode = new ElectricalNode(
                intermediateNodeId,
                ElectricalNodeType.Intermediate,
                TopologyOwnerType.InternalAggregate,
                intervalId);

            var circuitNode = new ElectricalNode(
                circuitNodeId,
                ElectricalNodeType.Circuit,
                TopologyOwnerType.InternalAggregate,
                intervalId);

            var earthNode = new ElectricalNode(
                earthNodeId,
                ElectricalNodeType.Earth,
                TopologyOwnerType.InternalAggregate,
                intervalId);

            var isolationSwitch = new SwitchDevice(
                Guid.NewGuid(),
                SwitchKind.IsolationSwitch,
                SwitchInstallationType.CabinetInterval,
                isolationFirstTerminalId,
                isolationSecondTerminalId,
                initialIsolationSwitchState,
                $"{sequence}号间隔隔离刀闸",
                TenKilovolts,
                intervalId);

            var circuitBreaker = new SwitchDevice(
                Guid.NewGuid(),
                SwitchKind.CircuitBreaker,
                SwitchInstallationType.CabinetInterval,
                breakerFirstTerminalId,
                breakerSecondTerminalId,
                initialCircuitBreakerState,
                $"{sequence}号间隔断路器",
                TenKilovolts,
                intervalId);

            var groundSwitch = new SwitchDevice(
                Guid.NewGuid(),
                SwitchKind.GroundSwitch,
                SwitchInstallationType.CabinetInterval,
                groundSwitchDeviceTerminalId,
                groundSwitchGroundTerminalId,
                initialGroundSwitchState,
                $"{sequence}号间隔接地刀闸",
                TenKilovolts,
                intervalId);

            SwitchAssembly switchAssembly = SwitchAssembly.CreateIntegratedFeederBase(
                Guid.NewGuid(),
                intervalId,
                isolationSwitch,
                circuitBreaker,
                groundSwitch);

            terminals.Add(
                CreateUnboundInternalTerminal(
                    isolationFirstTerminalId,
                    isolationSwitch.Id,
                    FirstTerminalRole,
                    TenKilovolts));
            terminals.Add(
                CreateUnboundInternalTerminal(
                    isolationSecondTerminalId,
                    isolationSwitch.Id,
                    SecondTerminalRole,
                    TenKilovolts));
            terminals.Add(
                CreateUnboundInternalTerminal(
                    breakerFirstTerminalId,
                    circuitBreaker.Id,
                    FirstTerminalRole,
                    TenKilovolts));
            terminals.Add(
                CreateUnboundInternalTerminal(
                    breakerSecondTerminalId,
                    circuitBreaker.Id,
                    SecondTerminalRole,
                    TenKilovolts));
            terminals.Add(
                CreateUnboundInternalTerminal(
                    groundSwitchDeviceTerminalId,
                    groundSwitch.Id,
                    DeviceSideRole,
                    TenKilovolts));

            AddTerminal(
                terminals,
                earthNode,
                new Terminal(
                    groundSwitchGroundTerminalId,
                    TopologyOwnerType.Device,
                    groundSwitch.Id,
                    GroundSideRole,
                    null,
                    false,
                    false,
                    earthNodeId));

            AddTerminal(
                terminals,
                circuitNode,
                new Terminal(
                    externalTerminalId,
                    TopologyOwnerType.InternalAggregate,
                    intervalId,
                    ExternalCircuitRole,
                    TenKilovolts,
                    true,
                    false,
                    circuitNodeId,
                    [ConnectionType.Cable, ConnectionType.OverheadLine]));

            electricalNodes.Add(intermediateNode);
            electricalNodes.Add(circuitNode);
            electricalNodes.Add(earthNode);

            intervals.Add(
                new RingCabinetInterval(
                    intervalId,
                    id,
                    sequence,
                    $"{sequence}号间隔",
                    IntervalKind.CircuitBreakerInterval,
                    [isolationSwitch, circuitBreaker, groundSwitch],
                    switchAssembly,
                    intermediateNodeId,
                    circuitNodeId,
                    earthNodeId,
                    externalTerminalId));
        }

        var cabinet = new RingCabinet(
            id,
            displayName.Trim(),
            CabinetKind.PrimarySecondaryIntegrated,
            mainBusNodeId,
            intervals,
            electricalNodes,
            terminals);

        cabinet.ValidateStructure();
        return cabinet;
    }

    internal void ValidateStructure()
    {
        if ((CabinetKind == CabinetKind.LoadSwitchType && _intervals.Count is < 3 or > 6) ||
            (CabinetKind == CabinetKind.PrimarySecondaryIntegrated &&
             _intervals.Count is not (4 or 6)))
        {
            throw new InvalidOperationException(
                "The cabinet interval count is invalid for its cabinet kind.");
        }

        EnsureAggregateObjectIdsAreUnique();

        Dictionary<Guid, ElectricalNode> nodes = _electricalNodes.ToDictionary(node => node.Id);
        Dictionary<Guid, Terminal> terminals = _terminals.ToDictionary(terminal => terminal.Id);

        if (!nodes.TryGetValue(MainBusNodeId, out ElectricalNode? mainBusNode) ||
            mainBusNode.Type != ElectricalNodeType.MainBus ||
            mainBusNode.OwnerType != TopologyOwnerType.Device ||
            mainBusNode.OwnerId != Id)
        {
            throw new InvalidOperationException("The cabinet main bus node is invalid.");
        }

        switch (CabinetKind)
        {
            case CabinetKind.LoadSwitchType:
                ValidateLoadSwitchStructure(nodes, terminals, mainBusNode);
                break;
            case CabinetKind.PrimarySecondaryIntegrated:
                ValidateIntegratedBaseStructure(nodes, terminals, mainBusNode);
                break;
            default:
                throw new InvalidOperationException("The cabinet kind is not supported.");
        }
    }

    private void ValidateLoadSwitchStructure(
        IReadOnlyDictionary<Guid, ElectricalNode> nodes,
        IReadOnlyDictionary<Guid, Terminal> terminals,
        ElectricalNode mainBusNode)
    {
        var expectedMainBusTerminalIds = new List<Guid>();

        for (int index = 0; index < _intervals.Count; index++)
        {
            RingCabinetInterval interval = _intervals[index];

            if (interval.ParentCabinetId != Id ||
                interval.Sequence != index + 1 ||
                interval.IntervalKind != IntervalKind.LoadSwitchInterval)
            {
                throw new InvalidOperationException(
                    $"Interval '{interval.IntervalId}' has an invalid owner, sequence, or kind.");
            }

            SwitchDevice[] loadSwitches = interval.SwitchDevices
                .Where(device => device.SwitchKind == SwitchKind.LoadSwitch)
                .ToArray();
            SwitchDevice[] groundSwitches = interval.SwitchDevices
                .Where(device => device.SwitchKind == SwitchKind.GroundSwitch)
                .ToArray();

            if (interval.SwitchDevices.Count != 2 ||
                loadSwitches.Length != 1 ||
                groundSwitches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Interval '{interval.IntervalId}' must contain one load switch and one ground switch.");
            }

            SwitchDevice loadSwitch = loadSwitches[0];
            SwitchDevice groundSwitch = groundSwitches[0];

            EnsureCabinetSwitchIsValid(loadSwitch, interval.IntervalId);
            EnsureCabinetSwitchIsValid(groundSwitch, interval.IntervalId);

            if (interval.SwitchAssembly.ParentIntervalId != interval.IntervalId ||
                interval.SwitchAssembly.AssemblyType !=
                    SwitchAssemblyType.LoadSwitchThreePosition ||
                !interval.SwitchAssembly.MemberSwitchIds.ToHashSet().SetEquals(
                    interval.SwitchDevices.Select(device => device.Id)) ||
                !interval.SwitchAssembly.Evaluate().IsValid)
            {
                throw new InvalidOperationException(
                    $"Interval '{interval.IntervalId}' has an invalid switch assembly.");
            }

            ElectricalNode circuitNode = GetRequiredNode(
                nodes,
                interval.CircuitNodeId,
                ElectricalNodeType.Circuit,
                interval.IntervalId);
            ElectricalNode earthNode = GetRequiredNode(
                nodes,
                interval.EarthNodeId,
                ElectricalNodeType.Earth,
                interval.IntervalId);

            Terminal loadBusTerminal = GetRequiredTerminal(
                terminals,
                loadSwitch.TerminalIds[0],
                TopologyOwnerType.Device,
                loadSwitch.Id,
                MainBusNodeId,
                false);
            Terminal loadCircuitTerminal = GetRequiredTerminal(
                terminals,
                loadSwitch.TerminalIds[1],
                TopologyOwnerType.Device,
                loadSwitch.Id,
                interval.CircuitNodeId,
                false);
            Terminal groundDeviceTerminal = GetRequiredTerminal(
                terminals,
                groundSwitch.TerminalIds[0],
                TopologyOwnerType.Device,
                groundSwitch.Id,
                interval.CircuitNodeId,
                false);
            Terminal groundSideTerminal = GetRequiredTerminal(
                terminals,
                groundSwitch.TerminalIds[1],
                TopologyOwnerType.Device,
                groundSwitch.Id,
                interval.EarthNodeId,
                false);
            Terminal externalTerminal = GetRequiredTerminal(
                terminals,
                interval.ExternalTerminalId,
                TopologyOwnerType.InternalAggregate,
                interval.IntervalId,
                interval.CircuitNodeId,
                true);

            if (externalTerminal.AllowsMultipleConnections ||
                !externalTerminal.Allows(ConnectionType.Cable) ||
                !externalTerminal.Allows(ConnectionType.OverheadLine))
            {
                throw new InvalidOperationException(
                    $"Interval '{interval.IntervalId}' external terminal has an invalid connection policy.");
            }

            EnsureNodeTerminals(
                circuitNode,
                [loadCircuitTerminal.Id, groundDeviceTerminal.Id, externalTerminal.Id]);
            EnsureNodeTerminals(earthNode, [groundSideTerminal.Id]);
            expectedMainBusTerminalIds.Add(loadBusTerminal.Id);
        }

        EnsureNodeTerminals(mainBusNode, expectedMainBusTerminalIds);
    }

    private void ValidateIntegratedBaseStructure(
        IReadOnlyDictionary<Guid, ElectricalNode> nodes,
        IReadOnlyDictionary<Guid, Terminal> terminals,
        ElectricalNode mainBusNode)
    {
        for (int index = 0; index < _intervals.Count; index++)
        {
            RingCabinetInterval interval = _intervals[index];

            if (interval.ParentCabinetId != Id ||
                interval.Sequence != index + 1 ||
                interval.IntervalKind != IntervalKind.CircuitBreakerInterval ||
                interval.IntermediateNodeId is not Guid intermediateNodeId)
            {
                throw new InvalidOperationException(
                    $"Interval '{interval.IntervalId}' has an invalid owner, sequence, kind, or node reference.");
            }

            SwitchDevice[] isolationSwitches = interval.SwitchDevices
                .Where(device => device.SwitchKind == SwitchKind.IsolationSwitch)
                .ToArray();
            SwitchDevice[] circuitBreakers = interval.SwitchDevices
                .Where(device => device.SwitchKind == SwitchKind.CircuitBreaker)
                .ToArray();
            SwitchDevice[] groundSwitches = interval.SwitchDevices
                .Where(device => device.SwitchKind == SwitchKind.GroundSwitch)
                .ToArray();

            if (interval.SwitchDevices.Count != 3 ||
                isolationSwitches.Length != 1 ||
                circuitBreakers.Length != 1 ||
                groundSwitches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Interval '{interval.IntervalId}' must contain one isolation switch, one circuit breaker, and one ground switch.");
            }

            SwitchDevice isolationSwitch = isolationSwitches[0];
            SwitchDevice circuitBreaker = circuitBreakers[0];
            SwitchDevice groundSwitch = groundSwitches[0];

            EnsureCabinetSwitchIsValid(isolationSwitch, interval.IntervalId);
            EnsureCabinetSwitchIsValid(circuitBreaker, interval.IntervalId);
            EnsureCabinetSwitchIsValid(groundSwitch, interval.IntervalId);

            SwitchAssemblyEvaluation evaluation = interval.SwitchAssembly.Evaluate();

            if (interval.SwitchAssembly.ParentIntervalId != interval.IntervalId ||
                interval.SwitchAssembly.AssemblyType != SwitchAssemblyType.IntegratedFeeder ||
                !interval.SwitchAssembly.MemberSwitchIds.ToHashSet().SetEquals(
                    interval.SwitchDevices.Select(device => device.Id)) ||
                interval.SwitchAssembly.InterlockRules.Count != 0 ||
                !evaluation.IsValid ||
                evaluation.OperationalState != OperationalState.Unclassified ||
                evaluation.IsEffectivelyGrounded)
            {
                throw new InvalidOperationException(
                    $"Interval '{interval.IntervalId}' has an invalid integrated-feeder base assembly.");
            }

            ElectricalNode intermediateNode = GetRequiredNode(
                nodes,
                intermediateNodeId,
                ElectricalNodeType.Intermediate,
                interval.IntervalId);
            ElectricalNode circuitNode = GetRequiredNode(
                nodes,
                interval.CircuitNodeId,
                ElectricalNodeType.Circuit,
                interval.IntervalId);
            ElectricalNode earthNode = GetRequiredNode(
                nodes,
                interval.EarthNodeId,
                ElectricalNodeType.Earth,
                interval.IntervalId);

            GetRequiredTerminal(
                terminals,
                isolationSwitch.TerminalIds[0],
                TopologyOwnerType.Device,
                isolationSwitch.Id,
                null,
                false);
            GetRequiredTerminal(
                terminals,
                isolationSwitch.TerminalIds[1],
                TopologyOwnerType.Device,
                isolationSwitch.Id,
                null,
                false);
            GetRequiredTerminal(
                terminals,
                circuitBreaker.TerminalIds[0],
                TopologyOwnerType.Device,
                circuitBreaker.Id,
                null,
                false);
            GetRequiredTerminal(
                terminals,
                circuitBreaker.TerminalIds[1],
                TopologyOwnerType.Device,
                circuitBreaker.Id,
                null,
                false);
            GetRequiredTerminal(
                terminals,
                groundSwitch.TerminalIds[0],
                TopologyOwnerType.Device,
                groundSwitch.Id,
                null,
                false);
            Terminal groundSideTerminal = GetRequiredTerminal(
                terminals,
                groundSwitch.TerminalIds[1],
                TopologyOwnerType.Device,
                groundSwitch.Id,
                interval.EarthNodeId,
                false);
            Terminal externalTerminal = GetRequiredTerminal(
                terminals,
                interval.ExternalTerminalId,
                TopologyOwnerType.InternalAggregate,
                interval.IntervalId,
                interval.CircuitNodeId,
                true);

            if (externalTerminal.AllowsMultipleConnections ||
                !externalTerminal.Allows(ConnectionType.Cable) ||
                !externalTerminal.Allows(ConnectionType.OverheadLine))
            {
                throw new InvalidOperationException(
                    $"Interval '{interval.IntervalId}' external terminal has an invalid connection policy.");
            }

            EnsureNodeTerminals(intermediateNode, []);
            EnsureNodeTerminals(circuitNode, [externalTerminal.Id]);
            EnsureNodeTerminals(earthNode, [groundSideTerminal.Id]);
        }

        EnsureNodeTerminals(mainBusNode, []);
    }

    private static void AddTerminal(
        ICollection<Terminal> terminals,
        ElectricalNode electricalNode,
        Terminal terminal)
    {
        terminals.Add(terminal);
        electricalNode.AttachTerminal(terminal.Id);
    }

    private static Terminal CreateUnboundInternalTerminal(
        Guid terminalId,
        Guid switchDeviceId,
        string role,
        string voltageLevel)
    {
        return new Terminal(
            terminalId,
            TopologyOwnerType.Device,
            switchDeviceId,
            role,
            voltageLevel,
            false,
            false);
    }

    private static void EnsureCabinetSwitchIsValid(SwitchDevice switchDevice, Guid intervalId)
    {
        if (switchDevice.InstallationType != SwitchInstallationType.CabinetInterval ||
            switchDevice.ParentId != intervalId ||
            switchDevice.TerminalIds.Count != 2)
        {
            throw new InvalidOperationException(
                $"Switch '{switchDevice.Id}' is not correctly owned by its interval.");
        }
    }

    private static ElectricalNode GetRequiredNode(
        IReadOnlyDictionary<Guid, ElectricalNode> nodes,
        Guid nodeId,
        ElectricalNodeType expectedType,
        Guid expectedOwnerId)
    {
        if (!nodes.TryGetValue(nodeId, out ElectricalNode? node) ||
            node.Type != expectedType ||
            node.OwnerType != TopologyOwnerType.InternalAggregate ||
            node.OwnerId != expectedOwnerId)
        {
            throw new InvalidOperationException(
                $"Electrical node '{nodeId}' is missing or has an invalid owner or type.");
        }

        return node;
    }

    private static Terminal GetRequiredTerminal(
        IReadOnlyDictionary<Guid, Terminal> terminals,
        Guid terminalId,
        TopologyOwnerType expectedOwnerType,
        Guid expectedOwnerId,
        Guid? expectedNodeId,
        bool expectedExternal)
    {
        if (!terminals.TryGetValue(terminalId, out Terminal? terminal) ||
            terminal.OwnerType != expectedOwnerType ||
            terminal.OwnerId != expectedOwnerId ||
            terminal.ElectricalNodeId != expectedNodeId ||
            terminal.IsExternal != expectedExternal)
        {
            throw new InvalidOperationException(
                $"Terminal '{terminalId}' is missing or has invalid references.");
        }

        return terminal;
    }

    private static void EnsureNodeTerminals(
        ElectricalNode node,
        IEnumerable<Guid> expectedTerminalIds)
    {
        if (!node.TerminalIds.SetEquals(expectedTerminalIds))
        {
            throw new InvalidOperationException(
                $"Electrical node '{node.Id}' has invalid terminal references.");
        }
    }

    private void EnsureAggregateObjectIdsAreUnique()
    {
        var ids = new HashSet<Guid>();

        AddUniqueId(ids, Id, nameof(RingCabinet));

        foreach (RingCabinetInterval interval in _intervals)
        {
            AddUniqueId(ids, interval.IntervalId, nameof(RingCabinetInterval));
            AddUniqueId(ids, interval.SwitchAssembly.AssemblyId, nameof(SwitchAssembly));

            foreach (SwitchDevice switchDevice in interval.SwitchDevices)
            {
                AddUniqueId(ids, switchDevice.Id, nameof(SwitchDevice));
            }
        }

        foreach (ElectricalNode node in _electricalNodes)
        {
            AddUniqueId(ids, node.Id, nameof(ElectricalNode));
        }

        foreach (Terminal terminal in _terminals)
        {
            AddUniqueId(ids, terminal.Id, nameof(Terminal));
        }
    }

    private static void AddUniqueId(HashSet<Guid> ids, Guid id, string objectName)
    {
        if (!ids.Add(id))
        {
            throw new InvalidOperationException(
                $"{objectName} ID '{id}' is duplicated in the cabinet aggregate.");
        }
    }
}
