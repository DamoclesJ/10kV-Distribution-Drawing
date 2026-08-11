using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Infrastructure.Persistence;

public sealed record ProjectDomainDto(
    Guid DocumentId,
    string Title,
    IReadOnlyList<ProjectDeviceDto> Devices,
    IReadOnlyList<ProjectRingCabinetDto> RingCabinets)
{
    public static ProjectDomainDto Empty(Guid documentId, string title)
    {
        return new ProjectDomainDto(documentId, title, [], []);
    }
}

public sealed record ProjectDeviceDto(
    Guid DeviceId,
    string DeviceKind,
    string DeviceType,
    string? DisplayName,
    string? VoltageLevel,
    Guid? ParentId,
    string? SwitchState,
    string? PoleNumber,
    string? PoleType,
    IReadOnlyList<Guid>? OverheadAnchorTerminalIds);

public sealed record ProjectRingCabinetDto(
    Guid CabinetId,
    string DisplayName,
    Guid MainBusNodeId,
    IReadOnlyList<ProjectRingCabinetIntervalDto> Intervals,
    IReadOnlyList<ProjectElectricalNodeDto> ElectricalNodes,
    IReadOnlyList<ProjectTerminalDto> Terminals);

public sealed record ProjectRingCabinetIntervalDto(
    Guid IntervalId,
    Guid ParentCabinetId,
    int Sequence,
    string DisplayName,
    string IntervalKind,
    string? GroundingStructureKind,
    Guid? IntermediateNodeId,
    Guid CircuitNodeId,
    Guid EarthNodeId,
    Guid ExternalTerminalId,
    Guid SwitchAssemblyId,
    IReadOnlyList<ProjectSwitchDeviceDto> Switches);

public sealed record ProjectSwitchDeviceDto(
    Guid DeviceId,
    string SwitchKind,
    string InstallationType,
    Guid FirstTerminalId,
    Guid SecondTerminalId,
    string SwitchState,
    string? DisplayName,
    string VoltageLevel,
    string? DispatchNumber);

public sealed record ProjectElectricalNodeDto(
    Guid NodeId,
    string NodeType,
    string OwnerType,
    Guid OwnerId,
    string? ElectricalState);

public sealed record ProjectTerminalDto(
    Guid TerminalId,
    string OwnerType,
    Guid OwnerId,
    string Role,
    string? VoltageLevel,
    bool IsExternal,
    bool AllowsMultipleConnections,
    Guid? ElectricalNodeId,
    IReadOnlyList<string> AllowedConnectionTypes);

internal static class ProjectDomainMapper
{
    public static ProjectDomainDto ToDto(DrawingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var devices = new List<ProjectDeviceDto>();
        var ringCabinets = new List<ProjectRingCabinetDto>();
        HashSet<Guid> ringCabinetSwitchIds = document.Devices
            .OfType<RingCabinet>()
            .SelectMany(cabinet => cabinet.Intervals)
            .SelectMany(interval => interval.SwitchDevices)
            .Select(device => device.Id)
            .ToHashSet();

        foreach (Device device in document.Devices)
        {
            switch (device)
            {
                case RingCabinet ringCabinet:
                    ringCabinets.Add(ToDto(ringCabinet));
                    break;
                case Pole pole:
                    if (pole.ParentId is not null)
                    {
                        throw new NotSupportedException(
                            $"Pole '{pole.Id}' has an unsupported parent reference.");
                    }
                    devices.Add(new ProjectDeviceDto(
                        pole.Id,
                        "pole",
                        Encode(pole.Type),
                        pole.DisplayName,
                        pole.VoltageLevel,
                        pole.ParentId,
                        null,
                        pole.PoleNumber,
                        Encode(pole.PoleType),
                        pole.OverheadAnchorTerminalIds.ToArray()));
                    break;
                case SwitchDevice:
                    if (ringCabinetSwitchIds.Contains(device.Id))
                    {
                        // Cabinet switches are persisted exactly once inside
                        // their owning interval DTO.
                        break;
                    }
                    throw new NotSupportedException(
                        "Top-level SwitchDevice DTO persistence is not implemented in M4-B-6-A.");
                default:
                    if (device.Type is DeviceType.PT or DeviceType.RingCabinet or
                        DeviceType.Pole or DeviceType.Switch or DeviceType.CableTermination)
                    {
                        throw new NotSupportedException(
                            $"Device type '{device.Type}' is not supported by the M4-B-6-A DTO contract.");
                    }
                    if (device.ParentId is not null || device.SwitchState is not null)
                    {
                        throw new NotSupportedException(
                            $"Basic device '{device.Id}' contains unsupported aggregate or switch state data.");
                    }
                    devices.Add(new ProjectDeviceDto(
                        device.Id,
                        "device",
                        Encode(device.Type),
                        device.DisplayName,
                        device.VoltageLevel,
                        device.ParentId,
                        device.SwitchState is SwitchState state ? Encode(state) : null,
                        null,
                        null,
                        null));
                    break;
            }
        }

        return new ProjectDomainDto(
            document.Id,
            document.Title,
            devices,
            ringCabinets);
    }

    public static DrawingDocument ToDomain(ProjectDomainDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.DocumentId == Guid.Empty)
        {
            throw new InvalidDataException("Domain document ID cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            throw new InvalidDataException("Domain document title is required.");
        }

        var document = new DrawingDocument(dto.DocumentId, dto.Title);

        foreach (ProjectDeviceDto deviceDto in dto.Devices ??
                 throw new InvalidDataException("Domain devices are required."))
        {
            Device device = deviceDto.DeviceKind switch
            {
                "pole" => RestorePole(deviceDto),
                "device" => RestoreBasicDevice(deviceDto),
                _ => throw new InvalidDataException(
                    $"Unsupported device kind '{deviceDto.DeviceKind}'.")
            };

            document.AddDevice(device);
        }

        foreach (ProjectRingCabinetDto cabinetDto in dto.RingCabinets ??
                 throw new InvalidDataException("Ring cabinets are required."))
        {
            document.AddDevice(RestoreRingCabinet(cabinetDto));
        }

        return document;
    }

    private static ProjectRingCabinetDto ToDto(RingCabinet cabinet)
    {
        return new ProjectRingCabinetDto(
            cabinet.Id,
            cabinet.DisplayName ?? string.Empty,
            cabinet.MainBusNodeId,
            cabinet.Intervals.Select(ToDto).ToArray(),
            cabinet.ElectricalNodes.Select(ToDto).ToArray(),
            cabinet.Terminals.Select(ToDto).ToArray());
    }

    private static ProjectRingCabinetIntervalDto ToDto(RingCabinetInterval interval)
    {
        return new ProjectRingCabinetIntervalDto(
            interval.IntervalId,
            interval.ParentCabinetId,
            interval.Sequence,
            interval.DisplayName,
            Encode(interval.IntervalKind),
            interval.GroundingStructureKind is GroundingStructureKind grounding
                ? Encode(grounding)
                : null,
            interval.IntermediateNodeId,
            interval.CircuitNodeId,
            interval.EarthNodeId,
            interval.ExternalTerminalId,
            interval.SwitchAssembly.AssemblyId,
            interval.SwitchDevices.Select(ToDto).ToArray());
    }

    private static ProjectSwitchDeviceDto ToDto(SwitchDevice device)
    {
        return new ProjectSwitchDeviceDto(
            device.Id,
            Encode(device.SwitchKind),
            Encode(device.InstallationType),
            device.TerminalIds[0],
            device.TerminalIds[1],
            Encode(device.SwitchState ?? throw new InvalidDataException(
                $"Switch '{device.Id}' has no state.")),
            device.DisplayName,
            device.VoltageLevel ?? string.Empty,
            device.DispatchNumber);
    }

    private static ProjectElectricalNodeDto ToDto(ElectricalNode node)
    {
        return new ProjectElectricalNodeDto(
            node.Id,
            Encode(node.Type),
            Encode(node.OwnerType),
            node.OwnerId,
            node.ElectricalState is ElectricalState state ? Encode(state) : null);
    }

    private static ProjectTerminalDto ToDto(Terminal terminal)
    {
        return new ProjectTerminalDto(
            terminal.Id,
            Encode(terminal.OwnerType),
            terminal.OwnerId,
            terminal.Role,
            terminal.VoltageLevel,
            terminal.IsExternal,
            terminal.AllowsMultipleConnections,
            terminal.ElectricalNodeId,
            terminal.AllowedConnectionTypes.Select(Encode).OrderBy(value => value).ToArray());
    }

    private static Pole RestorePole(ProjectDeviceDto dto)
    {
        if (dto.PoleNumber is null || dto.PoleType is null)
        {
            throw new InvalidDataException(
                $"Pole '{dto.DeviceId}' is missing pole fields.");
        }

        if (!string.Equals(dto.DeviceType, "pole", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Pole '{dto.DeviceId}' has an invalid device type.");
        }

        if (dto.ParentId is not null || dto.SwitchState is not null)
        {
            throw new InvalidDataException(
                $"Pole '{dto.DeviceId}' contains unsupported parent or switch state data.");
        }

        return new Pole(
            dto.DeviceId,
            dto.PoleNumber,
            Parse<PoleType>(dto.PoleType, dto.DeviceId, "poleType"),
            dto.DisplayName,
            dto.OverheadAnchorTerminalIds ?? []);
    }

    private static Device RestoreBasicDevice(ProjectDeviceDto dto)
    {
        if (dto.ParentId is not null || dto.PoleNumber is not null ||
            dto.PoleType is not null || dto.OverheadAnchorTerminalIds is not null)
        {
            throw new InvalidDataException(
                $"Basic device '{dto.DeviceId}' contains specialized fields.");
        }

        DeviceType type = Parse<DeviceType>(dto.DeviceType, dto.DeviceId, "deviceType");
        if (type is DeviceType.RingCabinet or DeviceType.Pole or
            DeviceType.Switch or DeviceType.CableTermination or DeviceType.PT)
        {
            throw new InvalidDataException(
                $"Basic device '{dto.DeviceId}' uses a specialized device type '{type}'.");
        }

        if (dto.SwitchState is not null)
        {
            throw new InvalidDataException(
                $"Basic device '{dto.DeviceId}' contains a switch state.");
        }

        return new Device(
            dto.DeviceId,
            type,
            dto.DisplayName,
            dto.VoltageLevel,
            null);
    }

    private static RingCabinet RestoreRingCabinet(ProjectRingCabinetDto dto)
    {
        var intervals = (dto.Intervals ?? throw new InvalidDataException(
                $"Ring cabinet '{dto.CabinetId}' is missing intervals."))
            .Select(interval => new RingCabinetIntervalRestoreDefinition(
                interval.IntervalId,
                interval.ParentCabinetId,
                interval.Sequence,
                interval.DisplayName,
                Parse<IntervalKind>(interval.IntervalKind, interval.IntervalId, "intervalKind"),
                interval.GroundingStructureKind is null
                    ? null
                    : Parse<GroundingStructureKind>(
                        interval.GroundingStructureKind,
                        interval.IntervalId,
                        "groundingStructureKind"),
                interval.IntermediateNodeId,
                interval.CircuitNodeId,
                interval.EarthNodeId,
                interval.ExternalTerminalId,
                interval.SwitchAssemblyId,
                (interval.Switches ?? throw new InvalidDataException(
                        $"Interval '{interval.IntervalId}' is missing switches."))
                    .Select(switchDto => new SwitchDeviceRestoreDefinition(
                        switchDto.DeviceId,
                        Parse<SwitchKind>(switchDto.SwitchKind, switchDto.DeviceId, "switchKind"),
                        Parse<SwitchInstallationType>(
                            switchDto.InstallationType,
                            switchDto.DeviceId,
                            "installationType"),
                        switchDto.FirstTerminalId,
                        switchDto.SecondTerminalId,
                        Parse<SwitchState>(switchDto.SwitchState, switchDto.DeviceId, "switchState"),
                        switchDto.DisplayName ?? string.Empty,
                        switchDto.VoltageLevel,
                        switchDto.DispatchNumber))
                    .ToArray()))
            .ToArray();

        RingCabinet cabinet = RingCabinet.Restore(
            new RingCabinetRestoreDefinition(
                dto.CabinetId,
                dto.DisplayName,
                dto.MainBusNodeId,
                intervals));

        ValidateRestoredAggregate(cabinet, dto);
        return cabinet;
    }

    private static void ValidateRestoredAggregate(
        RingCabinet cabinet,
        ProjectRingCabinetDto dto)
    {
        Dictionary<Guid, ProjectElectricalNodeDto> nodeDtos = (dto.ElectricalNodes ?? [])
            .ToDictionary(node => node.NodeId);
        Dictionary<Guid, ProjectTerminalDto> terminalDtos = (dto.Terminals ?? [])
            .ToDictionary(terminal => terminal.TerminalId);

        if (nodeDtos.Count != cabinet.ElectricalNodes.Count ||
            terminalDtos.Count != cabinet.Terminals.Count)
        {
            throw new InvalidDataException(
                $"Ring cabinet '{cabinet.Id}' topology object counts do not match.");
        }

        foreach (ElectricalNode node in cabinet.ElectricalNodes)
        {
            if (!nodeDtos.TryGetValue(node.Id, out ProjectElectricalNodeDto? nodeDto) ||
                nodeDto.NodeType != Encode(node.Type) ||
                nodeDto.OwnerType != Encode(node.OwnerType) ||
                nodeDto.OwnerId != node.OwnerId ||
                nodeDto.ElectricalState != EncodeNullable(node.ElectricalState))
            {
                throw new InvalidDataException(
                    $"Ring cabinet '{cabinet.Id}' has an inconsistent node '{node.Id}'.");
            }
        }

        foreach (Terminal terminal in cabinet.Terminals)
        {
            if (!terminalDtos.TryGetValue(terminal.Id, out ProjectTerminalDto? terminalDto) ||
                terminalDto.OwnerType != Encode(terminal.OwnerType) ||
                terminalDto.OwnerId != terminal.OwnerId ||
                terminalDto.Role != terminal.Role ||
                terminalDto.VoltageLevel != terminal.VoltageLevel ||
                terminalDto.IsExternal != terminal.IsExternal ||
                terminalDto.AllowsMultipleConnections != terminal.AllowsMultipleConnections ||
                terminalDto.ElectricalNodeId != terminal.ElectricalNodeId ||
                (terminalDto.AllowedConnectionTypes ?? [])
                    .ToHashSet(StringComparer.Ordinal)
                    .SetEquals(terminal.AllowedConnectionTypes.Select(Encode)))
            {
                throw new InvalidDataException(
                    $"Ring cabinet '{cabinet.Id}' has an inconsistent terminal '{terminal.Id}'.");
            }
        }
    }

    private static string Encode(DeviceType value) => value switch
    {
        DeviceType.RingCabinet => "ring-cabinet",
        DeviceType.Switch => "switch",
        DeviceType.Pole => "pole",
        DeviceType.CableTermination => "cable-termination",
        DeviceType.PT => "pt",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string Encode(PoleType value) => value switch
    {
        PoleType.Cement => "cement",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string Encode(SwitchKind value) => value switch
    {
        SwitchKind.LoadSwitch => "load-switch",
        SwitchKind.IsolationSwitch => "isolation-switch",
        SwitchKind.CircuitBreaker => "circuit-breaker",
        SwitchKind.GroundSwitch => "ground-switch",
        SwitchKind.DropoutFuse => "dropout-fuse",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string Encode(SwitchInstallationType value) => value switch
    {
        SwitchInstallationType.CabinetInterval => "cabinet-interval",
        SwitchInstallationType.Pole => "pole",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string Encode(SwitchState value) => value switch
    {
        SwitchState.Open => "open",
        SwitchState.Closed => "closed",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string Encode(IntervalKind value) => value switch
    {
        IntervalKind.LoadSwitchInterval => "load-switch-interval",
        IntervalKind.IntegratedFeederInterval => "integrated-feeder-interval",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string Encode(GroundingStructureKind value) => value switch
    {
        GroundingStructureKind.UpperIsolationGrounding => "upper-isolation-grounding",
        GroundingStructureKind.UpperLowerGrounding => "upper-lower-grounding",
        GroundingStructureKind.LowerLowerGrounding => "lower-lower-grounding",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string Encode(ElectricalNodeType value) => value switch
    {
        ElectricalNodeType.MainBus => "main-bus",
        ElectricalNodeType.Circuit => "circuit",
        ElectricalNodeType.Intermediate => "intermediate",
        ElectricalNodeType.Earth => "earth",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string Encode(TopologyOwnerType value) => value switch
    {
        TopologyOwnerType.Device => "device",
        TopologyOwnerType.InternalAggregate => "internal-aggregate",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string Encode(ConnectionType value) => value switch
    {
        ConnectionType.Cable => "cable",
        ConnectionType.OverheadLine => "overhead-line",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string Encode(ElectricalState value) => value switch
    {
        ElectricalState.Energized => "energized",
        ElectricalState.Deenergized => "deenergized",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string? EncodeNullable(ElectricalState? value)
    {
        return value is ElectricalState state ? Encode(state) : null;
    }

    private static T Parse<T>(string value, Guid objectId, string field)
        where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                $"Object '{objectId}' has an empty {field}.");
        }

        string enumName = string.Concat(
            value.Split('-', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

        if (Enum.TryParse(enumName, ignoreCase: true, out T result) &&
            Enum.IsDefined(result))
        {
            return result;
        }

        throw new InvalidDataException(
            $"Object '{objectId}' has unsupported {field} '{value}'.");
    }
}
