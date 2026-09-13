using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using System.Text.Json.Serialization;

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
    IReadOnlyList<ProjectIntermediateTerminalDto>? IntermediateTerminals = null,
    [property: JsonRequired] IReadOnlyList<ProjectTransformerDto>? Transformers = null,
    [property: JsonRequired] IReadOnlyList<ProjectCustomerStationDto>? CustomerStations = null)
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
    IReadOnlyList<ProjectTerminalDto> Terminals,
    string? LineName = null);

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
    [property: JsonRequired] Guid? CableTerminalId,
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
    string? DispatchNumber,
    ProjectSwitchOwnerReferenceDto? Owner = null);

[JsonConverter(typeof(StrictStringEnumConverter<ProjectSwitchOwnerKind>))]
public enum ProjectSwitchOwnerKind
{
    RingCabinetInterval,
    CustomerStationIncomingFeeder
}

public sealed record ProjectSwitchOwnerReferenceDto(
    [property: JsonRequired] ProjectSwitchOwnerKind OwnerKind,
    [property: JsonRequired] Guid OwnerId);

[JsonConverter(typeof(StrictStringEnumConverter<ProjectTransformerKind>))]
public enum ProjectTransformerKind
{
    PublicPoleMounted,
    DedicatedPoleMounted,
    PublicIndoor
}

public sealed record ProjectTransformerDto(
    [property: JsonRequired] Guid TransformerId,
    [property: JsonRequired] ProjectTransformerKind TransformerKind,
    [property: JsonRequired] Guid HvTerminalId,
    string? DisplayName = null);

[JsonConverter(typeof(StrictStringEnumConverter<ProjectStationKind>))]
public enum ProjectStationKind
{
    BoxStation,
    IndoorStation
}

public sealed record ProjectCustomerStationDto
{
    [JsonConstructor]
    public ProjectCustomerStationDto(
        Guid customerStationId,
        ProjectStationKind stationKind,
        IReadOnlyList<ProjectCustomerStationIncomingFeederDto> incomingFeeders)
    {
        if (customerStationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Customer station ID cannot be empty.",
                nameof(customerStationId));
        }

        if (!Enum.IsDefined(stationKind))
        {
            throw new ArgumentOutOfRangeException(nameof(stationKind));
        }

        ProjectCustomerStationIncomingFeederDto[] feeders = incomingFeeders?.ToArray()
            ?? throw new ArgumentNullException(nameof(incomingFeeders));
        bool validCount = stationKind switch
        {
            ProjectStationKind.BoxStation => feeders.Length == 1,
            ProjectStationKind.IndoorStation => feeders.Length is 1 or 2,
            _ => false
        };
        if (!validCount)
        {
            throw new ArgumentException(
                "BoxStation requires one incoming feeder; IndoorStation requires one or two.",
                nameof(incomingFeeders));
        }

        int[] expectedSequences = Enumerable.Range(1, feeders.Length).ToArray();
        if (!feeders.Select(feeder => feeder.Sequence).OrderBy(value => value)
                .SequenceEqual(expectedSequences))
        {
            throw new ArgumentException(
                "Customer station incoming feeder sequences must be unique, continuous, and start at one.",
                nameof(incomingFeeders));
        }

        foreach (ProjectCustomerStationIncomingFeederDto feeder in feeders)
        {
            if (feeder.IncomingFeederId == Guid.Empty ||
                feeder.CableTerminalId == Guid.Empty ||
                feeder.StationTerminalId == Guid.Empty ||
                feeder.ElectricalNodeId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Customer station incoming feeder IDs cannot be empty.",
                    nameof(incomingFeeders));
            }

            if (string.IsNullOrWhiteSpace(feeder.DisplayName))
            {
                throw new ArgumentException(
                    "Customer station incoming feeder display names are required.",
                    nameof(incomingFeeders));
            }

            if (feeder.IsolationSwitch is null ||
                feeder.IsolationSwitch.DeviceId == Guid.Empty ||
                !string.Equals(
                    feeder.IsolationSwitch.SwitchKind,
                    "isolation-switch",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    feeder.IsolationSwitch.InstallationType,
                    "customer-station-incoming-feeder",
                    StringComparison.Ordinal) ||
                feeder.IsolationSwitch.Owner is not
                {
                    OwnerKind: ProjectSwitchOwnerKind.CustomerStationIncomingFeeder,
                    OwnerId: var ownerId
                } || ownerId != feeder.IncomingFeederId ||
                feeder.IsolationSwitch.FirstTerminalId != feeder.CableTerminalId ||
                feeder.IsolationSwitch.SecondTerminalId != feeder.StationTerminalId)
            {
                throw new ArgumentException(
                    "Customer station isolation switch identity or typed owner is invalid.",
                    nameof(incomingFeeders));
            }
        }

        Guid[] aggregateIds =
        [
            customerStationId,
            .. feeders.SelectMany(feeder => new[]
            {
                feeder.IncomingFeederId,
                feeder.IsolationSwitch.DeviceId,
                feeder.CableTerminalId,
                feeder.StationTerminalId,
                feeder.ElectricalNodeId
            })
        ];
        if (aggregateIds.Distinct().Count() != aggregateIds.Length)
        {
            throw new ArgumentException(
                "Customer station aggregate IDs must be unique.",
                nameof(incomingFeeders));
        }

        CustomerStationId = customerStationId;
        StationKind = stationKind;
        IncomingFeeders = Array.AsReadOnly(feeders);
    }

    [JsonRequired]
    public Guid CustomerStationId { get; init; }

    [JsonRequired]
    public ProjectStationKind StationKind { get; init; }

    [JsonRequired]
    public IReadOnlyList<ProjectCustomerStationIncomingFeederDto> IncomingFeeders { get; init; }
}

public sealed record ProjectCustomerStationIncomingFeederDto(
    [property: JsonRequired] Guid IncomingFeederId,
    [property: JsonRequired] int Sequence,
    [property: JsonRequired] string DisplayName,
    [property: JsonRequired] Guid CableTerminalId,
    [property: JsonRequired] Guid StationTerminalId,
    [property: JsonRequired] Guid ElectricalNodeId,
    [property: JsonRequired] ProjectSwitchDeviceDto IsolationSwitch);

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
        var transformers = new List<ProjectTransformerDto>();
        var customerStations = new List<ProjectCustomerStationDto>();
        HashSet<Guid> ringCabinetIntervalIds = document.Devices
            .OfType<RingCabinet>()
            .SelectMany(cabinet => cabinet.Intervals)
            .Select(interval => interval.IntervalId)
            .ToHashSet();
        HashSet<Guid> ringCabinetSwitchIds = document.Devices
            .OfType<SwitchDevice>()
            .Where(device =>
                device.InstallationType == SwitchInstallationType.CabinetInterval &&
                device.ParentId is Guid parentId &&
                ringCabinetIntervalIds.Contains(parentId))
            .Select(device => device.Id)
            .ToHashSet();
        HashSet<Guid> ringCabinetObjectIds = document.Devices
            .OfType<RingCabinet>()
            .SelectMany(cabinet => cabinet.ElectricalNodes.Select(node => node.Id)
                .Concat(cabinet.Terminals.Select(terminal => terminal.Id)))
            .Concat(document.ElectricalNodes
                .Where(node => ringCabinetIntervalIds.Contains(node.OwnerId) ||
                    ringCabinetSwitchIds.Contains(node.OwnerId))
                .Select(node => node.Id))
            .Concat(document.Terminals
                .Where(terminal => ringCabinetIntervalIds.Contains(terminal.OwnerId) ||
                    ringCabinetSwitchIds.Contains(terminal.OwnerId))
                .Select(terminal => terminal.Id))
            .ToHashSet();
        ringCabinetSwitchIds.UnionWith(document.Devices
            .OfType<RingCabinet>()
            .SelectMany(cabinet => cabinet.Intervals)
            .SelectMany(interval => interval.SwitchDevices)
            .Select(device => device.Id));
        HashSet<Guid> customerStationSwitchIds = document.CustomerStations
            .SelectMany(station => station.IncomingFeeders)
            .Select(feeder => feeder.IsolationSwitch.Id)
            .ToHashSet();

        foreach (Device device in document.Devices)
        {
            switch (device)
            {
                case RingCabinet ringCabinet:
                    ringCabinets.Add(ToDto(ringCabinet));
                    break;
                case Transformer transformer:
                    if (transformer.IsLegacyNamingIncomplete ||
                        string.IsNullOrWhiteSpace(transformer.DisplayName))
                    {
                        throw new InvalidDataException(
                            $"Transformer '{transformer.Id}' requires a display name before persistence.");
                    }
                    transformers.Add(new ProjectTransformerDto(
                        transformer.Id,
                        Encode(transformer.TransformerKind),
                        transformer.HvTerminalId,
                        transformer.DisplayName));
                    break;
                case CustomerStation customerStation:
                    customerStations.Add(ToDto(customerStation));
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
                    if (ringCabinetSwitchIds.Contains(device.Id) ||
                        customerStationSwitchIds.Contains(device.Id))
                    {
                        // Aggregate-owned switches are persisted exactly once
                        // inside their typed owner DTO.
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
                .Where(device => !ringCabinetSwitchIds.Contains(device.Id) &&
                    !customerStationSwitchIds.Contains(device.Id))
                .Select(device => ToDto(device))
                .ToArray(),
            document.CableSegments.Select(ToDto).ToArray(),
            document.IntermediateTerminals.Select(ToDto).ToArray(),
            transformers,
            customerStations);

        ValidateTopology(document, result);
        return result;
    }

    public static DrawingDocument ToDomain(
        ProjectDomainDto dto,
        TransformerNamingContractMode transformerNamingMode = TransformerNamingContractMode.Current)
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

        var customerStationNodeIds = new HashSet<Guid>();
        var customerStationTerminalIds = new HashSet<Guid>();
        foreach (ProjectCustomerStationDto stationDto in dto.CustomerStations ??
                 throw new InvalidDataException("Customer stations are required."))
        {
            CustomerStation station = RestoreCustomerStation(
                stationDto,
                dto.ElectricalNodes ?? [],
                dto.Terminals ?? []);
            try
            {
                document.AddCustomerStation(station);
            }
            catch (Exception exception) when (exception is ArgumentException or
                                               InvalidOperationException)
            {
                throw new InvalidDataException(
                    $"Customer station '{stationDto.CustomerStationId}' aggregate is invalid.",
                    exception);
            }

            customerStationNodeIds.UnionWith(
                station.IncomingFeeders.Select(feeder => feeder.ElectricalNodeId));
            customerStationTerminalIds.UnionWith(
                station.IncomingFeeders.SelectMany(feeder => new[]
                {
                    feeder.CableTerminalId,
                    feeder.StationTerminalId
                }));
        }

        foreach (ProjectElectricalNodeDto nodeDto in dto.ElectricalNodes ?? [])
        {
            if (customerStationNodeIds.Contains(nodeDto.NodeId))
            {
                continue;
            }

            document.AddElectricalNode(RestoreElectricalNode(nodeDto));
        }

        HashSet<Guid> transformerTerminalIds = [];
        foreach (ProjectTransformerDto transformerDto in dto.Transformers ?? [])
        {
            if (transformerDto.TransformerId == Guid.Empty ||
                transformerDto.HvTerminalId == Guid.Empty ||
                transformerDto.TransformerId == transformerDto.HvTerminalId)
            {
                throw new InvalidDataException(
                    "Transformer and HV terminal IDs must be non-empty and distinct.");
            }

            if (!Enum.IsDefined(transformerDto.TransformerKind))
            {
                throw new InvalidDataException(
                    $"Transformer '{transformerDto.TransformerId}' has an invalid kind.");
            }

            ProjectTerminalDto[] matchingTerminals = (dto.Terminals ?? [])
                .Where(terminal => terminal.TerminalId == transformerDto.HvTerminalId)
                .ToArray();
            if (matchingTerminals.Length != 1)
            {
                throw new InvalidDataException(
                    $"Transformer '{transformerDto.TransformerId}' must have exactly one HV terminal DTO.");
            }
            ProjectTerminalDto terminalDto = matchingTerminals[0];
            Transformer transformer;
            if (transformerNamingMode == TransformerNamingContractMode.Legacy)
            {
                if (!string.IsNullOrWhiteSpace(transformerDto.DisplayName) &&
                    !string.Equals(
                        transformerDto.DisplayName,
                        transformerDto.DisplayName.Trim(),
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Transformer '{transformerDto.TransformerId}' has an untrimmed legacy display name.");
                }

                transformer = Transformer.RestoreLegacy(
                    transformerDto.TransformerId,
                    Decode(transformerDto.TransformerKind),
                    transformerDto.HvTerminalId,
                    transformerDto.DisplayName);
            }
            else if (transformerNamingMode == TransformerNamingContractMode.Current)
            {
                if (string.IsNullOrWhiteSpace(transformerDto.DisplayName) ||
                    !string.Equals(
                        transformerDto.DisplayName,
                        transformerDto.DisplayName.Trim(),
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Transformer '{transformerDto.TransformerId}' requires a trimmed display name in the current naming contract.");
                }

                transformer = new Transformer(
                    transformerDto.TransformerId,
                    Decode(transformerDto.TransformerKind),
                    transformerDto.HvTerminalId,
                    transformerDto.DisplayName);
            }
            else
            {
                throw new InvalidDataException(
                    $"Unsupported Transformer naming contract mode '{transformerNamingMode}'.");
            }
            try
            {
                document.AddTransformer(transformer, RestoreTerminal(terminalDto));
            }
            catch (Exception exception) when (exception is ArgumentException or
                                               InvalidOperationException)
            {
                throw new InvalidDataException(
                    $"Transformer '{transformerDto.TransformerId}' aggregate is invalid.",
                    exception);
            }
            transformerTerminalIds.Add(transformerDto.HvTerminalId);
        }

        foreach (ProjectTerminalDto terminalDto in dto.Terminals ?? [])
        {
            if (transformerTerminalIds.Contains(terminalDto.TerminalId) ||
                customerStationTerminalIds.Contains(terminalDto.TerminalId))
            {
                continue;
            }

            if (Parse<TopologyOwnerType>(
                    terminalDto.OwnerType,
                    terminalDto.TerminalId,
                    "ownerType") != TopologyOwnerType.IntermediateTerminal)
            {
                bool poleSwitchTerminal = document.Devices
                    .OfType<SwitchDevice>()
                    .Any(device => device.InstallationType == SwitchInstallationType.Pole &&
                        device.OwnsTerminal(terminalDto.TerminalId));
                document.AddTerminal(RestoreTerminal(terminalDto, poleSwitchTerminal));
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
            cabinet.Terminals.Select(ToDto).ToArray(),
            cabinet.LineName);
    }

    private static ProjectCustomerStationDto ToDto(CustomerStation station)
    {
        return new ProjectCustomerStationDto(
            station.Id,
            Encode(station.StationKind),
            station.IncomingFeeders.Select(feeder =>
                new ProjectCustomerStationIncomingFeederDto(
                    feeder.IncomingFeederId,
                    feeder.Sequence,
                    feeder.DisplayName,
                    feeder.CableTerminalId,
                    feeder.StationTerminalId,
                    feeder.ElectricalNodeId,
                    ToDto(
                        feeder.IsolationSwitch,
                        new ProjectSwitchOwnerReferenceDto(
                            ProjectSwitchOwnerKind.CustomerStationIncomingFeeder,
                            feeder.IncomingFeederId))))
                .ToArray());
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
            interval.CableTerminalId,
            interval.SwitchAssembly.AssemblyId,
            interval.SwitchDevices.Select(device => ToDto(
                device,
                new ProjectSwitchOwnerReferenceDto(
                    ProjectSwitchOwnerKind.RingCabinetInterval,
                    interval.IntervalId))).ToArray());
    }

    private static ProjectSwitchDeviceDto ToDto(
        SwitchDevice device,
        ProjectSwitchOwnerReferenceDto? owner = null)
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
            device.DispatchNumber,
            owner);
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
            DeviceType.Switch or DeviceType.CableTermination or DeviceType.PT or
            DeviceType.Transformer)
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
        if (dto.Owner is not null)
        {
            throw new InvalidDataException(
                $"Top-level switch '{dto.DeviceId}' cannot have an aggregate owner.");
        }

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

    private static Terminal RestoreTerminal(
        ProjectTerminalDto dto,
        bool forceMultipleConnections = false)
    {
        ConnectionType[] allowedConnectionTypes = (dto.AllowedConnectionTypes ?? [])
            .Select(value => Parse<ConnectionType>(value, dto.TerminalId, "allowedConnectionType"))
            .ToArray();

        bool allowsMultipleConnections = dto.AllowsMultipleConnections ||
            (forceMultipleConnections && IsPoleSwitchLeftTerminal(dto.Role));
        return new Terminal(
            dto.TerminalId,
            Parse<TopologyOwnerType>(dto.OwnerType, dto.TerminalId, "ownerType"),
            dto.OwnerId,
            dto.Role,
            dto.VoltageLevel,
            dto.IsExternal,
            allowsMultipleConnections,
            dto.ElectricalNodeId,
            allowedConnectionTypes);
    }

    private static bool IsPoleSwitchLeftTerminal(string role)
    {
        return string.Equals(role, "SwitchLeftTerminal", StringComparison.Ordinal) ||
            string.Equals(role, "SwitchTerminal1", StringComparison.Ordinal);
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
        IReadOnlyList<ProjectTransformerDto> transformerDtos = dto.Transformers ?? [];
        IReadOnlyList<ProjectCustomerStationDto> customerStationDtos =
            dto.CustomerStations ?? [];

        if (transformerDtos.Count != document.Devices.OfType<Transformer>().Count() ||
            transformerDtos.Select(item => item.TransformerId).Distinct().Count() !=
                transformerDtos.Count ||
            transformerDtos.Select(item => item.HvTerminalId).Distinct().Count() !=
                transformerDtos.Count)
        {
            throw new InvalidDataException(
                "Transformer DTOs contain duplicate or missing aggregates.");
        }

        foreach (Transformer transformer in document.Devices.OfType<Transformer>())
        {
            ProjectTransformerDto transformerDto = transformerDtos.SingleOrDefault(item =>
                    item.TransformerId == transformer.Id)
                ?? throw new InvalidDataException(
                    $"Transformer '{transformer.Id}' is missing from DTO.");
            if (transformerDto.HvTerminalId != transformer.HvTerminalId ||
                Decode(transformerDto.TransformerKind) != transformer.TransformerKind)
            {
                throw new InvalidDataException(
                    $"Transformer '{transformer.Id}' is inconsistent with its DTO.");
            }
        }

        ValidateCustomerStations(document, customerStationDtos, nodeDtos, terminalDtos);

        HashSet<Guid> rootNodeIds = nodeDtos.Select(node => node.NodeId).ToHashSet();
        HashSet<Guid> rootTerminalIds = terminalDtos.Select(terminal => terminal.TerminalId).ToHashSet();
        HashSet<Guid> ringCabinetIntervalIds = document.Devices
            .OfType<RingCabinet>()
            .SelectMany(cabinet => cabinet.Intervals)
            .Select(interval => interval.IntervalId)
            .ToHashSet();
        HashSet<Guid> ringCabinetSwitchIds = document.Devices
            .OfType<SwitchDevice>()
            .Where(device =>
                device.InstallationType == SwitchInstallationType.CabinetInterval &&
                device.ParentId is Guid parentId &&
                ringCabinetIntervalIds.Contains(parentId))
            .Select(device => device.Id)
            .ToHashSet();
        HashSet<Guid> ringNodeIds = document.Devices
            .OfType<RingCabinet>()
            .SelectMany(cabinet => cabinet.ElectricalNodes)
            .Select(node => node.Id)
            .Concat(document.ElectricalNodes
                .Where(node => ringCabinetIntervalIds.Contains(node.OwnerId) ||
                    ringCabinetSwitchIds.Contains(node.OwnerId))
                .Select(node => node.Id))
            .ToHashSet();
        HashSet<Guid> ringTerminalIds = document.Devices
            .OfType<RingCabinet>()
            .SelectMany(cabinet => cabinet.Terminals)
            .Select(terminal => terminal.Id)
            .Concat(document.Terminals
                .Where(terminal => ringCabinetIntervalIds.Contains(terminal.OwnerId) ||
                    ringCabinetSwitchIds.Contains(terminal.OwnerId))
                .Select(terminal => terminal.Id))
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

            bool legacyInternalTopology = node.TerminalIds.SetEquals(termination.TerminalIds);
            bool splitInternalTopology = node.TerminalIds.SetEquals(
                [termination.CableSideTerminalId]);
            Terminal overheadTerminal = document.Terminals.Single(candidate =>
                candidate.Id == termination.OverheadSideTerminalId);
            bool validOverheadTopology = overheadTerminal.ElectricalNodeId is Guid overheadNodeId &&
                document.ElectricalNodes.Any(candidate => candidate.Id == overheadNodeId);
            if (node.Type != ElectricalNodeType.Intermediate ||
                node.OwnerType != TopologyOwnerType.Device ||
                node.OwnerId != termination.Id ||
                !terminalIds.SetEquals(termination.TerminalIds) ||
                (!legacyInternalTopology &&
                 (!splitInternalTopology || !validOverheadTopology)))
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

    private static void ValidateCustomerStations(
        DrawingDocument document,
        IReadOnlyList<ProjectCustomerStationDto> stationDtos,
        IReadOnlyList<ProjectElectricalNodeDto> nodeDtos,
        IReadOnlyList<ProjectTerminalDto> terminalDtos)
    {
        CustomerStation[] stations = document.CustomerStations.ToArray();
        if (stationDtos.Count != stations.Length ||
            stationDtos.Select(station => station.CustomerStationId).Distinct().Count() !=
                stationDtos.Count)
        {
            throw new InvalidDataException(
                "Customer station DTOs contain duplicate or missing aggregates.");
        }

        var referencedChildIds = new HashSet<Guid>();
        foreach (CustomerStation station in stations)
        {
            ProjectCustomerStationDto stationDto = stationDtos.SingleOrDefault(candidate =>
                    candidate.CustomerStationId == station.Id)
                ?? throw new InvalidDataException(
                    $"Customer station '{station.Id}' is missing from DTO.");
            if (Decode(stationDto.StationKind) != station.StationKind ||
                stationDto.IncomingFeeders.Count != station.IncomingFeeders.Count)
            {
                throw new InvalidDataException(
                    $"Customer station '{station.Id}' is inconsistent with its DTO.");
            }

            foreach (IncomingFeeder feeder in station.IncomingFeeders)
            {
                ProjectCustomerStationIncomingFeederDto feederDto =
                    stationDto.IncomingFeeders.SingleOrDefault(candidate =>
                        candidate.IncomingFeederId == feeder.IncomingFeederId)
                    ?? throw new InvalidDataException(
                        $"Incoming feeder '{feeder.IncomingFeederId}' is missing from DTO.");
                ProjectSwitchDeviceDto switchDto = feederDto.IsolationSwitch;
                if (feederDto.Sequence != feeder.Sequence ||
                    feederDto.DisplayName.Trim() != feeder.DisplayName ||
                    feederDto.CableTerminalId != feeder.CableTerminalId ||
                    feederDto.StationTerminalId != feeder.StationTerminalId ||
                    feederDto.ElectricalNodeId != feeder.ElectricalNodeId ||
                    switchDto.DeviceId != feeder.IsolationSwitch.Id ||
                    switchDto.SwitchKind != Encode(feeder.IsolationSwitch.SwitchKind) ||
                    switchDto.InstallationType != Encode(feeder.IsolationSwitch.InstallationType) ||
                    switchDto.FirstTerminalId != feeder.CableTerminalId ||
                    switchDto.SecondTerminalId != feeder.StationTerminalId ||
                    switchDto.SwitchState != Encode(feeder.IsolationSwitch.SwitchState ??
                        throw new InvalidDataException(
                            $"Incoming switch '{feeder.IsolationSwitch.Id}' has no state.")) ||
                    switchDto.DisplayName != feeder.IsolationSwitch.DisplayName ||
                    switchDto.VoltageLevel != feeder.IsolationSwitch.VoltageLevel ||
                    switchDto.DispatchNumber != feeder.IsolationSwitch.DispatchNumber ||
                    switchDto.Owner is not
                    {
                        OwnerKind: ProjectSwitchOwnerKind.CustomerStationIncomingFeeder,
                        OwnerId: var ownerId
                    } || ownerId != feeder.IncomingFeederId)
                {
                    throw new InvalidDataException(
                        $"Incoming feeder '{feeder.IncomingFeederId}' is inconsistent with its DTO.");
                }

                Guid[] childIds =
                [
                    feeder.IncomingFeederId,
                    feeder.IsolationSwitch.Id,
                    feeder.CableTerminalId,
                    feeder.StationTerminalId,
                    feeder.ElectricalNodeId
                ];
                if (childIds.Any(id => !referencedChildIds.Add(id)))
                {
                    throw new InvalidDataException(
                        "Customer station DTOs share an aggregate child identity.");
                }

                ProjectTerminalDto cableTerminalDto = RequireSingle(
                    terminalDtos,
                    terminal => terminal.TerminalId == feeder.CableTerminalId,
                    $"Incoming feeder '{feeder.IncomingFeederId}' cable terminal");
                ProjectTerminalDto stationTerminalDto = RequireSingle(
                    terminalDtos,
                    terminal => terminal.TerminalId == feeder.StationTerminalId,
                    $"Incoming feeder '{feeder.IncomingFeederId}' station terminal");
                ProjectElectricalNodeDto nodeDto = RequireSingle(
                    nodeDtos,
                    node => node.NodeId == feeder.ElectricalNodeId,
                    $"Incoming feeder '{feeder.IncomingFeederId}' electrical node");
                if (!Matches(cableTerminalDto, feeder.CableTerminal) ||
                    !Matches(stationTerminalDto, feeder.StationTerminal) ||
                    !Matches(nodeDto, feeder.ElectricalNode) ||
                    !feeder.ElectricalNode.TerminalIds.ToHashSet()
                        .SetEquals([feeder.StationTerminalId]))
                {
                    throw new InvalidDataException(
                        $"Incoming feeder '{feeder.IncomingFeederId}' topology DTO is inconsistent.");
                }
            }
        }
    }

    private static bool Matches(ProjectTerminalDto dto, Terminal terminal)
    {
        return dto.OwnerType == Encode(terminal.OwnerType) &&
            dto.OwnerId == terminal.OwnerId &&
            dto.Role == terminal.Role &&
            dto.VoltageLevel == terminal.VoltageLevel &&
            dto.IsExternal == terminal.IsExternal &&
            dto.AllowsMultipleConnections == terminal.AllowsMultipleConnections &&
            dto.ElectricalNodeId == terminal.ElectricalNodeId &&
            (dto.AllowedConnectionTypes ?? []).ToHashSet(StringComparer.Ordinal)
                .SetEquals(terminal.AllowedConnectionTypes.Select(Encode));
    }

    private static bool Matches(ProjectElectricalNodeDto dto, ElectricalNode node)
    {
        return dto.NodeType == Encode(node.Type) &&
            dto.OwnerType == Encode(node.OwnerType) &&
            dto.OwnerId == node.OwnerId &&
            dto.ElectricalState == EncodeNullable(node.ElectricalState);
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
                intervals,
                dto.LineName));

        ValidateRestoredAggregate(cabinet, dto);
        return cabinet;
    }

    private static CustomerStation RestoreCustomerStation(
        ProjectCustomerStationDto dto,
        IReadOnlyList<ProjectElectricalNodeDto> nodeDtos,
        IReadOnlyList<ProjectTerminalDto> terminalDtos)
    {
        IncomingFeeder[] feeders = (dto.IncomingFeeders ?? throw new InvalidDataException(
                $"Customer station '{dto.CustomerStationId}' is missing incoming feeders."))
            .Select(feederDto => RestoreIncomingFeeder(feederDto, nodeDtos, terminalDtos))
            .ToArray();

        try
        {
            return new CustomerStation(
                dto.CustomerStationId,
                Decode(dto.StationKind),
                feeders);
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException)
        {
            throw new InvalidDataException(
                $"Customer station '{dto.CustomerStationId}' structure is invalid.",
                exception);
        }
    }

    private static IncomingFeeder RestoreIncomingFeeder(
        ProjectCustomerStationIncomingFeederDto dto,
        IReadOnlyList<ProjectElectricalNodeDto> nodeDtos,
        IReadOnlyList<ProjectTerminalDto> terminalDtos)
    {
        ProjectSwitchDeviceDto switchDto = dto.IsolationSwitch
            ?? throw new InvalidDataException(
                $"Incoming feeder '{dto.IncomingFeederId}' is missing its isolation switch.");
        if (switchDto.Owner is not
            {
                OwnerKind: ProjectSwitchOwnerKind.CustomerStationIncomingFeeder,
                OwnerId: var ownerId
            } || ownerId != dto.IncomingFeederId ||
            !string.Equals(
                switchDto.InstallationType,
                "customer-station-incoming-feeder",
                StringComparison.Ordinal) ||
            !string.Equals(switchDto.SwitchKind, "isolation-switch", StringComparison.Ordinal) ||
            switchDto.FirstTerminalId != dto.CableTerminalId ||
            switchDto.SecondTerminalId != dto.StationTerminalId)
        {
            throw new InvalidDataException(
                $"Incoming feeder '{dto.IncomingFeederId}' isolation switch is invalid.");
        }

        ProjectTerminalDto cableTerminalDto = RequireSingle(
            terminalDtos,
            terminal => terminal.TerminalId == dto.CableTerminalId,
            $"Incoming feeder '{dto.IncomingFeederId}' cable terminal");
        ProjectTerminalDto stationTerminalDto = RequireSingle(
            terminalDtos,
            terminal => terminal.TerminalId == dto.StationTerminalId,
            $"Incoming feeder '{dto.IncomingFeederId}' station terminal");
        ProjectElectricalNodeDto nodeDto = RequireSingle(
            nodeDtos,
            node => node.NodeId == dto.ElectricalNodeId,
            $"Incoming feeder '{dto.IncomingFeederId}' electrical node");

        SwitchDevice isolationSwitch =
            SwitchDevice.CreateForCustomerStationIncomingFeeder(
                switchDto.DeviceId,
                dto.IncomingFeederId,
                switchDto.FirstTerminalId,
                switchDto.SecondTerminalId,
                Parse<SwitchState>(switchDto.SwitchState, switchDto.DeviceId, "switchState"),
                switchDto.DisplayName ?? string.Empty,
                switchDto.VoltageLevel);
        isolationSwitch.SetDispatchNumber(switchDto.DispatchNumber);

        try
        {
            return new IncomingFeeder(
                dto.IncomingFeederId,
                dto.Sequence,
                dto.DisplayName,
                dto.CableTerminalId,
                dto.StationTerminalId,
                dto.ElectricalNodeId,
                isolationSwitch,
                RestoreTerminal(cableTerminalDto),
                RestoreTerminal(stationTerminalDto),
                RestoreElectricalNode(nodeDto));
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException)
        {
            throw new InvalidDataException(
                $"Incoming feeder '{dto.IncomingFeederId}' topology is invalid.",
                exception);
        }
    }

    private static T RequireSingle<T>(
        IEnumerable<T> values,
        Func<T, bool> predicate,
        string objectName)
    {
        T[] matches = values.Where(predicate).ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidDataException(
                $"{objectName} must have exactly one typed topology DTO.");
        }

        return matches[0];
    }

    private static RingCabinetIntervalRestoreDefinition RestoreRingCabinetInterval(
        ProjectRingCabinetIntervalDto interval)
    {
        if (interval.BayIndex < 1)
        {
            throw new InvalidDataException(
                $"Interval '{interval.IntervalId}' has an invalid bayIndex '{interval.BayIndex}'.");
        }

        if (interval.CableTerminalId == Guid.Empty)
        {
            throw new InvalidDataException(
                $"Interval '{interval.IntervalId}' has an empty cable terminal ID.");
        }

        IntervalKind intervalKind = Parse<IntervalKind>(
            interval.IntervalKind,
            interval.IntervalId,
            "intervalKind");
        if (intervalKind == IntervalKind.PTInterval && interval.CableTerminalId is null)
        {
            throw new InvalidDataException(
                $"PT interval '{interval.IntervalId}' requires a cable terminal.");
        }

        return new RingCabinetIntervalRestoreDefinition(
            interval.IntervalId,
            interval.ParentCabinetId,
            interval.Sequence,
            interval.BayIndex,
            interval.DisplayName,
            intervalKind,
            interval.GroundingStructureKind is null
                ? null
                : Parse<GroundingStructureKind>(
                    interval.GroundingStructureKind,
                    interval.IntervalId,
                    "groundingStructureKind"),
            interval.IntermediateNodeId,
            interval.CircuitNodeId,
            interval.EarthNodeId,
            interval.CableTerminalId,
            interval.SwitchAssemblyId,
            (interval.Switches ?? throw new InvalidDataException(
                    $"Interval '{interval.IntervalId}' is missing switches."))
                .Select(switchDto => RestoreCabinetSwitch(interval.IntervalId, switchDto))
                .ToArray());
    }

    private static SwitchDeviceRestoreDefinition RestoreCabinetSwitch(
        Guid intervalId,
        ProjectSwitchDeviceDto switchDto)
    {
        if (switchDto.Owner is not
            {
                OwnerKind: ProjectSwitchOwnerKind.RingCabinetInterval,
                OwnerId: var ownerId
            } || ownerId != intervalId)
        {
            throw new InvalidDataException(
                $"Cabinet switch '{switchDto.DeviceId}' has an invalid typed owner.");
        }

        return new SwitchDeviceRestoreDefinition(
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
            switchDto.DispatchNumber);
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

    private static ProjectTransformerKind Encode(TransformerKind value) => value switch
    {
        TransformerKind.PublicPoleMounted => ProjectTransformerKind.PublicPoleMounted,
        TransformerKind.DedicatedPoleMounted => ProjectTransformerKind.DedicatedPoleMounted,
        TransformerKind.PublicIndoor => ProjectTransformerKind.PublicIndoor,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static TransformerKind Decode(ProjectTransformerKind value) => value switch
    {
        ProjectTransformerKind.PublicPoleMounted => TransformerKind.PublicPoleMounted,
        ProjectTransformerKind.DedicatedPoleMounted => TransformerKind.DedicatedPoleMounted,
        ProjectTransformerKind.PublicIndoor => TransformerKind.PublicIndoor,
        _ => throw new InvalidDataException($"Unsupported transformer kind '{value}'.")
    };

    private static ProjectStationKind Encode(StationKind value) => value switch
    {
        StationKind.BoxStation => ProjectStationKind.BoxStation,
        StationKind.IndoorStation => ProjectStationKind.IndoorStation,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static StationKind Decode(ProjectStationKind value) => value switch
    {
        ProjectStationKind.BoxStation => StationKind.BoxStation,
        ProjectStationKind.IndoorStation => StationKind.IndoorStation,
        _ => throw new InvalidDataException($"Unsupported customer station kind '{value}'.")
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
        SwitchInstallationType.CustomerStationIncomingFeeder =>
            "customer-station-incoming-feeder",
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
