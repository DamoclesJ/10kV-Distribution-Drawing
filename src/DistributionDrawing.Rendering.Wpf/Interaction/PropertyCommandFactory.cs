using System.Globalization;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Interaction.Professional;
using DistributionDrawing.Rendering.Wpf.PropertyInspector;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class PropertyCommandFactory
{
    private readonly DeviceCommandFactory _deviceCommandFactory;

    public PropertyCommandFactory(DeviceCommandFactory? deviceCommandFactory = null)
    {
        _deviceCommandFactory = deviceCommandFactory ?? new DeviceCommandFactory();
    }

    public const string PoleNumberPropertyKey = "Pole.PoleNumber";
    public const string GroundingPointNumberPropertyKey = "GroundingPoint.Number";
    public const string GroundingPointLocationPropertyKey = "GroundingPoint.Location";
    public const string GroundingPointNotePropertyKey = "GroundingPoint.Note";
    public const string WorkScopeDescriptionPropertyKey = "WorkScope.Description";
    public const string CableTerminationDisplayNamePropertyKey =
        "CableTermination.DisplayName";

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

    public bool TryCreateAttachmentOffset(
        ResolvedSelection selection,
        RuntimeLayoutDocument runtimeLayout,
        string offsetX,
        string offsetY,
        out ICommand? command,
        out PropertyEditError? error)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(runtimeLayout);

        command = null;
        error = null;
        if (selection.Reference.Kind != SelectionTargetKind.PoleAttachment ||
            selection.PoleAttachment is null ||
            selection.AttachmentLayout is null)
        {
            error = new PropertyEditError(
                "TargetNotSupported",
                "Attachment offset editing requires a selected pole attachment with layout.");
            return false;
        }

        if (!TryParseFiniteCoordinate(offsetX, out double x) ||
            !TryParseFiniteCoordinate(offsetY, out double y))
        {
            error = new PropertyEditError(
                "InputInvalid",
                "Attachment offset coordinates must be finite numbers.");
            return false;
        }

        Guid attachmentId = selection.PoleAttachment.AttachmentId;
        if (!runtimeLayout.DrawingLayout.Attachments.TryGetValue(
                attachmentId,
                out AttachmentLayout? current))
        {
            error = new PropertyEditError(
                "TargetNotFound",
                "The selected attachment layout no longer exists.");
            return false;
        }

        var after = new DocumentPoint(x, y);
        if (after == current.Offset)
        {
            error = new PropertyEditError(
                "NoChange",
                "Attachment offset has not changed.");
            return false;
        }

        command = _deviceCommandFactory.CreateMoveAttachment(
            runtimeLayout,
            attachmentId,
            after);
        return true;
    }

    public bool TryCreateCableTerminationDisplayName(
        ResolvedSelection selection,
        string input,
        out ICommand? command,
        out PropertyEditError? error)
    {
        ArgumentNullException.ThrowIfNull(selection);

        command = null;
        error = null;
        if (selection.Reference.Kind != SelectionTargetKind.PoleAttachment ||
            selection.AttachedDevice is not CableTermination cableTermination)
        {
            error = new PropertyEditError(
                "TargetNotSupported",
                "Cable termination name editing requires a selected cable termination attachment.");
            return false;
        }

        string? after = NormalizeOptional(input);
        if (after == cableTermination.DisplayName)
        {
            error = new PropertyEditError(
                "NoChange",
                "Cable termination name has not changed.");
            return false;
        }

        command = _deviceCommandFactory.CreateRenameCableTermination(
            cableTermination,
            after);
        return true;
    }

    public bool TryCreateAttachmentLayout(
        ResolvedSelection selection,
        RuntimeLayoutDocument runtimeLayout,
        string width,
        string height,
        string labelOffsetX,
        string labelOffsetY,
        out ICommand? command,
        out PropertyEditError? error)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(runtimeLayout);

        command = null;
        error = null;
        if (selection.Reference.Kind != SelectionTargetKind.PoleAttachment ||
            selection.PoleAttachment is null)
        {
            error = new PropertyEditError(
                "TargetNotSupported",
                "Attachment layout editing requires a selected pole attachment.");
            return false;
        }

        if (!TryParseFiniteCoordinate(width, out double widthValue) ||
            !TryParseFiniteCoordinate(height, out double heightValue) ||
            !TryParseFiniteCoordinate(labelOffsetX, out double labelX) ||
            !TryParseFiniteCoordinate(labelOffsetY, out double labelY))
        {
            error = new PropertyEditError(
                "InputInvalid",
                "Attachment layout values must be finite numbers.");
            return false;
        }

        if (widthValue <= 0 || heightValue <= 0)
        {
            error = new PropertyEditError(
                "InputInvalid",
                "Attachment width and height must be greater than zero.");
            return false;
        }

        Guid attachmentId = selection.PoleAttachment.AttachmentId;
        if (!runtimeLayout.DrawingLayout.Attachments.TryGetValue(
                attachmentId,
                out AttachmentLayout? current))
        {
            error = new PropertyEditError(
                "TargetNotFound",
                "The selected attachment layout no longer exists.");
            return false;
        }

        AttachmentLayout after = current
            .Resize(widthValue, heightValue)
            .WithLabelOffset(new DocumentPoint(labelX, labelY));
        if (after == current)
        {
            error = new PropertyEditError(
                "NoChange",
                "Attachment layout has not changed.");
            return false;
        }

        command = _deviceCommandFactory.CreateChangeAttachmentLayout(
            runtimeLayout,
            attachmentId,
            widthValue,
            heightValue,
            new DocumentPoint(labelX, labelY));
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

    private static bool TryParseFiniteCoordinate(string input, out double value)
    {
        return double.TryParse(
                input,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value) &&
            double.IsFinite(value);
    }
}

public sealed record PropertyEditError(string Code, string Message);
