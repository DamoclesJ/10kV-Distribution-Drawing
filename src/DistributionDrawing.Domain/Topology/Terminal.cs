namespace DistributionDrawing.Domain.Topology;

public sealed class Terminal
{
    private readonly HashSet<ConnectionType> _allowedConnectionTypes;

    public Terminal(
        Guid id,
        Guid ownerDeviceId,
        string role,
        string? voltageLevel,
        bool isExternal,
        bool allowsMultipleConnections,
        IEnumerable<ConnectionType>? allowedConnectionTypes = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Terminal ID cannot be empty.", nameof(id));
        }

        if (ownerDeviceId == Guid.Empty)
        {
            throw new ArgumentException("Owner device ID cannot be empty.", nameof(ownerDeviceId));
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
        OwnerDeviceId = ownerDeviceId;
        Role = role.Trim();
        VoltageLevel = string.IsNullOrWhiteSpace(voltageLevel) ? null : voltageLevel.Trim();
        IsExternal = isExternal;
        AllowsMultipleConnections = allowsMultipleConnections;
    }

    public Guid Id { get; }

    public Guid OwnerDeviceId { get; }

    public string Role { get; }

    public string? VoltageLevel { get; }

    public bool IsExternal { get; }

    public bool AllowsMultipleConnections { get; }

    public IReadOnlySet<ConnectionType> AllowedConnectionTypes => _allowedConnectionTypes;

    public bool Allows(ConnectionType connectionType)
    {
        return IsExternal && _allowedConnectionTypes.Contains(connectionType);
    }
}
