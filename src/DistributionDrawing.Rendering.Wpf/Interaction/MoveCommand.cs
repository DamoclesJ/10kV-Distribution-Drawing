using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class MoveCommand
{
    public MoveCommand(PoleLayout before, PoleLayout after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        if (before.PoleId != after.PoleId)
        {
            throw new ArgumentException(
                "Move command states must refer to the same pole.",
                nameof(after));
        }

        Before = before;
        After = after;
    }

    public PoleLayout Before { get; }

    public PoleLayout After { get; }

    public void Execute(DrawingLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        layout.Replace(After);
    }

    public void Undo(DrawingLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        layout.Replace(Before);
    }
}
