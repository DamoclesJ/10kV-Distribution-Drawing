using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Layout;

public sealed record AttachmentLayout
{
    public AttachmentLayout(
        Guid attachmentId,
        DocumentPoint offset,
        double widthMillimeters = 18,
        double heightMillimeters = 10,
        DocumentPoint? labelOffset = null)
    {
        if (attachmentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Attachment ID cannot be empty.",
                nameof(attachmentId));
        }

        if (widthMillimeters <= 0 || heightMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(heightMillimeters),
                "Attachment layout dimensions must be greater than zero.");
        }

        AttachmentId = attachmentId;
        Offset = offset;
        WidthMillimeters = widthMillimeters;
        HeightMillimeters = heightMillimeters;
        LabelOffset = labelOffset ?? new DocumentPoint(0, -4);
    }

    public Guid AttachmentId { get; }

    public DocumentPoint Offset { get; }

    public double WidthMillimeters { get; }

    public double HeightMillimeters { get; }

    public DocumentPoint LabelOffset { get; }
}
