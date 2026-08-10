namespace DistributionDrawing.Domain.Topology;

public sealed class Terminal
{
    private readonly HashSet<ConnectionType> _allowedConnectionTypes;

    public Terminal(
        Guid id,
        TopologyOwnerType ownerType,
        Guid ownerId,
        string role,
        string? voltageLevel,
        bool isExternal,
        bool allowsMultipleConnections,
        Guid? electricalNodeId = null,
        IEnumerable<ConnectionType>? allowedConnectionTypes = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Terminal ID cannot be empty.", nameof(id));
        }

        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("Terminal owner ID cannot be empty.", nameof(ownerId));
        }

        if (electricalNodeId == Guid.Empty)
        {
            throw new ArgumentException(
                "Electrical node ID cannot be empty when specified.",
                nameof(electricalNodeId));
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            throw new ArgumentException("Terminal role is required.", nameof(role));
        }

        _allowedConnectionTypes = allowedConnectionTypes?.ToHashSet() ?? [];

        if (!isExternal && _allowedConnectionTypes.Count > 0)
        {
            throw new ArgumentException(
                "An internal terminal cannot allow external connection types.",
                nameof(allowedConnectionTypes));
        }

        if (isExternal && _allowedConnectionTypes.Count == 0)
        {
            throw new ArgumentException(
                "An external terminal must allow at least one connection type.",
                nameof(allowedConnectionTypes));
        }

        Id = id;
        OwnerType = ownerType;
        OwnerId = ownerId;
        Role = role.Trim();
        VoltageLevel = string.IsNullOrWhiteSpace(voltageLevel) ? null : voltageLevel.Trim();
        IsExternal = isExternal;
        AllowsMultipleConnections = allowsMultipleConnections;
        ElectricalNodeId = electricalNodeId;
    }

    public Guid Id { get; }

    public TopologyOwnerType OwnerType { get; }

    public Guid OwnerId { get; }

    public string Role { get; }

    public string? VoltageLevel { get; }

    public bool IsExternal { get; }

    public bool AllowsMultipleConnections { get; }

    public Guid? ElectricalNodeId { get; }

    public IReadOnlySet<ConnectionType> AllowedConnectionTypes => _allowedConnectionTypes;

    public bool Allows(ConnectionType connectionType)
    {
        return IsExternal && _allowedConnectionTypes.Contains(connectionType);
    }
}
