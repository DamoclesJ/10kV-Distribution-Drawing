using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Domain.Documents;

public sealed class DrawingDocument
{
    private readonly List<Device> _devices = [];
    private readonly List<Terminal> _terminals = [];
    private readonly List<Connection> _connections = [];

    public DrawingDocument(Guid id, string title)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Document ID cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Document title is required.", nameof(title));
        }

        Id = id;
        Title = title.Trim();
    }

    public Guid Id { get; }

    public string Title { get; private set; }

    public IReadOnlyList<Device> Devices => _devices;

    public IReadOnlyList<Terminal> Terminals => _terminals;

    public IReadOnlyList<Connection> Connections => _connections;

    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Document title is required.", nameof(title));
        }

        Title = title.Trim();
    }

    public void AddDevice(Device device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (_devices.Any(existing => existing.Id == device.Id))
        {
            throw new InvalidOperationException($"Device '{device.Id}' already exists.");
        }

        _devices.Add(device);
    }

    public void AddTerminal(Terminal terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);

        if (_terminals.Any(existing => existing.Id == terminal.Id))
        {
            throw new InvalidOperationException($"Terminal '{terminal.Id}' already exists.");
        }

        if (_devices.All(device => device.Id != terminal.OwnerDeviceId))
        {
            throw new InvalidOperationException(
                $"Terminal owner device '{terminal.OwnerDeviceId}' does not exist.");
        }

        _terminals.Add(terminal);
    }

    public void AddConnection(Connection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (_connections.Any(existing => existing.Id == connection.Id))
        {
            throw new InvalidOperationException($"Connection '{connection.Id}' already exists.");
        }

        Terminal start = GetTerminal(connection.StartTerminalId);
        Terminal end = GetTerminal(connection.EndTerminalId);

        EnsureTerminalAcceptsConnection(start, connection);
        EnsureTerminalAcceptsConnection(end, connection);

        _connections.Add(connection);
    }

    private Terminal GetTerminal(Guid terminalId)
    {
        return _terminals.FirstOrDefault(terminal => terminal.Id == terminalId)
            ?? throw new InvalidOperationException($"Terminal '{terminalId}' does not exist.");
    }

    private void EnsureTerminalAcceptsConnection(Terminal terminal, Connection connection)
    {
        if (!terminal.Allows(connection.Type))
        {
            throw new InvalidOperationException(
                $"Terminal '{terminal.Id}' does not allow connection type '{connection.Type}'.");
        }

        if (!string.Equals(
                terminal.VoltageLevel,
                connection.VoltageLevel,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Terminal '{terminal.Id}' voltage level is incompatible with the connection.");
        }

        if (!terminal.AllowsMultipleConnections &&
            _connections.Any(existing => existing.UsesTerminal(terminal.Id)))
        {
            throw new InvalidOperationException(
                $"Terminal '{terminal.Id}' already has a connection.");
        }
    }
}
