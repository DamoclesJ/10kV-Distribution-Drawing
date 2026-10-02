using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

public sealed record EnergizationVisualizationDiagnostic(
    ElectricalVisualIdentity Identity, string Message);

/// <summary>Styles exact analyzer identities; mapping defects are separate diagnostics.</summary>
public static class EnergizationSceneStyler
{
    public static IReadOnlyList<SceneElement> Build(DrawingScene scene, EnergizationResult result) =>
        Build(scene, result, out _);

    public static IReadOnlyList<SceneElement> Build(DrawingScene scene, EnergizationResult result,
        out IReadOnlyList<EnergizationVisualizationDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsSuccess)
        {
            diagnostics = [];
            return scene.Elements;
        }
        List<EnergizationVisualizationDiagnostic> issues = [];
        SceneElement[] elements = scene.Elements.Select(element =>
        {
            if (element.ElectricalIdentity is not { } identity) return element;
            ElectricalVisualState state = ResolveCore(identity, result, issues);
            // Deenergized retains every original color, fill and stroke pattern.
            // Normal denotes an unmapped visual, never a deenergized conclusion.
            if (state != ElectricalVisualState.Energized)
                return element with { ElectricalState = state };
            var color = EnergizationVisualStyleResolver.Resolve(state)!.Color;
            SceneElement styled = element switch
            {
                SceneLine line => line with { Stroke = color },
                SceneArc arc => arc with { Stroke = color },
                ScenePolyline polyline => polyline with { Stroke = color, Fill = polyline.Fill == polyline.Stroke ? color : polyline.Fill },
                SceneEllipse ellipse => ellipse with { Stroke = color, Fill = ellipse.Fill == ellipse.Stroke ? color : ellipse.Fill },
                SceneRectangle rectangle => rectangle with { Stroke = color, Fill = rectangle.Fill == rectangle.Stroke ? color : rectangle.Fill },
                SceneText text => text with { Foreground = color },
                _ => element
            };
            return styled with { ElectricalState = state };
        }).ToArray();
        diagnostics = issues.Distinct().ToArray();
        return elements;
    }

    public static ElectricalVisualState Resolve(ElectricalVisualIdentity identity,
        EnergizationResult result) => Resolve(identity, result, out _);

    public static ElectricalVisualState Resolve(ElectricalVisualIdentity identity,
        EnergizationResult result,
        out IReadOnlyList<EnergizationVisualizationDiagnostic> diagnostics)
    {
        List<EnergizationVisualizationDiagnostic> issues = [];
        ElectricalVisualState state = ResolveCore(identity, result, issues);
        diagnostics = issues.Distinct().ToArray();
        return state;
    }

    private static ElectricalVisualState ResolveCore(ElectricalVisualIdentity identity,
        EnergizationResult result, List<EnergizationVisualizationDiagnostic> diagnostics)
    {
        ElectricalVisualState Defect(string reason)
        {
            diagnostics.Add(new(identity,
                $"显示映射失败（{identity.Kind} / {identity.Id}）：{reason}；未映射图元保持原图外观，不代表已判定停电。"));
            return ElectricalVisualState.Normal;
        }
        if (!result.IsSuccess) return Defect("没有成功的带电分析结果");
        EnergizationPointResult? point;
        switch (identity.Kind)
        {
            case ElectricalVisualIdentityKind.Association:
            case ElectricalVisualIdentityKind.Hazard:
                if (identity.Members.Count == 0) return Defect("缺少成员电气标识");
                ElectricalVisualState[] members = identity.Members
                    .Select(member => ResolveCore(member, result, diagnostics)).ToArray();
                if (identity.Kind == ElectricalVisualIdentityKind.Hazard &&
                    members.Contains(ElectricalVisualState.Energized))
                    return ElectricalVisualState.Energized;
                if (members.Contains(ElectricalVisualState.Normal)) return ElectricalVisualState.Normal;
                return members.All(member => member == members[0])
                    ? members[0] : Defect("同一精确关联中的成员电气状态不一致");
            case ElectricalVisualIdentityKind.Terminal:
                if (!result.Terminals.TryGetValue(identity.Id, out point))
                    return Defect("未找到对应端子分析结果");
                break;
            case ElectricalVisualIdentityKind.Node:
                if (!result.Nodes.TryGetValue(identity.Id, out point))
                    return Defect("未找到对应节点分析结果");
                break;
            case ElectricalVisualIdentityKind.Edge:
                ElectricalConnectivityEdge? edge = result.ConductingEdges.FirstOrDefault(item =>
                    item.Type == identity.EdgeType && item.SourceId == identity.Id &&
                    item.Connects(identity.FirstTerminalId ?? Guid.Empty,
                        identity.SecondTerminalId ?? Guid.Empty));
                if (edge is null) return Defect("未找到匹配类型和双端子的导电边");
                if (!result.Terminals.TryGetValue(edge.FirstTerminalId, out point) ||
                    !result.Terminals.TryGetValue(edge.SecondTerminalId, out EnergizationPointResult? second))
                    return Defect("导电边端子分析结果缺失");
                if (point.State != second.State) return Defect("导电边两端电气状态不一致");
                break;
            default:
                return Defect("不支持的电气标识");
        }
        return point.State switch
        {
            EnergizationState.Energized => ElectricalVisualState.Energized,
            EnergizationState.Deenergized => ElectricalVisualState.Deenergized,
            _ => Defect("分析结果不是有效二元电气状态")
        };
    }
}
