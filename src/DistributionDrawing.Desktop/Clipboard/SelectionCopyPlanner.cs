using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Desktop.Clipboard;

internal sealed class SelectionCopyPlanner
{
    public CopyPlanResult Create(ProjectRuntimeSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        DrawingDocument document = session.PersistenceSession.Domain;
        SelectionSet selection = session.SelectionManager.SelectionSet;
        var poleIds = new HashSet<Guid>();
        var attachmentIds = new HashSet<Guid>();
        var cabinetIds = new HashSet<Guid>();
        var requestedConnectionIds = new HashSet<Guid>();
        var requestedCableIds = new HashSet<Guid>();
        var roots = new List<SelectionReference>();
        var warnings = new List<string>();

        foreach (SelectionReference reference in selection.SelectedReferences)
        {
            if (!IncludeReference(
                    reference,
                    document,
                    poleIds,
                    attachmentIds,
                    cabinetIds,
                    requestedConnectionIds,
                    requestedCableIds,
                    roots))
            {
                warnings.Add($"已忽略当前不支持复制的对象：{reference.Kind}。");
            }
        }

        foreach (Guid attachmentId in attachmentIds)
        {
            PoleAttachment attachment = document.PoleAttachments.Single(item =>
                item.AttachmentId == attachmentId);
            if (poleIds.Add(attachment.PoleId))
            {
                roots.Add(new SelectionReference(SelectionTargetKind.Device, attachment.PoleId));
            }
        }

        PoleSnapshot[] poles = poleIds.OrderBy(id => id)
            .Select(id => CapturePole(document, session.Layout, id))
            .ToArray();
        RingCabinetSnapshot[] cabinets = cabinetIds.OrderBy(id => id)
            .Select(id => CaptureCabinet(document, session.Layout, id))
            .ToArray();
        PoleSwitchAttachmentSnapshot[] switches = attachmentIds.OrderBy(id => id)
            .Select(id => CaptureSwitch(document, session.Layout, id))
            .OfType<PoleSwitchAttachmentSnapshot>()
            .ToArray();
        CableTerminationAttachmentSnapshot[] terminations = attachmentIds.OrderBy(id => id)
            .Select(id => CaptureCableTermination(document, session.Layout, id))
            .OfType<CableTerminationAttachmentSnapshot>()
            .ToArray();

        var includedTerminalIds = new HashSet<Guid>(
            poles.SelectMany(item => item.Terminals).Select(item => item.Id));
        includedTerminalIds.UnionWith(cabinets
            .SelectMany(item => item.Definition.Intervals)
            .SelectMany(item => item.Switches.SelectMany(switchItem =>
                    new[] { switchItem.FirstTerminalId, switchItem.SecondTerminalId })
                .Concat(item.CableTerminalId is Guid cableTerminalId
                    ? [cableTerminalId]
                    : [])));
        includedTerminalIds.UnionWith(switches.SelectMany(item =>
            new[] { item.FirstTerminal.Id, item.SecondTerminal.Id }));
        includedTerminalIds.UnionWith(terminations.SelectMany(item =>
            new[] { item.CableSideTerminal.Id, item.OverheadSideTerminal.Id }));

        OverheadLineSnapshot[] overheadLines = requestedConnectionIds.OrderBy(id => id)
            .Select(id => CaptureOverheadLine(
                document,
                session.Layout,
                id,
                poleIds,
                includedTerminalIds,
                warnings))
            .OfType<OverheadLineSnapshot>()
            .ToArray();
        CableSegmentSnapshot[] cableSegments = requestedCableIds.OrderBy(id => id)
            .Select(id => CaptureCableSegment(
                document,
                session.Layout,
                id,
                includedTerminalIds,
                warnings))
            .OfType<CableSegmentSnapshot>()
            .ToArray();

        if (poles.Length == 0 && cabinets.Length == 0)
        {
            return new CopyPlanResult(null, warnings.Count > 0
                ? warnings
                : ["当前选择中没有可复制的完整业务对象。"]);
        }

        SelectionReference? primary = selection.PrimarySelection is not null &&
                                      roots.Any(item => SameIdentity(item, selection.PrimarySelection))
            ? selection.PrimarySelection
            : roots.LastOrDefault();
        return new CopyPlanResult(
            new ClipboardDrawingFragment(
                primary,
                Distinct(roots),
                poles,
                switches,
                terminations,
                cabinets,
                overheadLines,
                cableSegments),
            Array.AsReadOnly(warnings.ToArray()));
    }

    private static bool IncludeReference(
        SelectionReference reference,
        DrawingDocument document,
        ISet<Guid> poleIds,
        ISet<Guid> attachmentIds,
        ISet<Guid> cabinetIds,
        ISet<Guid> connectionIds,
        ISet<Guid> cableIds,
        ICollection<SelectionReference> roots)
    {
        switch (reference.Kind)
        {
            case SelectionTargetKind.Device:
                Device? device = document.Devices.SingleOrDefault(item => item.Id == reference.ObjectId);
                switch (device)
                {
                    case Pole:
                        poleIds.Add(device.Id);
                        roots.Add(reference);
                        return true;
                    case SwitchDevice poleSwitch
                        when poleSwitch.InstallationType == SwitchInstallationType.Pole:
                        PoleAttachment switchAttachment = document.PoleAttachments.Single(item =>
                            item.AttachedDeviceId == poleSwitch.Id);
                        attachmentIds.Add(switchAttachment.AttachmentId);
                        roots.Add(reference);
                        return true;
                    case SwitchDevice cabinetSwitch
                        when cabinetSwitch.ParentId is Guid intervalId:
                        RingCabinet parentCabinet = document.Devices.OfType<RingCabinet>().Single(item =>
                            item.Intervals.Any(interval => interval.IntervalId == intervalId));
                        cabinetIds.Add(parentCabinet.Id);
                        roots.Add(reference);
                        roots.Add(new SelectionReference(SelectionTargetKind.RingCabinet, parentCabinet.Id));
                        return true;
                    case RingCabinet cabinet:
                        cabinetIds.Add(cabinet.Id);
                        roots.Add(new SelectionReference(SelectionTargetKind.RingCabinet, cabinet.Id));
                        return true;
                    case CableTermination termination:
                        PoleAttachment terminationAttachment = document.PoleAttachments.Single(item =>
                            item.AttachedDeviceId == termination.Id);
                        attachmentIds.Add(terminationAttachment.AttachmentId);
                        roots.Add(reference);
                        return true;
                    default:
                        return false;
                }

            case SelectionTargetKind.RingCabinet:
                if (document.Devices.OfType<RingCabinet>().Any(item => item.Id == reference.ObjectId))
                {
                    cabinetIds.Add(reference.ObjectId);
                    roots.Add(reference);
                    return true;
                }
                return false;

            case SelectionTargetKind.RingCabinetInterval:
                RingCabinet? intervalCabinet = document.Devices.OfType<RingCabinet>()
                    .SingleOrDefault(item => item.Intervals.Any(interval =>
                        interval.IntervalId == reference.ObjectId));
                if (intervalCabinet is null) return false;
                cabinetIds.Add(intervalCabinet.Id);
                roots.Add(reference);
                roots.Add(new SelectionReference(SelectionTargetKind.RingCabinet, intervalCabinet.Id));
                return true;

            case SelectionTargetKind.PoleAttachment:
                if (document.PoleAttachments.Any(item => item.AttachmentId == reference.ObjectId))
                {
                    attachmentIds.Add(reference.ObjectId);
                    roots.Add(reference);
                    return true;
                }
                return false;

            case SelectionTargetKind.Connection:
                if (document.OverheadLines.Any(item => item.ConnectionId == reference.ObjectId))
                {
                    connectionIds.Add(reference.ObjectId);
                    roots.Add(reference);
                    return true;
                }
                return false;

            case SelectionTargetKind.CableSegment:
                if (document.CableSegments.Any(item => item.Id == reference.ObjectId))
                {
                    cableIds.Add(reference.ObjectId);
                    roots.Add(reference);
                    return true;
                }
                return false;

            default:
                return false;
        }
    }

    private static PoleSnapshot CapturePole(
        DrawingDocument document,
        RuntimeLayoutDocument layout,
        Guid poleId)
    {
        Pole pole = document.Devices.OfType<Pole>().Single(item => item.Id == poleId);
        PoleLayout poleLayout = layout.DrawingLayout.Poles[poleId];
        return new PoleSnapshot(
            pole.Id,
            pole.PoleNumber,
            pole.DisplayName,
            pole.PoleType,
            Array.AsReadOnly(pole.OverheadAnchorTerminalIds.OrderBy(id => id).ToArray()),
            Array.AsReadOnly(document.ElectricalNodes
                .Where(item => item.OwnerType == TopologyOwnerType.Device && item.OwnerId == poleId)
                .Select(CaptureNode)
                .ToArray()),
            Array.AsReadOnly(document.Terminals
                .Where(item => item.OwnerType == TopologyOwnerType.Device && item.OwnerId == poleId)
                .Select(CaptureTerminal)
                .ToArray()),
            Clone(poleLayout));
    }

    private static RingCabinetSnapshot CaptureCabinet(
        DrawingDocument document,
        RuntimeLayoutDocument layout,
        Guid cabinetId)
    {
        RingCabinet cabinet = document.Devices.OfType<RingCabinet>().Single(item => item.Id == cabinetId);
        return new RingCabinetSnapshot(
            Clone(cabinet.CaptureRestoreDefinition()),
            Clone(layout.RingCabinetLayouts[cabinetId]));
    }

    private static PoleSwitchAttachmentSnapshot? CaptureSwitch(
        DrawingDocument document,
        RuntimeLayoutDocument layout,
        Guid attachmentId)
    {
        PoleAttachment attachment = document.PoleAttachments.Single(item =>
            item.AttachmentId == attachmentId);
        if (document.Devices.Single(item => item.Id == attachment.AttachedDeviceId) is not SwitchDevice device)
        {
            return null;
        }

        Terminal first = document.Terminals.Single(item => item.Id == device.TerminalIds[0]);
        Terminal second = document.Terminals.Single(item => item.Id == device.TerminalIds[1]);
        ElectricalState? nodeState = second.ElectricalNodeId is Guid nodeId
            ? document.ElectricalNodes.Single(item => item.Id == nodeId).ElectricalState
            : null;
        return new PoleSwitchAttachmentSnapshot(
            attachment.AttachmentId,
            attachment.PoleId,
            device.Id,
            device.SwitchKind,
            device.SwitchState ?? SwitchState.Open,
            device.DisplayName ?? "Pole switch",
            device.VoltageLevel ?? "10kV",
            device.DispatchNumber,
            CaptureTerminal(first),
            CaptureTerminal(second),
            nodeState,
            Clone(layout.DrawingLayout.Attachments[attachmentId]));
    }

    private static CableTerminationAttachmentSnapshot? CaptureCableTermination(
        DrawingDocument document,
        RuntimeLayoutDocument layout,
        Guid attachmentId)
    {
        PoleAttachment attachment = document.PoleAttachments.Single(item =>
            item.AttachmentId == attachmentId);
        if (document.Devices.Single(item => item.Id == attachment.AttachedDeviceId) is not CableTermination device)
        {
            return null;
        }

        ElectricalNode node = document.ElectricalNodes.Single(item => item.Id == device.InternalNodeId);
        return new CableTerminationAttachmentSnapshot(
            attachment.AttachmentId,
            attachment.PoleId,
            device.Id,
            device.DisplayName,
            device.VoltageLevel ?? "10kV",
            device.InternalNodeId,
            CaptureNode(node),
            CaptureTerminal(document.Terminals.Single(item => item.Id == device.CableSideTerminalId)),
            CaptureTerminal(document.Terminals.Single(item => item.Id == device.OverheadSideTerminalId)),
            Clone(layout.DrawingLayout.Attachments[attachmentId]));
    }

    private static OverheadLineSnapshot? CaptureOverheadLine(
        DrawingDocument document,
        RuntimeLayoutDocument layout,
        Guid connectionId,
        IReadOnlySet<Guid> poleIds,
        IReadOnlySet<Guid> terminalIds,
        ICollection<string> warnings)
    {
        Connection connection = document.Connections.Single(item => item.Id == connectionId);
        OverheadLine line = document.OverheadLines.Single(item => item.ConnectionId == connectionId);
        if (!terminalIds.Contains(connection.StartTerminalId) ||
            !terminalIds.Contains(connection.EndTerminalId) ||
            line.SupportPoleIds.Any(id => !poleIds.Contains(id)))
        {
            warnings.Add($"架空线“{connection.DisplayName}”跨越复制边界，已忽略。");
            return null;
        }

        return new OverheadLineSnapshot(
            Clone(connection),
            Clone(line),
            Clone(layout.DrawingLayout.OverheadLines[connectionId]));
    }

    private static CableSegmentSnapshot? CaptureCableSegment(
        DrawingDocument document,
        RuntimeLayoutDocument layout,
        Guid cableId,
        IReadOnlySet<Guid> terminalIds,
        ICollection<string> warnings)
    {
        CableSegment segment = document.CableSegments.Single(item => item.Id == cableId);
        Connection connection = document.Connections.Single(item => item.Id == segment.ConnectionId);
        if (!terminalIds.Contains(connection.StartTerminalId) ||
            !terminalIds.Contains(connection.EndTerminalId))
        {
            warnings.Add($"电缆“{segment.Name}”跨越复制边界，已忽略。");
            return null;
        }

        layout.CableRouteGuides.TryGetValue(segment.Id, out CableRouteGuide? guide);
        return new CableSegmentSnapshot(
            Clone(connection),
            new CableSegment(
                segment.Id,
                segment.Name,
                segment.CableType,
                segment.Length,
                segment.VoltageLevel,
                segment.ConnectionId,
                segment.StartTerminalId,
                segment.EndTerminalId),
            guide is null ? null : new CableRouteGuide(guide.CableSegmentId, guide.HorizontalYMillimeters));
    }

    private static ElectricalNodeSnapshot CaptureNode(ElectricalNode node) => new(
        node.Id,
        node.Type,
        node.OwnerType,
        node.OwnerId,
        node.ElectricalState);

    private static TerminalSnapshot CaptureTerminal(Terminal terminal) => new(
        terminal.Id,
        terminal.OwnerType,
        terminal.OwnerId,
        terminal.Role,
        terminal.VoltageLevel,
        terminal.IsExternal,
        terminal.AllowsMultipleConnections,
        terminal.ElectricalNodeId,
        Array.AsReadOnly(terminal.AllowedConnectionTypes.OrderBy(item => item).ToArray()));

    private static Connection Clone(Connection value) => new(
        value.Id,
        value.Type,
        value.StartTerminalId,
        value.EndTerminalId,
        value.DisplayName,
        value.VoltageLevel);

    private static OverheadLine Clone(OverheadLine value) => new(
        value.ConnectionId,
        value.LineModel,
        value.SupportPoleIds.ToArray(),
        value.IsContinued,
        value.ContinuationTerminalId,
        value.ContinuationState,
        value.ContinuationDescription,
        value.LengthMeters);

    private static PoleLayout Clone(PoleLayout value) => new(
        value.PoleId,
        value.Position,
        value.WidthMillimeters,
        value.HeightMillimeters,
        value.LabelOffset);

    private static AttachmentLayout Clone(AttachmentLayout value) => new(
        value.AttachmentId,
        value.Offset,
        value.WidthMillimeters,
        value.HeightMillimeters,
        value.LabelOffset,
        value.RotationQuarterTurns);

    private static OverheadLineLayout Clone(OverheadLineLayout value) => new(
        value.ConnectionId,
        value.Start,
        value.End,
        value.IsContinued,
        value.ContinuationOffset);

    private static RingCabinetRestoreDefinition Clone(RingCabinetRestoreDefinition value) => new(
        value.CabinetId,
        value.DisplayName,
        value.MainBusNodeId,
        Array.AsReadOnly(value.Intervals.Select(interval => new RingCabinetIntervalRestoreDefinition(
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
            interval.CableTerminalId,
            interval.SwitchAssemblyId,
            Array.AsReadOnly(interval.Switches.Select(item => item with { }).ToArray()))).ToArray()),
        value.LineName);

    private static RingCabinetLayout Clone(RingCabinetLayout value) => new(
        value.CabinetId,
        value.Position,
        value.WidthMillimeters,
        value.HeightMillimeters,
        value.MainBusYMillimeters,
        value.IntervalLayouts.Values.Select(interval => new RingCabinetIntervalLayout(
            interval.IntervalId,
            interval.RelativePosition,
            interval.WidthMillimeters,
            interval.HeightMillimeters,
            interval.SequenceLabelOffset,
            interval.NameLabelOffset,
            interval.SwitchLayouts.Values.Select(item => new RingCabinetSwitchLayout(
                item.SwitchDeviceId,
                item.RelativePosition,
                item.WidthMillimeters,
                item.HeightMillimeters,
                item.LabelOffset)),
            interval.PTSymbolPosition)),
        value.LabelOffset);

    private static IReadOnlyList<SelectionReference> Distinct(
        IEnumerable<SelectionReference> values) => Array.AsReadOnly(values
        .GroupBy(item => (item.Kind, item.ObjectId))
        .Select(group => group.First())
        .ToArray());

    private static bool SameIdentity(SelectionReference first, SelectionReference second) =>
        first.Kind == second.Kind && first.ObjectId == second.ObjectId;
}
