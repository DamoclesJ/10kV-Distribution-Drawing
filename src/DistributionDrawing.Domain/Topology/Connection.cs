namespace DistributionDrawing.Domain.Topology;

public sealed class Connection
{
    public Connection(
        Guid id,
        ConnectionType type,
        Guid startTerminalId,
        Guid endTerminalId,
        string displayName,
        string voltageLevel)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Connection ID cannot be empty.", nameof(id));
        }

        if (startTerminalId == Guid.Empty)
        {
            throw new ArgumentException("Start terminal ID cannot be empty.", nameof(startTerminalId));
        }

        if (endTerminalId == Guid.Empty)
        {
            throw new ArgumentException("End terminal ID cannot be empty.", nameof(endTerminalId));
        }

        if (startTerminalId == endTerminalId)
        {
            throw new ArgumentException("A connection requires two different terminals.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Connection display name is required.", nameof(displayName));
        }

        if (string.IsNullOrWhiteSpace(voltageLevel))
        {
            throw new ArgumentException("Connection voltage level is required.", nameof(voltageLevel));
        }

        Id = id;
        Type = type;
        StartTerminalId = startTerminalId;
        EndTerminalId = endTerminalId;
        DisplayName = displayName.Trim();
        VoltageLevel = voltageLevel.Trim();
    }

    public Guid Id { get; }

    public ConnectionType Type { get; }

    public Guid StartTerminalId { get; }

    public Guid EndTerminalId { get; }

    public string DisplayName { get; }

    public string VoltageLevel { get; }

    public bool UsesTerminal(Guid terminalId)
    {
        return StartTerminalId == terminalId || EndTerminalId == terminalId;
    }
}
