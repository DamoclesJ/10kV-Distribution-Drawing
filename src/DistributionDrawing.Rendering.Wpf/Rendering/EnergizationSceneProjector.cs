using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

/// <summary>Assigns analysis state to the original scene elements through exact model identity.</summary>
public static class EnergizationSceneProjector
{
    public static DrawingScene Project(
        DrawingScene scene, DrawingDocument drawing, EnergizationResult result)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(drawing);
        ArgumentNullException.ThrowIfNull(result);

        var resolved = new Dictionary<ElectricalVisualIdentity, ElectricalVisualState>();
        return new DrawingScene(scene.Elements.Select(element =>
            element.ElectricalIdentity is { } identity
                ? element with { ElectricalState = ResolveCached(identity) }
                : element), scene.HitTestIndex, scene.Diagnostics, scene.Routes);

        ElectricalVisualState ResolveCached(ElectricalVisualIdentity identity)
        {
            if (!resolved.TryGetValue(identity, out ElectricalVisualState state))
            {
                state = Resolve(identity, result, drawing);
                resolved.Add(identity, state);
            }
            return state;
        }
    }

    public static ElectricalVisualState Resolve(
        ElectricalVisualIdentity identity, EnergizationResult result,
        DrawingDocument? drawing = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(result);

        switch (identity.Kind)
        {
            case ElectricalVisualIdentityKind.Terminal:
                return Point(result.Terminals, identity.Id, result.CanConcludeDeenergized);
            case ElectricalVisualIdentityKind.Node:
                return Point(result.Nodes, identity.Id, result.CanConcludeDeenergized);
            case ElectricalVisualIdentityKind.Edge:
                ElectricalConnectivityEdge? edge = result.ConductingEdges.FirstOrDefault(item =>
                    item.Type == identity.EdgeType && item.SourceId == identity.Id &&
                    item.Connects(identity.FirstTerminalId ?? Guid.Empty,
                        identity.SecondTerminalId ?? Guid.Empty));
                if (edge is null) return ElectricalVisualState.Unknown;
                ElectricalVisualState first = Point(result.Terminals, edge.FirstTerminalId,
                    result.CanConcludeDeenergized);
                ElectricalVisualState second = Point(result.Terminals, edge.SecondTerminalId,
                    result.CanConcludeDeenergized);
                return first == second ? first : ElectricalVisualState.Unknown;
            case ElectricalVisualIdentityKind.Pole:
                Pole? pole = drawing?.Devices.OfType<Pole>().FirstOrDefault(item =>
                    item.Id == identity.Id);
                return pole is null ? ElectricalVisualState.Unknown : PoleState(
                    pole.OverheadAnchorTerminalIds.Select(id => Point(result.Terminals, id,
                        result.CanConcludeDeenergized)));
            case ElectricalVisualIdentityKind.SwitchDevice:
                SwitchDevice? switchDevice = drawing?.Devices.OfType<SwitchDevice>()
                    .FirstOrDefault(item => item.Id == identity.Id);
                if (switchDevice is null) return ElectricalVisualState.Unknown;
                Guid[] terminals = switchDevice.SwitchKind == SwitchKind.GroundSwitch
                    ? [switchDevice.FirstTerminalId]
                    : [switchDevice.FirstTerminalId, switchDevice.SecondTerminalId];
                return UniformState(terminals.Select(id => Point(result.Terminals, id,
                    result.CanConcludeDeenergized)));
            case ElectricalVisualIdentityKind.CableTermination:
                CableTermination? termination = drawing?.Devices.OfType<CableTermination>()
                    .FirstOrDefault(item => item.Id == identity.Id);
                if (termination is null) return ElectricalVisualState.Unknown;
                ElectricalVisualState cableState = Point(result.Terminals,
                    termination.CableSideTerminalId,
                    result.CanConcludeDeenergized);
                ElectricalVisualState overheadState = Point(result.Terminals,
                    termination.OverheadSideTerminalId,
                    result.CanConcludeDeenergized);
                return cableState == overheadState ? cableState : ElectricalVisualState.Unknown;
            case ElectricalVisualIdentityKind.PoleAttachment:
                Guid? deviceId = drawing?.PoleAttachments.FirstOrDefault(item =>
                    item.AttachmentId == identity.Id)?.AttachedDeviceId;
                Device? device = drawing?.Devices.FirstOrDefault(item => item.Id == deviceId);
                return device switch
                {
                    SwitchDevice => Resolve(ElectricalVisualIdentity.SwitchDevice(device.Id),
                        result, drawing),
                    CableTermination => Resolve(
                        ElectricalVisualIdentity.CableTermination(device.Id), result, drawing),
                    _ => ElectricalVisualState.Unknown
                };
            default:
                return ElectricalVisualState.Unknown;
        }
    }

    private static ElectricalVisualState Point(
        IReadOnlyDictionary<Guid, EnergizationPointResult> points, Guid id,
        bool canConcludeDeenergized)
    {
        if (!points.TryGetValue(id, out EnergizationPointResult? point))
            return ElectricalVisualState.Unknown;
        return point.State switch
        {
            EnergizationState.Energized => ElectricalVisualState.Energized,
            EnergizationState.Deenergized when canConcludeDeenergized =>
                ElectricalVisualState.Deenergized,
            _ => ElectricalVisualState.Unknown
        };
    }

    private static ElectricalVisualState PoleState(
        IEnumerable<ElectricalVisualState> states)
    {
        ElectricalVisualState[] values = states.ToArray();
        if (values.Length == 0) return ElectricalVisualState.Unknown;
        if (values.Contains(ElectricalVisualState.Energized))
            return ElectricalVisualState.Energized;
        return values.All(state => state == ElectricalVisualState.Deenergized)
            ? ElectricalVisualState.Deenergized : ElectricalVisualState.Unknown;
    }

    private static ElectricalVisualState UniformState(
        IEnumerable<ElectricalVisualState> states)
    {
        ElectricalVisualState[] values = states.ToArray();
        return values.Length > 0 && values.All(state => state == values[0])
            ? values[0] : ElectricalVisualState.Unknown;
    }
}
