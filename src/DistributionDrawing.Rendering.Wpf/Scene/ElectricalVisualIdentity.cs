using DistributionDrawing.Application.Topology;

namespace DistributionDrawing.Rendering.Wpf.Scene;

public enum ElectricalVisualIdentityKind { Terminal, Node, Edge, Association, Hazard }

// Normal includes unmapped visuals. Unknown is a legacy value, never emitted or styled.
public enum ElectricalVisualState { Normal, Energized, Deenergized, Unknown }

public sealed record ElectricalVisualIdentity(
    ElectricalVisualIdentityKind Kind,
    Guid Id,
    ElectricalConnectivityEdgeType? EdgeType = null,
    Guid? FirstTerminalId = null,
    Guid? SecondTerminalId = null)
{
    // Exact associations require every explicit member to agree. Hazard associations
    // summarize device/location identifiers: any energized member is a hazard.
    public IReadOnlyList<ElectricalVisualIdentity> Members { get; init; } = [];

    public bool Equals(ElectricalVisualIdentity? other) => other is not null &&
        Kind == other.Kind && Id == other.Id && EdgeType == other.EdgeType &&
        FirstTerminalId == other.FirstTerminalId && SecondTerminalId == other.SecondTerminalId &&
        Members.SequenceEqual(other.Members);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind); hash.Add(Id); hash.Add(EdgeType);
        hash.Add(FirstTerminalId); hash.Add(SecondTerminalId);
        foreach (var member in Members) hash.Add(member);
        return hash.ToHashCode();
    }

    public static ElectricalVisualIdentity Association(Guid ownerId,
        IEnumerable<ElectricalVisualIdentity> members) =>
        new(ElectricalVisualIdentityKind.Association, ownerId)
        { Members = Array.AsReadOnly(members.Distinct().ToArray()) };

    public static ElectricalVisualIdentity HazardAssociation(Guid ownerId,
        IEnumerable<ElectricalVisualIdentity> members) =>
        new(ElectricalVisualIdentityKind.Hazard, ownerId)
        { Members = Array.AsReadOnly(members.Distinct().ToArray()) };

    public static ElectricalVisualIdentity SwitchPath(
        DistributionDrawing.Domain.Devices.SwitchDevice device) =>
        device.SwitchState == DistributionDrawing.Domain.Devices.SwitchState.Closed
            ? Edge(ElectricalConnectivityEdgeType.ClosedSwitch, device.Id,
                device.FirstTerminalId, device.SecondTerminalId)
            : Terminal(device.SecondTerminalId);

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
