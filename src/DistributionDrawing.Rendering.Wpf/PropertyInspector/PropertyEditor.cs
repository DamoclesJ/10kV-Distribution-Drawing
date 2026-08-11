using DistributionDrawing.Rendering.Wpf.Interaction;

namespace DistributionDrawing.Rendering.Wpf.PropertyInspector;

public sealed class PropertyEditor
{
    private readonly SelectionObjectResolver _resolver;
    private readonly PropertyCommandFactory _commandFactory;
    private readonly CommandStack _commandStack;

    public PropertyEditor(
        SelectionObjectResolver resolver,
        CommandStack commandStack,
        PropertyCommandFactory? commandFactory = null)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(commandStack);

        _resolver = resolver;
        _commandFactory = commandFactory ?? new PropertyCommandFactory();
        _commandStack = commandStack;
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
