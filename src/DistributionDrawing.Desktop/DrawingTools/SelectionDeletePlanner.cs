using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Desktop.CableConnection;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Connections;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Desktop.DrawingTools;

public sealed class SelectionDeletePlanner
{
    private readonly DeviceCommandFactory _deviceCommandFactory = new();
    private readonly OverheadLineCommandFactory _overheadLineCommandFactory = new();

    public ICommand Create(ProjectRuntimeSession session, SelectionSet selection)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.Count == 0) throw new InvalidOperationException("请先选择要删除的对象。");

        DrawingDocument document = session.PersistenceSession.Domain;
        RuntimeLayoutDocument layout = session.Layout;
        HashSet<Guid> poleIds = selection.SelectedReferences
            .Where(item => item.Kind == SelectionTargetKind.Device)
            .Select(item => document.Devices.SingleOrDefault(device => device.Id == item.ObjectId))
            .OfType<Pole>()
            .Select(pole => pole.Id)
            .ToHashSet();
        HashSet<Guid> cabinetIds = selection.SelectedReferences
            .Where(item => item.Kind == SelectionTargetKind.RingCabinet)
            .Select(item => item.ObjectId)
            .ToHashSet();
        HashSet<Guid> attachmentIds = [];
        HashSet<Guid> cableIds = [];
        HashSet<Guid> overheadIds = [];

        foreach (SelectionReference reference in selection.SelectedReferences)
        {
            switch (reference.Kind)
            {
                case SelectionTargetKind.RingCabinet:
                    break;
                case SelectionTargetKind.PoleAttachment:
                    attachmentIds.Add(reference.ObjectId);
                    break;
                case SelectionTargetKind.Device:
                    Device? device = document.Devices.SingleOrDefault(item => item.Id == reference.ObjectId);
                    if (device is SwitchDevice or CableTermination)
                    {
                        if (device is SwitchDevice { ParentId: Guid intervalId } &&
                            document.Devices.OfType<RingCabinet>().Any(cabinet =>
                                cabinet.Intervals.Any(interval => interval.IntervalId == intervalId) &&
                                cabinetIds.Contains(cabinet.Id)))
                        {
                            break;
                        }

                        PoleAttachment? attachment = document.PoleAttachments.SingleOrDefault(item =>
                            item.AttachedDeviceId == reference.ObjectId || item.AttachmentId == reference.ParentId);
                        if (attachment is null) throw new InvalidOperationException("所选安装设备不存在对应的杆塔安装关系。");
                        attachmentIds.Add(attachment.AttachmentId);
                    }
                    else if (device is not Pole)
                    {
                        throw new InvalidOperationException("当前对象不支持删除。");
                    }

                    break;
                case SelectionTargetKind.CableSegment:
                    cableIds.Add(reference.ObjectId);
                    break;
                case SelectionTargetKind.Connection:
                    overheadIds.Add(reference.ObjectId);
                    break;
                case SelectionTargetKind.RingCabinetInterval:
                    if (reference.ParentId is not Guid parentId || !cabinetIds.Contains(parentId))
                    {
                        throw new InvalidOperationException("请选中整个环网柜后再删除其间隔。");
                    }

                    break;
                default:
                    throw new InvalidOperationException("当前对象不支持删除。");
            }
        }

        foreach (PoleAttachment attachment in document.PoleAttachments.Where(item => poleIds.Contains(item.PoleId)))
        {
            attachmentIds.Add(attachment.AttachmentId);
        }

        var commands = new List<ICommand>();
        foreach (Guid cableId in cableIds.OrderBy(id => id))
        {
            CableSegment cable = document.CableSegments.SingleOrDefault(item => item.Id == cableId)
                ?? throw new InvalidOperationException("所选电缆不存在。");
            Connection connection = document.Connections.SingleOrDefault(item => item.Id == cable.ConnectionId)
                ?? throw new InvalidOperationException("所选电缆的连接不存在。");
            commands.Add(new RemoveCableSegmentCommand(document, cable, connection, layout));
        }

        foreach (Guid connectionId in overheadIds.OrderBy(id => id))
        {
            commands.Add(_overheadLineCommandFactory.CreateRemove(document, layout, connectionId));
        }

        foreach (Guid attachmentId in attachmentIds.OrderBy(id => id))
        {
            PoleAttachment attachment = document.PoleAttachments.SingleOrDefault(item => item.AttachmentId == attachmentId)
                ?? throw new InvalidOperationException("所选杆塔安装设备不存在。");
            Device attachedDevice = document.Devices.SingleOrDefault(item => item.Id == attachment.AttachedDeviceId)
                ?? throw new InvalidOperationException("所选杆塔安装设备的设备不存在。");
            commands.Add(attachedDevice switch
            {
                CableTermination => _deviceCommandFactory.CreateRemoveCableTerminationAttachment(
                    document, layout, attachmentId),
                SwitchDevice => _deviceCommandFactory.CreateRemovePoleSwitchAndBypass(
                    document, layout, attachmentId),
                _ => throw new InvalidOperationException("当前杆塔安装设备不支持删除.")
            });
        }

        foreach (Guid cabinetId in cabinetIds.OrderBy(id => id))
        {
            RingCabinet cabinet = document.Devices.OfType<RingCabinet>().SingleOrDefault(item => item.Id == cabinetId)
                ?? throw new InvalidOperationException("所选环网柜不存在。");
            RingCabinetLayout cabinetLayout = layout.RingCabinetLayouts.GetValueOrDefault(cabinetId)
                ?? throw new InvalidOperationException("所选环网柜的布局不存在。");
            commands.Add(new RemoveRingCabinetCommand(document, layout, cabinet, cabinetLayout));
        }

        foreach (Guid poleId in poleIds.OrderBy(id => id))
        {
            Pole pole = document.Devices.OfType<Pole>().SingleOrDefault(item => item.Id == poleId)
                ?? throw new InvalidOperationException("所选杆塔不存在。");
            PoleLayout poleLayout = layout.DrawingLayout.Poles.GetValueOrDefault(poleId)
                ?? throw new InvalidOperationException("所选杆塔的布局不存在。");
            commands.Add(new RemovePoleCommand(document, layout, pole, poleLayout));
        }

        if (commands.Count == 0) throw new InvalidOperationException("当前选择中没有可删除的对象。");
        return new CompositeDeleteCommand(commands);
    }
}

internal sealed class CompositeDeleteCommand : ICommand
{
    private readonly IReadOnlyList<ICommand> _commands;

    public CompositeDeleteCommand(IEnumerable<ICommand> commands)
    {
        _commands = commands?.ToArray() ?? throw new ArgumentNullException(nameof(commands));
        if (_commands.Count == 0) throw new ArgumentException("At least one delete command is required.", nameof(commands));
    }

    public void Execute()
    {
        int executed = 0;
        try
        {
            foreach (ICommand command in _commands)
            {
                command.Execute();
                executed++;
            }
        }
        catch
        {
            foreach (ICommand command in _commands.Take(executed).Reverse()) command.Undo();
            throw;
        }
    }

    public void Undo()
    {
        foreach (ICommand command in _commands.Reverse()) command.Undo();
    }

    public void Redo() => Execute();
}
