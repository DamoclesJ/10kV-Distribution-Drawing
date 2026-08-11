using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Interaction;

namespace DistributionDrawing.Rendering.Wpf.PropertyInspector;

public sealed class SelectionObjectResolver
{
    private PropertyInspectionSource _source = new();

    public void SetSource(PropertyInspectionSource? source)
    {
        _source = source ?? new PropertyInspectionSource();
    }

    public ResolvedSelection? Resolve(SelectionReference? reference)
    {
        if (reference is null)
        {
            return null;
        }

        ResolvedSelection? resolved = reference.Kind switch
        {
            SelectionTargetKind.RingCabinet => ResolveRingCabinet(reference),
            SelectionTargetKind.RingCabinetInterval => ResolveInterval(reference),
            SelectionTargetKind.Device => ResolveDevice(reference),
            SelectionTargetKind.PoleAttachment => ResolveAttachment(reference),
            SelectionTargetKind.Connection => ResolveConnection(reference),
            _ => null
        };

        if (resolved is null)
        {
            return null;
        }

        return new ResolvedSelection
        {
            Reference = resolved.Reference,
            RingCabinet = resolved.RingCabinet,
            RingCabinetInterval = resolved.RingCabinetInterval,
            SwitchDevice = resolved.SwitchDevice,
            Pole = resolved.Pole,
            PoleAttachment = resolved.PoleAttachment,
            Connection = resolved.Connection,
            OverheadLine = resolved.OverheadLine,
            RingCabinetLayout = resolved.RingCabinetLayout,
            RingCabinetIntervalLayout = resolved.RingCabinetIntervalLayout,
            PoleLayout = resolved.PoleLayout,
            AttachmentLayout = resolved.AttachmentLayout,
            OverheadLineLayout = resolved.OverheadLineLayout,
            HitTestEntry = _source.HitTestIndex?.Find(reference)
        };
    }

    private ResolvedSelection? ResolveRingCabinet(SelectionReference reference)
    {
        if (_source.RingCabinet is not { } cabinet || cabinet.Id != reference.ObjectId)
        {
            return null;
        }

        return new ResolvedSelection
        {
            Reference = reference,
            RingCabinet = cabinet,
            RingCabinetLayout = _source.RingCabinetLayout
        };
    }

    private ResolvedSelection? ResolveInterval(SelectionReference reference)
    {
        if (_source.RingCabinet is not { } cabinet ||
            (reference.ParentId is Guid parentId && parentId != cabinet.Id))
        {
            return null;
        }

        RingCabinetInterval? interval = cabinet.Intervals
            .SingleOrDefault(candidate => candidate.IntervalId == reference.ObjectId);
        if (interval is null || _source.RingCabinetLayout is not { } cabinetLayout ||
            !cabinetLayout.IntervalLayouts.TryGetValue(
                interval.IntervalId,
                out RingCabinetIntervalLayout intervalLayout))
        {
            return null;
        }

        return new ResolvedSelection
        {
            Reference = reference,
            RingCabinet = cabinet,
            RingCabinetInterval = interval,
            RingCabinetLayout = _source.RingCabinetLayout,
            RingCabinetIntervalLayout = intervalLayout
        };
    }

    private ResolvedSelection? ResolveDevice(SelectionReference reference)
    {
        Pole? pole = _source.Poles.SingleOrDefault(candidate => candidate.Id == reference.ObjectId);
        if (pole is not null)
        {
            PoleLayout? poleLayout = null;
            if (_source.DrawingLayout is { } drawingLayout &&
                drawingLayout.Poles.TryGetValue(pole.Id, out PoleLayout foundPoleLayout))
            {
                poleLayout = foundPoleLayout;
            }

            return new ResolvedSelection
            {
                Reference = reference,
                Pole = pole,
                PoleLayout = poleLayout
            };
        }

        if (_source.RingCabinet is not { } cabinet)
        {
            return null;
        }

        RingCabinetInterval? interval = cabinet.Intervals
            .SingleOrDefault(candidate => candidate.IntervalId == reference.ParentId);
        SwitchDevice? switchDevice = interval?.SwitchDevices
            .SingleOrDefault(candidate => candidate.Id == reference.ObjectId);
        RingCabinetIntervalLayout? intervalLayout = null;
        if (interval is not null && _source.RingCabinetLayout is { } cabinetLayout &&
            cabinetLayout.IntervalLayouts.TryGetValue(interval.IntervalId, out RingCabinetIntervalLayout foundIntervalLayout))
        {
            intervalLayout = foundIntervalLayout;
        }

        return switchDevice is null
            ? null
            : new ResolvedSelection
            {
                Reference = reference,
                RingCabinet = cabinet,
                RingCabinetInterval = interval,
                RingCabinetLayout = _source.RingCabinetLayout,
                RingCabinetIntervalLayout = intervalLayout,
                SwitchDevice = switchDevice
            };
    }

    private ResolvedSelection? ResolveAttachment(SelectionReference reference)
    {
        PoleAttachment? attachment = _source.PoleAttachments
            .SingleOrDefault(candidate => candidate.AttachmentId == reference.ObjectId);
        if (attachment is null ||
            (reference.ParentId is Guid parentId && parentId != attachment.PoleId))
        {
            return null;
        }

        PoleLayout? poleLayout = null;
        AttachmentLayout? attachmentLayout = null;
        if (_source.DrawingLayout is { } drawingLayout)
        {
            if (drawingLayout.Poles.TryGetValue(attachment.PoleId, out PoleLayout foundPoleLayout))
            {
                poleLayout = foundPoleLayout;
            }

            if (drawingLayout.Attachments.TryGetValue(
                    attachment.AttachmentId,
                    out AttachmentLayout foundAttachmentLayout))
            {
                attachmentLayout = foundAttachmentLayout;
            }
        }

        return new ResolvedSelection
        {
            Reference = reference,
            PoleAttachment = attachment,
            PoleLayout = poleLayout,
            AttachmentLayout = attachmentLayout
        };
    }

    private ResolvedSelection? ResolveConnection(SelectionReference reference)
    {
        OverheadLine? overheadLine = _source.OverheadLines
            .SingleOrDefault(candidate => candidate.ConnectionId == reference.ObjectId);
        Connection? connection = _source.Connections
            .SingleOrDefault(candidate => candidate.Id == reference.ObjectId);
        if (overheadLine is null)
        {
            return null;
        }

        OverheadLineLayout? lineLayout = null;
        if (_source.DrawingLayout is { } drawingLayout &&
            drawingLayout.OverheadLines.TryGetValue(
                overheadLine.ConnectionId,
                out OverheadLineLayout foundLineLayout))
        {
            lineLayout = foundLineLayout;
        }

        return new ResolvedSelection
        {
            Reference = reference,
            Connection = connection,
            OverheadLine = overheadLine,
            OverheadLineLayout = lineLayout
        };
    }
}
