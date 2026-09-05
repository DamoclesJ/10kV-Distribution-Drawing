using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Domain.Devices.RingCabinets;

namespace DistributionDrawing.Rendering.Wpf.PropertyInspector;

public sealed class PropertyEditor
{
    private readonly SelectionObjectResolver _resolver;
    private readonly PropertyCommandFactory _commandFactory;
    private readonly CommandStack _commandStack;
    private readonly RuntimeLayoutDocument? _runtimeLayout;

    public PropertyEditor(
        SelectionObjectResolver resolver,
        CommandStack commandStack,
        RuntimeLayoutDocument? runtimeLayout = null,
        PropertyCommandFactory? commandFactory = null)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(commandStack);

        _resolver = resolver;
        _commandFactory = commandFactory ?? new PropertyCommandFactory();
        _commandStack = commandStack;
        _runtimeLayout = runtimeLayout;
    }

    public PropertyEditResult TryEdit(
        SelectionReference target,
        string propertyKey,
        string input)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyKey);

        ResolvedSelection? selection = _resolver.Resolve(target);
        if (selection is null)
        {
            return PropertyEditResult.Failure(
                "TargetNotFound",
                "The selected object no longer exists.");
        }

        if (!_commandFactory.TryCreate(
                selection,
                propertyKey,
                input,
                out ICommand? command,
                out PropertyEditError? error))
        {
            PropertyEditError failure = error ??
                new PropertyEditError("PropertyInvalid", "The property edit was rejected.");
            return PropertyEditResult.Failure(failure.Code, failure.Message);
        }

        try
        {
            _commandStack.ExecuteCommand(command!);
            return PropertyEditResult.Success();
        }
        catch (ArgumentException exception)
        {
            return PropertyEditResult.Failure("DomainRuleViolation", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return PropertyEditResult.Failure("DomainRuleViolation", exception.Message);
        }
    }

    public PropertyEditResult TryChangeIntervalType(
        SelectionReference target,
        IntervalKind intervalKind,
        GroundingStructureKind? groundingStructureKind)
    {
        ArgumentNullException.ThrowIfNull(target);
        ResolvedSelection? selection = _resolver.Resolve(target);
        if (selection is null)
        {
            return PropertyEditResult.Failure("TargetNotFound", "The selected interval no longer exists.");
        }

        if (_runtimeLayout is null)
        {
            return PropertyEditResult.Failure(
                "LayoutUnavailable",
                "The ring-cabinet runtime layout is unavailable.");
        }

        if (!_commandFactory.TryCreateIntervalTypeChange(
                selection,
                _runtimeLayout,
                intervalKind,
                groundingStructureKind,
                out ICommand? command,
                out PropertyEditError? error))
        {
            PropertyEditError failure = error ??
                new PropertyEditError("PropertyInvalid", "The interval change was rejected.");
            return PropertyEditResult.Failure(failure.Code, failure.Message);
        }

        try
        {
            _commandStack.ExecuteCommand(command!);
            return PropertyEditResult.Success();
        }
        catch (ArgumentException exception)
        {
            return PropertyEditResult.Failure("DomainRuleViolation", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return PropertyEditResult.Failure("DomainRuleViolation", exception.Message);
        }
    }

    public PropertyEditResult TrySetIntervalCableTerminalPresence(
        SelectionReference target,
        bool isPresent)
    {
        ArgumentNullException.ThrowIfNull(target);
        ResolvedSelection? selection = _resolver.Resolve(target);
        if (selection is null)
        {
            return PropertyEditResult.Failure(
                "TargetNotFound",
                "The selected interval no longer exists.");
        }

        if (!_commandFactory.TryCreateIntervalCableTerminalPresenceChange(
                selection,
                isPresent,
                out ICommand? command,
                out PropertyEditError? error))
        {
            PropertyEditError failure = error ??
                new PropertyEditError("PropertyInvalid", "The cable-terminal change was rejected.");
            return PropertyEditResult.Failure(failure.Code, failure.Message);
        }

        try
        {
            _commandStack.ExecuteCommand(command!);
            return PropertyEditResult.Success();
        }
        catch (ArgumentException exception)
        {
            return PropertyEditResult.Failure("DomainRuleViolation", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return PropertyEditResult.Failure("DomainRuleViolation", exception.Message);
        }
    }

    public PropertyEditResult TryEditGroundingPoint(
        SelectionReference target,
        string location,
        string? number,
        string? note)
    {
        ArgumentNullException.ThrowIfNull(target);

        ResolvedSelection? selection = _resolver.Resolve(target);
        if (selection is null)
        {
            return PropertyEditResult.Failure(
                "TargetNotFound",
                "The selected object no longer exists.");
        }

        if (!_commandFactory.TryCreateGroundingPoint(
                selection,
                location,
                number,
                note,
                out ICommand? command,
                out PropertyEditError? error))
        {
            PropertyEditError failure = error ??
                new PropertyEditError("PropertyInvalid", "The property edit was rejected.");
            return PropertyEditResult.Failure(failure.Code, failure.Message);
        }

        try
        {
            _commandStack.ExecuteCommand(command!);
            return PropertyEditResult.Success();
        }
        catch (ArgumentException exception)
        {
            return PropertyEditResult.Failure("DomainRuleViolation", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return PropertyEditResult.Failure("DomainRuleViolation", exception.Message);
        }
    }

    public PropertyEditResult TryEditAttachmentOffset(
        RuntimeLayoutDocument runtimeLayout,
        SelectionReference target,
        string offsetX,
        string offsetY,
        out ICommand? executedCommand)
    {
        ArgumentNullException.ThrowIfNull(runtimeLayout);
        ArgumentNullException.ThrowIfNull(target);
        executedCommand = null;

        ResolvedSelection? selection = _resolver.Resolve(target);
        if (selection is null)
        {
            return PropertyEditResult.Failure(
                "TargetNotFound",
                "The selected object no longer exists.");
        }

        if (!_commandFactory.TryCreateAttachmentOffset(
                selection,
                runtimeLayout,
                offsetX,
                offsetY,
                out ICommand? command,
                out PropertyEditError? error))
        {
            PropertyEditError failure = error ??
                new PropertyEditError("PropertyInvalid", "The attachment offset edit was rejected.");
            return PropertyEditResult.Failure(failure.Code, failure.Message);
        }

        try
        {
            _commandStack.ExecuteCommand(command!);
            executedCommand = command;
            return PropertyEditResult.Success();
        }
        catch (ArgumentException exception)
        {
            return PropertyEditResult.Failure("LayoutRuleViolation", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return PropertyEditResult.Failure("LayoutRuleViolation", exception.Message);
        }
    }

    public PropertyEditResult TryEditCableTerminationDisplayName(
        SelectionReference target,
        string input,
        out ICommand? executedCommand)
    {
        ArgumentNullException.ThrowIfNull(target);
        executedCommand = null;

        ResolvedSelection? selection = _resolver.Resolve(target);
        if (selection is null)
        {
            return PropertyEditResult.Failure(
                "TargetNotFound",
                "The selected object no longer exists.");
        }

        if (!_commandFactory.TryCreateCableTerminationDisplayName(
                selection,
                input,
                out ICommand? command,
                out PropertyEditError? error))
        {
            PropertyEditError failure = error ??
                new PropertyEditError("PropertyInvalid", "The property edit was rejected.");
            return PropertyEditResult.Failure(failure.Code, failure.Message);
        }

        try
        {
            _commandStack.ExecuteCommand(command!);
            executedCommand = command;
            return PropertyEditResult.Success();
        }
        catch (ArgumentException exception)
        {
            return PropertyEditResult.Failure("DomainRuleViolation", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return PropertyEditResult.Failure("DomainRuleViolation", exception.Message);
        }
    }

    public PropertyEditResult TryEditAttachmentLayout(
        RuntimeLayoutDocument runtimeLayout,
        SelectionReference target,
        string width,
        string height,
        string labelOffsetX,
        string labelOffsetY,
        out ICommand? executedCommand)
    {
        ArgumentNullException.ThrowIfNull(runtimeLayout);
        ArgumentNullException.ThrowIfNull(target);
        executedCommand = null;

        ResolvedSelection? selection = _resolver.Resolve(target);
        if (selection is null)
        {
            return PropertyEditResult.Failure(
                "TargetNotFound",
                "The selected object no longer exists.");
        }

        if (!_commandFactory.TryCreateAttachmentLayout(
                selection,
                runtimeLayout,
                width,
                height,
                labelOffsetX,
                labelOffsetY,
                out ICommand? command,
                out PropertyEditError? error))
        {
            PropertyEditError failure = error ??
                new PropertyEditError("PropertyInvalid", "The property edit was rejected.");
            return PropertyEditResult.Failure(failure.Code, failure.Message);
        }

        try
        {
            _commandStack.ExecuteCommand(command!);
            executedCommand = command;
            return PropertyEditResult.Success();
        }
        catch (ArgumentException exception)
        {
            return PropertyEditResult.Failure("LayoutRuleViolation", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return PropertyEditResult.Failure("LayoutRuleViolation", exception.Message);
        }
    }

    public PropertyEditResult TryEditWorkScope(
        SelectionReference target,
        string description,
        IEnumerable<Guid>? groundingPointIds)
    {
        ArgumentNullException.ThrowIfNull(target);

        ResolvedSelection? selection = _resolver.Resolve(target);
        if (selection is null)
        {
            return PropertyEditResult.Failure(
                "TargetNotFound",
                "The selected object no longer exists.");
        }

        if (!_commandFactory.TryCreateWorkScope(
                selection,
                description,
                groundingPointIds,
                out ICommand? command,
                out PropertyEditError? error))
        {
            PropertyEditError failure = error ??
                new PropertyEditError("PropertyInvalid", "The WorkScope edit was rejected.");
            return PropertyEditResult.Failure(failure.Code, failure.Message);
        }

        try
        {
            _commandStack.ExecuteCommand(command!);
            return PropertyEditResult.Success();
        }
        catch (ArgumentException exception)
        {
            return PropertyEditResult.Failure("DomainRuleViolation", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return PropertyEditResult.Failure("DomainRuleViolation", exception.Message);
        }
    }
}

public sealed record PropertyEditResult(
    bool IsSuccess,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static PropertyEditResult Success() => new(true, null, null);

    public static PropertyEditResult Failure(string code, string message) =>
        new(false, code, message);
}
