using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class MoveAttachmentCommand : ICommand
{
    private readonly DrawingLayout _layout;

    public MoveAttachmentCommand(
        DrawingLayout layout,
        Guid attachmentId,
        DocumentPoint beforeOffset,
        DocumentPoint afterOffset)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        if (attachmentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Attachment ID cannot be empty.",
                nameof(attachmentId));
        }

        AttachmentId = attachmentId;
        BeforeOffset = beforeOffset;
        AfterOffset = afterOffset;
    }

    public Guid AttachmentId { get; }

    public DocumentPoint BeforeOffset { get; }

    public DocumentPoint AfterOffset { get; }

    public void Execute() => Apply(AfterOffset);

    public void Undo() => Apply(BeforeOffset);

    public void Redo() => Execute();

    private void Apply(DocumentPoint offset)
    {
        if (!_layout.Attachments.TryGetValue(
                AttachmentId,
                out AttachmentLayout? current))
        {
            throw new InvalidOperationException(
                $"No layout exists for attachment '{AttachmentId}'.");
        }

        _layout.Replace(current.MoveTo(offset));
    }
}
