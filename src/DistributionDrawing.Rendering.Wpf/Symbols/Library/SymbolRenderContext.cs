using System.Windows.Media;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Symbols.Library;

public sealed record SymbolRenderContext
{
    public SymbolRenderContext(
        DocumentPoint origin,
        double widthMillimeters,
        double heightMillimeters,
        DocumentPoint? end = null,
        DocumentPoint? labelOrigin = null,
        string? label = null,
        SymbolVisualState state = SymbolVisualState.None,
        Color? stroke = null,
        Color? fill = null,
        double thicknessMillimeters = 0.8)
    {
        if (widthMillimeters <= 0 || heightMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(heightMillimeters),
                "Symbol dimensions must be greater than zero.");
        }

        if (thicknessMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(thicknessMillimeters),
                "Symbol line thickness must be greater than zero.");
        }

        Origin = origin;
        WidthMillimeters = widthMillimeters;
        HeightMillimeters = heightMillimeters;
        End = end;
        LabelOrigin = labelOrigin ?? origin;
        Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        State = state;
        Stroke = stroke ?? Colors.Black;
        Fill = fill ?? Colors.Transparent;
        ThicknessMillimeters = thicknessMillimeters;
    }

    public DocumentPoint Origin { get; }

    public double WidthMillimeters { get; }

    public double HeightMillimeters { get; }

    public DocumentPoint? End { get; }

    public DocumentPoint LabelOrigin { get; }

    public string? Label { get; }

    public SymbolVisualState State { get; }

    public Color Stroke { get; }

    public Color Fill { get; }

    public double ThicknessMillimeters { get; }
}
