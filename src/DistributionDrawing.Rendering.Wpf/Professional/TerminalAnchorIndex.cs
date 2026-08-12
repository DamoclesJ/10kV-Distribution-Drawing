using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Professional;

/// <summary>
/// Builds a transient terminal-to-document-coordinate index from the Domain
/// and current millimeter layout. No Domain objects are retained.
/// </summary>
public sealed class TerminalAnchorIndex
{
    private const double IntegratedTerminalWidth = 10;
    private const double IntegratedTerminalHeight = 8;

    private readonly IReadOnlyDictionary<Guid, TerminalAnchor> _anchors;

    private TerminalAnchorIndex(IReadOnlyDictionary<Guid, TerminalAnchor> anchors)
    {
        _anchors = anchors;
    }

    public IReadOnlyCollection<TerminalAnchor> Anchors => _anchors.Values;

    public bool TryGet(Guid terminalId, out TerminalAnchor anchor)
    {
        return _anchors.TryGetValue(terminalId, out anchor);
    }

    public static TerminalAnchorIndex Build(
        DrawingDocument document,
        DrawingLayout drawingLayout,
        IReadOnlyDictionary<Guid, RingCabinetLayout> ringCabinetLayouts)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(drawingLayout);
        ArgumentNullException.ThrowIfNull(ringCabinetLayouts);

        var anchors = new Dictionary<Guid, TerminalAnchor>();

        foreach (Pole pole in document.Devices.OfType<Pole>())
        {
            if (!drawingLayout.Poles.TryGetValue(pole.Id, out PoleLayout poleLayout))
            {
                continue;
            }

            DocumentPoint poleAnchor = new(
                poleLayout.Position.XMillimeters + poleLayout.WidthMillimeters / 2,
                poleLayout.Position.YMillimeters);
            foreach (Guid terminalId in pole.OverheadAnchorTerminalIds)
            {
                Set(anchors, terminalId, poleAnchor);
            }
        }

        foreach (PoleAttachment attachment in document.PoleAttachments)
        {
            if (!drawingLayout.Attachments.TryGetValue(
                    attachment.AttachmentId,
                    out AttachmentLayout attachmentLayout) ||
                !drawingLayout.Poles.TryGetValue(attachment.PoleId, out PoleLayout poleLayout))
            {
                continue;
            }

            Device? attachedDevice = document.Devices
                .SingleOrDefault(device => device.Id == attachment.AttachedDeviceId);
            if (attachedDevice is null)
            {
                continue;
            }

            DocumentPoint attachmentOrigin = new(
                poleLayout.Position.XMillimeters + attachmentLayout.Offset.XMillimeters,
                poleLayout.Position.YMillimeters + attachmentLayout.Offset.YMillimeters);
            DocumentPoint attachmentAnchor = new(
                attachmentOrigin.XMillimeters + attachmentLayout.WidthMillimeters / 2,
                attachmentOrigin.YMillimeters + attachmentLayout.HeightMillimeters / 2);

            foreach (Guid terminalId in GetTerminalIds(attachedDevice))
            {
                Set(anchors, terminalId, attachmentAnchor);
            }
        }

        foreach (RingCabinet cabinet in document.Devices.OfType<RingCabinet>())
        {
            if (!ringCabinetLayouts.TryGetValue(cabinet.Id, out RingCabinetLayout? cabinetLayout))
            {
                continue;
            }

            foreach (RingCabinetInterval interval in cabinet.Intervals)
            {
                if (!cabinetLayout.IntervalLayouts.TryGetValue(
                        interval.IntervalId,
                        out RingCabinetIntervalLayout intervalLayout))
                {
                    continue;
                }

                DocumentPoint origin = new(
                    cabinetLayout.Position.XMillimeters + intervalLayout.RelativePosition.XMillimeters,
                    cabinetLayout.Position.YMillimeters + intervalLayout.RelativePosition.YMillimeters);
                DocumentPoint terminalPosition = interval.IntervalKind == IntervalKind.IntegratedFeederInterval
                    ? new DocumentPoint(
                        origin.XMillimeters + (intervalLayout.WidthMillimeters - IntegratedTerminalWidth) / 2 +
                        IntegratedTerminalWidth / 2,
                        origin.YMillimeters + intervalLayout.HeightMillimeters -
                        IntegratedTerminalHeight - 4)
                    : new DocumentPoint(
                        origin.XMillimeters + intervalLayout.WidthMillimeters / 2,
                        origin.YMillimeters + intervalLayout.HeightMillimeters);
                Set(anchors, interval.ExternalTerminalId, terminalPosition);
            }
        }

        return new TerminalAnchorIndex(
            new Dictionary<Guid, TerminalAnchor>(anchors));
    }

    private static IReadOnlyList<Guid> GetTerminalIds(Device device)
    {
        return device switch
        {
            CableTermination termination => termination.TerminalIds,
            SwitchDevice switchDevice => switchDevice.TerminalIds,
            _ => []
        };
    }

    private static void Set(
        IDictionary<Guid, TerminalAnchor> anchors,
        Guid terminalId,
        DocumentPoint position)
    {
        if (terminalId != Guid.Empty)
        {
            anchors[terminalId] = new TerminalAnchor(terminalId, position);
        }
    }
}
