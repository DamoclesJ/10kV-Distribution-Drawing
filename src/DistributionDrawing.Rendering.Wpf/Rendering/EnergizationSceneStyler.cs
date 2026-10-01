using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

/// <summary>
/// Projects analyzer facts onto explicit electrical scene bindings. It never infers
/// connectivity from drawing positions or selection targets.
/// </summary>
public static class EnergizationSceneStyler
{
    public static IReadOnlyList<SceneElement> Build(DrawingScene scene, EnergizationResult result)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(result);

        return scene.Elements.Select(element =>
        {
            if (element.ElectricalIdentity is not { } identity) return element;
            ElectricalVisualState state = Resolve(identity, result);
            var color = EnergizationVisualStyleResolver.Resolve(state).Color;
            SceneStrokeStyle Pattern(SceneStrokeStyle original) =>
                EnergizationVisualStyleResolver.Resolve(state, original).StrokeStyle;
            SceneElement styled = element switch
            {
                SceneLine line => line with { Stroke = color, StrokeStyle = Pattern(line.StrokeStyle) },
                SceneArc arc => arc with { Stroke = color, StrokeStyle = Pattern(arc.StrokeStyle) },
                ScenePolyline polyline => polyline with { Stroke = color, Fill = polyline.Fill == polyline.Stroke ? color : polyline.Fill, StrokeStyle = Pattern(polyline.StrokeStyle) },
                SceneEllipse ellipse => ellipse with { Stroke = color, Fill = ellipse.Fill == ellipse.Stroke ? color : ellipse.Fill, StrokeStyle = Pattern(ellipse.StrokeStyle) },
                SceneRectangle rectangle => rectangle with { Stroke = color, Fill = rectangle.Fill == rectangle.Stroke ? color : rectangle.Fill, StrokeStyle = Pattern(rectangle.StrokeStyle) },
                SceneText text => text with { Foreground = color },
                _ => element
            };
            return styled with { ElectricalState = state };
        }).ToArray();
    }

    public static ElectricalVisualState Resolve(
        ElectricalVisualIdentity identity, EnergizationResult result)
    {
        EnergizationState state;
        switch (identity.Kind)
        {
            case ElectricalVisualIdentityKind.Association:
                ElectricalVisualState[] members = identity.Members.Select(member => Resolve(member, result)).ToArray();
                return members.Length > 0 && members.All(member => member == members[0])
                    ? members[0] : ElectricalVisualState.Unknown;
            case ElectricalVisualIdentityKind.Terminal:
                state = result.Terminals.TryGetValue(identity.Id, out EnergizationPointResult? terminal)
                    ? terminal.State : EnergizationState.Unknown;
                break;
            case ElectricalVisualIdentityKind.Node:
                state = result.Nodes.TryGetValue(identity.Id, out EnergizationPointResult? node)
                    ? node.State : EnergizationState.Unknown;
                break;
            case ElectricalVisualIdentityKind.Edge:
                ElectricalConnectivityEdge? edge = result.ConductingEdges.FirstOrDefault(item =>
                    item.Type == identity.EdgeType && item.SourceId == identity.Id &&
                    item.Connects(identity.FirstTerminalId ?? Guid.Empty,
                        identity.SecondTerminalId ?? Guid.Empty));
                if (edge is null ||
                    !result.Terminals.TryGetValue(edge.FirstTerminalId, out EnergizationPointResult? first) ||
                    !result.Terminals.TryGetValue(edge.SecondTerminalId, out EnergizationPointResult? second) ||
                    first.State != second.State)
                    return ElectricalVisualState.Unknown;
                state = first.State;
                break;
            default:
                return ElectricalVisualState.Unknown;
        }

        return state switch
        {
            EnergizationState.Energized => ElectricalVisualState.Energized,
            EnergizationState.Deenergized when result.CanConcludeDeenergized =>
                ElectricalVisualState.Deenergized,
            _ => ElectricalVisualState.Unknown
        };
    }

}
