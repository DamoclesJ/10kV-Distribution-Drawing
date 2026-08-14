using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Labels;

public sealed record LabelRequest
{
    public LabelRequest(
        LabelTargetKind targetKind,
        Guid targetId,
        string text,
        DocumentPoint anchor,
        DocumentPoint offset,
        LabelAlignment preferredAlignment = LabelAlignment.Center,
        int priority = 0,
        double fontSizeMillimeters = 3)
    {
        if (targetId == Guid.Empty)
        {
            throw new ArgumentException("Label target ID cannot be empty.", nameof(targetId));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Label text cannot be empty.", nameof(text));
        }

        if (fontSizeMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fontSizeMillimeters),
                "Label font size must be greater than zero.");
        }

        TargetKind = targetKind;
        TargetId = targetId;
        Text = text;
        Anchor = anchor;
        Offset = offset;
        PreferredAlignment = preferredAlignment;
        Priority = priority;
        FontSizeMillimeters = fontSizeMillimeters;
    }

    public LabelTargetKind TargetKind { get; }

    public Guid TargetId { get; }

    public string Text { get; }

    public DocumentPoint Anchor { get; }

    public DocumentPoint Offset { get; }

    public LabelAlignment PreferredAlignment { get; }

    public int Priority { get; }

    public double FontSizeMillimeters { get; }
}
