namespace DistributionDrawing.Domain.Topology;

public sealed class IntermediateTerminal
{
    public IntermediateTerminal(
        Guid id,
        string displayName,
        Guid terminalId)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Intermediate terminal ID cannot be empty.",
                nameof(id));
        }

        if (terminalId == Guid.Empty)
        {
            throw new ArgumentException(
                "Intermediate terminal child terminal ID cannot be empty.",
                nameof(terminalId));
        }

        if (id == terminalId)
        {
            throw new ArgumentException(
                "Intermediate terminal owner and child terminal IDs must be different.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException(
                "Intermediate terminal display name is required.",
                nameof(displayName));
        }

        Id = id;
        DisplayName = displayName.Trim();
        TerminalId = terminalId;
    }

    public Guid Id { get; }

    public string DisplayName { get; }

    public Guid TerminalId { get; }
}
