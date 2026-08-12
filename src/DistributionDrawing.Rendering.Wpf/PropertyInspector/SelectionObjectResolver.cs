using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
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
            SelectionTargetKind.GroundingPoint => ResolveGroundingPoint(reference),
            SelectionTargetKind.WorkScope => ResolveWorkScope(reference),
            SelectionTargetKind.Terminal => ResolveTerminal(reference),
            _ => null
        };

        if (resolved is null)
        {
            return null;
        }

        return new ResolvedSelection
        {
            Reference = resolved.Reference,
            Document = _source.Document,
            RingCabinet = resolved.RingCabinet,
            RingCabinetInterval = resolved.RingCabinetInterval,
            SwitchDevice = resolved.SwitchDevice,
            Pole = resolved.Pole,
            PoleAttachment = resolved.PoleAttachment,
            Connection = resolved.Connection,
            OverheadLine = resolved.OverheadLine,
            WorkScope = resolved.WorkScope,
            GroundingPoint = resolved.GroundingPoint,
            Terminal = resolved.Terminal,
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
        RingCabinet? cabinet = GetRingCabinets()
            .SingleOrDefault(candidate => candidate.Id == reference.ObjectId);
        if (cabinet is null ||
            !_source.RingCabinetLayouts.TryGetValue(cabinet.Id, out RingCabinetLayout? layout))
        {
            return null;
        }

        return new ResolvedSelection
        {
            Reference = reference,
            RingCabinet = cabinet,
            RingCabinetLayout = layout
        };
    }

    private ResolvedSelection? ResolveInterval(SelectionReference reference)
    {
        RingCabinet? cabinet = reference.ParentId is Guid parentId
            ? GetRingCabinets().SingleOrDefault(candidate => candidate.Id == parentId)
            : GetRingCabinets().SingleOrDefault(candidate =>
                candidate.Intervals.Any(interval => interval.IntervalId == reference.ObjectId));
        if (cabinet is null)
        {
            return null;
        }

        RingCabinetInterval? interval = cabinet.Intervals
            .SingleOrDefault(candidate => candidate.IntervalId == reference.ObjectId);
        if (interval is null ||
            !_source.RingCabinetLayouts.TryGetValue(cabinet.Id, out RingCabinetLayout? cabinetLayout) ||
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
            RingCabinetLayout = cabinetLayout,
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

        RingCabinet? cabinet = GetRingCabinets().SingleOrDefault(candidate =>
            candidate.Intervals.Any(interval => interval.IntervalId == reference.ParentId));
        RingCabinetInterval? interval = cabinet?.Intervals
            .SingleOrDefault(candidate => candidate.IntervalId == reference.ParentId);
        SwitchDevice? switchDevice = interval?.SwitchDevices
            .SingleOrDefault(candidate => candidate.Id == reference.ObjectId);
        RingCabinetLayout? resolvedCabinetLayout = null;
        RingCabinetIntervalLayout? intervalLayout = null;
        if (cabinet is not null && interval is not null &&
            _source.RingCabinetLayouts.TryGetValue(cabinet.Id, out resolvedCabinetLayout) &&
            resolvedCabinetLayout.IntervalLayouts.TryGetValue(interval.IntervalId, out RingCabinetIntervalLayout foundIntervalLayout))
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
                RingCabinetLayout = resolvedCabinetLayout,
                RingCabinetIntervalLayout = intervalLayout,
                SwitchDevice = switchDevice
            };
    }

    private IEnumerable<RingCabinet> GetRingCabinets()
    {
        RingCabinet[] cabinets = _source.Devices.OfType<RingCabinet>().ToArray();
        return cabinets.Length > 0
            ? cabinets
            : _source.RingCabinet is null ? [] : [_source.RingCabinet];
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

    private ResolvedSelection? ResolveGroundingPoint(SelectionReference reference)
    {
        IReadOnlyList<GroundingPoint> groundingPoints = _source.GroundingPoints.Count > 0
            ? _source.GroundingPoints
            : _source.Document?.GroundingPoints ?? [];
        GroundingPoint? groundingPoint = groundingPoints
            .SingleOrDefault(candidate => candidate.GroundingPointId == reference.ObjectId);
        return groundingPoint is null
            ? null
            : new ResolvedSelection
            {
                Reference = reference,
                GroundingPoint = groundingPoint
            };
    }

    private ResolvedSelection? ResolveWorkScope(SelectionReference reference)
    {
        IReadOnlyList<WorkScope> workScopes = _source.WorkScopes.Count > 0
            ? _source.WorkScopes
            : _source.Document?.WorkScopes ?? [];
        WorkScope? workScope = workScopes
            .SingleOrDefault(candidate => candidate.WorkScopeId == reference.ObjectId);
        return workScope is null
            ? null
            : new ResolvedSelection
            {
                Reference = reference,
                WorkScope = workScope
            };
    }

    private ResolvedSelection? ResolveTerminal(SelectionReference reference)
    {
        IReadOnlyList<Terminal> terminals = _source.Terminals.Count > 0
            ? _source.Terminals
            : _source.Document?.Terminals ?? [];
        Terminal? terminal = terminals
            .SingleOrDefault(candidate => candidate.Id == reference.ObjectId);
        return terminal is null
            ? null
            : new ResolvedSelection
            {
                Reference = reference,
                Terminal = terminal
            };
    }
}
