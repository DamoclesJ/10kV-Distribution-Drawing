using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.Interaction;

public sealed class InspectorResolver
{
    private readonly DrawingDocument _document;

    public InspectorResolver(DrawingDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    public InspectorModel? Resolve(SelectionTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return target.TargetKind switch
        {
            SelectionTargetKind.RingCabinet => ResolveRingCabinet(target.TargetId),
            SelectionTargetKind.Interval => ResolveInterval(target.TargetId),
            SelectionTargetKind.SwitchDevice => ResolveSwitchDevice(target.TargetId),
            SelectionTargetKind.Pole => ResolvePole(target.TargetId),
            SelectionTargetKind.PoleAttachment => ResolveAttachment(target.TargetId),
            SelectionTargetKind.CableSegment => ResolveCableSegment(target.TargetId),
            SelectionTargetKind.IntermediateTerminal => ResolveIntermediateTerminal(target.TargetId),
            _ => null
        };
    }

    private InspectorModel? ResolveRingCabinet(Guid id)
    {
        RingCabinet? cabinet = _document.Devices
            .OfType<RingCabinet>()
            .SingleOrDefault(candidate => candidate.Id == id);
        return cabinet is null
            ? null
            : Model(
                cabinet.DisplayName ?? "Ring Cabinet",
                ("Id", cabinet.Id),
                ("IntervalCount", cabinet.Intervals.Count),
                ("CompositionKind", cabinet.CompositionKind));
    }

    private InspectorModel? ResolveInterval(Guid id)
    {
        RingCabinetInterval? interval = _document.Devices
            .OfType<RingCabinet>()
            .SelectMany(cabinet => cabinet.Intervals)
            .SingleOrDefault(candidate => candidate.IntervalId == id);
        return interval is null
            ? null
            : Model(
                interval.DisplayName,
                ("IntervalId", interval.IntervalId),
                ("Sequence", interval.Sequence),
                ("IntervalKind", interval.IntervalKind),
                ("SwitchCount", interval.SwitchDevices.Count));
    }

    private InspectorModel? ResolveSwitchDevice(Guid id)
    {
        SwitchDevice? switchDevice = _document.Devices
            .OfType<SwitchDevice>()
            .SingleOrDefault(candidate => candidate.Id == id)
            ?? _document.Devices
                .OfType<RingCabinet>()
                .SelectMany(cabinet => cabinet.Intervals)
                .SelectMany(interval => interval.SwitchDevices)
                .SingleOrDefault(candidate => candidate.Id == id);
        return switchDevice is null
            ? null
            : Model(
                switchDevice.DisplayName ?? "Switch Device",
                ("Id", switchDevice.Id),
                ("SwitchKind", switchDevice.SwitchKind),
                ("SwitchState", switchDevice.SwitchState),
                ("InstallationType", switchDevice.InstallationType));
    }

    private InspectorModel? ResolvePole(Guid id)
    {
        Pole? pole = _document.Devices
            .OfType<Pole>()
            .SingleOrDefault(candidate => candidate.Id == id);
        return pole is null
            ? null
            : Model(
                pole.DisplayName ?? pole.PoleNumber,
                ("Id", pole.Id),
                ("PoleNumber", pole.PoleNumber),
                ("PoleType", pole.PoleType),
                ("AttachmentCount", _document.PoleAttachments.Count(attachment => attachment.PoleId == pole.Id)));
    }

    private InspectorModel? ResolveAttachment(Guid id)
    {
        PoleAttachment? attachment = _document.PoleAttachments
            .SingleOrDefault(candidate => candidate.AttachmentId == id);
        return attachment is null
            ? null
            : Model(
                "Pole Attachment",
                ("AttachmentId", attachment.AttachmentId),
                ("PoleId", attachment.PoleId),
                ("AttachedDeviceId", attachment.AttachedDeviceId));
    }

    private InspectorModel? ResolveCableSegment(Guid id)
    {
        CableSegment? cable = _document.CableSegments
            .SingleOrDefault(candidate => candidate.Id == id);
        return cable is null
            ? null
            : Model(
                cable.Name,
                ("Id", cable.Id),
                ("CableType", cable.CableType),
                ("Length", cable.Length),
                ("VoltageLevel", cable.VoltageLevel));
    }

    private InspectorModel? ResolveIntermediateTerminal(Guid id)
    {
        IntermediateTerminal? terminal = _document.IntermediateTerminals
            .SingleOrDefault(candidate => candidate.Id == id);
        return terminal is null
            ? null
            : Model(
                terminal.DisplayName,
                ("Id", terminal.Id),
                ("TerminalId", terminal.TerminalId));
    }

    private static InspectorModel Model(string title, params (string Key, object? Value)[] properties)
    {
        return new InspectorModel(
            title,
            properties
                .Select(property => new InspectorProperty(
                    property.Key,
                    property.Value?.ToString() ?? string.Empty))
                .ToArray());
    }
}
