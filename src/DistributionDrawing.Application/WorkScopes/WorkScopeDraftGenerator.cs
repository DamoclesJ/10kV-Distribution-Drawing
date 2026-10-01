using DistributionDrawing.Application.Energization;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.WorkScopes;

public sealed class WorkScopeDraftGenerator
{
    private readonly WorkScopeBoundaryResolver _resolver = new();

    public WorkScopeDraftResult Generate(DrawingDocument drawing,
        EnergizationScenario scenario, EnergizationAnalysisState analysis)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(analysis);

        List<WorkScopeBoundaryAnchor> anchors = [];
        WorkScopeStructuralGraph? graph = null;
        IReadOnlySet<Guid> region = new HashSet<Guid>();
        Guid[] regionNodes = [];
        Guid[] regionConnections = [];
        WorkScopeRegionItem[] summary = [];
        List<string> warnings = [];
        EnergizationResult? result = analysis.CurrentResult;

        WorkScopeDraftResult Finish(WorkScopeDraftStatus status, string? diagnostic = null) =>
            new(status, anchors, region, regionNodes, regionConnections, summary,
                diagnostic is null ? [] : [diagnostic], warnings, scenario.Id,
                result, status == WorkScopeDraftStatus.Success
                    ? WorkScopeDraftContext.Capture(drawing, scenario) : null);

        if (scenario.Seeds.Count == 0)
            return Finish(WorkScopeDraftStatus.NoSeeds, "尚未选择电源边界。");
        if (scenario.Seeds.Count < 2)
            return Finish(WorkScopeDraftStatus.TooFewBoundaries,
                "至少需要两个电源边界才能自动确定停电工作范围。");
        if (!scenario.IsSourceSetComplete)
            return Finish(WorkScopeDraftStatus.SourceSetIncomplete,
                "电源点尚未确认完整，不能自动确定停电工作范围。");
        if (result is null)
            return Finish(WorkScopeDraftStatus.ResultNotCurrent,
                "当前带电分析结果已失效或尚未执行，请重新分析。");
        if (result.Validity != EnergizationValidity.Complete)
        {
            EnergizationDiagnostic? seedIssue = result.Diagnostics.FirstOrDefault(item =>
                item.SeedId is not null);
            if (seedIssue is not null)
            {
                EnergizedSeed? failed = scenario.Seeds.FirstOrDefault(item =>
                    item.Id == seedIssue.SeedId);
                string name = drawing.Devices.FirstOrDefault(item =>
                    item.Id == failed?.BoundaryDeviceId)?.DisplayName ??
                    failed?.BoundaryDeviceId.ToString() ?? "未知设备";
                return Finish(WorkScopeDraftStatus.BoundaryResolutionFailed,
                    $"电源边界 {name} 无法解析：{EnergizationUiService.DiagnosticText(seedIssue.Code)}。");
            }
            return Finish(WorkScopeDraftStatus.ResultNotComplete,
                "当前带电分析结果不是完整分析；请检查拓扑。");
        }

        foreach (EnergizedSeed seed in scenario.Seeds)
        {
            if (!_resolver.TryResolve(drawing, seed, out WorkScopeBoundaryAnchor? anchor))
            {
                string name = drawing.Devices.FirstOrDefault(item =>
                    item.Id == seed.BoundaryDeviceId)?.DisplayName ?? seed.BoundaryDeviceId.ToString();
                return Finish(WorkScopeDraftStatus.BoundaryResolutionFailed,
                    $"电源边界 {name} 无法同时解析电源侧和工作侧。");
            }
            anchors.Add(anchor!);
        }
        if (anchors.Select(item => item.BoundaryDeviceId).Distinct().Count() != anchors.Count)
            return Finish(WorkScopeDraftStatus.AmbiguousRegion,
                "同一边界设备被重复声明，无法唯一确定停电范围。");

        try
        {
            graph = WorkScopeStructuralGraph.Build(drawing,
                anchors.Select(item => item.BoundaryDeviceId).ToHashSet());
            region = graph.FindRegion(anchors[0].WorkTerminalId);
            if (anchors.Skip(1).Any(item => !region.Contains(item.WorkTerminalId)))
                return Finish(WorkScopeDraftStatus.DisconnectedAnchors,
                    "多个边界的工作侧未共同围定同一个结构区域。");
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            return Finish(WorkScopeDraftStatus.UnsupportedTopology,
                $"当前拓扑无法生成结构性停电范围：{error.Message}");
        }
        if (anchors.Any(item => region.Contains(item.SourceTerminalId)))
            return Finish(WorkScopeDraftStatus.AmbiguousRegion,
                "边界电源侧仍可从工作侧到达，当前拓扑无法唯一确定范围。");

        regionNodes = drawing.ElectricalNodes
            .Where(node => node.Type != ElectricalNodeType.Earth &&
                node.TerminalIds.Any(region.Contains))
            .Select(node => node.Id).ToArray();
        regionConnections = graph.Edges.Where(edge => edge.ConnectionId is not null &&
                region.Contains(edge.FirstTerminalId) && region.Contains(edge.SecondTerminalId))
            .Select(edge => edge.ConnectionId!.Value).Distinct().ToArray();

        (WorkScopeDraftStatus safetyStatus, string? safetyDiagnostic) =
            WorkScopeRegionSafetyValidator.Check(drawing, region, regionNodes, result);
        if (safetyStatus != WorkScopeDraftStatus.Success)
            return Finish(safetyStatus, safetyDiagnostic);

        summary = CreateSummary(drawing, region, regionConnections,
            anchors.Select(item => item.BoundaryDeviceId).ToHashSet());
        if (anchors.Any(item => drawing.Devices.OfType<SwitchDevice>()
                .Any(device => device.Id == item.BoundaryDeviceId &&
                    device.SwitchKind == SwitchKind.DropoutFuse)))
            warnings.Add("跌落式熔断器的熔管装设或摘除状态尚需现场人工核对。");
        warnings.Add("仅基于当前图纸及已建模拓扑生成候选范围；请核对现场可能的外部接入与工作对象。");
        return Finish(WorkScopeDraftStatus.Success);
    }

    private static WorkScopeRegionItem[] CreateSummary(DrawingDocument drawing,
        IReadOnlySet<Guid> region, IReadOnlyCollection<Guid> connectionIds,
        IReadOnlySet<Guid> boundaryIds)
    {
        Dictionary<Guid, WorkScopeRegionItem> items = [];
        void Add(Guid id, string kind, string name)
        {
            if (!boundaryIds.Contains(id)) items.TryAdd(id, new WorkScopeRegionItem(id, kind, name));
        }
        foreach (Terminal terminal in drawing.Terminals.Where(item => region.Contains(item.Id)))
        {
            Device? device = drawing.Devices.SingleOrDefault(item => item.Id == terminal.OwnerId);
            if (device is not null)
            {
                Add(device.Id, device is RingCabinet ? "环网柜" :
                    device is CableTermination ? "电缆终端" :
                    device is Pole ? "杆塔" : "设备", device.DisplayName ?? device.Id.ToString());
                if (device is SwitchDevice)
                    foreach (RingCabinet cabinet in drawing.Devices.OfType<RingCabinet>()
                        .Where(cabinet => cabinet.Intervals.Any(interval =>
                            interval.SwitchDevices.Any(sw => sw.Id == device.Id))))
                        Add(cabinet.Id, "环网柜", cabinet.DisplayName ?? cabinet.Id.ToString());
            }
            else if (terminal.OwnerType == TopologyOwnerType.InternalAggregate)
                foreach (RingCabinet cabinet in drawing.Devices.OfType<RingCabinet>()
                    .Where(cabinet => cabinet.Intervals.Any(interval =>
                        interval.IntervalId == terminal.OwnerId)))
                    Add(cabinet.Id, "环网柜", cabinet.DisplayName ?? cabinet.Id.ToString());
        }
        foreach (Connection connection in drawing.Connections.Where(item =>
            connectionIds.Contains(item.Id)))
        {
            Add(connection.Id, connection.Type == ConnectionType.Cable ? "电缆" : "架空线",
                connection.DisplayName);
            foreach (OverheadLine line in drawing.OverheadLines.Where(item =>
                item.ConnectionId == connection.Id))
                foreach (Guid poleId in line.SupportPoleIds)
                    if (drawing.Devices.OfType<Pole>().SingleOrDefault(item => item.Id == poleId)
                        is { } pole)
                        Add(pole.Id, "杆塔", pole.PoleNumber);
        }
        return items.Values.OrderBy(item => item.Kind).ThenBy(item => item.Name).ToArray();
    }
}
