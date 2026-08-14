using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Labels;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

/// <summary>
/// Renders an existing ring-cabinet aggregate without creating or modifying Domain objects.
/// </summary>
public sealed class RingCabinetRenderer
{
    private readonly RingCabinetSymbol _symbol;
    private readonly LabelLayoutEngine _labelLayoutEngine;

    public RingCabinetRenderer(
        SymbolLibrary? symbolLibrary = null,
        LabelLayoutEngine? labelLayoutEngine = null)
    {
        _symbol = new RingCabinetSymbol(symbolLibrary ?? new SymbolLibrary());
        _labelLayoutEngine = labelLayoutEngine ?? new LabelLayoutEngine();
    }

    public IReadOnlyList<SceneElement> Render(
        RingCabinet cabinet,
        RingCabinetLayout layout)
    {
        ArgumentNullException.ThrowIfNull(cabinet);
        ArgumentNullException.ThrowIfNull(layout);

        IReadOnlyList<SceneElement> symbols = _symbol.CreateElements(
            cabinet,
            layout,
            includeLabels: false);
        IReadOnlyList<LabelLayoutResult> labels = _labelLayoutEngine.Layout(
            _symbol.CreateLabelRequests(cabinet, layout));

        var elements = symbols.ToList();
        elements.AddRange(labels.Select(result => new SceneText(
            result.Position,
            result.Text,
            System.Windows.Media.Colors.Black,
            result.Request.FontSizeMillimeters)));
        return elements;
    }
}
