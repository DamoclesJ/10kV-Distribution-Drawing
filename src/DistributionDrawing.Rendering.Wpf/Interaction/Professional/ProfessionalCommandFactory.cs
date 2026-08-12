using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Professional;

public sealed class ProfessionalCommandFactory
{
    public ICommand CreateAddGroundingPoint(
        DrawingDocument document,
        Guid terminalId,
        string location,
        string? number = null,
        string? note = null,
        Guid? groundingPointId = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (terminalId == Guid.Empty)
        {
            throw new ArgumentException(
                "A grounding point requires a terminal.",
                nameof(terminalId));
        }

        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException(
                "Grounding point location cannot be empty.",
                nameof(location));
        }

        return new AddGroundingPointCommand(
            document,
            new GroundingPointCommandSnapshot(
                groundingPointId ?? Guid.NewGuid(),
                terminalId,
                location.Trim(),
                NormalizeOptional(number),
                NormalizeOptional(note)));
    }

    public ICommand CreateRemoveGroundingPoint(
        DrawingDocument document,
        Guid groundingPointId)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new RemoveGroundingPointCommand(
            document,
            GroundingPointCommandSnapshot.From(
                document.GetGroundingPoint(groundingPointId)));
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
