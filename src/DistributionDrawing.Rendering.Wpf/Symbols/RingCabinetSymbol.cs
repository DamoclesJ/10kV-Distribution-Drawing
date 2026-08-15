using System.Windows.Media;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Labels;
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
        RingCabinetLayout layout,
        bool includeLabels = true)
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
                    layout.Position,
                    includeLabels));
        }

        return elements;
    }

    public IReadOnlyList<LabelRequest> CreateLabelRequests(
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

        var requests = new List<LabelRequest>();
        if (!string.IsNullOrWhiteSpace(cabinet.DisplayName))
        {
            requests.Add(new LabelRequest(
                LabelTargetKind.RingCabinet,
                cabinet.Id,
                cabinet.DisplayName,
                layout.Position,
                layout.LabelOffset,
                priority: 100,
                fontSizeMillimeters: 4));
        }

        foreach (RingCabinetInterval interval in cabinet.Intervals.OrderBy(
                     candidate => candidate.Sequence))
        {
            if (!layout.IntervalLayouts.TryGetValue(
                    interval.IntervalId,
                    out RingCabinetIntervalLayout? intervalLayout) ||
                intervalLayout is null)
            {
                throw new InvalidOperationException(
                    $"No layout exists for interval '{interval.IntervalId}'.");
            }

            DocumentPoint origin = new(
                layout.Position.XMillimeters + intervalLayout.RelativePosition.XMillimeters,
                layout.Position.YMillimeters + intervalLayout.RelativePosition.YMillimeters);
            requests.Add(new LabelRequest(
                LabelTargetKind.Interval,
                interval.IntervalId,
                interval.BusinessNumber,
                origin,
                intervalLayout.SequenceLabelOffset,
                priority: 80));

            if (!string.IsNullOrWhiteSpace(interval.DisplayName))
            {
                requests.Add(new LabelRequest(
                    LabelTargetKind.Interval,
                    interval.IntervalId,
                    interval.DisplayName,
                    origin,
                    intervalLayout.NameLabelOffset,
                    priority: 70));
            }

            foreach (SwitchDevice switchDevice in interval.SwitchDevices)
            {
                if (!intervalLayout.SwitchLayouts.TryGetValue(
                        switchDevice.Id,
                        out RingCabinetSwitchLayout? switchLayout) ||
                    switchLayout is null)
                {
                    throw new InvalidOperationException(
                        $"No layout exists for switch '{switchDevice.Id}' in interval '{interval.IntervalId}'.");
                }

                string? switchBusinessNumber = interval.GetSwitchBusinessNumber(switchDevice.Id);
                DocumentPoint switchOrigin = new(
                    origin.XMillimeters + switchLayout.RelativePosition.XMillimeters,
                    origin.YMillimeters + switchLayout.RelativePosition.YMillimeters);

                if (!string.IsNullOrWhiteSpace(switchBusinessNumber))
                {
                    requests.Add(new LabelRequest(
                        LabelTargetKind.SwitchDevice,
                        switchDevice.Id,
                        switchBusinessNumber,
                        switchOrigin,
                        switchLayout.LabelOffset,
                        priority: 70));
                }

                if (!string.IsNullOrWhiteSpace(switchDevice.DisplayName))
                {
                    requests.Add(new LabelRequest(
                        LabelTargetKind.SwitchDevice,
                        switchDevice.Id,
                        switchDevice.DisplayName,
                        switchOrigin,
                        switchLayout.LabelOffset,
                        priority: 60));
                }
            }
        }

        return requests;
    }
}
