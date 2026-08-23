using System.Globalization;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
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
    public const string CableTypePropertyKey = EditPropertyCommand.CableTypeProperty;
    public const string CableLengthPropertyKey = EditPropertyCommand.CableLengthProperty;
    public const string IntervalDisplayNamePropertyKey = "Interval.DisplayName";
    public const string RingCabinetDisplayNamePropertyKey =
        EditPropertyCommand.RingCabinetDisplayNameProperty;
    public const string RingCabinetLineNamePropertyKey =
        EditPropertyCommand.RingCabinetLineNameProperty;

    public bool TryCreateIntervalTypeChange(
        ResolvedSelection selection,
        RuntimeLayoutDocument runtimeLayout,
        IntervalKind targetIntervalKind,
        GroundingStructureKind? targetGroundingStructureKind,
        out ICommand? command,
        out PropertyEditError? error)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(runtimeLayout);
        command = null;
        error = null;

        if (selection.RingCabinet is null || selection.RingCabinetInterval is null ||
            selection.Reference.Kind != SelectionTargetKind.RingCabinetInterval)
        {
            error = new PropertyEditError(
                "TargetNotSupported",
                "Interval type editing requires a selected ring-cabinet interval.");
            return false;
        }

        if (!Enum.IsDefined(targetIntervalKind))
        {
            error = new PropertyEditError("InputInvalid", "The interval type is invalid.");
            return false;
        }

        if (targetIntervalKind == IntervalKind.IntegratedFeederInterval &&
            targetGroundingStructureKind is not GroundingStructureKind)
        {
            error = new PropertyEditError(
                "InputInvalid",
                "Integrated-feeder intervals require a grounding structure.");
            return false;
        }

        if (targetIntervalKind != IntervalKind.IntegratedFeederInterval &&
            targetGroundingStructureKind is not null)
        {
            error = new PropertyEditError(
                "InputInvalid",
                "Only integrated-feeder intervals accept a grounding structure.");
            return false;
        }

        if (selection.RingCabinetInterval.IntervalKind == targetIntervalKind &&
            selection.RingCabinetInterval.GroundingStructureKind == targetGroundingStructureKind)
        {
            error = new PropertyEditError("NoChange", "The interval configuration has not changed.");
            return false;
        }

        command = new ChangeIntervalTypeCommand(
            selection.RingCabinet,
            runtimeLayout,
            selection.RingCabinetInterval.IntervalId,
            targetIntervalKind,
            targetGroundingStructureKind);
        return true;
    }

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

        if (selection.RingCabinet is not null &&
            selection.RingCabinetInterval is null &&
            selection.Reference.Kind == SelectionTargetKind.RingCabinet)
        {
            if (propertyKey is not (RingCabinetDisplayNamePropertyKey or
                RingCabinetLineNamePropertyKey))
            {
                error = new PropertyEditError(
                    "PropertyReadOnly",
                    $"Property '{propertyKey}' is not editable.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(input))
            {
                error = new PropertyEditError(
                    "InputInvalid",
                    propertyKey == RingCabinetDisplayNamePropertyKey
                        ? "环网柜名称不能为空。"
                        : "线路名称不能为空。");
                return false;
            }

            string cabinetAfter = input.Trim();
            string before = propertyKey == RingCabinetDisplayNamePropertyKey
                ? selection.RingCabinet.DisplayName ?? string.Empty
                : selection.RingCabinet.LineName;
            if (cabinetAfter == before)
            {
                error = new PropertyEditError("NoChange", "名称没有变化。");
                return false;
            }

            command = new EditPropertyCommand(
                selection.RingCabinet,
                propertyKey,
                before,
                cabinetAfter);
            return true;
        }

        if (selection.RingCabinet is not null &&
            selection.RingCabinetInterval is not null &&
            selection.Reference.Kind == SelectionTargetKind.RingCabinetInterval)
        {
            if (propertyKey != IntervalDisplayNamePropertyKey)
            {
                error = new PropertyEditError(
                    "PropertyReadOnly",
                    $"Property '{propertyKey}' is not editable in this MVP.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(input))
            {
                error = new PropertyEditError("InputInvalid", "Interval name cannot be empty.");
                return false;
            }

            command = new RenameRingCabinetIntervalCommand(
                selection.RingCabinet,
                selection.RingCabinetInterval.IntervalId,
                input);
            return true;
        }

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

        if (selection.CableSegment is not null)
        {
            return TryCreateCableProperty(
                selection,
                propertyKey,
                input,
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

    private static bool TryCreateCableProperty(
        ResolvedSelection selection,
        string propertyKey,
        string input,
        out ICommand? command,
        out PropertyEditError? error)
    {
        command = null;
        error = null;
        if (selection.Reference.Kind != SelectionTargetKind.CableSegment ||
            selection.CableSegment is not { } cable)
        {
            error = new PropertyEditError(
                "TargetNotSupported",
                "电缆属性编辑需要选中电缆。");
            return false;
        }

        if (propertyKey == CableTypePropertyKey)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                error = new PropertyEditError("InputInvalid", "电缆型号不能为空。");
                return false;
            }

            string after = input.Trim();
            if (after == cable.CableType)
            {
                error = new PropertyEditError("NoChange", "电缆型号没有变化。");
                return false;
            }

            command = new EditPropertyCommand(
                cable,
                CableTypePropertyKey,
                cable.CableType,
                after);
            return true;
        }

        if (propertyKey == CableLengthPropertyKey)
        {
            if (!TryParseLength(input, out double after) || after <= 0)
            {
                error = new PropertyEditError(
                    "InputInvalid",
                    "请输入大于零的电缆长度。");
                return false;
            }

            if (after == cable.Length)
            {
                error = new PropertyEditError("NoChange", "电缆长度没有变化。");
                return false;
            }

            command = new EditPropertyCommand(
                cable,
                CableLengthPropertyKey,
                cable.Length,
                after);
            return true;
        }

        error = new PropertyEditError(
            "PropertyReadOnly",
            "电缆起点、终点和连接标识为只读属性。");
        return false;
    }

    private static bool TryParseLength(string input, out double length)
    {
        if (!double.TryParse(
                input,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out length) &&
            !double.TryParse(
                input,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out length))
        {
            return false;
        }

        return !double.IsNaN(length) && !double.IsInfinity(length);
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
