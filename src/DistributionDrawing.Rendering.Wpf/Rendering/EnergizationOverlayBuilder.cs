using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

/// <summary>
/// Projects analyzer facts onto explicit electrical scene bindings. It never infers
/// connectivity from drawing positions or selection targets.
/// </summary>
public static class EnergizationOverlayBuilder
{
    public static IReadOnlyList<SceneElement> Build(DrawingScene scene, EnergizationResult result)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(result);

        var output = new List<SceneElement>();
        foreach (SceneElement element in scene.Elements)
        {
            if (element.ElectricalIdentity is not ElectricalVisualIdentity identity)
                continue;
            ElectricalVisualState state = Resolve(identity, result);
            EnergizationVisualStyle style = EnergizationVisualStyleResolver.Resolve(state);
            switch (element)
            {
                case SceneLine line:
                    (DocumentPoint start, DocumentPoint end) = Offset(line.Start, line.End);
                    output.Add(new SceneLine(start, end, style.Color,
                        style.ThicknessMillimeters, style.StrokeStyle)
                    {
                        ElectricalIdentity = identity,
                        ElectricalState = state
                    });
                    break;
                case SceneArc arc:
                    output.Add(new SceneArc(arc.Center, arc.RadiusMillimeters,
                        arc.StartAngleDegrees, arc.SweepAngleDegrees, style.Color,
                        style.ThicknessMillimeters, style.StrokeStyle)
                    {
                        ElectricalIdentity = identity,
                        ElectricalState = state,
                        HitTestBounds = null
                    });
                    break;
                case ScenePolyline polyline when !polyline.IsClosed:
                    output.Add(new ScenePolyline(polyline.Points, false, style.Color,
                        style.ThicknessMillimeters, strokeStyle: style.StrokeStyle)
                    {
                        ElectricalIdentity = identity,
                        ElectricalState = state,
                        HitTestBounds = null
                    });
                    break;
            }
        }
        return output;
    }

    public static ElectricalVisualState Resolve(
        ElectricalVisualIdentity identity, EnergizationResult result)
    {
        EnergizationState state;
        switch (identity.Kind)
        {
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

    private static (DocumentPoint Start, DocumentPoint End) Offset(
        DocumentPoint start, DocumentPoint end)
    {
        double dx = end.XMillimeters - start.XMillimeters;
        double dy = end.YMillimeters - start.YMillimeters;
        double length = Math.Sqrt(dx * dx + dy * dy);
        if (length == 0) return (start, end);
        const double separation = 1.1;
        double shiftX = -dy / length * separation;
        double shiftY = dx / length * separation;
        return (
            new DocumentPoint(start.XMillimeters + shiftX, start.YMillimeters + shiftY),
            new DocumentPoint(end.XMillimeters + shiftX, end.YMillimeters + shiftY));
    }
}
