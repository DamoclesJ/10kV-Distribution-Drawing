namespace DistributionDrawing.Domain.Topology;

public sealed class ElectricalNode
{
    private readonly HashSet<Guid> _terminalIds = [];

    public ElectricalNode(
        Guid id,
        ElectricalNodeType type,
        TopologyOwnerType ownerType,
        Guid ownerId,
        ElectricalState? electricalState = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Electrical node ID cannot be empty.", nameof(id));
        }

        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("Electrical node owner ID cannot be empty.", nameof(ownerId));
        }

        if (type == ElectricalNodeType.Earth && electricalState is not null)
        {
            throw new ArgumentException(
                "An earth node cannot have an electrical state.",
                nameof(electricalState));
        }

        Id = id;
        Type = type;
        OwnerType = ownerType;
        OwnerId = ownerId;
        ElectricalState = electricalState;
    }

    public Guid Id { get; }

    public ElectricalNodeType Type { get; }

    public TopologyOwnerType OwnerType { get; }

    public Guid OwnerId { get; }

    public ElectricalState? ElectricalState { get; private set; }

    public IReadOnlySet<Guid> TerminalIds => _terminalIds;

    public void SetElectricalState(ElectricalState electricalState)
    {
        if (Type == ElectricalNodeType.Earth)
        {
            throw new InvalidOperationException("An earth node cannot have an electrical state.");
        }

        ElectricalState = electricalState;
    }

    internal void AttachTerminal(Guid terminalId)
    {
        _terminalIds.Add(terminalId);
    }
}
