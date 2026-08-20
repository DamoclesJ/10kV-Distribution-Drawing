using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Metrics;

namespace DistributionDrawing.Rendering.Wpf.Layout;

public sealed record AttachmentLayout
{
    public AttachmentLayout(
        Guid attachmentId,
        DocumentPoint offset,
        double? widthMillimeters = null,
        double? heightMillimeters = null,
        DocumentPoint? labelOffset = null)
    {
        DrawingMetrics metrics = DrawingMetrics.Default;
        widthMillimeters ??= metrics.PoleAttachment.SymbolWidth;
        heightMillimeters ??= metrics.PoleAttachment.SymbolHeight;
        if (attachmentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Attachment ID cannot be empty.",
                nameof(attachmentId));
        }

        if (!double.IsFinite(widthMillimeters.Value) || widthMillimeters <= 0 ||
            !double.IsFinite(heightMillimeters.Value) || heightMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(heightMillimeters),
                "Attachment layout dimensions must be greater than zero.");
        }

        if (!double.IsFinite(labelOffset?.XMillimeters ?? 0) ||
            !double.IsFinite(labelOffset?.YMillimeters ?? -4))
        {
            throw new ArgumentException(
                "Attachment label offset coordinates must be finite.",
                nameof(labelOffset));
        }

        AttachmentId = attachmentId;
        Offset = offset;
        WidthMillimeters = widthMillimeters.Value;
        HeightMillimeters = heightMillimeters.Value;
        LabelOffset = labelOffset ?? metrics.PoleAttachment.LabelOffset;
    }

    public Guid AttachmentId { get; }

    public DocumentPoint Offset { get; }

    public double WidthMillimeters { get; }

    public double HeightMillimeters { get; }

    public DocumentPoint LabelOffset { get; }

    public AttachmentLayout MoveTo(DocumentPoint offset)
    {
        return new AttachmentLayout(
            AttachmentId,
            offset,
            WidthMillimeters,
            HeightMillimeters,
            LabelOffset);
    }

    public AttachmentLayout Resize(
        double widthMillimeters,
        double heightMillimeters)
    {
        return new AttachmentLayout(
            AttachmentId,
            Offset,
            widthMillimeters,
            heightMillimeters,
            LabelOffset);
    }

    public AttachmentLayout WithLabelOffset(DocumentPoint labelOffset)
    {
        return new AttachmentLayout(
            AttachmentId,
            Offset,
            WidthMillimeters,
            HeightMillimeters,
            labelOffset);
    }
}
