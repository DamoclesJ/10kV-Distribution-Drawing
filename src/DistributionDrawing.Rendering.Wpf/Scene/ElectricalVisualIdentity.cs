using DistributionDrawing.Application.Topology;

namespace DistributionDrawing.Rendering.Wpf.Scene;

public enum ElectricalVisualIdentityKind { Terminal, Node, Edge }

public enum ElectricalVisualState { Normal, Energized, Deenergized, Unknown }

public sealed record ElectricalVisualIdentity(
    ElectricalVisualIdentityKind Kind,
    Guid Id,
    ElectricalConnectivityEdgeType? EdgeType = null,
    Guid? FirstTerminalId = null,
    Guid? SecondTerminalId = null)
{
    public static ElectricalVisualIdentity Terminal(Guid id) =>
        new(ElectricalVisualIdentityKind.Terminal, id);

    public static ElectricalVisualIdentity Node(Guid id) =>
        new(ElectricalVisualIdentityKind.Node, id);

    public static ElectricalVisualIdentity Edge(
        ElectricalConnectivityEdgeType type, Guid sourceId, Guid firstTerminalId,
        Guid secondTerminalId) =>
        new(ElectricalVisualIdentityKind.Edge, sourceId, type, firstTerminalId,
            secondTerminalId);
}
