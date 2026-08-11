using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class MoveCommand : ICommand
{
    private readonly DrawingLayout? _layout;

    public MoveCommand(PoleLayout before, PoleLayout after)
        : this(null, before, after)
    {
    }

    public MoveCommand(
        DrawingLayout? layout,
        PoleLayout before,
        PoleLayout after)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        if (before.PoleId != after.PoleId)
        {
            throw new ArgumentException(
                "Move command states must refer to the same pole.",
                nameof(after));
        }

        _layout = layout;
        Before = before;
        After = after;
    }

    public PoleLayout Before { get; }

    public PoleLayout After { get; }

    public void Execute()
    {
        Apply(_layout, After);
    }

    public void Undo()
    {
        Apply(_layout, Before);
    }

    public void Redo()
    {
        Execute();
    }

    public void Execute(DrawingLayout layout)
    {
        Apply(layout, After);
    }

    public void Undo(DrawingLayout layout)
    {
        Apply(layout, Before);
    }

    private static void Apply(DrawingLayout? layout, PoleLayout value)
    {
        if (layout is null)
        {
            throw new InvalidOperationException(
                "This move command is not bound to a drawing layout.");
        }

        layout.Replace(value);
    }
}
