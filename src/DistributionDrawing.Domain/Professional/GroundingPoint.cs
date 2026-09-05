namespace DistributionDrawing.Domain.Professional;

/// <summary>
/// A user-defined temporary work grounding location bound to one typed target.
/// </summary>
public sealed class GroundingPoint
{
    private GroundingPoint(
        Guid groundingPointId,
        GroundingTarget target,
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

        ArgumentNullException.ThrowIfNull(target);

        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException("Grounding point location is required.", nameof(location));
        }

        GroundingPointId = groundingPointId;
        Target = target;
        Location = location.Trim();
        Number = NormalizeOptional(number);
        Note = NormalizeOptional(note);
    }

    public Guid GroundingPointId { get; }

    public GroundingTarget Target { get; private set; }

    // Compatibility view for legacy callers; typed targets should use Target.
    public Guid TerminalId => Target.Kind == GroundingTargetKind.Terminal
        ? Target.TargetId
        : Guid.Empty;

    public string Location { get; private set; }

    public string? Number { get; private set; }

    public string? Note { get; private set; }

    public static GroundingPoint Create(
        Guid groundingPointId,
        GroundingTarget target,
        string location,
        string? number = null,
        string? note = null)
    {
        return new GroundingPoint(
            groundingPointId,
            target,
            location,
            number,
            note);
    }

    public static GroundingPoint Create(
        Guid groundingPointId,
        Guid terminalId,
        string location,
        string? number = null,
        string? note = null) => Create(
            groundingPointId,
            GroundingTarget.ForTerminal(terminalId),
            location,
            number,
            note);

    internal void Update(
        GroundingTarget target,
        string location,
        string? number,
        string? note)
    {
        GroundingPoint replacement = Create(
            GroundingPointId,
            target,
            location,
            number,
            note);

        Target = replacement.Target;
        Location = replacement.Location;
        Number = replacement.Number;
        Note = replacement.Note;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
