using DistributionDrawing.Application.Topology;

namespace DistributionDrawing.Rendering.Wpf.Scene;

public enum ElectricalVisualIdentityKind
{
    Terminal, Node, Edge, Pole, SwitchDevice, CableTermination, PoleAttachment
}

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

    public static ElectricalVisualIdentity Pole(Guid id) =>
        new(ElectricalVisualIdentityKind.Pole, id);

    public static ElectricalVisualIdentity SwitchDevice(Guid id) =>
        new(ElectricalVisualIdentityKind.SwitchDevice, id);

    public static ElectricalVisualIdentity CableTermination(Guid id) =>
        new(ElectricalVisualIdentityKind.CableTermination, id);

    public static ElectricalVisualIdentity PoleAttachment(Guid id) =>
        new(ElectricalVisualIdentityKind.PoleAttachment, id);

    public static ElectricalVisualIdentity Edge(
        ElectricalConnectivityEdgeType type, Guid sourceId, Guid firstTerminalId,
        Guid secondTerminalId) =>
        new(ElectricalVisualIdentityKind.Edge, sourceId, type, firstTerminalId,
            secondTerminalId);
}
