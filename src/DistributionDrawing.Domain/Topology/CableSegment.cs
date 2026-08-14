namespace DistributionDrawing.Domain.Topology;

public sealed class CableSegment
{
    public CableSegment(
        Guid id,
        string name,
        string cableType,
        double length,
        string voltageLevel,
        Guid connectionId,
        Guid startTerminalId,
        Guid endTerminalId)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Cable segment ID cannot be empty.", nameof(id));
        }

        if (connectionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Cable segment connection ID cannot be empty.",
                nameof(connectionId));
        }

        if (id == connectionId)
        {
            throw new ArgumentException(
                "Cable segment and connection IDs must be different.",
                nameof(connectionId));
        }

        if (startTerminalId == Guid.Empty || endTerminalId == Guid.Empty)
        {
            throw new ArgumentException("Cable segment terminal IDs are required.");
        }

        if (startTerminalId == endTerminalId)
        {
            throw new ArgumentException(
                "A cable segment requires two different terminals.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Cable segment name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(cableType))
        {
            throw new ArgumentException(
                "Cable segment cable type is required.",
                nameof(cableType));
        }

        if (double.IsNaN(length) || double.IsInfinity(length) || length <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                "Cable segment length must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(voltageLevel))
        {
            throw new ArgumentException(
                "Cable segment voltage level is required.",
                nameof(voltageLevel));
        }

        Id = id;
        Name = name.Trim();
        CableType = cableType.Trim();
        Length = length;
        VoltageLevel = voltageLevel.Trim();
        ConnectionId = connectionId;
        StartTerminalId = startTerminalId;
        EndTerminalId = endTerminalId;
    }

    public Guid Id { get; }

    public string Name { get; }

    public string CableType { get; private set; }

    public double Length { get; private set; }

    public string VoltageLevel { get; }

    public Guid ConnectionId { get; }

    public Guid StartTerminalId { get; }

    public Guid EndTerminalId { get; }

    public void ChangeCableType(string cableType)
    {
        if (string.IsNullOrWhiteSpace(cableType))
        {
            throw new ArgumentException(
                "Cable segment cable type is required.",
                nameof(cableType));
        }

        CableType = cableType.Trim();
    }

    public void ChangeLength(double length)
    {
        if (double.IsNaN(length) || double.IsInfinity(length) || length <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                "Cable segment length must be greater than zero.");
        }

        Length = length;
    }
}
