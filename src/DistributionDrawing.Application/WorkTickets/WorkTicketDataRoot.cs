namespace DistributionDrawing.Application.WorkTickets;

public sealed class WorkTicketDataRoot
{
    private readonly List<WorkTicketSession> _tickets;

    public WorkTicketDataRoot(Guid documentId, IEnumerable<WorkTicketSession>? tickets = null)
    {
        if (documentId == Guid.Empty) throw new ArgumentException("Document ID is required.", nameof(documentId));
        DocumentId = documentId;
        _tickets = tickets?.ToList() ?? [];
        if (_tickets.Any(item => item.Id == Guid.Empty) ||
            _tickets.Select(item => item.Id).Distinct().Count() != _tickets.Count)
            throw new ArgumentException("Work ticket IDs must be nonempty and unique.", nameof(tickets));
    }

    public Guid DocumentId { get; }
    public IReadOnlyList<WorkTicketSession> Tickets => _tickets.AsReadOnly();
    public WorkTicketSession? Selected(Guid? ticketId) =>
        ticketId is Guid id ? _tickets.SingleOrDefault(ticket => ticket.Id == id) : null;

    public void Add(WorkTicketSession ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        if (_tickets.Any(item => item.Id == ticket.Id)) throw new InvalidOperationException("Duplicate work ticket ID.");
        _tickets.Add(ticket);
    }

    public void Replace(WorkTicketSession ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        int index = _tickets.FindIndex(item => item.Id == ticket.Id);
        if (index < 0) throw new InvalidOperationException("Work ticket does not exist.");
        _tickets[index] = ticket;
    }

    public void Remove(Guid id)
    {
        int index = _tickets.FindIndex(item => item.Id == id);
        if (index < 0) throw new InvalidOperationException("Work ticket does not exist.");
        _tickets.RemoveAt(index);
    }
}
