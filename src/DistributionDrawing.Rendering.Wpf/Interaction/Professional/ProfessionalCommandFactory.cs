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

    public ICommand CreateAddWorkScope(
        DrawingDocument document,
        BoundaryPointCommandValue startBoundary,
        BoundaryPointCommandValue endBoundary,
        string description,
        IEnumerable<Guid>? groundingPointIds = null,
        Guid? workScopeId = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(startBoundary);
        ArgumentNullException.ThrowIfNull(endBoundary);

        return new AddWorkScopeCommand(
            document,
            new WorkScopeCommandSnapshot(
                workScopeId ?? Guid.NewGuid(),
                startBoundary,
                endBoundary,
                NormalizeRequired(description, "Work scope description cannot be empty."),
                NormalizeIds(groundingPointIds)));
    }

    public ICommand CreateRemoveWorkScope(
        DrawingDocument document,
        Guid workScopeId)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new RemoveWorkScopeCommand(
            document,
            WorkScopeCommandSnapshot.From(document.GetWorkScope(workScopeId)));
    }

    /// <summary>
    /// The first WorkScope editor only changes Description and existing
    /// GroundingPointId references. Boundary values are retained verbatim;
    /// rebinding them belongs to a later explicit Pick workflow.
    /// </summary>
    public ICommand CreateChangeWorkScope(
        DrawingDocument document,
        Guid workScopeId,
        string description,
        IEnumerable<Guid>? groundingPointIds)
    {
        ArgumentNullException.ThrowIfNull(document);

        WorkScopeCommandSnapshot before =
            WorkScopeCommandSnapshot.From(document.GetWorkScope(workScopeId));
        WorkScopeCommandSnapshot after = before with
        {
            Description = NormalizeRequired(
                description,
                "Work scope description cannot be empty."),
            GroundingPointIds = NormalizeIds(groundingPointIds)
        };

        if (before.Description == after.Description &&
            before.StartBoundary == after.StartBoundary &&
            before.EndBoundary == after.EndBoundary &&
            before.GroundingPointIds.SequenceEqual(after.GroundingPointIds))
        {
            throw new InvalidOperationException("No WorkScope property has changed.");
        }

        return new ChangeWorkScopeCommand(document, before, after);
    }

    private static IReadOnlyList<Guid> NormalizeIds(IEnumerable<Guid>? ids)
    {
        return (ids ?? Array.Empty<Guid>()).ToArray();
    }

    private static string NormalizeRequired(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, nameof(value));
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
