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
                fontSizeMillimeters: _metrics.Typography.CabinetNameFontSize));
        }

        if (!string.IsNullOrWhiteSpace(cabinet.LineName))
        {
            requests.Add(new LabelRequest(
                LabelTargetKind.RingCabinet,
                cabinet.Id,
                cabinet.LineName,
                new DocumentPoint(
                    layout.Position.XMillimeters + layout.WidthMillimeters / 2,
                    layout.Position.YMillimeters + layout.MainBusYMillimeters),
                new DocumentPoint(0, -_metrics.Typography.LineNameFontSize - 2),
                priority: 95,
                fontSizeMillimeters: _metrics.Typography.LineNameFontSize));
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
                fontSizeMillimeters: _metrics.Typography.IntervalNumberFontSize));

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
                if (!string.IsNullOrWhiteSpace(switchBusinessNumber) &&
                    !string.Equals(
                        switchBusinessNumber,
                        interval.BusinessNumber,
                        StringComparison.Ordinal))
                {
                    (DocumentPoint labelAnchor, DocumentPoint labelOffset, LabelAlignment alignment) =
                        GetSwitchNumberPlacement(
                            interval,
                            intervalLayout,
                            origin,
                            switchDevice,
                            switchLayout,
                            switchBusinessNumber);
                    requests.Add(new LabelRequest(
                        LabelTargetKind.SwitchDevice,
                        switchDevice.Id,
                        switchBusinessNumber,
                        labelAnchor,
                        labelOffset,
                        alignment,
                        priority: 70,
                        fontSizeMillimeters: _metrics.Typography.SwitchNumberFontSize));
                }

            }
        }

        return requests;
    }

    private (DocumentPoint Anchor, DocumentPoint Offset, LabelAlignment Alignment)
        GetSwitchNumberPlacement(
            RingCabinetInterval interval,
            RingCabinetIntervalLayout intervalLayout,
            DocumentPoint intervalOrigin,
            SwitchDevice switchDevice,
            RingCabinetSwitchLayout switchLayout,
            string label)
    {
        double gap = _metrics.Switch.ContactRadius + 1;
        double fontSize = _metrics.Typography.SwitchNumberFontSize;
        double estimatedWidth = Math.Max(fontSize, label.Length * fontSize * 0.6);
        double left = intervalOrigin.XMillimeters + switchLayout.RelativePosition.XMillimeters;
        double top = intervalOrigin.YMillimeters + switchLayout.RelativePosition.YMillimeters;
        double right = left + switchLayout.WidthMillimeters;
        double bottom = top + switchLayout.HeightMillimeters;
        double intervalLeft = intervalOrigin.XMillimeters + 1;
        double intervalRight = intervalOrigin.XMillimeters + intervalLayout.WidthMillimeters - 1;

        if (switchDevice.SwitchKind == SwitchKind.GroundSwitch)
        {
            double centerX = Math.Clamp(
                (left + right) / 2,
                intervalLeft + estimatedWidth / 2,
                intervalRight - estimatedWidth / 2);
            bool placeAbove = interval.GroundingStructureKind ==
                GroundingStructureKind.UpperLowerGrounding;
            return (
                new DocumentPoint(centerX, placeAbove ? top : bottom),
                new DocumentPoint(0, placeAbove ? -gap : gap + fontSize),
                LabelAlignment.Center);
        }

        double baseline = (top + bottom + fontSize) / 2;
        bool fitsRight = right + gap + estimatedWidth <= intervalRight;
        return fitsRight
            ? (
                new DocumentPoint(right, baseline),
                new DocumentPoint(gap, 0),
                LabelAlignment.Left)
            : (
                new DocumentPoint(left, baseline),
                new DocumentPoint(-gap, 0),
                LabelAlignment.Right);
    }

    private (DocumentPoint Anchor, DocumentPoint Offset) GetIntervalNumberPlacement(
        RingCabinetInterval interval,
        RingCabinetIntervalLayout intervalLayout,
        DocumentPoint origin)
    {
        if (interval.IntervalKind == IntervalKind.PTInterval)
        {
            double secondaryY =
                _metrics.RingCabinet.BusbarOffset -
                _metrics.RingCabinet.CabinetPadding +
                _metrics.RingCabinet.DeviceVerticalSpacing +
                _metrics.Switch.LogicalHitHeight *
                _metrics.RingCabinet.SwitchSymbolScale +
                _metrics.RingCabinet.DeviceVerticalSpacing;
            return (
                new DocumentPoint(origin.XMillimeters, origin.YMillimeters + secondaryY),
                new DocumentPoint(
                    _metrics.RingCabinet.StandardIntervalWidth / 2 + 3,
                    -2));
        }

        SwitchDevice? primarySwitch = interval.SwitchDevices
            .OrderBy(device => device.SwitchKind switch
            {
                SwitchKind.CircuitBreaker => 0,
                SwitchKind.LoadSwitch => 1,
                SwitchKind.IsolationSwitch => 2,
                _ => 3
            })
            .FirstOrDefault(device => device.SwitchKind is
                SwitchKind.CircuitBreaker or
                SwitchKind.LoadSwitch or
                SwitchKind.IsolationSwitch);

        if (primarySwitch is not null &&
            intervalLayout.SwitchLayouts.TryGetValue(primarySwitch.Id, out RingCabinetSwitchLayout? primaryLayout) &&
            primaryLayout is not null)
        {
            DocumentPoint anchor = new(
                origin.XMillimeters + primaryLayout.RelativePosition.XMillimeters,
                origin.YMillimeters + primaryLayout.RelativePosition.YMillimeters);
            return (anchor, new DocumentPoint(primaryLayout.WidthMillimeters + 3, -2));
        }

        return (origin, intervalLayout.SequenceLabelOffset);
    }
}
