using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Layout;

public sealed record TransformerLayout
{
    public TransformerLayout(
        Guid transformerId,
        DocumentPoint position,
        TransformerOrientation orientation,
        TransformerKind transformerKind)
    {
        if (transformerId == Guid.Empty)
        {
            throw new ArgumentException(
                "Transformer ID cannot be empty.",
                nameof(transformerId));
        }

        if (!double.IsFinite(position.XMillimeters) ||
            !double.IsFinite(position.YMillimeters))
        {
            throw new ArgumentOutOfRangeException(
                nameof(position),
                "Transformer position must be finite.");
        }

        if (!Enum.IsDefined(orientation))
        {
            throw new ArgumentOutOfRangeException(nameof(orientation));
        }

        TransformerId = transformerId;
        Position = position;
        Orientation = orientation;
        ValidateFor(transformerKind);
    }

    public Guid TransformerId { get; }

    public DocumentPoint Position { get; }

    public TransformerOrientation Orientation { get; }

    public TransformerLayout MoveTo(DocumentPoint position, TransformerKind transformerKind) =>
        new(TransformerId, position, Orientation, transformerKind);

    public void ValidateFor(TransformerKind transformerKind)
    {
        if (!Enum.IsDefined(transformerKind))
        {
            throw new ArgumentOutOfRangeException(nameof(transformerKind));
        }

        if ((transformerKind is TransformerKind.PublicPoleMounted or
                TransformerKind.DedicatedPoleMounted) &&
            Orientation != TransformerOrientation.Vertical)
        {
            throw new InvalidOperationException(
                $"Pole-mounted transformer '{TransformerId}' requires canonical Vertical orientation.");
        }
    }
}
