using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.WorkTickets;

public static class WorkTicketRangeSetup
{
    public static string SlotName(int index)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        string name = "";
        for (int value = index; value >= 0; value = value / 26 - 1)
            name = (char)('A' + value % 26) + name;
        return name;
    }

    public static IReadOnlyList<BoundarySide> AvailableSides(SwitchDevice device) =>
        device.SwitchKind == SwitchKind.GroundSwitch ? [] : device.InstallationType switch
        {
            SwitchInstallationType.CabinetInterval => [BoundarySide.Bus, BoundarySide.Line],
            SwitchInstallationType.Pole => [BoundarySide.SmallerNumber, BoundarySide.LargerNumber],
            _ => []
        };

    public static bool TryResolve(DrawingDocument drawing, Guid deviceId, BoundarySide side,
        out IsolationBoundary? boundary, out string issue)
    {
        boundary = null;
        SwitchDevice? device = drawing.Devices.OfType<SwitchDevice>()
            .SingleOrDefault(item => item.Id == deviceId);
        if (device is null || !AvailableSides(device).Contains(side))
        {
            issue = "待确认 / 无法确定电气侧";
            return false;
        }

        if (device.InstallationType == SwitchInstallationType.CabinetInterval)
        {
            Guid terminalId = side == BoundarySide.Bus ? device.FirstTerminalId : device.SecondTerminalId;
            RingCabinetInterval? interval = drawing.Devices.OfType<RingCabinet>()
                .SelectMany(cabinet => cabinet.Intervals)
                .SingleOrDefault(item => item.SwitchDevices.Any(sw => sw.Id == deviceId));
            Guid[] connections = side == BoundarySide.Line && interval?.CableTerminalId is Guid cableTerminal
                ? drawing.Connections.Where(item => item.UsesTerminal(cableTerminal))
                    .Select(item => item.Id).ToArray() : [];
            var candidate = new IsolationBoundary(deviceId, side, terminalId,
                connections.Length == 1 ? connections[0] : null);
            issue = new FirstKindRulePack().BoundaryIssue(drawing, candidate) ?? "";
            if (issue.Length != 0) return false;
            boundary = candidate;
            return true;
        }

        PoleAttachment? attachment = drawing.PoleAttachments.SingleOrDefault(item => item.AttachedDeviceId == deviceId);
        Pole? pole = attachment is null ? null : drawing.Devices.OfType<Pole>()
            .SingleOrDefault(item => item.Id == attachment.PoleId);
        if (pole is null)
        {
            issue = "待确认 / 无法确定电气侧";
            return false;
        }

        var mapped = new Dictionary<BoundarySide, Guid>();
        foreach (Guid terminalId in device.TerminalIds)
        foreach (Connection connection in drawing.Connections.Where(item => item.UsesTerminal(terminalId)))
        {
            OverheadLine? line = drawing.OverheadLines.SingleOrDefault(item => item.ConnectionId == connection.Id);
            if (line is null) continue;
            int index = line.SupportPoleIds.ToList().IndexOf(pole.Id);
            if (index < 0) continue;
            Guid? adjacentId = index == 0 && line.SupportPoleIds.Count > 1
                ? line.SupportPoleIds[1]
                : index == line.SupportPoleIds.Count - 1 && index > 0
                    ? line.SupportPoleIds[index - 1] : null;
            Pole? adjacent = adjacentId is Guid id ? drawing.Devices.OfType<Pole>()
                .SingleOrDefault(item => item.Id == id) : null;
            if (adjacent is null) continue;
            PoleNumberOrder numberOrder = PoleNumberComparer.Compare(adjacent.PoleNumber, pole.PoleNumber);
            if (numberOrder is PoleNumberOrder.Unresolved or PoleNumberOrder.Equal) continue;
            BoundarySide direction = numberOrder == PoleNumberOrder.Less
                ? BoundarySide.SmallerNumber : BoundarySide.LargerNumber;
            if (mapped.TryGetValue(direction, out Guid previous) && previous != terminalId)
            {
                issue = "待确认 / 无法确定电气侧";
                return false;
            }
            mapped[direction] = terminalId;
        }

        if (mapped.Count == 0 || mapped.Values.Distinct().Count() != mapped.Count)
        {
            issue = "待确认 / 无法确定电气侧";
            return false;
        }
        if (!mapped.TryGetValue(side, out Guid resolvedTerminal))
        {
            issue = "待确认 / 无法确定电气侧";
            return false;
        }
        Guid[] terminalConnections = drawing.Connections.Where(item =>
                item.UsesTerminal(resolvedTerminal) &&
                drawing.OverheadLines.Any(line => line.ConnectionId == item.Id &&
                    line.SupportPoleIds.Contains(pole.Id)))
            .Select(item => item.Id).ToArray();
        boundary = new IsolationBoundary(deviceId, side, resolvedTerminal,
            terminalConnections.Length == 1 ? terminalConnections[0] : null);
        issue = "";
        return true;
    }

    public static WorkTicketSession Confirm(DrawingDocument drawing, WorkTicketSession ticket,
        IReadOnlyList<IsolationBoundary?> slots)
    {
        if (slots.Count == 0 || slots.Any(item => item is null))
            throw new InvalidOperationException("请完成至少一个隔离边界。");
        IsolationBoundary[] boundaries = slots.Select(item => item!).ToArray();
        ValidateBoundaries(drawing, boundaries);
        if (ticket.IsolationBoundaries.SequenceEqual(boundaries))
            return ticket;
        return (ticket with
        {
            IsolationBoundaries = boundaries
        }).Invalidate();
    }

    public static void ValidateBoundaries(DrawingDocument drawing,
        IReadOnlyList<IsolationBoundary> boundaries)
    {
        if (boundaries.Count == 0)
            throw new InvalidOperationException("请至少设置一个隔离边界。");
        if (boundaries.Select(item => item.DeviceId).Distinct().Count() != boundaries.Count)
            throw new InvalidOperationException("同一个边界设备不能重复选择。");
        foreach (IsolationBoundary item in boundaries)
        {
            if (!drawing.Devices.Any(device => device.Id == item.DeviceId))
                throw new InvalidOperationException($"隔离边界设备 {item.DeviceId} 不存在。");
            if (!TryResolve(drawing, item.DeviceId, item.Side, out IsolationBoundary? resolved,
                    out string issue))
                throw new InvalidOperationException(issue);
            if (item.TerminalId != resolved!.TerminalId)
                throw new InvalidOperationException($"隔离边界设备 {item.DeviceId} 的侧别与端子引用不一致，请重新选择边界。");
            if (item.ConnectionId != resolved.ConnectionId)
                throw new InvalidOperationException($"隔离边界设备 {item.DeviceId} 的连接或拓扑已变化，请重新选择边界。");
        }
    }

}
