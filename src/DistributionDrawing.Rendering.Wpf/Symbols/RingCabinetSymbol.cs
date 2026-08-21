using System.Windows.Media;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Labels;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Symbols;

public sealed class RingCabinetSymbol
{
    private readonly IntervalSymbol _intervalSymbol;
    private readonly DrawingMetrics _metrics;

    public RingCabinetSymbol(
        SymbolLibrary symbolLibrary,
        DrawingMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(symbolLibrary);

        _intervalSymbol = new IntervalSymbol(symbolLibrary);
        _metrics = metrics ?? DrawingMetrics.Default;
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
        elements.Add(new SceneLogicalBounds(new DocumentRect(
            layout.Position.XMillimeters,
            layout.Position.YMillimeters,
            layout.WidthMillimeters,
            layout.HeightMillimeters)));

        double busY = layout.Position.YMillimeters + layout.MainBusYMillimeters;
        RingCabinetIntervalLayout[] orderedLayouts = cabinet.Intervals
            .OrderBy(candidate => candidate.Sequence)
            .Select(interval => layout.IntervalLayouts.GetValueOrDefault(interval.IntervalId)
                ?? throw new InvalidOperationException(
                    $"No layout exists for interval '{interval.IntervalId}'."))
            .ToArray();
        double busStartX = layout.Position.XMillimeters +
                           orderedLayouts[0].RelativePosition.XMillimeters;
        RingCabinetIntervalLayout lastLayout = orderedLayouts[^1];
        double busEndX = layout.Position.XMillimeters +
                         lastLayout.RelativePosition.XMillimeters +
                         lastLayout.WidthMillimeters;
        elements.Add(
            new SceneLine(
                new DocumentPoint(busStartX, busY),
                new DocumentPoint(busEndX, busY),
                Colors.Black,
                _metrics.RingCabinet.BusbarHeight));

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
                    includeLabels,
                    busY));
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
                new DocumentPoint(
                    layout.Position.XMillimeters + layout.WidthMillimeters / 2,
                    layout.Position.YMillimeters),
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

            (DocumentPoint intervalNumberAnchor, DocumentPoint intervalNumberOffset) =
                GetIntervalNumberPlacement(interval, intervalLayout, origin);
            requests.Add(new LabelRequest(
                LabelTargetKind.Interval,
                interval.IntervalId,
                interval.BusinessNumber,
                intervalNumberAnchor,
                intervalNumberOffset,
                priority: 80,
                fontSizeMillimeters: _metrics.General.SmallFontSize));

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

                if (!string.IsNullOrWhiteSpace(switchBusinessNumber) &&
                    !string.Equals(
                        switchBusinessNumber,
                        interval.BusinessNumber,
                        StringComparison.Ordinal))
                {
                    double labelGap = _metrics.Switch.ContactRadius + 1;
                    DocumentPoint labelOffset = switchDevice.SwitchKind == SwitchKind.GroundSwitch
                        ? new DocumentPoint(0, switchLayout.HeightMillimeters + labelGap)
                        : new DocumentPoint(switchLayout.WidthMillimeters + labelGap, -labelGap);
                    requests.Add(new LabelRequest(
                        LabelTargetKind.SwitchDevice,
                        switchDevice.Id,
                        switchBusinessNumber,
                        switchOrigin,
                        labelOffset,
                        priority: 70,
                        fontSizeMillimeters: _metrics.General.SmallFontSize));
                }

            }
        }

        return requests;
    }

    private static (DocumentPoint Anchor, DocumentPoint Offset) GetIntervalNumberPlacement(
        RingCabinetInterval interval,
        RingCabinetIntervalLayout intervalLayout,
        DocumentPoint origin)
    {
        SwitchDevice? primarySwitch = interval.SwitchDevices.FirstOrDefault(device =>
            device.SwitchKind is SwitchKind.CircuitBreaker or SwitchKind.LoadSwitch);

        if (primarySwitch is not null &&
            intervalLayout.SwitchLayouts.TryGetValue(primarySwitch.Id, out RingCabinetSwitchLayout? primaryLayout) &&
            primaryLayout is not null)
        {
            DocumentPoint anchor = new(
                origin.XMillimeters + primaryLayout.RelativePosition.XMillimeters,
                origin.YMillimeters + primaryLayout.RelativePosition.YMillimeters);
            return (anchor, new DocumentPoint(primaryLayout.WidthMillimeters + 3, -2));
        }

        if (interval.IntervalKind == IntervalKind.PTInterval &&
            intervalLayout.PTSymbolPosition is DocumentPoint ptPosition)
        {
            return (
                new DocumentPoint(
                    origin.XMillimeters + ptPosition.XMillimeters,
                    origin.YMillimeters + ptPosition.YMillimeters),
                new DocumentPoint(16, 0));
        }

        return (origin, intervalLayout.SequenceLabelOffset);
    }
}
