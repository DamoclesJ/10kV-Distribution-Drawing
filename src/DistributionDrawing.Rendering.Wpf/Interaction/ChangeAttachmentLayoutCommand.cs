using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class ChangeAttachmentLayoutCommand : ICommand
{
    private readonly DrawingLayout _layout;

    public ChangeAttachmentLayoutCommand(
        DrawingLayout layout,
        AttachmentLayout before,
        AttachmentLayout after)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));

        if (Before.AttachmentId != After.AttachmentId)
        {
            throw new ArgumentException(
                "Attachment layout snapshots must use the same attachment ID.");
        }
    }

    public AttachmentLayout Before { get; }

    public AttachmentLayout After { get; }

    public Guid AttachmentId => Before.AttachmentId;

    public void Execute() => Apply(After);

    public void Undo() => Apply(Before);

    public void Redo() => Execute();

    private void Apply(AttachmentLayout layout)
    {
        _layout.Replace(layout);
    }
}
