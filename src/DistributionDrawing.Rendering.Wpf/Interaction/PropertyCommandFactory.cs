using DistributionDrawing.Rendering.Wpf.PropertyInspector;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class PropertyCommandFactory
{
    public const string PoleNumberPropertyKey = "Pole.PoleNumber";

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
}

public sealed record PropertyEditError(string Code, string Message);
