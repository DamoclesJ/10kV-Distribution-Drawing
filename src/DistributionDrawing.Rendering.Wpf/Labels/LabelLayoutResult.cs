using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Labels;

public sealed record LabelLayoutResult
{
    public LabelLayoutResult(
        LabelRequest request,
        DocumentPoint position,
        DocumentRect bounds,
        LabelAlignment alignment,
        bool wasAdjusted,
        bool hasCollision)
    {
        ArgumentNullException.ThrowIfNull(request);

        Request = request;
        TargetKind = request.TargetKind;
        TargetId = request.TargetId;
        Text = request.Text;
        Position = position;
        Bounds = bounds;
        Alignment = alignment;
        WasAdjusted = wasAdjusted;
        HasCollision = hasCollision;
    }

    public LabelRequest Request { get; }

    public LabelTargetKind TargetKind { get; }

    public Guid TargetId { get; }

    public string Text { get; }

    public DocumentPoint Position { get; }

    public DocumentRect Bounds { get; }

    public LabelAlignment Alignment { get; }

    public bool WasAdjusted { get; }

    public bool HasCollision { get; }
}
