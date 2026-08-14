using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Infrastructure.Persistence;

public sealed record ProjectDomainDto(
    Guid DocumentId,
    string Title,
    IReadOnlyList<ProjectDeviceDto> Devices,
    IReadOnlyList<ProjectRingCabinetDto> RingCabinets,
    IReadOnlyList<ProjectElectricalNodeDto>? ElectricalNodes = null,
    IReadOnlyList<ProjectTerminalDto>? Terminals = null,
    IReadOnlyList<ProjectConnectionDto>? Connections = null,
    IReadOnlyList<ProjectOverheadLineDto>? OverheadLines = null,
    IReadOnlyList<ProjectPoleAttachmentDto>? PoleAttachments = null,
    IReadOnlyList<ProjectSwitchDeviceDto>? SwitchDevices = null,
    IReadOnlyList<ProjectCableSegmentDto>? CableSegments = null,
    IReadOnlyList<ProjectIntermediateTerminalDto>? IntermediateTerminals = null)
{
    public static ProjectDomainDto Empty(Guid documentId, string title)
    {
        return new ProjectDomainDto(
            documentId,
            title,
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            []);
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
    IReadOnlyList<Guid>? OverheadAnchorTerminalIds,
    ProjectCableTerminationDto? CableTermination = null);

public sealed record ProjectCableTerminationDto(
    Guid CableSideTerminalId,
    Guid OverheadSideTerminalId,
    Guid InternalNodeId);

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
    int BayIndex,
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

public sealed record ProjectConnectionDto(
    Guid ConnectionId,
    string ConnectionType,
    Guid StartTerminalId,
    Guid EndTerminalId,
    string DisplayName,
    string VoltageLevel);

public sealed record ProjectOverheadLineDto(
    Guid ConnectionId,
    string LineModel,
    double? LengthMeters,
    IReadOnlyList<Guid> SupportPoleIds,
    bool IsContinued,
    Guid? ContinuationTerminalId,
    string? ContinuationState,
    string? ContinuationDescription);

public sealed record ProjectPoleAttachmentDto(
    Guid AttachmentId,
    Guid PoleId,
    Guid AttachedDeviceId);

public sealed record ProjectCableSegmentDto(
    Guid Id,
    string DisplayName,
    string CableType,
    double Length,
    string VoltageLevel,
    Guid ConnectionId,
    Guid StartTerminalId,
    Guid EndTerminalId);

public sealed record ProjectIntermediateTerminalDto(
    Guid Id,
    string DisplayName,
    Guid TerminalId);

internal static class ProjectDomainMapper
{
    public static ProjectDomainDto ToDto(DrawingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var devices = new List<ProjectDeviceDto>();
        var ringCabinets = new List<ProjectRingCabinetDto>();
        HashSet<Guid> ringCabinetObjectIds = document.Devices
            .OfType<RingCabinet>()
            .SelectMany(cabinet => cabinet.ElectricalNodes.Select(node => node.Id)
                .Concat(cabinet.Terminals.Select(terminal => terminal.Id)))
            .ToHashSet();
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
                case CableTermination termination:
                    devices.Add(new ProjectDeviceDto(
                        termination.Id,
                        "cable-termination",
                        Encode(termination.Type),
                        termination.DisplayName,
                        termination.VoltageLevel,
                        termination.ParentId,
                        null,
                        null,
                        null,
                        null,
                        new ProjectCableTerminationDto(
                            termination.CableSideTerminalId,
                            termination.OverheadSideTerminalId,
                            termination.InternalNodeId)));
                    break;
                case SwitchDevice:
                    if (ringCabinetSwitchIds.Contains(device.Id))
                    {
                        // Cabinet switches are persisted exactly once inside
                        // their owning interval DTO.
                        break;
                    }
                    if (device is not SwitchDevice poleSwitch ||
                        poleSwitch.InstallationType != SwitchInstallationType.Pole)
                    {
                        throw new NotSupportedException(
                            $"Top-level SwitchDevice '{device.Id}' must be pole-installed.");
                    }
                    break;
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

        ProjectDomainDto result = new(
            document.Id,
            document.Title,
            devices,
            ringCabinets,
            document.ElectricalNodes
                .Where(node => !ringCabinetObjectIds.Contains(node.Id))
                .Select(ToDto)
                .ToArray(),
            document.Terminals
                .Where(terminal => !ringCabinetObjectIds.Contains(terminal.Id))
                .Select(ToDto)
                .ToArray(),
            document.Connections.Select(ToDto).ToArray(),
            document.OverheadLines.Select(line => ToDto(line, document)).ToArray(),
            document.PoleAttachments.Select(ToDto).ToArray(),
            document.Devices
                .OfType<SwitchDevice>()
                .Where(device => !ringCabinetSwitchIds.Contains(device.Id))
                .Select(ToDto)
                .ToArray(),
            document.CableSegments.Select(ToDto).ToArray(),
            document.IntermediateTerminals.Select(ToDto).ToArray());

        ValidateTopology(document, result);
        return result;
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
                "cable-termination" => RestoreCableTermination(deviceDto),
                "device" => RestoreBasicDevice(deviceDto),
                _ => throw new InvalidDataException(
                    $"Unsupported device kind '{deviceDto.DeviceKind}'.")
            };

            document.AddDevice(device);
        }

        foreach (ProjectSwitchDeviceDto switchDto in dto.SwitchDevices ?? [])
        {
            document.AddDevice(RestoreTopLevelSwitch(switchDto));
        }

        foreach (ProjectRingCabinetDto cabinetDto in dto.RingCabinets ??
                 throw new InvalidDataException("Ring cabinets are required."))
        {
            document.AddDevice(RestoreRingCabinet(cabinetDto));
        }

        foreach (ProjectElectricalNodeDto nodeDto in dto.ElectricalNodes ?? [])
        {
            document.AddElectricalNode(RestoreElectricalNode(nodeDto));
        }

        foreach (ProjectTerminalDto terminalDto in dto.Terminals ?? [])
        {
            if (Parse<TopologyOwnerType>(
                    terminalDto.OwnerType,
                    terminalDto.TerminalId,
                    "ownerType") != TopologyOwnerType.IntermediateTerminal)
            {
                document.AddTerminal(RestoreTerminal(terminalDto));
            }
        }

        foreach (ProjectIntermediateTerminalDto intermediateDto in
                 dto.IntermediateTerminals ?? [])
        {
            ProjectTerminalDto terminalDto = (dto.Terminals ?? [])
                .SingleOrDefault(terminal => terminal.TerminalId == intermediateDto.TerminalId)
                ?? throw new InvalidDataException(
                    $"Intermediate terminal '{intermediateDto.Id}' child terminal is missing.");
            document.AddIntermediateTerminal(
                RestoreIntermediateTerminal(intermediateDto),
                RestoreTerminal(terminalDto));
        }

        HashSet<Guid> cableConnectionIds = (dto.CableSegments ?? [])
            .Select(segment => segment.ConnectionId)
            .ToHashSet();
        foreach (ProjectConnectionDto connectionDto in dto.Connections ?? [])
        {
            if (cableConnectionIds.Contains(connectionDto.ConnectionId))
            {
                continue;
            }

            document.AddConnection(RestoreConnection(connectionDto));
        }

        foreach (ProjectCableSegmentDto cableSegmentDto in dto.CableSegments ?? [])
        {
            ProjectConnectionDto connectionDto = (dto.Connections ?? [])
                .SingleOrDefault(candidate => candidate.ConnectionId == cableSegmentDto.ConnectionId)
                ?? throw new InvalidDataException(
                    $"Cable segment '{cableSegmentDto.Id}' connection is missing.");
            Connection connection = RestoreConnection(connectionDto);
            document.AddCableSegment(
                RestoreCableSegment(cableSegmentDto),
                connection);
        }

        foreach (ProjectPoleAttachmentDto attachmentDto in dto.PoleAttachments ?? [])
        {
            document.AddPoleAttachment(
                new PoleAttachment(
                    attachmentDto.AttachmentId,
                    attachmentDto.PoleId,
                    attachmentDto.AttachedDeviceId));
        }

        foreach (ProjectOverheadLineDto overheadLineDto in dto.OverheadLines ?? [])
        {
            document.AddOverheadLine(RestoreOverheadLine(overheadLineDto));
        }

        ValidateTopology(document, dto);

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

    private static ProjectConnectionDto ToDto(Connection connection)
    {
        return new ProjectConnectionDto(
            connection.Id,
            Encode(connection.Type),
            connection.StartTerminalId,
            connection.EndTerminalId,
            connection.DisplayName,
            connection.VoltageLevel);
    }

    private static ProjectOverheadLineDto ToDto(
        OverheadLine overheadLine,
        DrawingDocument document)
    {
        Connection connection = document.Connections.SingleOrDefault(
                candidate => candidate.Id == overheadLine.ConnectionId)
            ?? throw new InvalidDataException(
                $"Overhead line '{overheadLine.ConnectionId}' has no connection.");
        overheadLine.ValidateAgainst(connection);

        return new ProjectOverheadLineDto(
            overheadLine.ConnectionId,
            overheadLine.LineModel,
            overheadLine.LengthMeters,
            overheadLine.SupportPoleIds.ToArray(),
            overheadLine.IsContinued,
            overheadLine.ContinuationTerminalId,
            overheadLine.ContinuationState is ContinuationState state
                ? Encode(state)
                : null,
            overheadLine.ContinuationDescription);
    }

    private static ProjectPoleAttachmentDto ToDto(PoleAttachment attachment)
    {
        return new ProjectPoleAttachmentDto(
            attachment.AttachmentId,
            attachment.PoleId,
            attachment.AttachedDeviceId);
    }

    private static ProjectCableSegmentDto ToDto(CableSegment cableSegment)
    {
        return new ProjectCableSegmentDto(
            cableSegment.Id,
            cableSegment.Name,
            cableSegment.CableType,
            cableSegment.Length,
            cableSegment.VoltageLevel,
            cableSegment.ConnectionId,
            cableSegment.StartTerminalId,
            cableSegment.EndTerminalId);
    }

    private static ProjectIntermediateTerminalDto ToDto(
        IntermediateTerminal intermediateTerminal)
    {
        return new ProjectIntermediateTerminalDto(
            intermediateTerminal.Id,
            intermediateTerminal.DisplayName,
            intermediateTerminal.TerminalId);
    }

    private static ProjectRingCabinetIntervalDto ToDto(RingCabinetInterval interval)
    {
        return new ProjectRingCabinetIntervalDto(
            interval.IntervalId,
            interval.ParentCabinetId,
            interval.Sequence,
            interval.BayIndex,
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

    private static SwitchDevice RestoreTopLevelSwitch(ProjectSwitchDeviceDto dto)
    {
        SwitchInstallationType installationType = Parse<SwitchInstallationType>(
            dto.InstallationType,
            dto.DeviceId,
            "installationType");
        if (installationType != SwitchInstallationType.Pole)
        {
            throw new InvalidDataException(
                $"Top-level switch '{dto.DeviceId}' must be pole-installed.");
        }

        if (dto.FirstTerminalId == Guid.Empty || dto.SecondTerminalId == Guid.Empty ||
            dto.FirstTerminalId == dto.SecondTerminalId)
        {
            throw new InvalidDataException(
                $"Top-level switch '{dto.DeviceId}' has invalid terminal IDs.");
        }

        return SwitchDevice.CreateForPole(
            dto.DeviceId,
            Parse<SwitchKind>(dto.SwitchKind, dto.DeviceId, "switchKind"),
            dto.FirstTerminalId,
            dto.SecondTerminalId,
            Parse<SwitchState>(dto.SwitchState, dto.DeviceId, "switchState"),
            dto.DisplayName ?? string.Empty,
            dto.VoltageLevel,
            dto.DispatchNumber);
    }

    private static CableTermination RestoreCableTermination(ProjectDeviceDto dto)
    {
        if (!string.Equals(dto.DeviceType, "cable-termination", StringComparison.Ordinal) ||
            dto.CableTermination is null)
        {
            throw new InvalidDataException(
                $"Cable termination '{dto.DeviceId}' is missing its device details.");
        }

        if (dto.ParentId is not null || dto.SwitchState is not null ||
            dto.PoleNumber is not null || dto.PoleType is not null ||
            dto.OverheadAnchorTerminalIds is not null)
        {
            throw new InvalidDataException(
                $"Cable termination '{dto.DeviceId}' contains incompatible fields.");
        }

        ProjectCableTerminationDto details = dto.CableTermination;
        return new CableTermination(
            dto.DeviceId,
            details.CableSideTerminalId,
            details.OverheadSideTerminalId,
            details.InternalNodeId,
            dto.DisplayName,
            dto.VoltageLevel ?? "10kV");
    }

    private static ElectricalNode RestoreElectricalNode(ProjectElectricalNodeDto dto)
    {
        return new ElectricalNode(
            dto.NodeId,
            Parse<ElectricalNodeType>(dto.NodeType, dto.NodeId, "nodeType"),
            Parse<TopologyOwnerType>(dto.OwnerType, dto.NodeId, "ownerType"),
            dto.OwnerId,
            dto.ElectricalState is null
                ? null
                : Parse<ElectricalState>(dto.ElectricalState, dto.NodeId, "electricalState"));
    }

    private static Terminal RestoreTerminal(ProjectTerminalDto dto)
    {
        ConnectionType[] allowedConnectionTypes = (dto.AllowedConnectionTypes ?? [])
            .Select(value => Parse<ConnectionType>(value, dto.TerminalId, "allowedConnectionType"))
            .ToArray();

        return new Terminal(
            dto.TerminalId,
            Parse<TopologyOwnerType>(dto.OwnerType, dto.TerminalId, "ownerType"),
            dto.OwnerId,
            dto.Role,
            dto.VoltageLevel,
            dto.IsExternal,
            dto.AllowsMultipleConnections,
            dto.ElectricalNodeId,
            allowedConnectionTypes);
    }

    private static IntermediateTerminal RestoreIntermediateTerminal(
        ProjectIntermediateTerminalDto dto)
    {
        return new IntermediateTerminal(
            dto.Id,
            dto.DisplayName,
            dto.TerminalId);
    }

    private static Connection RestoreConnection(ProjectConnectionDto dto)
    {
        return new Connection(
            dto.ConnectionId,
            Parse<ConnectionType>(dto.ConnectionType, dto.ConnectionId, "connectionType"),
            dto.StartTerminalId,
            dto.EndTerminalId,
            dto.DisplayName,
            dto.VoltageLevel);
    }

    private static CableSegment RestoreCableSegment(ProjectCableSegmentDto dto)
    {
        return new CableSegment(
            dto.Id,
            dto.DisplayName,
            dto.CableType,
            dto.Length,
            dto.VoltageLevel,
            dto.ConnectionId,
            dto.StartTerminalId,
            dto.EndTerminalId);
    }

    private static OverheadLine RestoreOverheadLine(ProjectOverheadLineDto dto)
    {
        ContinuationState? continuationState = dto.ContinuationState is null
            ? null
            : Parse<ContinuationState>(
                dto.ContinuationState,
                dto.ConnectionId,
                "continuationState");

        return new OverheadLine(
            dto.ConnectionId,
            dto.LineModel,
            dto.LengthMeters,
            dto.SupportPoleIds ?? throw new InvalidDataException(
                $"Overhead line '{dto.ConnectionId}' is missing support poles."),
            dto.IsContinued,
            dto.ContinuationTerminalId,
            continuationState,
            dto.ContinuationDescription);
    }

    private static void ValidateTopology(
        DrawingDocument document,
        ProjectDomainDto dto)
    {
        IReadOnlyList<ProjectElectricalNodeDto> nodeDtos = dto.ElectricalNodes ?? [];
        IReadOnlyList<ProjectTerminalDto> terminalDtos = dto.Terminals ?? [];
        IReadOnlyList<ProjectConnectionDto> connectionDtos = dto.Connections ?? [];
        IReadOnlyList<ProjectOverheadLineDto> overheadLineDtos = dto.OverheadLines ?? [];
        IReadOnlyList<ProjectCableSegmentDto> cableSegmentDtos = dto.CableSegments ?? [];
        IReadOnlyList<ProjectIntermediateTerminalDto> intermediateTerminalDtos =
            dto.IntermediateTerminals ?? [];

        HashSet<Guid> rootNodeIds = nodeDtos.Select(node => node.NodeId).ToHashSet();
        HashSet<Guid> rootTerminalIds = terminalDtos.Select(terminal => terminal.TerminalId).ToHashSet();
        HashSet<Guid> ringNodeIds = document.Devices
            .OfType<RingCabinet>()
            .SelectMany(cabinet => cabinet.ElectricalNodes)
            .Select(node => node.Id)
            .ToHashSet();
        HashSet<Guid> ringTerminalIds = document.Devices
            .OfType<RingCabinet>()
            .SelectMany(cabinet => cabinet.Terminals)
            .Select(terminal => terminal.Id)
            .ToHashSet();

        if (rootNodeIds.Count != nodeDtos.Count ||
            rootTerminalIds.Count != terminalDtos.Count ||
            rootNodeIds.Overlaps(ringNodeIds) ||
            rootTerminalIds.Overlaps(ringTerminalIds))
        {
            throw new InvalidDataException(
                "Domain topology contains duplicate or cross-aggregate IDs.");
        }

        HashSet<Guid> actualRootNodeIds = document.ElectricalNodes
            .Where(node => !ringNodeIds.Contains(node.Id))
            .Select(node => node.Id)
            .ToHashSet();
        HashSet<Guid> actualRootTerminalIds = document.Terminals
            .Where(terminal => !ringTerminalIds.Contains(terminal.Id))
            .Select(terminal => terminal.Id)
            .ToHashSet();

        if (!actualRootNodeIds.SetEquals(rootNodeIds) ||
            !actualRootTerminalIds.SetEquals(rootTerminalIds))
        {
            throw new InvalidDataException(
                "Domain topology DTO does not match restored nodes or terminals.");
        }

        if (intermediateTerminalDtos.Count != document.IntermediateTerminals.Count ||
            intermediateTerminalDtos.Select(item => item.Id).Distinct().Count() !=
                intermediateTerminalDtos.Count ||
            intermediateTerminalDtos.Select(item => item.TerminalId).Distinct().Count() !=
                intermediateTerminalDtos.Count)
        {
            throw new InvalidDataException(
                "Intermediate terminal DTOs contain duplicate or missing objects.");
        }

        foreach (IntermediateTerminal intermediateTerminal in document.IntermediateTerminals)
        {
            ProjectIntermediateTerminalDto intermediateDto =
                intermediateTerminalDtos.SingleOrDefault(item =>
                    item.Id == intermediateTerminal.Id)
                ?? throw new InvalidDataException(
                    $"Intermediate terminal '{intermediateTerminal.Id}' is missing from DTO.");
            if (intermediateDto.TerminalId != intermediateTerminal.TerminalId)
            {
                throw new InvalidDataException(
                    $"Intermediate terminal '{intermediateTerminal.Id}' has an inconsistent terminal reference.");
            }

            Terminal terminal = document.Terminals.Single(candidate =>
                candidate.Id == intermediateTerminal.TerminalId);
            if (terminal.OwnerType != TopologyOwnerType.IntermediateTerminal ||
                terminal.OwnerId != intermediateTerminal.Id ||
                terminal.ElectricalNodeId is not null)
            {
                throw new InvalidDataException(
                    $"Intermediate terminal '{intermediateTerminal.Id}' owner relationship is invalid.");
            }
        }

        if (cableSegmentDtos.Count != document.CableSegments.Count ||
            cableSegmentDtos.Select(item => item.Id).Distinct().Count() != cableSegmentDtos.Count ||
            cableSegmentDtos.Select(item => item.ConnectionId).Distinct().Count() !=
                cableSegmentDtos.Count)
        {
            throw new InvalidDataException(
                "Cable segment DTOs contain duplicate or missing objects.");
        }

        foreach (CableSegment cableSegment in document.CableSegments)
        {
            ProjectCableSegmentDto cableSegmentDto = cableSegmentDtos.SingleOrDefault(item =>
                    item.Id == cableSegment.Id)
                ?? throw new InvalidDataException(
                    $"Cable segment '{cableSegment.Id}' is missing from DTO.");
            if (cableSegmentDto.ConnectionId != cableSegment.ConnectionId ||
                cableSegmentDto.StartTerminalId != cableSegment.StartTerminalId ||
                cableSegmentDto.EndTerminalId != cableSegment.EndTerminalId ||
                cableSegmentDto.DisplayName != cableSegment.Name ||
                cableSegmentDto.CableType != cableSegment.CableType ||
                cableSegmentDto.Length != cableSegment.Length ||
                cableSegmentDto.VoltageLevel != cableSegment.VoltageLevel)
            {
                throw new InvalidDataException(
                    $"Cable segment '{cableSegment.Id}' is inconsistent with its DTO.");
            }

            Connection connection = document.Connections.SingleOrDefault(candidate =>
                    candidate.Id == cableSegment.ConnectionId)
                ?? throw new InvalidDataException(
                    $"Cable segment '{cableSegment.Id}' connection is missing.");
            if (connection.Type != ConnectionType.Cable ||
                connection.StartTerminalId != cableSegment.StartTerminalId ||
                connection.EndTerminalId != cableSegment.EndTerminalId)
            {
                throw new InvalidDataException(
                    $"Cable segment '{cableSegment.Id}' connection relationship is invalid.");
            }
        }

        foreach (ElectricalNode node in document.ElectricalNodes)
        {
            if (node.TerminalIds.Count == 0)
            {
                throw new InvalidDataException(
                    $"Electrical node '{node.Id}' is orphaned.");
            }
        }

        foreach (Pole pole in document.Devices.OfType<Pole>())
        {
            foreach (Guid terminalId in pole.OverheadAnchorTerminalIds)
            {
                Terminal terminal = document.Terminals.SingleOrDefault(
                        candidate => candidate.Id == terminalId)
                    ?? throw new InvalidDataException(
                        $"Pole '{pole.Id}' references missing terminal '{terminalId}'.");

                if (terminal.OwnerType != TopologyOwnerType.Device ||
                    terminal.OwnerId != pole.Id)
                {
                    throw new InvalidDataException(
                        $"Pole '{pole.Id}' does not own terminal '{terminalId}'.");
                }
            }
        }

        foreach (CableTermination termination in document.Devices.OfType<CableTermination>())
        {
            ElectricalNode node = document.ElectricalNodes.SingleOrDefault(
                    candidate => candidate.Id == termination.InternalNodeId)
                ?? throw new InvalidDataException(
                    $"Cable termination '{termination.Id}' internal node is missing.");
            HashSet<Guid> terminalIds = document.Terminals
                .Where(terminal => terminal.OwnerId == termination.Id)
                .Select(terminal => terminal.Id)
                .ToHashSet();

            if (node.Type != ElectricalNodeType.Intermediate ||
                node.OwnerType != TopologyOwnerType.Device ||
                node.OwnerId != termination.Id ||
                !terminalIds.SetEquals(termination.TerminalIds) ||
                !node.TerminalIds.SetEquals(termination.TerminalIds))
            {
                throw new InvalidDataException(
                    $"Cable termination '{termination.Id}' topology is incomplete.");
            }
        }

        if (connectionDtos.Count != document.Connections.Count ||
            connectionDtos.Select(connection => connection.ConnectionId).Distinct().Count() !=
                connectionDtos.Count)
        {
            throw new InvalidDataException(
                "Connection DTOs do not match restored connections.");
        }

        HashSet<Guid> overheadConnectionIds = overheadLineDtos
            .Select(line => line.ConnectionId)
            .ToHashSet();
        if (overheadConnectionIds.Count != overheadLineDtos.Count ||
            overheadLineDtos.Count != document.OverheadLines.Count)
        {
            throw new InvalidDataException(
                "Overhead line details contain duplicate or missing connection IDs.");
        }

        foreach (Connection connection in document.Connections)
        {
            bool hasOverheadDetail = overheadConnectionIds.Contains(connection.Id);
            if ((connection.Type == ConnectionType.OverheadLine) != hasOverheadDetail)
            {
                throw new InvalidDataException(
                    $"Connection '{connection.Id}' has an invalid overhead-line detail relationship.");
            }
        }
    }

    private static RingCabinet RestoreRingCabinet(ProjectRingCabinetDto dto)
    {
        var intervals = (dto.Intervals ?? throw new InvalidDataException(
                $"Ring cabinet '{dto.CabinetId}' is missing intervals."))
            .Select(RestoreRingCabinetInterval)
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

    private static RingCabinetIntervalRestoreDefinition RestoreRingCabinetInterval(
        ProjectRingCabinetIntervalDto interval)
    {
        if (interval.BayIndex < 1)
        {
            throw new InvalidDataException(
                $"Interval '{interval.IntervalId}' has an invalid bayIndex '{interval.BayIndex}'.");
        }

        return new RingCabinetIntervalRestoreDefinition(
            interval.IntervalId,
            interval.ParentCabinetId,
            interval.Sequence,
            interval.BayIndex,
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
                .ToArray());
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
                !(terminalDto.AllowedConnectionTypes ?? [])
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
        IntervalKind.PTInterval => "pt-interval",
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
        TopologyOwnerType.IntermediateTerminal => "intermediate-terminal",
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

    private static string Encode(ContinuationState value) => value switch
    {
        ContinuationState.Energized => "energized",
        ContinuationState.Unknown => "unknown",
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
