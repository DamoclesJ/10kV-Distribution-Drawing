namespace DistributionDrawing.Domain.Professional;

/// <summary>
/// A user-defined temporary work grounding location. The terminal is the
/// only persisted topology reference; device ownership is resolved from it.
/// </summary>
public sealed class GroundingPoint
{
    private GroundingPoint(
        Guid groundingPointId,
        Guid terminalId,
        string location,
        string? number,
        string? note)
    {
        if (groundingPointId == Guid.Empty)
        {
            throw new ArgumentException(
                "Grounding point ID cannot be empty.",
                nameof(groundingPointId));
        }

        if (terminalId == Guid.Empty)
        {
            throw new ArgumentException(
                "Grounding terminal ID cannot be empty.",
                nameof(terminalId));
        }

        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException("Grounding point location is required.", nameof(location));
        }

        GroundingPointId = groundingPointId;
        TerminalId = terminalId;
        Location = location.Trim();
        Number = NormalizeOptional(number);
        Note = NormalizeOptional(note);
    }

    public Guid GroundingPointId { get; }

    public Guid TerminalId { get; private set; }

    public string Location { get; private set; }

    public string? Number { get; private set; }

    public string? Note { get; private set; }

    public static GroundingPoint Create(
        Guid groundingPointId,
        Guid terminalId,
        string location,
        string? number = null,
        string? note = null)
    {
        return new GroundingPoint(
            groundingPointId,
            terminalId,
            location,
            number,
            note);
    }

    internal void Update(
        Guid terminalId,
        string location,
        string? number,
        string? note)
    {
        GroundingPoint replacement = Create(
            GroundingPointId,
            terminalId,
            location,
            number,
            note);

        TerminalId = replacement.TerminalId;
        Location = replacement.Location;
        Number = replacement.Number;
        Note = replacement.Note;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
