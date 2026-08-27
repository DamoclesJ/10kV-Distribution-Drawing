using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class DeviceCommandFactory
{
    private readonly RingCabinetCreationFactory _ringCabinetCreationFactory;
    private readonly RingCabinetLayoutFactory _ringCabinetLayoutFactory;
    private readonly CableTerminationAttachmentCreationFactory
        _cableTerminationAttachmentCreationFactory;
    private readonly PoleSwitchAttachmentCreationFactory _poleSwitchAttachmentCreationFactory;

    public DeviceCommandFactory(
        RingCabinetCreationFactory? ringCabinetCreationFactory = null,
        RingCabinetLayoutFactory? ringCabinetLayoutFactory = null,
        CableTerminationAttachmentCreationFactory?
            cableTerminationAttachmentCreationFactory = null)
    {
        _ringCabinetCreationFactory = ringCabinetCreationFactory ?? new RingCabinetCreationFactory();
        _ringCabinetLayoutFactory = ringCabinetLayoutFactory ?? new RingCabinetLayoutFactory();
        _cableTerminationAttachmentCreationFactory =
            cableTerminationAttachmentCreationFactory ??
            new CableTerminationAttachmentCreationFactory();
        _poleSwitchAttachmentCreationFactory = new PoleSwitchAttachmentCreationFactory();
    }

    public AddPoleCommand CreateAddPole(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        DocumentPoint position)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(runtimeLayout);

        Guid poleId = Guid.NewGuid();
        var pole = new Pole(poleId, NextPoleNumber(document));
        Terminal terminal = pole.CreateOverheadAnchorTerminal(Guid.NewGuid(), allowsMultipleConnections: true);
        return new AddPoleCommand(
            document,
            runtimeLayout,
            pole,
            terminal,
            new PoleLayout(poleId, position));
    }

    public AddRingCabinetCommand CreateAddRingCabinet(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        RingCabinetCreationConfiguration configuration,
        DocumentPoint position)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(runtimeLayout);
        ArgumentNullException.ThrowIfNull(configuration);

        RingCabinet cabinet = _ringCabinetCreationFactory.Create(configuration);
        return new AddRingCabinetCommand(
            document,
            runtimeLayout,
            cabinet,
            _ringCabinetLayoutFactory.Create(cabinet, position));
    }

    public AddRingCabinetCommand CreateAddRingCabinet(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        RingCabinet cabinet,
        RingCabinetLayout layout)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(runtimeLayout);
        ArgumentNullException.ThrowIfNull(cabinet);
        ArgumentNullException.ThrowIfNull(layout);

        return new AddRingCabinetCommand(
            document,
            runtimeLayout,
            cabinet,
            layout);
    }

    public AddCableTerminationAttachmentCommand CreateAddCableTerminationAttachment(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        Guid poleId,
        string? displayName,
        DocumentPoint attachmentOffset)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(runtimeLayout);

        CableTerminationAttachmentCreation creation =
            _cableTerminationAttachmentCreationFactory.Create(
                poleId,
                displayName,
                attachmentOffset);
        return new AddCableTerminationAttachmentCommand(
            document,
            runtimeLayout,
            creation);
    }

    public AddPoleSwitchAttachmentCommand CreateAddPoleSwitchAttachment(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        Guid poleId,
        SwitchKind switchKind,
        DocumentPoint attachmentOffset,
        Guid? controlledConnectionId = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(runtimeLayout);
        if (document.Devices.SingleOrDefault(device => device.Id == poleId) is not Pole pole)
        {
            throw new InvalidOperationException("请选择一个杆塔后再添加柱上开关。");
        }

        PoleSwitchAttachmentCreation creation = _poleSwitchAttachmentCreationFactory.Create(
            poleId,
            switchKind,
            attachmentOffset);
        IReadOnlyList<OverheadConnectionEndpointTransition> transitions =
            CreatePoleSwitchOverheadTransitions(
                document,
                runtimeLayout,
                pole,
                creation,
                controlledConnectionId);
        return new AddPoleSwitchAttachmentCommand(
            document,
            runtimeLayout,
            creation,
            transitions);
    }

    private static IReadOnlyList<OverheadConnectionEndpointTransition>
        CreatePoleSwitchOverheadTransitions(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        Pole pole,
        PoleSwitchAttachmentCreation creation,
        Guid? controlledConnectionId = null)
    {
        HashSet<Guid> poleTerminalIds = pole.OverheadAnchorTerminalIds.ToHashSet();
        Connection[] attachedConnections = document.Connections
            .Where(connection => connection.Type == ConnectionType.OverheadLine &&
                (poleTerminalIds.Contains(connection.StartTerminalId) ||
                 poleTerminalIds.Contains(connection.EndTerminalId)))
            .OrderBy(connection => connection.Id)
            .ToArray();
        // A switch may be installed on an already branched pole, but the
        // existing branches are intentionally left on the pole junction.
        // Automatic endpoint migration is only unambiguous for one or two
        // through-line connections.
        if (attachedConnections.Length > 2 && controlledConnectionId is null)
        {
            throw new InvalidOperationException("请选择柱上开关要控制的架空线路。");
        }

        if (controlledConnectionId is Guid selectedId &&
            !attachedConnections.Any(connection => connection.Id == selectedId))
        {
            throw new InvalidOperationException("所选架空线路不属于当前杆塔。");
        }

        if (attachedConnections.Length == 0)
        {
            return [];
        }

        PoleLayout poleLayout = runtimeLayout.DrawingLayout.Poles.TryGetValue(
            pole.Id,
            out PoleLayout? foundPoleLayout)
            ? foundPoleLayout
            : throw new InvalidOperationException("所选杆塔缺少布局信息。");
        PoleAttachmentGeometry switchGeometry = PoleProfessionalGeometry.GetAttachmentGeometry(
            poleLayout,
            creation.Layout,
            SymbolLibrary.ResolveAttachmentKind(creation.SwitchDevice));
        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            runtimeLayout.DrawingLayout,
            runtimeLayout.RingCabinetLayouts,
            document.Connections,
            document.CableSegments);
        var connectionPositions = attachedConnections.Select(connection =>
        {
            Guid poleTerminalId = poleTerminalIds.Contains(connection.StartTerminalId)
                ? connection.StartTerminalId
                : connection.EndTerminalId;
            Guid otherTerminalId = connection.StartTerminalId == poleTerminalId
                ? connection.EndTerminalId
                : connection.StartTerminalId;
            if (!anchors.TryGet(otherTerminalId, out TerminalAnchor otherAnchor))
            {
                throw new InvalidOperationException("架空线另一端缺少可用的端子位置。");
            }

            return (Connection: connection, PoleTerminalId: poleTerminalId,
                OtherPosition: otherAnchor.Position);
        }).ToArray();

        if (controlledConnectionId is Guid chosenConnectionId)
        {
            (Connection Connection, Guid PoleTerminalId, DocumentPoint OtherPosition) chosen =
                connectionPositions.Single(item => item.Connection.Id == chosenConnectionId);
            Connection before = chosen.Connection;
            Guid start = before.StartTerminalId == chosen.PoleTerminalId
                ? creation.SecondTerminal.Id
                : before.StartTerminalId;
            Guid end = before.EndTerminalId == chosen.PoleTerminalId
                ? creation.SecondTerminal.Id
                : before.EndTerminalId;
            OverheadLine line = document.OverheadLines.Single(item => item.ConnectionId == before.Id);
            return [new OverheadConnectionEndpointTransition(
                before,
                new Connection(
                    before.Id,
                    before.Type,
                    start,
                    end,
                    before.DisplayName,
                    before.VoltageLevel),
                line)];
        }

        Guid[] targetTerminalIds = connectionPositions.Length == 1
            ? [NearestTerminal(
                connectionPositions[0].OtherPosition,
                creation,
                switchGeometry)]
            : AssignTwoTerminals(connectionPositions, creation, switchGeometry);

        return connectionPositions.Select((item, index) =>
        {
            Connection before = item.Connection;
            Guid targetTerminalId = targetTerminalIds[index];
            var after = new Connection(
                before.Id,
                before.Type,
                before.StartTerminalId == item.PoleTerminalId
                    ? targetTerminalId
                    : before.StartTerminalId,
                before.EndTerminalId == item.PoleTerminalId
                    ? targetTerminalId
                    : before.EndTerminalId,
                before.DisplayName,
                before.VoltageLevel);
            OverheadLine overheadLine = document.OverheadLines.Single(line =>
                line.ConnectionId == before.Id);
            if (overheadLine.ContinuationTerminalId == item.PoleTerminalId)
            {
                throw new InvalidOperationException(
                    "续接架空线暂不能自动迁移到新柱上开关端子。");
            }

            return new OverheadConnectionEndpointTransition(before, after, overheadLine);
        }).ToArray();
    }

    private static Guid NearestTerminal(
        DocumentPoint otherPosition,
        PoleSwitchAttachmentCreation creation,
        PoleAttachmentGeometry geometry) =>
        Distance(otherPosition, geometry.FirstTerminal) <=
        Distance(otherPosition, geometry.SecondTerminal)
            ? creation.FirstTerminal.Id
            : creation.SecondTerminal.Id;

    private static Guid[] AssignTwoTerminals(
        (Connection Connection, Guid PoleTerminalId, DocumentPoint OtherPosition)[] connections,
        PoleSwitchAttachmentCreation creation,
        PoleAttachmentGeometry geometry)
    {
        double direct = Distance(connections[0].OtherPosition, geometry.FirstTerminal) +
            Distance(connections[1].OtherPosition, geometry.SecondTerminal);
        double crossed = Distance(connections[0].OtherPosition, geometry.SecondTerminal) +
            Distance(connections[1].OtherPosition, geometry.FirstTerminal);
        return direct <= crossed
            ? [creation.FirstTerminal.Id, creation.SecondTerminal.Id]
            : [creation.SecondTerminal.Id, creation.FirstTerminal.Id];
    }

    private static double Distance(DocumentPoint first, DocumentPoint second)
    {
        double deltaX = first.XMillimeters - second.XMillimeters;
        double deltaY = first.YMillimeters - second.YMillimeters;
        return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }

    public RemoveCableTerminationAttachmentCommand CreateRemoveCableTerminationAttachment(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        Guid attachmentId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(runtimeLayout);

        PoleAttachment attachment = document.PoleAttachments.SingleOrDefault(candidate =>
                candidate.AttachmentId == attachmentId)
            ?? throw new InvalidOperationException(
                $"Pole attachment '{attachmentId}' does not exist.");
        CableTermination cableTermination = document.Devices.SingleOrDefault(candidate =>
                candidate.Id == attachment.AttachedDeviceId) as CableTermination
            ?? throw new InvalidOperationException(
                $"Attachment '{attachmentId}' does not reference a cable termination.");
        ElectricalNode internalNode = document.ElectricalNodes.SingleOrDefault(candidate =>
                candidate.Id == cableTermination.InternalNodeId)
            ?? throw new InvalidOperationException(
                $"Cable termination '{cableTermination.Id}' internal node is missing.");
        Terminal cableSideTerminal = document.Terminals.SingleOrDefault(candidate =>
                candidate.Id == cableTermination.CableSideTerminalId)
            ?? throw new InvalidOperationException(
                $"Cable termination '{cableTermination.Id}' cable-side terminal is missing.");
        Terminal overheadSideTerminal = document.Terminals.SingleOrDefault(candidate =>
                candidate.Id == cableTermination.OverheadSideTerminalId)
            ?? throw new InvalidOperationException(
                $"Cable termination '{cableTermination.Id}' overhead-side terminal is missing.");
        AttachmentLayout layout = runtimeLayout.DrawingLayout.Attachments[attachmentId];

        return new RemoveCableTerminationAttachmentCommand(
            document,
            runtimeLayout,
            new CableTerminationAttachmentCreation(
                cableTermination,
                internalNode,
                cableSideTerminal,
                overheadSideTerminal,
                attachment,
                layout));
    }

    public MoveAttachmentCommand CreateMoveAttachment(
        RuntimeLayoutDocument runtimeLayout,
        Guid attachmentId,
        DocumentPoint offset)
    {
        ArgumentNullException.ThrowIfNull(runtimeLayout);

        AttachmentLayout current = runtimeLayout.DrawingLayout.Attachments
            .GetValueOrDefault(attachmentId)
            ?? throw new InvalidOperationException(
                $"No layout exists for attachment '{attachmentId}'.");
        return new MoveAttachmentCommand(
            runtimeLayout.DrawingLayout,
            attachmentId,
            current.Offset,
            offset);
    }

    public RenameCableTerminationCommand CreateRenameCableTermination(
        CableTermination cableTermination,
        string? displayName)
    {
        ArgumentNullException.ThrowIfNull(cableTermination);

        return new RenameCableTerminationCommand(
            cableTermination,
            cableTermination.DisplayName,
            displayName);
    }

    public ChangeAttachmentLayoutCommand CreateChangeAttachmentLayout(
        RuntimeLayoutDocument runtimeLayout,
        Guid attachmentId,
        double widthMillimeters,
        double heightMillimeters,
        DocumentPoint labelOffset)
    {
        ArgumentNullException.ThrowIfNull(runtimeLayout);

        AttachmentLayout before = runtimeLayout.DrawingLayout.Attachments
            .GetValueOrDefault(attachmentId)
            ?? throw new InvalidOperationException(
                $"No layout exists for attachment '{attachmentId}'.");
        AttachmentLayout after = before
            .Resize(widthMillimeters, heightMillimeters)
            .WithLabelOffset(labelOffset);
        return new ChangeAttachmentLayoutCommand(
            runtimeLayout.DrawingLayout,
            before,
            after);
    }

    public RemovePoleSwitchAttachmentCommand CreateRemovePoleSwitchAttachment(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        Guid attachmentId)
    {
        PoleAttachment attachment = document.PoleAttachments.SingleOrDefault(item =>
                item.AttachmentId == attachmentId)
            ?? throw new InvalidOperationException($"Pole attachment '{attachmentId}' does not exist.");
        SwitchDevice switchDevice = document.Devices.SingleOrDefault(item =>
                item.Id == attachment.AttachedDeviceId) as SwitchDevice
            ?? throw new InvalidOperationException("所选附着设备不是柱上开关。");
        Terminal first = document.Terminals.Single(item => item.Id == switchDevice.TerminalIds[0]);
        Terminal second = document.Terminals.Single(item => item.Id == switchDevice.TerminalIds[1]);
        AttachmentLayout layout = runtimeLayout.DrawingLayout.Attachments[attachmentId];
        return new RemovePoleSwitchAttachmentCommand(
            document,
            runtimeLayout,
            new PoleSwitchAttachmentCreation(switchDevice, first, second, attachment, layout));
    }

    public RemovePoleSwitchAndBypassCommand CreateRemovePoleSwitchAndBypass(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        Guid attachmentId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(runtimeLayout);
        PoleAttachment attachment = document.PoleAttachments.SingleOrDefault(item =>
                item.AttachmentId == attachmentId)
            ?? throw new InvalidOperationException("所选杆塔安装设备不存在。");
        SwitchDevice switchDevice = document.Devices.SingleOrDefault(item =>
                item.Id == attachment.AttachedDeviceId) as SwitchDevice
            ?? throw new InvalidOperationException("所选附着设备不是柱上开关。");
        Terminal first = document.Terminals.Single(item => item.Id == switchDevice.TerminalIds[0]);
        Terminal second = document.Terminals.Single(item => item.Id == switchDevice.TerminalIds[1]);
        AttachmentLayout layout = runtimeLayout.DrawingLayout.Attachments[attachmentId];
        Pole pole = document.Devices.OfType<Pole>().Single(item => item.Id == attachment.PoleId);
        Guid[] switchTerminalIds = [first.Id, second.Id];
        Guid? poleTerminalId = pole.OverheadAnchorTerminalIds
            .Select(id => document.Terminals.SingleOrDefault(item => item.Id == id))
            .Where(item => item?.ElectricalNodeId == first.ElectricalNodeId)
            .Select(item => item!.Id)
            .FirstOrDefault();
        if (poleTerminalId is null)
        {
            throw new InvalidOperationException("柱上开关缺少对应的杆塔汇流端子。");
        }

        Connection[] connections = document.Connections
            .Where(item => switchTerminalIds.Any(item.UsesTerminal))
            .ToArray();
        if (connections.Any(item => item.UsesTerminal(first.Id) && item.UsesTerminal(second.Id)))
        {
            throw new InvalidOperationException("柱上开关连接状态不一致，不能旁路删除。");
        }

        var transitions = connections.Select(connection =>
        {
            OverheadLine line = document.OverheadLines.SingleOrDefault(item =>
                    item.ConnectionId == connection.Id)
                ?? throw new InvalidOperationException("柱上开关关联的架空线明细缺失。");
            Guid start = switchTerminalIds.Contains(connection.StartTerminalId)
                ? poleTerminalId.Value
                : connection.StartTerminalId;
            Guid end = switchTerminalIds.Contains(connection.EndTerminalId)
                ? poleTerminalId.Value
                : connection.EndTerminalId;
            return new OverheadConnectionEndpointTransition(
                connection,
                new Connection(
                    connection.Id,
                    connection.Type,
                    start,
                    end,
                    connection.DisplayName,
                    connection.VoltageLevel),
                line);
        }).ToArray();

        return new RemovePoleSwitchAndBypassCommand(
            document,
            runtimeLayout,
            new PoleSwitchAttachmentCreation(switchDevice, first, second, attachment, layout),
            transitions);
    }

    public ICommand CreateRemove(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        Guid deviceId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(runtimeLayout);

        Device device = document.Devices.SingleOrDefault(candidate => candidate.Id == deviceId)
            ?? throw new InvalidOperationException($"Device '{deviceId}' does not exist.");
        return device switch
        {
            Pole pole => new RemovePoleCommand(
                document,
                runtimeLayout,
                pole,
                runtimeLayout.DrawingLayout.Poles[pole.Id]),
            RingCabinet cabinet => new RemoveRingCabinetCommand(
                document,
                runtimeLayout,
                cabinet,
                runtimeLayout.RingCabinetLayouts[cabinet.Id]),
            _ => throw new InvalidOperationException(
                "Only Pole and RingCabinet deletion is supported in this phase.")
        };
    }

    private static string NextPoleNumber(DrawingDocument document)
    {
        int sequence = document.Devices.OfType<Pole>().Count() + 1;
        string candidate;
        do
        {
            candidate = $"P-{sequence:00}";
            sequence++;
        }
        while (document.Devices.OfType<Pole>().Any(pole => pole.PoleNumber == candidate));

        return candidate;
    }
}
