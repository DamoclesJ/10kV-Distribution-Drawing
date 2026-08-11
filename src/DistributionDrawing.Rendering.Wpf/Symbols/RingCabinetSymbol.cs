using System.Windows.Media;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Symbols;

public sealed class RingCabinetSymbol
{
    private readonly SymbolLibrary _symbolLibrary;
    private readonly IntervalSymbol _intervalSymbol;

    public RingCabinetSymbol(SymbolLibrary symbolLibrary)
    {
        ArgumentNullException.ThrowIfNull(symbolLibrary);

        _symbolLibrary = symbolLibrary;
        _intervalSymbol = new IntervalSymbol(symbolLibrary);
    }

    public IntervalSymbol IntervalSymbol => _intervalSymbol;

    public IReadOnlyList<SceneElement> CreateElements(
        RingCabinet cabinet,
        RingCabinetLayout layout)
    {
        ArgumentNullException.ThrowIfNull(cabinet);
        ArgumentNullException.ThrowIfNull(layout);

        if (cabinet.Id != layout.CabinetId)
        {
            throw new InvalidOperationException(
                "Ring cabinet and cabinet layout IDs must match.");
        }

        var elements = new List<SceneElement>();
        elements.AddRange(
            _symbolLibrary.Create(
                SymbolKind.RingCabinet,
                new SymbolRenderContext(
                    layout.Position,
                    layout.WidthMillimeters,
                    layout.HeightMillimeters,
                    labelOrigin: new DocumentPoint(
                        layout.Position.XMillimeters + layout.LabelOffset.XMillimeters,
                        layout.Position.YMillimeters + layout.LabelOffset.YMillimeters),
                    label: cabinet.DisplayName,
                    fill: Colors.White,
                    thicknessMillimeters: 1)));

        double busY = layout.Position.YMillimeters + layout.MainBusYMillimeters;
        elements.Add(
            new SceneLine(
                new DocumentPoint(layout.Position.XMillimeters, busY),
                new DocumentPoint(
                    layout.Position.XMillimeters + layout.WidthMillimeters,
                    busY),
                Colors.Black,
                1));

        foreach (RingCabinetInterval interval in cabinet.Intervals.OrderBy(
                     candidate => candidate.Sequence))
        {
            if (!layout.IntervalLayouts.TryGetValue(
                    interval.IntervalId,
                    out RingCabinetIntervalLayout intervalLayout))
            {
                throw new InvalidOperationException(
                    $"No layout exists for interval '{interval.IntervalId}'.");
            }

            elements.AddRange(
                _intervalSymbol.CreateElements(
                    interval,
                    intervalLayout,
                    layout.Position));
        }

        return elements;
    }
}
