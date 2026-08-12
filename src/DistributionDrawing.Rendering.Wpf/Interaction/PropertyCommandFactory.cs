using DistributionDrawing.Rendering.Wpf.Interaction.Professional;
using DistributionDrawing.Rendering.Wpf.PropertyInspector;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class PropertyCommandFactory
{
    public const string PoleNumberPropertyKey = "Pole.PoleNumber";
    public const string GroundingPointNumberPropertyKey = "GroundingPoint.Number";
    public const string GroundingPointLocationPropertyKey = "GroundingPoint.Location";
    public const string GroundingPointNotePropertyKey = "GroundingPoint.Note";
    public const string WorkScopeDescriptionPropertyKey = "WorkScope.Description";

    public bool TryCreate(
        ResolvedSelection selection,
        string propertyKey,
        string input,
        out ICommand? command,
        out PropertyEditError? error)
    {
        ArgumentNullException.ThrowIfNull(selection);

        command = null;
        error = null;

        if (selection.GroundingPoint is not null)
        {
            return TryCreateGroundingPointCommand(
                selection,
                propertyKey,
                input,
                out command,
                out error);
        }

        if (selection.WorkScope is not null)
        {
            if (propertyKey != WorkScopeDescriptionPropertyKey)
            {
                error = new PropertyEditError(
                    "PropertyReadOnly",
                    $"Property '{propertyKey}' is not editable in this MVP.");
                return false;
            }

            return TryCreateWorkScope(
                selection,
                input,
                selection.WorkScope.GroundingPointIds,
                out command,
                out error);
        }

        if (propertyKey != PoleNumberPropertyKey)
        {
            error = new PropertyEditError(
                "PropertyReadOnly",
                $"Property '{propertyKey}' is not editable in this MVP.");
            return false;
        }

        if (selection.Pole is null || selection.Reference.Kind != SelectionTargetKind.Device)
        {
            error = new PropertyEditError(
                "TargetNotSupported",
                "Pole number editing requires a selected pole.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            error = new PropertyEditError(
                "InputInvalid",
                "Pole number cannot be empty.");
            return false;
        }

        string after = input.Trim();
        if (after == selection.Pole.PoleNumber)
        {
            error = new PropertyEditError(
                "NoChange",
                "Pole number has not changed.");
            return false;
        }

        command = new ChangePropertyCommand(
            selection.Pole,
            PoleNumberPropertyKey,
            selection.Pole.PoleNumber,
            after);
        return true;
    }

    public bool TryCreateGroundingPoint(
        ResolvedSelection selection,
        string location,
        string? number,
        string? note,
        out ICommand? command,
        out PropertyEditError? error)
    {
        ArgumentNullException.ThrowIfNull(selection);

        command = null;
        error = null;
        if (selection.GroundingPoint is null ||
            selection.Reference.Kind != SelectionTargetKind.GroundingPoint ||
            selection.Document is null)
        {
            error = new PropertyEditError(
                "TargetNotSupported",
                "Grounding point editing requires a document-backed selection.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(location))
        {
            error = new PropertyEditError(
                "InputInvalid",
                "Grounding point location cannot be empty.");
            return false;
        }

        GroundingPointCommandSnapshot before =
            GroundingPointCommandSnapshot.From(selection.GroundingPoint);
        GroundingPointCommandSnapshot after = before with
        {
            Location = location.Trim(),
            Number = NormalizeOptional(number),
            Note = NormalizeOptional(note)
        };
        if (before == after)
        {
            error = new PropertyEditError("NoChange", "No grounding point property has changed.");
            return false;
        }

        command = new ChangeGroundingPointCommand(
            selection.Document,
            before,
            after);
        return true;
    }

    public bool TryCreateWorkScope(
        ResolvedSelection selection,
        string description,
        IEnumerable<Guid>? groundingPointIds,
        out ICommand? command,
        out PropertyEditError? error)
    {
        ArgumentNullException.ThrowIfNull(selection);

        command = null;
        error = null;
        if (selection.WorkScope is null ||
            selection.Reference.Kind != SelectionTargetKind.WorkScope ||
            selection.Document is null)
        {
            error = new PropertyEditError(
                "TargetNotSupported",
                "WorkScope editing requires a document-backed selection.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            error = new PropertyEditError(
                "InputInvalid",
                "Work scope description cannot be empty.");
            return false;
        }

        Guid[] ids = (groundingPointIds ?? Array.Empty<Guid>()).ToArray();
        if (ids.Distinct().Count() != ids.Length)
        {
            error = new PropertyEditError(
                "InputInvalid",
                "A WorkScope cannot reference the same grounding point twice.");
            return false;
        }

        WorkScopeCommandSnapshot before =
            WorkScopeCommandSnapshot.From(selection.WorkScope);
        WorkScopeCommandSnapshot after = before with
        {
            Description = description.Trim(),
            GroundingPointIds = ids
        };
        if (before.Description == after.Description &&
            before.GroundingPointIds.SequenceEqual(after.GroundingPointIds))
        {
            error = new PropertyEditError(
                "NoChange",
                "No WorkScope property has changed.");
            return false;
        }

        command = new ChangeWorkScopeCommand(
            selection.Document,
            before,
            after);
        return true;
    }

    private static bool TryCreateGroundingPointCommand(
        ResolvedSelection selection,
        string propertyKey,
        string input,
        out ICommand? command,
        out PropertyEditError? error)
    {
        command = null;
        error = null;

        if (selection.Reference.Kind != SelectionTargetKind.GroundingPoint ||
            selection.Document is null)
        {
            error = new PropertyEditError(
                "TargetNotSupported",
                "Grounding point editing requires a document-backed selection.");
            return false;
        }

        GroundingPointCommandSnapshot before =
            GroundingPointCommandSnapshot.From(selection.GroundingPoint!);
        GroundingPointCommandSnapshot after = propertyKey switch
        {
            GroundingPointNumberPropertyKey => before with
            {
                Number = NormalizeOptional(input)
            },
            GroundingPointLocationPropertyKey => before with
            {
                Location = input.Trim()
            },
            GroundingPointNotePropertyKey => before with
            {
                Note = NormalizeOptional(input)
            },
            _ => before
        };

        if (propertyKey is not GroundingPointNumberPropertyKey and
            not GroundingPointLocationPropertyKey and
            not GroundingPointNotePropertyKey)
        {
            error = new PropertyEditError(
                "PropertyReadOnly",
                $"Property '{propertyKey}' is not editable in this MVP.");
            return false;
        }

        if (propertyKey == GroundingPointLocationPropertyKey &&
            string.IsNullOrWhiteSpace(input))
        {
            error = new PropertyEditError(
                "InputInvalid",
                "Grounding point location cannot be empty.");
            return false;
        }

        if (after == before)
        {
            error = new PropertyEditError("NoChange", "The property has not changed.");
            return false;
        }

        command = new ChangeGroundingPointCommand(
            selection.Document,
            before,
            after);
        return true;
    }

    private static string? NormalizeOptional(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record PropertyEditError(string Code, string Message);
