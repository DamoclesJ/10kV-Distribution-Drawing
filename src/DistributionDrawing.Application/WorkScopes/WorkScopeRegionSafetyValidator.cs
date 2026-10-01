using DistributionDrawing.Application.Energization;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.WorkScopes;

public static class WorkScopeRegionSafetyValidator
{
    public static (WorkScopeDraftStatus Status, string? Diagnostic) Check(
        DrawingDocument drawing, IReadOnlySet<Guid> terminalIds,
        IReadOnlyCollection<Guid> nodeIds, EnergizationResult result)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        ArgumentNullException.ThrowIfNull(terminalIds);
        ArgumentNullException.ThrowIfNull(nodeIds);
        ArgumentNullException.ThrowIfNull(result);

        foreach (Guid id in terminalIds.Order())
            if (result.Terminals.TryGetValue(id, out EnergizationPointResult? point) &&
                point.State == EnergizationState.Energized)
            {
                bool grounded = drawing.GroundingPoints.Any(item =>
                        item.Target.Kind == GroundingTargetKind.Terminal &&
                        item.Target.TargetId == id) ||
                    result.GroundingSwitchConnections.Any(item =>
                        item.DeviceSideTerminalId == id);
                return (WorkScopeDraftStatus.EnergizedInsideScope,
                    $"候选停电范围内仍存在带电位置：{DescribeTerminal(drawing, id)}" +
                    (grounded ? "；该位置同时存在明确接地事实。" : "。"));
            }
        foreach (Guid id in nodeIds.Order())
            if (result.Nodes.TryGetValue(id, out EnergizationPointResult? point) &&
                point.State == EnergizationState.Energized)
                return (WorkScopeDraftStatus.EnergizedInsideScope,
                    $"候选停电范围内仍存在带电节点：{id}。");
        foreach (Guid id in terminalIds.Order())
            if (!result.Terminals.TryGetValue(id, out EnergizationPointResult? point) ||
                point.State == EnergizationState.Unknown)
                return (WorkScopeDraftStatus.UnknownInsideScope,
                    $"候选停电范围内存在状态未知位置：{DescribeTerminal(drawing, id)}。");
        foreach (Guid id in nodeIds.Order())
            if (!result.Nodes.TryGetValue(id, out EnergizationPointResult? point) ||
                point.State == EnergizationState.Unknown)
                return (WorkScopeDraftStatus.UnknownInsideScope,
                    $"候选停电范围内存在状态未知节点：{id}。");
        return (WorkScopeDraftStatus.Success, null);
    }

    private static string DescribeTerminal(DrawingDocument drawing, Guid id)
    {
        Terminal? terminal = drawing.Terminals.SingleOrDefault(item => item.Id == id);
        if (terminal is null) return $"端子 {id}";
        string? name = drawing.Devices.SingleOrDefault(item => item.Id == terminal.OwnerId)?.DisplayName;
        return $"{name ?? terminal.OwnerId.ToString()} / {terminal.Role}（端子 {id}）";
    }
}
