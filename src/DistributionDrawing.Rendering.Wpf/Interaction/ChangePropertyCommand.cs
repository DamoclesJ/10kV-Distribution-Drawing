using DistributionDrawing.Domain.Devices;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class ChangePropertyCommand : ICommand
{
    private readonly Pole _pole;

    public ChangePropertyCommand(
        Pole pole,
        string propertyKey,
        string before,
        string after)
    {
        ArgumentNullException.ThrowIfNull(pole);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(before);
        ArgumentException.ThrowIfNullOrWhiteSpace(after);

        if (propertyKey != PropertyCommandFactory.PoleNumberPropertyKey)
        {
            throw new ArgumentException(
                $"Property '{propertyKey}' is not supported by this command.",
                nameof(propertyKey));
        }

        _pole = pole;
        PropertyKey = propertyKey;
        Before = before.Trim();
        After = after.Trim();
    }

    public string PropertyKey { get; }

    public string Before { get; }

    public string After { get; }

    public void Execute()
    {
        _pole.RenamePoleNumber(After);
    }

    public void Undo()
    {
        _pole.RenamePoleNumber(Before);
    }

    public void Redo()
    {
        Execute();
    }
}
