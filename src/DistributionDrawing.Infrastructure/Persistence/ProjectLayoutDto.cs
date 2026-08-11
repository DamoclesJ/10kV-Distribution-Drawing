using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Infrastructure.Persistence;

public sealed record ProjectLayoutDto(
    Guid DocumentId,
    string CoordinateUnit,
    IReadOnlyList<ProjectRingCabinetLayoutDto> RingCabinets,
    IReadOnlyList<ProjectPoleLayoutDto> Poles,
    IReadOnlyList<ProjectAttachmentLayoutDto> Attachments,
    IReadOnlyList<ProjectOverheadLineLayoutDto> OverheadLines)
{
    public static ProjectLayoutDto Empty(Guid documentId)
    {
        return new ProjectLayoutDto(documentId, "mm", [], [], [], []);
    }
}

public sealed record ProjectPointDto(
    double XMillimeters,
    double YMillimeters);

public sealed record ProjectRingCabinetLayoutDto(
    Guid CabinetId,
    ProjectPointDto Position,
    double WidthMillimeters,
    double HeightMillimeters,
    double MainBusYMillimeters,
    ProjectPointDto LabelOffset,
    IReadOnlyList<ProjectRingCabinetIntervalLayoutDto> Intervals);

public sealed record ProjectRingCabinetIntervalLayoutDto(
    Guid IntervalId,
    ProjectPointDto RelativePosition,
    double WidthMillimeters,
    double HeightMillimeters,
    ProjectPointDto SequenceLabelOffset,
    ProjectPointDto NameLabelOffset,
    IReadOnlyList<ProjectRingCabinetSwitchLayoutDto> Switches);

public sealed record ProjectRingCabinetSwitchLayoutDto(
    Guid SwitchDeviceId,
    ProjectPointDto RelativePosition,
    double WidthMillimeters,
    double HeightMillimeters,
    ProjectPointDto LabelOffset);

public sealed record ProjectPoleLayoutDto(
    Guid PoleId,
    ProjectPointDto Position,
    double WidthMillimeters,
    double HeightMillimeters,
    ProjectPointDto LabelOffset);

public sealed record ProjectAttachmentLayoutDto(
    Guid AttachmentId,
    ProjectPointDto Offset,
    double WidthMillimeters,
    double HeightMillimeters,
    ProjectPointDto LabelOffset);

public sealed record ProjectOverheadLineLayoutDto(
    Guid ConnectionId,
    ProjectPointDto Start,
    ProjectPointDto End,
    ProjectPointDto ContinuationOffset);

/// <summary>
/// Persistence-neutral, validated layout snapshot. It deliberately contains
/// no WPF Point, DIP, Visual, transform, selection, or hit-test state.
/// </summary>
public sealed record ProjectLayoutSnapshot(ProjectLayoutDto Data)
{
    public Guid DocumentId => Data.DocumentId;

    public string CoordinateUnit => Data.CoordinateUnit;

    public IReadOnlyList<ProjectRingCabinetLayoutDto> RingCabinets => Data.RingCabinets;

    public IReadOnlyList<ProjectPoleLayoutDto> Poles => Data.Poles;

    public IReadOnlyList<ProjectAttachmentLayoutDto> Attachments => Data.Attachments;

    public IReadOnlyList<ProjectOverheadLineLayoutDto> OverheadLines => Data.OverheadLines;

    public static ProjectLayoutSnapshot Empty(Guid documentId)
    {
        return new ProjectLayoutSnapshot(ProjectLayoutDto.Empty(documentId));
    }
}

internal static class ProjectLayoutMapper
{
    public static ProjectLayoutSnapshot ToSnapshot(
        DrawingDocument domain,
        ProjectLayoutDto? dto)
    {
        ArgumentNullException.ThrowIfNull(domain);

        ProjectLayoutDto layout = dto ?? ProjectLayoutDto.Empty(domain.Id);
        Validate(domain, layout);
        return new ProjectLayoutSnapshot(layout);
    }

    public static ProjectLayoutDto ToDto(
        DrawingDocument domain,
        ProjectLayoutSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(snapshot);

        Validate(domain, snapshot.Data);
        return snapshot.Data;
    }

    private static void Validate(DrawingDocument domain, ProjectLayoutDto layout)
    {
        if (layout.DocumentId != domain.Id)
        {
            throw new InvalidDataException(
                "Layout document ID does not match the restored Domain document.");
        }

        if (!string.Equals(layout.CoordinateUnit, "mm", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported layout coordinate unit '{layout.CoordinateUnit}'.");
        }

        IReadOnlyList<ProjectRingCabinetLayoutDto> cabinetDtos =
            layout.RingCabinets ?? throw new InvalidDataException(
                "Ring cabinet layouts are required.");
        IReadOnlyList<ProjectPoleLayoutDto> poleDtos =
            layout.Poles ?? throw new InvalidDataException("Pole layouts are required.");
        IReadOnlyList<ProjectAttachmentLayoutDto> attachmentDtos =
            layout.Attachments ?? throw new InvalidDataException(
                "Attachment layouts are required.");
        IReadOnlyList<ProjectOverheadLineLayoutDto> overheadLineDtos =
            layout.OverheadLines ?? throw new InvalidDataException(
                "Overhead line layouts are required.");

        EnsureUnique(cabinetDtos.Select(layoutDto => layoutDto.CabinetId), "ring cabinet layout");
        EnsureUnique(poleDtos.Select(layoutDto => layoutDto.PoleId), "pole layout");
        EnsureUnique(
            attachmentDtos.Select(layoutDto => layoutDto.AttachmentId),
            "attachment layout");
        EnsureUnique(
            overheadLineDtos.Select(layoutDto => layoutDto.ConnectionId),
            "overhead line layout");

        HashSet<Guid> intervalLayoutIds = [];
        HashSet<Guid> switchLayoutIds = [];
        foreach (ProjectRingCabinetLayoutDto cabinetDto in cabinetDtos)
        {
            foreach (ProjectRingCabinetIntervalLayoutDto intervalDto in cabinetDto.Intervals ?? [])
            {
                if (!intervalLayoutIds.Add(intervalDto.IntervalId))
                {
                    throw new InvalidDataException(
                        $"Duplicate interval layout '{intervalDto.IntervalId}'.");
                }

                foreach (ProjectRingCabinetSwitchLayoutDto switchDto in intervalDto.Switches ?? [])
                {
                    if (!switchLayoutIds.Add(switchDto.SwitchDeviceId))
                    {
                        throw new InvalidDataException(
                            $"Duplicate switch layout '{switchDto.SwitchDeviceId}'.");
                    }
                }
            }
        }

        Dictionary<Guid, RingCabinet> cabinets = domain.Devices
            .OfType<RingCabinet>()
            .ToDictionary(cabinet => cabinet.Id);
        Dictionary<Guid, Pole> poles = domain.Devices
            .OfType<Pole>()
            .ToDictionary(pole => pole.Id);
        Dictionary<Guid, PoleAttachment> attachments = domain.PoleAttachments
            .ToDictionary(attachment => attachment.AttachmentId);
        HashSet<Guid> overheadLineIds = domain.OverheadLines
            .Select(line => line.ConnectionId)
            .ToHashSet();

        if (cabinetDtos.Count != cabinets.Count ||
            poleDtos.Count != poles.Count ||
            attachmentDtos.Count != attachments.Count ||
            overheadLineDtos.Count != overheadLineIds.Count)
        {
            throw new InvalidDataException(
                "Layout coverage does not match the Domain object set.");
        }

        foreach (ProjectRingCabinetLayoutDto cabinetDto in cabinetDtos)
        {
            if (!cabinets.TryGetValue(cabinetDto.CabinetId, out RingCabinet? cabinet))
            {
                throw new InvalidDataException(
                    $"Ring cabinet layout references missing cabinet '{cabinetDto.CabinetId}'.");
            }

            ValidateRingCabinetLayout(cabinet, cabinetDto);
        }

        foreach (ProjectPoleLayoutDto poleDto in poleDtos)
        {
            if (!poles.ContainsKey(poleDto.PoleId))
            {
                throw new InvalidDataException(
                    $"Pole layout references missing pole '{poleDto.PoleId}'.");
            }

            ValidateDimensions(
                poleDto.WidthMillimeters,
                poleDto.HeightMillimeters,
                $"pole '{poleDto.PoleId}'");
            ValidatePoint(poleDto.Position, $"pole '{poleDto.PoleId}' position");
            ValidatePoint(poleDto.LabelOffset, $"pole '{poleDto.PoleId}' label offset");
        }

        foreach (ProjectAttachmentLayoutDto attachmentDto in attachmentDtos)
        {
            if (!attachments.ContainsKey(attachmentDto.AttachmentId))
            {
                throw new InvalidDataException(
                    $"Attachment layout references missing attachment '{attachmentDto.AttachmentId}'.");
            }

            ValidateDimensions(
                attachmentDto.WidthMillimeters,
                attachmentDto.HeightMillimeters,
                $"attachment '{attachmentDto.AttachmentId}'");
            ValidatePoint(attachmentDto.Offset, $"attachment '{attachmentDto.AttachmentId}' offset");
            ValidatePoint(
                attachmentDto.LabelOffset,
                $"attachment '{attachmentDto.AttachmentId}' label offset");
        }

        foreach (ProjectOverheadLineLayoutDto overheadLineDto in overheadLineDtos)
        {
            if (!overheadLineIds.Contains(overheadLineDto.ConnectionId))
            {
                throw new InvalidDataException(
                    $"Overhead line layout references missing line '{overheadLineDto.ConnectionId}'.");
            }

            ValidatePoint(overheadLineDto.Start, $"overhead line '{overheadLineDto.ConnectionId}' start");
            ValidatePoint(overheadLineDto.End, $"overhead line '{overheadLineDto.ConnectionId}' end");
            ValidatePoint(
                overheadLineDto.ContinuationOffset,
                $"overhead line '{overheadLineDto.ConnectionId}' continuation offset");
        }
    }

    private static void ValidateRingCabinetLayout(
        RingCabinet cabinet,
        ProjectRingCabinetLayoutDto cabinetDto)
    {
        ValidateDimensions(
            cabinetDto.WidthMillimeters,
            cabinetDto.HeightMillimeters,
            $"ring cabinet '{cabinet.Id}'");
        ValidatePoint(cabinetDto.Position, $"ring cabinet '{cabinet.Id}' position");
        ValidatePoint(cabinetDto.LabelOffset, $"ring cabinet '{cabinet.Id}' label offset");

        if (!IsFinite(cabinetDto.MainBusYMillimeters) ||
            cabinetDto.MainBusYMillimeters < 0 ||
            cabinetDto.MainBusYMillimeters > cabinetDto.HeightMillimeters)
        {
            throw new InvalidDataException(
                $"Ring cabinet '{cabinet.Id}' has an invalid main bus position.");
        }

        IReadOnlyList<ProjectRingCabinetIntervalLayoutDto> intervalDtos =
            cabinetDto.Intervals ?? throw new InvalidDataException(
                $"Ring cabinet '{cabinet.Id}' is missing interval layouts.");
        EnsureUnique(
            intervalDtos.Select(layout => layout.IntervalId),
            $"ring cabinet '{cabinet.Id}' interval layout");

        if (intervalDtos.Count != cabinet.Intervals.Count)
        {
            throw new InvalidDataException(
                $"Ring cabinet '{cabinet.Id}' layout coverage does not match intervals.");
        }

        Dictionary<Guid, RingCabinetInterval> intervals = cabinet.Intervals
            .ToDictionary(interval => interval.IntervalId);
        foreach (ProjectRingCabinetIntervalLayoutDto intervalDto in intervalDtos)
        {
            if (!intervals.TryGetValue(intervalDto.IntervalId, out RingCabinetInterval? interval))
            {
                throw new InvalidDataException(
                    $"Interval layout references missing interval '{intervalDto.IntervalId}'.");
            }

            ValidateDimensions(
                intervalDto.WidthMillimeters,
                intervalDto.HeightMillimeters,
                $"interval '{interval.IntervalId}'");
            ValidatePoint(
                intervalDto.RelativePosition,
                $"interval '{interval.IntervalId}' relative position");
            ValidatePoint(
                intervalDto.SequenceLabelOffset,
                $"interval '{interval.IntervalId}' sequence label offset");
            ValidatePoint(
                intervalDto.NameLabelOffset,
                $"interval '{interval.IntervalId}' name label offset");

            IReadOnlyList<ProjectRingCabinetSwitchLayoutDto> switchDtos =
                intervalDto.Switches ?? throw new InvalidDataException(
                    $"Interval '{interval.IntervalId}' is missing switch layouts.");
            EnsureUnique(
                switchDtos.Select(layout => layout.SwitchDeviceId),
                $"interval '{interval.IntervalId}' switch layout");

            if (switchDtos.Count != interval.SwitchDevices.Count)
            {
                throw new InvalidDataException(
                    $"Interval '{interval.IntervalId}' layout coverage does not match switches.");
            }

            HashSet<Guid> switchIds = interval.SwitchDevices
                .Select(device => device.Id)
                .ToHashSet();
            foreach (ProjectRingCabinetSwitchLayoutDto switchDto in switchDtos)
            {
                if (!switchIds.Contains(switchDto.SwitchDeviceId))
                {
                    throw new InvalidDataException(
                        $"Switch layout references missing switch '{switchDto.SwitchDeviceId}'.");
                }

                ValidateDimensions(
                    switchDto.WidthMillimeters,
                    switchDto.HeightMillimeters,
                    $"switch '{switchDto.SwitchDeviceId}'");
                ValidatePoint(
                    switchDto.RelativePosition,
                    $"switch '{switchDto.SwitchDeviceId}' relative position");
                ValidatePoint(
                    switchDto.LabelOffset,
                    $"switch '{switchDto.SwitchDeviceId}' label offset");
            }
        }
    }

    private static void EnsureUnique(IEnumerable<Guid> ids, string objectName)
    {
        Guid[] values = ids.ToArray();
        if (values.Any(id => id == Guid.Empty) || values.Distinct().Count() != values.Length)
        {
            throw new InvalidDataException($"Duplicate or empty ID in {objectName}.");
        }
    }

    private static void ValidateDimensions(double width, double height, string objectName)
    {
        if (!IsFinite(width) || !IsFinite(height) || width <= 0 || height <= 0)
        {
            throw new InvalidDataException(
                $"Invalid dimensions in {objectName}; width and height must be finite and positive.");
        }
    }

    private static void ValidatePoint(ProjectPointDto? point, string fieldName)
    {
        if (point is null || !IsFinite(point.XMillimeters) || !IsFinite(point.YMillimeters))
        {
            throw new InvalidDataException(
                $"Invalid millimeter point in {fieldName}.");
        }
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
