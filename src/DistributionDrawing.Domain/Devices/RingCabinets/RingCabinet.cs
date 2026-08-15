using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.SwitchAssemblies;
using DistributionDrawing.Domain.Topology;
using SwitchStateValue = DistributionDrawing.Domain.Devices.SwitchState;

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

    private IReadOnlyList<RingCabinetInterval> _intervals;
    private IReadOnlyList<ElectricalNode> _electricalNodes;
    private IReadOnlyList<Terminal> _terminals;

    private RingCabinet(
        Guid id,
        string displayName,
        Guid mainBusNodeId,
        IEnumerable<RingCabinetInterval> intervals,
        IEnumerable<ElectricalNode> electricalNodes,
        IEnumerable<Terminal> terminals)
        : base(id, DeviceType.RingCabinet, displayName, TenKilovolts)
    {
        MainBusNodeId = mainBusNodeId;
        _intervals = Array.AsReadOnly(intervals.ToArray());
        _electricalNodes = Array.AsReadOnly(electricalNodes.ToArray());
        _terminals = Array.AsReadOnly(terminals.ToArray());
        CompositionKind = DetermineCompositionKind(_intervals);
    }

    public Guid MainBusNodeId { get; }

    public CabinetCompositionKind CompositionKind { get; private set; }

    public IReadOnlyList<RingCabinetInterval> Intervals => _intervals;

    public IReadOnlyList<ElectricalNode> ElectricalNodes => _electricalNodes;

    public IReadOnlyList<Terminal> Terminals => _terminals;

    public RingCabinetRestoreDefinition CaptureRestoreDefinition()
    {
        return new RingCabinetRestoreDefinition(
            Id,
            DisplayName ?? throw new InvalidOperationException(
                "A ring cabinet must have a display name."),
            MainBusNodeId,
            _intervals.Select(CreateRestoreDefinition).ToArray());
    }

    public void RestoreState(RingCabinetRestoreDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.CabinetId != Id)
        {
            throw new ArgumentException(
                "The restore definition belongs to another ring cabinet.",
                nameof(definition));
        }

        if (definition.MainBusNodeId != MainBusNodeId)
        {
            throw new ArgumentException(
                "The restore definition belongs to another ring cabinet topology.",
                nameof(definition));
        }

        RingCabinet candidate = Restore(definition);
        _intervals = candidate._intervals;
        _electricalNodes = candidate._electricalNodes;
        _terminals = candidate._terminals;
        CompositionKind = candidate.CompositionKind;
    }

    public void ChangeIntervalType(
        Guid intervalId,
        IntervalKind targetIntervalKind,
        GroundingStructureKind? targetGroundingStructureKind = null)
    {
        if (intervalId == Guid.Empty)
        {
            throw new ArgumentException("Interval ID cannot be empty.", nameof(intervalId));
        }

        if (!Enum.IsDefined(targetIntervalKind))
        {
            throw new ArgumentOutOfRangeException(nameof(targetIntervalKind));
        }

        RingCabinetInterval currentInterval = _intervals
            .FirstOrDefault(interval => interval.IntervalId == intervalId)
            ?? throw new InvalidOperationException(
                $"Interval '{intervalId}' does not belong to cabinet '{Id}'.");

        ValidateTypeChangeConfiguration(
            currentInterval,
            targetIntervalKind,
            targetGroundingStructureKind);

        if (currentInterval.IntervalKind == targetIntervalKind &&
            currentInterval.GroundingStructureKind == targetGroundingStructureKind)
        {
            return;
        }

        RingCabinetIntervalRestoreDefinition[] definitions = _intervals
            .Select(interval => interval.IntervalId == intervalId
                ? CreateTypeChangeIntervalDefinition(
                    interval,
                    targetIntervalKind,
                    targetGroundingStructureKind)
                : CreateRestoreDefinition(interval))
            .ToArray();

        RingCabinet candidate = Restore(new RingCabinetRestoreDefinition(
            Id,
            DisplayName ?? throw new InvalidOperationException(
                "A ring cabinet must have a display name."),
            MainBusNodeId,
            definitions));

        _intervals = candidate._intervals;
        _electricalNodes = candidate._electricalNodes;
        _terminals = candidate._terminals;
        CompositionKind = candidate.CompositionKind;
    }

    internal IEnumerable<SwitchDevice> InternalSwitchDevices =>
        _intervals.SelectMany(interval => interval.SwitchDevices);

    internal IEnumerable<SwitchAssembly> InternalSwitchAssemblies =>
        _intervals.Select(interval => interval.SwitchAssembly);

    public static RingCabinet Create(RingCabinetDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        Guid mainBusNodeId = Guid.NewGuid();
        var electricalNodes = new List<ElectricalNode>();
        var terminals = new List<Terminal>();
        var intervals = new List<RingCabinetInterval>();

        var mainBusNode = new ElectricalNode(
            mainBusNodeId,
            ElectricalNodeType.MainBus,
            TopologyOwnerType.Device,
            definition.CabinetId);

        electricalNodes.Add(mainBusNode);

        for (int index = 0; index < definition.IntervalDefinitions.Count; index++)
        {
            RingCabinetIntervalDefinition intervalDefinition =
                definition.IntervalDefinitions[index];
            int sequence = index + 1;

            RingCabinetInterval interval = intervalDefinition.IntervalKind switch
            {
                IntervalKind.LoadSwitchInterval => CreateLoadSwitchInterval(
                    definition.CabinetId,
                    sequence,
                    intervalDefinition,
                    mainBusNode,
                    electricalNodes,
                    terminals),
                IntervalKind.IntegratedFeederInterval => CreateIntegratedFeederInterval(
                    definition.CabinetId,
                    sequence,
                    intervalDefinition,
                    mainBusNode,
                    electricalNodes,
                    terminals),
                IntervalKind.PTInterval => CreatePTInterval(
                    definition.CabinetId,
                    sequence,
                    intervalDefinition,
                    mainBusNode,
                    electricalNodes,
                    terminals),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(intervalDefinition),
                    $"Unsupported interval kind '{intervalDefinition.IntervalKind}'.")
            };

            intervals.Add(interval);
        }

        var cabinet = new RingCabinet(
            definition.CabinetId,
            definition.DisplayName,
            mainBusNodeId,
            intervals,
            electricalNodes,
            terminals);

        cabinet.ValidateStructure();
        return cabinet;
    }

    /// <summary>
    /// Rebuilds a complete cabinet aggregate using the IDs from persistence.
    /// Unlike the creation factories, this method never generates replacement
    /// IDs for the cabinet's internal objects.
    /// </summary>
    public static RingCabinet Restore(RingCabinetRestoreDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        RingCabinetIntervalRestoreDefinition[] intervalDefinitions =
            definition.Intervals?.ToArray()
            ?? throw new ArgumentNullException(nameof(definition.Intervals));

        if (intervalDefinitions.Length == 0)
        {
            throw new ArgumentException(
                "A ring cabinet requires at least one interval.",
                nameof(definition));
        }

        var mainBusNode = new ElectricalNode(
            definition.MainBusNodeId,
            ElectricalNodeType.MainBus,
            TopologyOwnerType.Device,
            definition.CabinetId);
        var electricalNodes = new List<ElectricalNode> { mainBusNode };
        var terminals = new List<Terminal>();
        var intervals = new List<RingCabinetInterval>(intervalDefinitions.Length);
        var bayIndexes = new HashSet<int>();

        for (int index = 0; index < intervalDefinitions.Length; index++)
        {
            RingCabinetIntervalRestoreDefinition intervalDefinition =
                intervalDefinitions[index];

            if (intervalDefinition.ParentCabinetId != definition.CabinetId ||
                intervalDefinition.Sequence != index + 1)
            {
                throw new InvalidOperationException(
                    $"Interval '{intervalDefinition.IntervalId}' has an invalid owner or sequence.");
            }

            int bayIndex = intervalDefinition.BayIndex;
            if (bayIndex < 1 || !bayIndexes.Add(bayIndex))
            {
                throw new InvalidOperationException(
                    $"Interval '{intervalDefinition.IntervalId}' has an invalid or duplicate bay index.");
            }

            RingCabinetInterval interval = intervalDefinition.IntervalKind switch
            {
                IntervalKind.LoadSwitchInterval => CreateRestoredLoadSwitchInterval(
                    definition.CabinetId,
                    intervalDefinition,
                    mainBusNode,
                    electricalNodes,
                    terminals),
                IntervalKind.IntegratedFeederInterval => CreateRestoredIntegratedFeederInterval(
                    definition.CabinetId,
                    intervalDefinition,
                    mainBusNode,
                    electricalNodes,
                    terminals),
                IntervalKind.PTInterval => CreateRestoredPTInterval(
                    definition.CabinetId,
                    intervalDefinition,
                    mainBusNode,
                    electricalNodes,
                    terminals),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(intervalDefinition.IntervalKind))
            };

            intervals.Add(interval);
        }

        var cabinet = new RingCabinet(
            definition.CabinetId,
            definition.DisplayName,
            definition.MainBusNodeId,
            intervals,
            electricalNodes,
            terminals);

        cabinet.ValidateStructure();
        return cabinet;
    }

    private void ValidateTypeChangeConfiguration(
        RingCabinetInterval currentInterval,
        IntervalKind targetIntervalKind,
        GroundingStructureKind? targetGroundingStructureKind)
    {
        if (targetIntervalKind == IntervalKind.IntegratedFeederInterval)
        {
            if (targetGroundingStructureKind is not GroundingStructureKind structureKind ||
                !Enum.IsDefined(structureKind))
            {
                throw new ArgumentException(
                    "An integrated-feeder interval requires a valid grounding structure.",
                    nameof(targetGroundingStructureKind));
            }
        }
        else if (targetGroundingStructureKind is not null)
        {
            throw new ArgumentException(
                "Only an integrated-feeder interval accepts a grounding structure.",
                nameof(targetGroundingStructureKind));
        }

        bool alreadyContainsPT = _intervals.Any(interval =>
            interval.IntervalKind == IntervalKind.PTInterval &&
            interval.IntervalId != currentInterval.IntervalId);
        if (targetIntervalKind == IntervalKind.PTInterval && alreadyContainsPT)
        {
            throw new InvalidOperationException(
                "A ring cabinet can contain at most one PT interval.");
        }
    }

    private static RingCabinetIntervalRestoreDefinition CreateRestoreDefinition(
        RingCabinetInterval interval)
    {
        return new RingCabinetIntervalRestoreDefinition(
            interval.IntervalId,
            interval.ParentCabinetId,
            interval.Sequence,
            interval.BayIndex,
            interval.DisplayName,
            interval.IntervalKind,
            interval.GroundingStructureKind,
            interval.IntermediateNodeId,
            interval.CircuitNodeId,
            interval.EarthNodeId,
            interval.ExternalTerminalId,
            interval.SwitchAssembly.AssemblyId,
            interval.SwitchDevices.Select(device => new SwitchDeviceRestoreDefinition(
                device.Id,
                device.SwitchKind,
                device.InstallationType,
                device.TerminalIds[0],
                device.TerminalIds[1],
                device.SwitchState ?? throw new InvalidOperationException(
                    "A ring cabinet switch must have a state."),
                device.DisplayName ?? throw new InvalidOperationException(
                    "A ring cabinet switch must have a display name."),
                device.VoltageLevel ?? TenKilovolts,
                device.DispatchNumber)).ToArray());
    }

    private static RingCabinetIntervalRestoreDefinition CreateTypeChangeIntervalDefinition(
        RingCabinetInterval interval,
        IntervalKind targetIntervalKind,
        GroundingStructureKind? targetGroundingStructureKind)
    {
        Guid circuitNodeId = Guid.NewGuid();
        Guid earthNodeId = Guid.NewGuid();
        Guid externalTerminalId = Guid.NewGuid();
        Guid? intermediateNodeId = null;
        List<SwitchDeviceRestoreDefinition> switches = [];

        SwitchState GetState(SwitchKind switchKind)
        {
            return interval.SwitchDevices
                .FirstOrDefault(device => device.SwitchKind == switchKind)
                ?.SwitchState
                ?? SwitchStateValue.Open;
        }

        string switchName(string suffix) => $"{interval.DisplayName}{suffix}";

        switch (targetIntervalKind)
        {
            case IntervalKind.LoadSwitchInterval:
                switches.Add(CreateSwitchRestoreDefinition(
                    SwitchKind.LoadSwitch,
                    GetState(SwitchKind.LoadSwitch),
                    switchName("负荷开关"),
                    Guid.NewGuid(),
                    Guid.NewGuid()));
                switches.Add(CreateSwitchRestoreDefinition(
                    SwitchKind.GroundSwitch,
                    GetState(SwitchKind.GroundSwitch),
                    switchName("接地刀闸"),
                    Guid.NewGuid(),
                    Guid.NewGuid()));
                break;

            case IntervalKind.PTInterval:
                switches.Add(CreateSwitchRestoreDefinition(
                    SwitchKind.IsolationSwitch,
                    GetState(SwitchKind.IsolationSwitch),
                    switchName("PT隔离刀闸"),
                    Guid.NewGuid(),
                    Guid.NewGuid()));
                switches.Add(CreateSwitchRestoreDefinition(
                    SwitchKind.GroundSwitch,
                    GetState(SwitchKind.GroundSwitch),
                    switchName("PT接地刀闸"),
                    Guid.NewGuid(),
                    Guid.NewGuid()));
                break;

            case IntervalKind.IntegratedFeederInterval:
                intermediateNodeId = Guid.NewGuid();
                switches.Add(CreateSwitchRestoreDefinition(
                    SwitchKind.IsolationSwitch,
                    GetState(SwitchKind.IsolationSwitch),
                    switchName("隔离刀闸"),
                    Guid.NewGuid(),
                    Guid.NewGuid()));
                switches.Add(CreateSwitchRestoreDefinition(
                    SwitchKind.CircuitBreaker,
                    GetState(SwitchKind.CircuitBreaker),
                    switchName("断路器"),
                    Guid.NewGuid(),
                    Guid.NewGuid()));
                switches.Add(CreateSwitchRestoreDefinition(
                    SwitchKind.GroundSwitch,
                    GetState(SwitchKind.GroundSwitch),
                    switchName("接地刀闸"),
                    Guid.NewGuid(),
                    Guid.NewGuid()));
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(targetIntervalKind));
        }

        return new RingCabinetIntervalRestoreDefinition(
            interval.IntervalId,
            interval.ParentCabinetId,
            interval.Sequence,
            interval.BayIndex,
            interval.DisplayName,
            targetIntervalKind,
            targetGroundingStructureKind,
            intermediateNodeId,
            circuitNodeId,
            earthNodeId,
            externalTerminalId,
            Guid.NewGuid(),
            switches);
    }

    private static SwitchDeviceRestoreDefinition CreateSwitchRestoreDefinition(
        SwitchKind switchKind,
        SwitchState switchState,
        string displayName,
        Guid firstTerminalId,
        Guid secondTerminalId)
    {
        return new SwitchDeviceRestoreDefinition(
            Guid.NewGuid(),
            switchKind,
            SwitchInstallationType.CabinetInterval,
            firstTerminalId,
            secondTerminalId,
            switchState,
            displayName,
            TenKilovolts,
            null);
    }

    private static RingCabinetInterval CreateRestoredLoadSwitchInterval(
        Guid cabinetId,
        RingCabinetIntervalRestoreDefinition definition,
        ElectricalNode mainBusNode,
        ICollection<ElectricalNode> electricalNodes,
        ICollection<Terminal> terminals)
    {
        if (definition.GroundingStructureKind is not null ||
            definition.IntermediateNodeId is not null)
        {
            throw new ArgumentException(
                "A load-switch interval cannot have integrated-feeder fields.",
                nameof(definition));
        }

        SwitchDevice loadSwitch = CreateRestoredSwitch(
            GetRestoredSwitch(definition, SwitchKind.LoadSwitch),
            definition.IntervalId);
        SwitchDevice groundSwitch = CreateRestoredSwitch(
            GetRestoredSwitch(definition, SwitchKind.GroundSwitch),
            definition.IntervalId);

        var circuitNode = new ElectricalNode(
            definition.CircuitNodeId,
            ElectricalNodeType.Circuit,
            TopologyOwnerType.InternalAggregate,
            definition.IntervalId);
        var earthNode = new ElectricalNode(
            definition.EarthNodeId,
            ElectricalNodeType.Earth,
            TopologyOwnerType.InternalAggregate,
            definition.IntervalId);

        SwitchAssembly switchAssembly = SwitchAssembly.CreateLoadSwitchThreePosition(
            definition.SwitchAssemblyId,
            definition.IntervalId,
            loadSwitch,
            groundSwitch);

        AddTerminal(
            terminals,
            mainBusNode,
            new Terminal(
                loadSwitch.TerminalIds[0],
                TopologyOwnerType.Device,
                loadSwitch.Id,
                BusSideRole,
                TenKilovolts,
                false,
                false,
                mainBusNode.Id));
        AddTerminal(
            terminals,
            circuitNode,
            new Terminal(
                loadSwitch.TerminalIds[1],
                TopologyOwnerType.Device,
                loadSwitch.Id,
                CircuitSideRole,
                TenKilovolts,
                false,
                false,
                circuitNode.Id));
        AddTerminal(
            terminals,
            circuitNode,
            new Terminal(
                groundSwitch.TerminalIds[0],
                TopologyOwnerType.Device,
                groundSwitch.Id,
                DeviceSideRole,
                TenKilovolts,
                false,
                false,
                circuitNode.Id));
        AddTerminal(
            terminals,
            earthNode,
            new Terminal(
                groundSwitch.TerminalIds[1],
                TopologyOwnerType.Device,
                groundSwitch.Id,
                GroundSideRole,
                null,
                false,
                false,
                earthNode.Id));
        AddTerminal(
            terminals,
            circuitNode,
            CreateExternalTerminal(
                definition.ExternalTerminalId,
                definition.IntervalId,
                circuitNode.Id));

        electricalNodes.Add(circuitNode);
        electricalNodes.Add(earthNode);

        return new RingCabinetInterval(
            definition.IntervalId,
            cabinetId,
            definition.Sequence,
            definition.BayIndex,
            definition.DisplayName,
            IntervalKind.LoadSwitchInterval,
            [loadSwitch, groundSwitch],
            switchAssembly,
            null,
            null,
            definition.CircuitNodeId,
            definition.EarthNodeId,
            definition.ExternalTerminalId);
    }

    private static RingCabinetInterval CreateRestoredPTInterval(
        Guid cabinetId,
        RingCabinetIntervalRestoreDefinition definition,
        ElectricalNode mainBusNode,
        ICollection<ElectricalNode> electricalNodes,
        ICollection<Terminal> terminals)
    {
        if (definition.GroundingStructureKind is not null ||
            definition.IntermediateNodeId is not null)
        {
            throw new ArgumentException(
                "A PT interval cannot have integrated-feeder fields.",
                nameof(definition));
        }

        SwitchDevice isolationSwitch = CreateRestoredSwitch(
            GetRestoredSwitch(definition, SwitchKind.IsolationSwitch),
            definition.IntervalId);
        SwitchDevice groundSwitch = CreateRestoredSwitch(
            GetRestoredSwitch(definition, SwitchKind.GroundSwitch),
            definition.IntervalId);

        var circuitNode = new ElectricalNode(
            definition.CircuitNodeId,
            ElectricalNodeType.Circuit,
            TopologyOwnerType.InternalAggregate,
            definition.IntervalId);
        var earthNode = new ElectricalNode(
            definition.EarthNodeId,
            ElectricalNodeType.Earth,
            TopologyOwnerType.InternalAggregate,
            definition.IntervalId);
        SwitchAssembly switchAssembly = SwitchAssembly.CreatePT(
            definition.SwitchAssemblyId,
            definition.IntervalId,
            isolationSwitch,
            groundSwitch);

        AddInternalSwitchTerminal(
            terminals, mainBusNode, isolationSwitch.TerminalIds[0], isolationSwitch.Id,
            FirstTerminalRole, TenKilovolts);
        AddInternalSwitchTerminal(
            terminals, circuitNode, isolationSwitch.TerminalIds[1], isolationSwitch.Id,
            SecondTerminalRole, TenKilovolts);
        AddInternalSwitchTerminal(
            terminals, circuitNode, groundSwitch.TerminalIds[0], groundSwitch.Id,
            DeviceSideRole, TenKilovolts);
        AddTerminal(
            terminals,
            earthNode,
            new Terminal(
                groundSwitch.TerminalIds[1],
                TopologyOwnerType.Device,
                groundSwitch.Id,
                GroundSideRole,
                null,
                false,
                false,
                earthNode.Id));
        AddTerminal(
            terminals,
            circuitNode,
            CreateExternalTerminal(
                definition.ExternalTerminalId,
                definition.IntervalId,
                definition.CircuitNodeId));

        electricalNodes.Add(circuitNode);
        electricalNodes.Add(earthNode);

        return new RingCabinetInterval(
            definition.IntervalId,
            cabinetId,
            definition.Sequence,
            definition.BayIndex,
            definition.DisplayName,
            IntervalKind.PTInterval,
            [isolationSwitch, groundSwitch],
            switchAssembly,
            null,
            null,
            definition.CircuitNodeId,
            definition.EarthNodeId,
            definition.ExternalTerminalId);
    }

    private static RingCabinetInterval CreateRestoredIntegratedFeederInterval(
        Guid cabinetId,
        RingCabinetIntervalRestoreDefinition definition,
        ElectricalNode mainBusNode,
        ICollection<ElectricalNode> electricalNodes,
        ICollection<Terminal> terminals)
    {
        GroundingStructureKind groundingStructureKind =
            definition.GroundingStructureKind
            ?? throw new ArgumentException(
                "An integrated-feeder interval requires a grounding structure.",
                nameof(definition));
        Guid intermediateNodeId = definition.IntermediateNodeId
            ?? throw new ArgumentException(
                "An integrated-feeder interval requires an intermediate node.",
                nameof(definition));

        SwitchDevice isolationSwitch = CreateRestoredSwitch(
            GetRestoredSwitch(definition, SwitchKind.IsolationSwitch),
            definition.IntervalId);
        SwitchDevice circuitBreaker = CreateRestoredSwitch(
            GetRestoredSwitch(definition, SwitchKind.CircuitBreaker),
            definition.IntervalId);
        SwitchDevice groundSwitch = CreateRestoredSwitch(
            GetRestoredSwitch(definition, SwitchKind.GroundSwitch),
            definition.IntervalId);

        var intermediateNode = new ElectricalNode(
            intermediateNodeId,
            ElectricalNodeType.Intermediate,
            TopologyOwnerType.InternalAggregate,
            definition.IntervalId);
        var circuitNode = new ElectricalNode(
            definition.CircuitNodeId,
            ElectricalNodeType.Circuit,
            TopologyOwnerType.InternalAggregate,
            definition.IntervalId);
        var earthNode = new ElectricalNode(
            definition.EarthNodeId,
            ElectricalNodeType.Earth,
            TopologyOwnerType.InternalAggregate,
            definition.IntervalId);

        SwitchAssembly switchAssembly = SwitchAssembly.CreateIntegratedFeeder(
            definition.SwitchAssemblyId,
            definition.IntervalId,
            groundingStructureKind,
            isolationSwitch,
            circuitBreaker,
            groundSwitch);

        (
            ElectricalNode isolationFirstNode,
            ElectricalNode isolationSecondNode,
            ElectricalNode breakerFirstNode,
            ElectricalNode breakerSecondNode,
            ElectricalNode groundSwitchDeviceNode) = groundingStructureKind switch
        {
            GroundingStructureKind.UpperIsolationGrounding =>
                (mainBusNode, intermediateNode, intermediateNode, circuitNode, intermediateNode),
            GroundingStructureKind.UpperLowerGrounding =>
                (mainBusNode, intermediateNode, intermediateNode, circuitNode, circuitNode),
            GroundingStructureKind.LowerLowerGrounding =>
                (intermediateNode, circuitNode, mainBusNode, intermediateNode, circuitNode),
            _ => throw new ArgumentOutOfRangeException(nameof(groundingStructureKind))
        };

        AddInternalSwitchTerminal(
            terminals,
            isolationFirstNode,
            isolationSwitch.TerminalIds[0],
            isolationSwitch.Id,
            FirstTerminalRole,
            TenKilovolts);
        AddInternalSwitchTerminal(
            terminals,
            isolationSecondNode,
            isolationSwitch.TerminalIds[1],
            isolationSwitch.Id,
            SecondTerminalRole,
            TenKilovolts);
        AddInternalSwitchTerminal(
            terminals,
            breakerFirstNode,
            circuitBreaker.TerminalIds[0],
            circuitBreaker.Id,
            FirstTerminalRole,
            TenKilovolts);
        AddInternalSwitchTerminal(
            terminals,
            breakerSecondNode,
            circuitBreaker.TerminalIds[1],
            circuitBreaker.Id,
            SecondTerminalRole,
            TenKilovolts);
        AddInternalSwitchTerminal(
            terminals,
            groundSwitchDeviceNode,
            groundSwitch.TerminalIds[0],
            groundSwitch.Id,
            DeviceSideRole,
            TenKilovolts);
        AddTerminal(
            terminals,
            earthNode,
            new Terminal(
                groundSwitch.TerminalIds[1],
                TopologyOwnerType.Device,
                groundSwitch.Id,
                GroundSideRole,
                null,
                false,
                false,
                earthNode.Id));
        AddTerminal(
            terminals,
            circuitNode,
            CreateExternalTerminal(
                definition.ExternalTerminalId,
                definition.IntervalId,
                circuitNode.Id));

        electricalNodes.Add(intermediateNode);
        electricalNodes.Add(circuitNode);
        electricalNodes.Add(earthNode);

        return new RingCabinetInterval(
            definition.IntervalId,
            cabinetId,
            definition.Sequence,
            definition.BayIndex,
            definition.DisplayName,
            IntervalKind.IntegratedFeederInterval,
            [isolationSwitch, circuitBreaker, groundSwitch],
            switchAssembly,
            groundingStructureKind,
            intermediateNodeId,
            definition.CircuitNodeId,
            definition.EarthNodeId,
            definition.ExternalTerminalId);
    }

    private static SwitchDeviceRestoreDefinition GetRestoredSwitch(
        RingCabinetIntervalRestoreDefinition definition,
        SwitchKind switchKind)
    {
        SwitchDeviceRestoreDefinition[] matches = definition.Switches
            ?.Where(candidate => candidate.SwitchKind == switchKind)
            .ToArray()
            ?? throw new ArgumentNullException(nameof(definition.Switches));

        if (matches.Length != 1)
        {
            throw new ArgumentException(
                $"Interval '{definition.IntervalId}' requires exactly one '{switchKind}' switch.",
                nameof(definition));
        }

        return matches[0];
    }

    private static SwitchDevice CreateRestoredSwitch(
        SwitchDeviceRestoreDefinition definition,
        Guid intervalId)
    {
        if (definition.InstallationType != SwitchInstallationType.CabinetInterval)
        {
            throw new ArgumentException(
                "A ring cabinet switch must use CabinetInterval installation.",
                nameof(definition));
        }

        return new SwitchDevice(
            definition.Id,
            definition.SwitchKind,
            definition.InstallationType,
            definition.FirstTerminalId,
            definition.SecondTerminalId,
            definition.SwitchState,
            definition.DisplayName,
            definition.VoltageLevel,
            intervalId,
            definition.DispatchNumber);
    }

    public SwitchAssemblyEvaluation EvaluateIntegratedFeederInterval(Guid intervalId)
    {
        if (intervalId == Guid.Empty)
        {
            throw new ArgumentException("Interval ID cannot be empty.", nameof(intervalId));
        }

        ValidateStructure();

        RingCabinetInterval interval = _intervals
            .FirstOrDefault(candidate => candidate.IntervalId == intervalId)
            ?? throw new InvalidOperationException(
                $"Interval '{intervalId}' does not belong to cabinet '{Id}'.");

        if (interval.IntervalKind != IntervalKind.IntegratedFeederInterval ||
            interval.GroundingStructureKind is not GroundingStructureKind groundingStructureKind)
        {
            throw new InvalidOperationException(
                $"Interval '{intervalId}' is not an integrated-feeder interval.");
        }

        SwitchDevice isolationSwitch = GetSingleSwitch(
            interval,
            SwitchKind.IsolationSwitch,
            expectedDeviceCount: 3);
        SwitchDevice circuitBreaker = GetSingleSwitch(interval, SwitchKind.CircuitBreaker);
        SwitchDevice groundSwitch = GetSingleSwitch(interval, SwitchKind.GroundSwitch);

        SwitchAssemblyEvaluation interlockEvaluation = interval.SwitchAssembly.Evaluate();

        if (!interlockEvaluation.IsValid)
        {
            return new SwitchAssemblyEvaluation(
                false,
                OperationalState.Unclassified,
                false,
                interlockEvaluation.ViolatedRuleCodes);
        }

        SwitchState isolationState = GetRequiredSwitchState(isolationSwitch);
        SwitchState circuitBreakerState = GetRequiredSwitchState(circuitBreaker);
        SwitchState groundState = GetRequiredSwitchState(groundSwitch);
        OperationalState operationalState = EvaluateIntegratedFeederOperationalState(
            groundingStructureKind,
            isolationState,
            circuitBreakerState,
            groundState);

        Dictionary<Guid, ElectricalNode> nodes =
            _electricalNodes.ToDictionary(node => node.Id);
        Dictionary<Guid, Terminal> terminals = _terminals.ToDictionary(terminal => terminal.Id);
        bool isEffectivelyGrounded = HasClosedPathFromExternalTerminalToEarth(
            interval,
            nodes,
            terminals);

        return new SwitchAssemblyEvaluation(
            true,
            operationalState,
            isEffectivelyGrounded,
            []);
    }

    internal void ValidateStructure()
    {
        if (_intervals.Count == 0)
        {
            throw new InvalidOperationException(
                "A ring cabinet must contain at least one interval.");
        }

        ValidatePureTemplateIntervalCount();
        EnsureAggregateObjectIdsAreUnique();

        if (_intervals.Select(interval => interval.BayIndex).Distinct().Count() != _intervals.Count)
        {
            throw new InvalidOperationException(
                "Bay indexes must be unique within a ring cabinet.");
        }

        if (_intervals.Count(interval => interval.IntervalKind == IntervalKind.PTInterval) > 1)
        {
            throw new InvalidOperationException(
                "A ring cabinet can contain at most one PT interval.");
        }

        Dictionary<Guid, ElectricalNode> nodes = _electricalNodes.ToDictionary(node => node.Id);
        Dictionary<Guid, Terminal> terminals = _terminals.ToDictionary(terminal => terminal.Id);

        if (!nodes.TryGetValue(MainBusNodeId, out ElectricalNode? mainBusNode) ||
            mainBusNode.Type != ElectricalNodeType.MainBus ||
            mainBusNode.OwnerType != TopologyOwnerType.Device ||
            mainBusNode.OwnerId != Id)
        {
            throw new InvalidOperationException("The cabinet main bus node is invalid.");
        }

        var expectedMainBusTerminalIds = new List<Guid>();

        for (int index = 0; index < _intervals.Count; index++)
        {
            RingCabinetInterval interval = _intervals[index];

            if (interval.ParentCabinetId != Id || interval.Sequence != index + 1)
            {
                throw new InvalidOperationException(
                    $"Interval '{interval.IntervalId}' has an invalid owner or sequence.");
            }

            switch (interval.IntervalKind)
            {
                case IntervalKind.LoadSwitchInterval:
                    expectedMainBusTerminalIds.Add(
                        ValidateLoadSwitchInterval(interval, nodes, terminals));
                    break;
                case IntervalKind.IntegratedFeederInterval:
                    expectedMainBusTerminalIds.Add(
                        ValidateIntegratedFeederInterval(interval, nodes, terminals));
                    break;
                case IntervalKind.PTInterval:
                    expectedMainBusTerminalIds.Add(
                        ValidatePTInterval(interval, nodes, terminals));
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Interval '{interval.IntervalId}' has an unsupported kind.");
            }
        }

        EnsureNodeTerminals(mainBusNode, expectedMainBusTerminalIds);
    }

    private static RingCabinetInterval CreateLoadSwitchInterval(
        Guid cabinetId,
        int sequence,
        RingCabinetIntervalDefinition definition,
        ElectricalNode mainBusNode,
        ICollection<ElectricalNode> electricalNodes,
        ICollection<Terminal> terminals)
    {
        SwitchState initialLoadSwitchState = definition.InitialLoadSwitchState
            ?? throw new InvalidOperationException(
                "A load-switch interval definition requires a load-switch state.");

        Guid intervalId = Guid.NewGuid();
        Guid circuitNodeId = Guid.NewGuid();
        Guid earthNodeId = Guid.NewGuid();
        Guid externalTerminalId = Guid.NewGuid();
        Guid loadSwitchBusTerminalId = Guid.NewGuid();
        Guid loadSwitchCircuitTerminalId = Guid.NewGuid();
        Guid groundSwitchDeviceTerminalId = Guid.NewGuid();
        Guid groundSwitchGroundTerminalId = Guid.NewGuid();
        string intervalName = definition.DisplayName ?? $"{sequence}号间隔";

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
            $"{intervalName}负荷开关",
            TenKilovolts,
            intervalId);
        var groundSwitch = new SwitchDevice(
            Guid.NewGuid(),
            SwitchKind.GroundSwitch,
            SwitchInstallationType.CabinetInterval,
            groundSwitchDeviceTerminalId,
            groundSwitchGroundTerminalId,
            definition.InitialGroundSwitchState,
            $"{intervalName}接地刀闸",
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
                mainBusNode.Id));
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
            CreateExternalTerminal(externalTerminalId, intervalId, circuitNodeId));

        electricalNodes.Add(circuitNode);
        electricalNodes.Add(earthNode);

        return new RingCabinetInterval(
            intervalId,
            cabinetId,
            sequence,
            definition.BayIndex,
            intervalName,
            IntervalKind.LoadSwitchInterval,
            [loadSwitch, groundSwitch],
            switchAssembly,
            null,
            null,
            circuitNodeId,
            earthNodeId,
            externalTerminalId);
    }

    private static RingCabinetInterval CreatePTInterval(
        Guid cabinetId,
        int sequence,
        RingCabinetIntervalDefinition definition,
        ElectricalNode mainBusNode,
        ICollection<ElectricalNode> electricalNodes,
        ICollection<Terminal> terminals)
    {
        SwitchState isolationState = definition.InitialIsolationSwitchState
            ?? throw new InvalidOperationException(
                "A PT interval definition requires an isolation-switch state.");

        Guid intervalId = Guid.NewGuid();
        Guid circuitNodeId = Guid.NewGuid();
        Guid earthNodeId = Guid.NewGuid();
        Guid externalTerminalId = Guid.NewGuid();
        Guid isolationFirstTerminalId = Guid.NewGuid();
        Guid isolationSecondTerminalId = Guid.NewGuid();
        Guid groundDeviceTerminalId = Guid.NewGuid();
        Guid groundTerminalId = Guid.NewGuid();
        string intervalName = definition.DisplayName ?? $"{sequence}号PT间隔";

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
            isolationState,
            $"{intervalName}PT隔离刀闸",
            TenKilovolts,
            intervalId);
        var groundSwitch = new SwitchDevice(
            Guid.NewGuid(),
            SwitchKind.GroundSwitch,
            SwitchInstallationType.CabinetInterval,
            groundDeviceTerminalId,
            groundTerminalId,
            definition.InitialGroundSwitchState,
            $"{intervalName}PT接地刀闸",
            TenKilovolts,
            intervalId);
        SwitchAssembly switchAssembly = SwitchAssembly.CreatePT(
            Guid.NewGuid(),
            intervalId,
            isolationSwitch,
            groundSwitch);

        AddInternalSwitchTerminal(
            terminals, mainBusNode, isolationFirstTerminalId, isolationSwitch.Id,
            FirstTerminalRole, TenKilovolts);
        AddInternalSwitchTerminal(
            terminals, circuitNode, isolationSecondTerminalId, isolationSwitch.Id,
            SecondTerminalRole, TenKilovolts);
        AddInternalSwitchTerminal(
            terminals, circuitNode, groundDeviceTerminalId, groundSwitch.Id,
            DeviceSideRole, TenKilovolts);
        AddTerminal(
            terminals,
            earthNode,
            new Terminal(
                groundTerminalId,
                TopologyOwnerType.Device,
                groundSwitch.Id,
                GroundSideRole,
                null,
                false,
                false,
                earthNode.Id));
        AddTerminal(
            terminals,
            circuitNode,
            CreateExternalTerminal(externalTerminalId, intervalId, circuitNodeId));

        electricalNodes.Add(circuitNode);
        electricalNodes.Add(earthNode);

        return new RingCabinetInterval(
            intervalId,
            cabinetId,
            sequence,
            definition.BayIndex,
            intervalName,
            IntervalKind.PTInterval,
            [isolationSwitch, groundSwitch],
            switchAssembly,
            null,
            null,
            circuitNodeId,
            earthNodeId,
            externalTerminalId);
    }

    private static RingCabinetInterval CreateIntegratedFeederInterval(
        Guid cabinetId,
        int sequence,
        RingCabinetIntervalDefinition definition,
        ElectricalNode mainBusNode,
        ICollection<ElectricalNode> electricalNodes,
        ICollection<Terminal> terminals)
    {
        GroundingStructureKind groundingStructureKind = definition.GroundingStructureKind
            ?? throw new InvalidOperationException(
                "An integrated-feeder interval definition requires a grounding structure.");
        SwitchState initialIsolationSwitchState = definition.InitialIsolationSwitchState
            ?? throw new InvalidOperationException(
                "An integrated-feeder interval definition requires an isolation-switch state.");
        SwitchState initialCircuitBreakerState = definition.InitialCircuitBreakerState
            ?? throw new InvalidOperationException(
                "An integrated-feeder interval definition requires a circuit-breaker state.");

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
        string intervalName = definition.DisplayName ?? $"{sequence}号间隔";

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
            $"{intervalName}隔离刀闸",
            TenKilovolts,
            intervalId);
        var circuitBreaker = new SwitchDevice(
            Guid.NewGuid(),
            SwitchKind.CircuitBreaker,
            SwitchInstallationType.CabinetInterval,
            breakerFirstTerminalId,
            breakerSecondTerminalId,
            initialCircuitBreakerState,
            $"{intervalName}断路器",
            TenKilovolts,
            intervalId);
        var groundSwitch = new SwitchDevice(
            Guid.NewGuid(),
            SwitchKind.GroundSwitch,
            SwitchInstallationType.CabinetInterval,
            groundSwitchDeviceTerminalId,
            groundSwitchGroundTerminalId,
            definition.InitialGroundSwitchState,
            $"{intervalName}接地刀闸",
            TenKilovolts,
            intervalId);

        SwitchAssembly switchAssembly = SwitchAssembly.CreateIntegratedFeeder(
            Guid.NewGuid(),
            intervalId,
            groundingStructureKind,
            isolationSwitch,
            circuitBreaker,
            groundSwitch);

        (
            ElectricalNode isolationFirstNode,
            ElectricalNode isolationSecondNode,
            ElectricalNode breakerFirstNode,
            ElectricalNode breakerSecondNode,
            ElectricalNode groundSwitchDeviceNode) = groundingStructureKind switch
        {
            GroundingStructureKind.UpperIsolationGrounding =>
                (mainBusNode, intermediateNode, intermediateNode, circuitNode, intermediateNode),
            GroundingStructureKind.UpperLowerGrounding =>
                (mainBusNode, intermediateNode, intermediateNode, circuitNode, circuitNode),
            GroundingStructureKind.LowerLowerGrounding =>
                (intermediateNode, circuitNode, mainBusNode, intermediateNode, circuitNode),
            _ => throw new ArgumentOutOfRangeException(nameof(groundingStructureKind))
        };

        AddInternalSwitchTerminal(
            terminals,
            isolationFirstNode,
            isolationFirstTerminalId,
            isolationSwitch.Id,
            FirstTerminalRole,
            TenKilovolts);
        AddInternalSwitchTerminal(
            terminals,
            isolationSecondNode,
            isolationSecondTerminalId,
            isolationSwitch.Id,
            SecondTerminalRole,
            TenKilovolts);
        AddInternalSwitchTerminal(
            terminals,
            breakerFirstNode,
            breakerFirstTerminalId,
            circuitBreaker.Id,
            FirstTerminalRole,
            TenKilovolts);
        AddInternalSwitchTerminal(
            terminals,
            breakerSecondNode,
            breakerSecondTerminalId,
            circuitBreaker.Id,
            SecondTerminalRole,
            TenKilovolts);
        AddInternalSwitchTerminal(
            terminals,
            groundSwitchDeviceNode,
            groundSwitchDeviceTerminalId,
            groundSwitch.Id,
            DeviceSideRole,
            TenKilovolts);
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
            CreateExternalTerminal(externalTerminalId, intervalId, circuitNodeId));

        electricalNodes.Add(intermediateNode);
        electricalNodes.Add(circuitNode);
        electricalNodes.Add(earthNode);

        return new RingCabinetInterval(
            intervalId,
            cabinetId,
            sequence,
            definition.BayIndex,
            intervalName,
            IntervalKind.IntegratedFeederInterval,
            [isolationSwitch, circuitBreaker, groundSwitch],
            switchAssembly,
            groundingStructureKind,
            intermediateNodeId,
            circuitNodeId,
            earthNodeId,
            externalTerminalId);
    }

    private Guid ValidateLoadSwitchInterval(
        RingCabinetInterval interval,
        IReadOnlyDictionary<Guid, ElectricalNode> nodes,
        IReadOnlyDictionary<Guid, Terminal> terminals)
    {
        if (interval.IntermediateNodeId is not null)
        {
            throw new InvalidOperationException(
                $"Load-switch interval '{interval.IntervalId}' cannot have an intermediate node.");
        }

        SwitchDevice loadSwitch = GetSingleSwitch(
            interval,
            SwitchKind.LoadSwitch,
            expectedDeviceCount: 2);
        SwitchDevice groundSwitch = GetSingleSwitch(interval, SwitchKind.GroundSwitch);

        EnsureCabinetSwitchIsValid(loadSwitch, interval.IntervalId);
        EnsureCabinetSwitchIsValid(groundSwitch, interval.IntervalId);
        EnsureSwitchAssemblyIsValid(
            interval,
            SwitchAssemblyType.LoadSwitchThreePosition,
            requireRules: true);

        if (!interval.SwitchAssembly.Evaluate().IsValid)
        {
            throw new InvalidOperationException(
                $"Interval '{interval.IntervalId}' has an invalid switch state combination.");
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

        EnsureExternalTerminalPolicy(interval, externalTerminal);
        EnsureNodeTerminals(
            circuitNode,
            [loadCircuitTerminal.Id, groundDeviceTerminal.Id, externalTerminal.Id]);
        EnsureNodeTerminals(earthNode, [groundSideTerminal.Id]);

        return loadBusTerminal.Id;
    }

    private Guid ValidatePTInterval(
        RingCabinetInterval interval,
        IReadOnlyDictionary<Guid, ElectricalNode> nodes,
        IReadOnlyDictionary<Guid, Terminal> terminals)
    {
        if (interval.GroundingStructureKind is not null ||
            interval.IntermediateNodeId is not null)
        {
            throw new InvalidOperationException(
                $"PT interval '{interval.IntervalId}' has invalid feeder fields.");
        }

        SwitchDevice isolationSwitch = GetSingleSwitch(
            interval,
            SwitchKind.IsolationSwitch,
            expectedDeviceCount: 2);
        SwitchDevice groundSwitch = GetSingleSwitch(interval, SwitchKind.GroundSwitch);
        EnsureCabinetSwitchIsValid(isolationSwitch, interval.IntervalId);
        EnsureCabinetSwitchIsValid(groundSwitch, interval.IntervalId);
        EnsureSwitchAssemblyIsValid(
            interval,
            SwitchAssemblyType.PT,
            requireRules: false);

        ElectricalNode circuitNode = GetRequiredNode(
            nodes, interval.CircuitNodeId, ElectricalNodeType.Circuit, interval.IntervalId);
        ElectricalNode earthNode = GetRequiredNode(
            nodes, interval.EarthNodeId, ElectricalNodeType.Earth, interval.IntervalId);
        Terminal isolationBusTerminal = GetRequiredTerminal(
            terminals,
            isolationSwitch.TerminalIds[0],
            TopologyOwnerType.Device,
            isolationSwitch.Id,
            MainBusNodeId,
            false);
        GetRequiredTerminal(
            terminals,
            isolationSwitch.TerminalIds[1],
            TopologyOwnerType.Device,
            isolationSwitch.Id,
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

        EnsureExternalTerminalPolicy(interval, externalTerminal);
        EnsureNodeTerminals(
            circuitNode,
            [isolationSwitch.TerminalIds[1], groundDeviceTerminal.Id, externalTerminal.Id]);
        EnsureNodeTerminals(earthNode, [groundSideTerminal.Id]);
        return isolationBusTerminal.Id;
    }

    private Guid ValidateIntegratedFeederInterval(
        RingCabinetInterval interval,
        IReadOnlyDictionary<Guid, ElectricalNode> nodes,
        IReadOnlyDictionary<Guid, Terminal> terminals)
    {
        if (interval.GroundingStructureKind is not GroundingStructureKind groundingStructureKind)
        {
            throw new InvalidOperationException(
                $"Integrated-feeder interval '{interval.IntervalId}' requires a grounding structure.");
        }

        if (interval.IntermediateNodeId is not Guid intermediateNodeId)
        {
            throw new InvalidOperationException(
                $"Integrated-feeder interval '{interval.IntervalId}' requires an intermediate node.");
        }

        SwitchDevice isolationSwitch = GetSingleSwitch(
            interval,
            SwitchKind.IsolationSwitch,
            expectedDeviceCount: 3);
        SwitchDevice circuitBreaker = GetSingleSwitch(interval, SwitchKind.CircuitBreaker);
        SwitchDevice groundSwitch = GetSingleSwitch(interval, SwitchKind.GroundSwitch);

        EnsureCabinetSwitchIsValid(isolationSwitch, interval.IntervalId);
        EnsureCabinetSwitchIsValid(circuitBreaker, interval.IntervalId);
        EnsureCabinetSwitchIsValid(groundSwitch, interval.IntervalId);
        EnsureSwitchAssemblyIsValid(
            interval,
            SwitchAssemblyType.IntegratedFeeder,
            requireRules: true,
            expectedRuleSetRef:
                SwitchAssembly.GetIntegratedFeederRuleSetRef(groundingStructureKind));

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

        (
            Guid isolationFirstNodeId,
            Guid isolationSecondNodeId,
            Guid breakerFirstNodeId,
            Guid breakerSecondNodeId,
            Guid groundSwitchDeviceNodeId) = groundingStructureKind switch
        {
            GroundingStructureKind.UpperIsolationGrounding =>
                (MainBusNodeId, intermediateNodeId, intermediateNodeId,
                    interval.CircuitNodeId, intermediateNodeId),
            GroundingStructureKind.UpperLowerGrounding =>
                (MainBusNodeId, intermediateNodeId, intermediateNodeId,
                    interval.CircuitNodeId, interval.CircuitNodeId),
            GroundingStructureKind.LowerLowerGrounding =>
                (intermediateNodeId, interval.CircuitNodeId, MainBusNodeId,
                    intermediateNodeId, interval.CircuitNodeId),
            _ => throw new InvalidOperationException(
                $"Interval '{interval.IntervalId}' has an unsupported grounding structure.")
        };

        Terminal isolationFirstTerminal = GetRequiredTerminal(
            terminals,
            isolationSwitch.TerminalIds[0],
            TopologyOwnerType.Device,
            isolationSwitch.Id,
            isolationFirstNodeId,
            false);
        Terminal isolationSecondTerminal = GetRequiredTerminal(
            terminals,
            isolationSwitch.TerminalIds[1],
            TopologyOwnerType.Device,
            isolationSwitch.Id,
            isolationSecondNodeId,
            false);
        Terminal breakerFirstTerminal = GetRequiredTerminal(
            terminals,
            circuitBreaker.TerminalIds[0],
            TopologyOwnerType.Device,
            circuitBreaker.Id,
            breakerFirstNodeId,
            false);
        Terminal breakerSecondTerminal = GetRequiredTerminal(
            terminals,
            circuitBreaker.TerminalIds[1],
            TopologyOwnerType.Device,
            circuitBreaker.Id,
            breakerSecondNodeId,
            false);
        Terminal groundDeviceTerminal = GetRequiredTerminal(
            terminals,
            groundSwitch.TerminalIds[0],
            TopologyOwnerType.Device,
            groundSwitch.Id,
            groundSwitchDeviceNodeId,
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

        EnsureExternalTerminalPolicy(interval, externalTerminal);
        Terminal[] mainCircuitTerminals =
        [
            isolationFirstTerminal,
            isolationSecondTerminal,
            breakerFirstTerminal,
            breakerSecondTerminal,
            groundDeviceTerminal
        ];

        EnsureNodeTerminals(
            intermediateNode,
            mainCircuitTerminals
                .Where(terminal => terminal.ElectricalNodeId == intermediateNodeId)
                .Select(terminal => terminal.Id));
        EnsureNodeTerminals(
            circuitNode,
            mainCircuitTerminals
                .Where(terminal => terminal.ElectricalNodeId == interval.CircuitNodeId)
                .Select(terminal => terminal.Id)
                .Append(externalTerminal.Id));
        EnsureNodeTerminals(earthNode, [groundSideTerminal.Id]);

        return mainCircuitTerminals.Single(
            terminal => terminal.ElectricalNodeId == MainBusNodeId).Id;
    }

    private void ValidatePureTemplateIntervalCount()
    {
        if (CompositionKind == CabinetCompositionKind.LoadSwitchOnly &&
            _intervals.Count is < 3 or > 6)
        {
            throw new InvalidOperationException(
                "A load-switch-only cabinet must contain 3, 4, 5, or 6 intervals.");
        }

        if (CompositionKind == CabinetCompositionKind.IntegratedFeederOnly &&
            _intervals.Count is not (4 or 6))
        {
            throw new InvalidOperationException(
                "An integrated-feeder-only cabinet must contain 4 or 6 intervals.");
        }

        if (CompositionKind == CabinetCompositionKind.PTOnly && _intervals.Count != 1)
        {
            throw new InvalidOperationException(
                "A PT-only cabinet must contain exactly one interval.");
        }
    }

    private static CabinetCompositionKind DetermineCompositionKind(
        IReadOnlyCollection<RingCabinetInterval> intervals)
    {
        bool containsLoadSwitch = intervals.Any(
            interval => interval.IntervalKind == IntervalKind.LoadSwitchInterval);
        bool containsIntegratedFeeder = intervals.Any(
            interval => interval.IntervalKind == IntervalKind.IntegratedFeederInterval);
        bool containsPT = intervals.Any(
            interval => interval.IntervalKind == IntervalKind.PTInterval);

        if ((containsLoadSwitch ? 1 : 0) +
            (containsIntegratedFeeder ? 1 : 0) +
            (containsPT ? 1 : 0) > 1)
        {
            return CabinetCompositionKind.Mixed;
        }

        if (containsLoadSwitch)
        {
            return CabinetCompositionKind.LoadSwitchOnly;
        }

        if (containsIntegratedFeeder)
        {
            return CabinetCompositionKind.IntegratedFeederOnly;
        }

        if (containsPT)
        {
            return CabinetCompositionKind.PTOnly;
        }

        throw new InvalidOperationException("The cabinet has no supported ordinary intervals.");
    }

    private static OperationalState EvaluateIntegratedFeederOperationalState(
        GroundingStructureKind groundingStructureKind,
        SwitchState isolationSwitchState,
        SwitchState circuitBreakerState,
        SwitchState groundSwitchState)
    {
        return groundingStructureKind switch
        {
            GroundingStructureKind.UpperIsolationGrounding =>
                (isolationSwitchState, circuitBreakerState, groundSwitchState) switch
                {
                    (SwitchStateValue.Open, SwitchStateValue.Open, SwitchStateValue.Open) =>
                        OperationalState.ColdStandby,
                    (SwitchStateValue.Open, SwitchStateValue.Closed, SwitchStateValue.Closed) =>
                        OperationalState.Maintenance,
                    (SwitchStateValue.Closed, SwitchStateValue.Open, SwitchStateValue.Open) =>
                        OperationalState.HotStandby,
                    (SwitchStateValue.Closed, SwitchStateValue.Closed, SwitchStateValue.Open) =>
                        OperationalState.Running,
                    _ => OperationalState.Unclassified
                },
            GroundingStructureKind.UpperLowerGrounding =>
                (isolationSwitchState, circuitBreakerState, groundSwitchState) switch
                {
                    (SwitchStateValue.Open, SwitchStateValue.Open, SwitchStateValue.Open) =>
                        OperationalState.ColdStandby,
                    (SwitchStateValue.Open, SwitchStateValue.Open, SwitchStateValue.Closed) =>
                        OperationalState.Grounded,
                    (SwitchStateValue.Closed, SwitchStateValue.Open, SwitchStateValue.Open) =>
                        OperationalState.HotStandby,
                    (SwitchStateValue.Closed, SwitchStateValue.Closed, SwitchStateValue.Open) =>
                        OperationalState.Running,
                    _ => OperationalState.Unclassified
                },
            GroundingStructureKind.LowerLowerGrounding =>
                (isolationSwitchState, circuitBreakerState, groundSwitchState) switch
                {
                    (SwitchStateValue.Open, SwitchStateValue.Open, SwitchStateValue.Closed) =>
                        OperationalState.Grounded,
                    _ => OperationalState.Unclassified
                },
            _ => throw new ArgumentOutOfRangeException(nameof(groundingStructureKind))
        };
    }

    private static bool HasClosedPathFromExternalTerminalToEarth(
        RingCabinetInterval interval,
        IReadOnlyDictionary<Guid, ElectricalNode> nodes,
        IReadOnlyDictionary<Guid, Terminal> terminals)
    {
        if (!terminals.TryGetValue(interval.ExternalTerminalId, out Terminal? externalTerminal) ||
            externalTerminal.ElectricalNodeId is not Guid externalNodeId)
        {
            throw new InvalidOperationException(
                $"Interval '{interval.IntervalId}' has an invalid external terminal.");
        }

        if (!nodes.ContainsKey(externalNodeId) || !nodes.ContainsKey(interval.EarthNodeId))
        {
            throw new InvalidOperationException(
                $"Interval '{interval.IntervalId}' has an invalid grounding topology.");
        }

        var adjacency = new Dictionary<Guid, HashSet<Guid>>();

        foreach (SwitchDevice switchDevice in interval.SwitchDevices)
        {
            if (GetRequiredSwitchState(switchDevice) != SwitchStateValue.Closed)
            {
                continue;
            }

            if (!terminals.TryGetValue(switchDevice.TerminalIds[0], out Terminal? firstTerminal) ||
                !terminals.TryGetValue(switchDevice.TerminalIds[1], out Terminal? secondTerminal) ||
                firstTerminal.ElectricalNodeId is not Guid firstNodeId ||
                secondTerminal.ElectricalNodeId is not Guid secondNodeId ||
                !nodes.TryGetValue(firstNodeId, out ElectricalNode? firstNode) ||
                !nodes.TryGetValue(secondNodeId, out ElectricalNode? secondNode) ||
                !firstNode.TerminalIds.Contains(firstTerminal.Id) ||
                !secondNode.TerminalIds.Contains(secondTerminal.Id))
            {
                throw new InvalidOperationException(
                    $"Switch '{switchDevice.Id}' has invalid electrical-node references.");
            }

            AddGraphEdge(adjacency, firstNodeId, secondNodeId);
        }

        var visited = new HashSet<Guid> { externalNodeId };
        var pending = new Queue<Guid>();
        pending.Enqueue(externalNodeId);

        while (pending.Count > 0)
        {
            Guid currentNodeId = pending.Dequeue();

            if (currentNodeId == interval.EarthNodeId)
            {
                return true;
            }

            if (!adjacency.TryGetValue(currentNodeId, out HashSet<Guid>? neighbours))
            {
                continue;
            }

            foreach (Guid neighbour in neighbours)
            {
                if (visited.Add(neighbour))
                {
                    pending.Enqueue(neighbour);
                }
            }
        }

        return false;
    }

    private static void AddGraphEdge(
        IDictionary<Guid, HashSet<Guid>> adjacency,
        Guid firstNodeId,
        Guid secondNodeId)
    {
        if (!adjacency.TryGetValue(firstNodeId, out HashSet<Guid>? firstNeighbours))
        {
            firstNeighbours = [];
            adjacency.Add(firstNodeId, firstNeighbours);
        }

        if (!adjacency.TryGetValue(secondNodeId, out HashSet<Guid>? secondNeighbours))
        {
            secondNeighbours = [];
            adjacency.Add(secondNodeId, secondNeighbours);
        }

        firstNeighbours.Add(secondNodeId);
        secondNeighbours.Add(firstNodeId);
    }

    private static SwitchState GetRequiredSwitchState(SwitchDevice switchDevice)
    {
        return switchDevice.SwitchState
            ?? throw new InvalidOperationException(
                $"Switch '{switchDevice.Id}' does not have a switch state.");
    }

    private static SwitchDevice GetSingleSwitch(
        RingCabinetInterval interval,
        SwitchKind switchKind,
        int? expectedDeviceCount = null)
    {
        if (expectedDeviceCount is int count && interval.SwitchDevices.Count != count)
        {
            throw new InvalidOperationException(
                $"Interval '{interval.IntervalId}' has an invalid switch-device count.");
        }

        SwitchDevice[] matches = interval.SwitchDevices
            .Where(device => device.SwitchKind == switchKind)
            .ToArray();

        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Interval '{interval.IntervalId}' requires exactly one '{switchKind}' switch.");
        }

        return matches[0];
    }

    private static void EnsureSwitchAssemblyIsValid(
        RingCabinetInterval interval,
        SwitchAssemblyType expectedAssemblyType,
        bool requireRules,
        string? expectedRuleSetRef = null)
    {
        bool hasRules = interval.SwitchAssembly.InterlockRules.Count > 0;

        if (interval.SwitchAssembly.ParentIntervalId != interval.IntervalId ||
            interval.SwitchAssembly.AssemblyType != expectedAssemblyType ||
            !interval.SwitchAssembly.MemberSwitchIds.ToHashSet().SetEquals(
                interval.SwitchDevices.Select(device => device.Id)) ||
            hasRules != requireRules ||
            (expectedRuleSetRef is not null &&
             !string.Equals(
                 interval.SwitchAssembly.RuleSetRef,
                 expectedRuleSetRef,
                 StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Interval '{interval.IntervalId}' has an invalid switch assembly.");
        }
    }

    private static Terminal CreateExternalTerminal(
        Guid terminalId,
        Guid intervalId,
        Guid circuitNodeId)
    {
        return new Terminal(
            terminalId,
            TopologyOwnerType.InternalAggregate,
            intervalId,
            ExternalCircuitRole,
            TenKilovolts,
            true,
            false,
            circuitNodeId,
            [ConnectionType.Cable, ConnectionType.OverheadLine]);
    }

    private static void AddTerminal(
        ICollection<Terminal> terminals,
        ElectricalNode electricalNode,
        Terminal terminal)
    {
        terminals.Add(terminal);
        electricalNode.AttachTerminal(terminal.Id);
    }

    private static void AddInternalSwitchTerminal(
        ICollection<Terminal> terminals,
        ElectricalNode electricalNode,
        Guid terminalId,
        Guid switchDeviceId,
        string role,
        string voltageLevel)
    {
        AddTerminal(
            terminals,
            electricalNode,
            new Terminal(
                terminalId,
                TopologyOwnerType.Device,
                switchDeviceId,
                role,
                voltageLevel,
                false,
                false,
                electricalNode.Id));
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

    private static void EnsureExternalTerminalPolicy(
        RingCabinetInterval interval,
        Terminal externalTerminal)
    {
        if (externalTerminal.AllowsMultipleConnections ||
            !externalTerminal.Allows(ConnectionType.Cable) ||
            !externalTerminal.Allows(ConnectionType.OverheadLine))
        {
            throw new InvalidOperationException(
                $"Interval '{interval.IntervalId}' external terminal has an invalid connection policy.");
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
