using System.Globalization;
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

            var placedSwitchLabelBounds = new List<DocumentRect>();
            foreach (SwitchDevice switchDevice in interval.SwitchDevices
                         .OrderBy(device => intervalLayout.SwitchLayouts[device.Id]
                             .RelativePosition.YMillimeters)
                         .ThenBy(device => device.SwitchKind)
                         .ThenBy(device => device.Id))
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
                    (DocumentPoint labelAnchor, DocumentPoint labelOffset,
                        LabelAlignment alignment, DocumentRect labelBounds) =
                        GetSwitchNumberPlacement(
                            intervalLayout,
                            origin,
                            switchLayout,
                            switchBusinessNumber,
                            placedSwitchLabelBounds);
                    requests.Add(new LabelRequest(
                        LabelTargetKind.SwitchDevice,
                        switchDevice.Id,
                        switchBusinessNumber,
                        labelAnchor,
                        labelOffset,
                        alignment,
                        priority: 70,
                        fontSizeMillimeters: _metrics.Typography.SwitchNumberFontSize,
                        allowCollisionAdjustment: false,
                        measuredWidthMillimeters: labelBounds.WidthMillimeters));
                    placedSwitchLabelBounds.Add(labelBounds);
                }

            }
        }

        return requests;
    }

    private (DocumentPoint Anchor, DocumentPoint Offset, LabelAlignment Alignment,
        DocumentRect Bounds)
        GetSwitchNumberPlacement(
            RingCabinetIntervalLayout intervalLayout,
            DocumentPoint intervalOrigin,
            RingCabinetSwitchLayout switchLayout,
            string label,
            IReadOnlyList<DocumentRect> placedLabelBounds)
    {
        const double safetyMargin = 2;
        double gap = _metrics.Switch.ContactRadius + safetyMargin;
        double fontSize = _metrics.Typography.SwitchNumberFontSize;
        double estimatedWidth = MeasureTextWidth(label, fontSize);
        double left = intervalOrigin.XMillimeters + switchLayout.RelativePosition.XMillimeters;
        double top = intervalOrigin.YMillimeters + switchLayout.RelativePosition.YMillimeters;
        double right = left + switchLayout.WidthMillimeters;
        double bottom = top + switchLayout.HeightMillimeters;
        var intervalBounds = new DocumentRect(
            intervalOrigin.XMillimeters + 1,
            intervalOrigin.YMillimeters + 1,
            intervalLayout.WidthMillimeters - 2,
            intervalLayout.HeightMillimeters - 2);
        var ownBounds = new DocumentRect(
            left,
            top,
            switchLayout.WidthMillimeters,
            switchLayout.HeightMillimeters);
        var obstacles = intervalLayout.SwitchLayouts.Values
            .Select(layout => new DocumentRect(
                intervalOrigin.XMillimeters + layout.RelativePosition.XMillimeters,
                intervalOrigin.YMillimeters + layout.RelativePosition.YMillimeters,
                layout.WidthMillimeters,
                layout.HeightMillimeters))
            .Select(bounds => Expand(bounds, safetyMargin))
            .ToList();
        double busbarY = intervalOrigin.YMillimeters +
                         _metrics.RingCabinet.BusbarOffset -
                         _metrics.RingCabinet.CabinetPadding;
        obstacles.Add(Expand(new DocumentRect(
            intervalOrigin.XMillimeters,
            busbarY - _metrics.RingCabinet.BusbarHeight / 2,
            intervalLayout.WidthMillimeters,
            _metrics.RingCabinet.BusbarHeight), 1));
        obstacles.Add(CreateCableTerminalBounds(intervalOrigin, intervalLayout, safetyMargin));
        if (intervalLayout.PTSymbolPosition is DocumentPoint pt)
        {
            obstacles.Add(Expand(new DocumentRect(
                intervalOrigin.XMillimeters + pt.XMillimeters,
                intervalOrigin.YMillimeters + pt.YMillimeters,
                _metrics.PT.CoilRadius * 2,
                _metrics.PT.CoilRadius * 4 - _metrics.PT.CoilSpacing), safetyMargin));
        }

        double centerX = (left + right) / 2;
        double centerBaseline = (top + bottom + fontSize) / 2;
        var candidates = new List<(DocumentPoint Position, LabelAlignment Alignment)>
        {
            (new DocumentPoint(right + gap, centerBaseline), LabelAlignment.Left),
            (new DocumentPoint(left - gap, centerBaseline), LabelAlignment.Right)
        };
        for (int ring = 0; ring < 12; ring++)
        {
            double verticalDistance = gap + ring * (fontSize + 1);
            candidates.Add((
                new DocumentPoint(centerX, top - verticalDistance),
                LabelAlignment.Center));
            candidates.Add((
                new DocumentPoint(centerX, bottom + verticalDistance + fontSize),
                LabelAlignment.Center));
            candidates.Add((
                new DocumentPoint(right + gap, top - verticalDistance),
                LabelAlignment.Left));
            candidates.Add((
                new DocumentPoint(left - gap, top - verticalDistance),
                LabelAlignment.Right));
            candidates.Add((
                new DocumentPoint(right + gap, bottom + verticalDistance + fontSize),
                LabelAlignment.Left));
            candidates.Add((
                new DocumentPoint(left - gap, bottom + verticalDistance + fontSize),
                LabelAlignment.Right));
        }

        var legal = candidates
            .Select((candidate, index) => new
            {
                candidate.Position,
                candidate.Alignment,
                Bounds = MeasureLabel(
                    candidate.Position,
                    candidate.Alignment,
                    estimatedWidth,
                    fontSize),
                Index = index
            })
            .Where(candidate => Contains(intervalBounds, candidate.Bounds))
            .Where(candidate => obstacles.All(obstacle => !Overlaps(candidate.Bounds, obstacle)))
            .Where(candidate => placedLabelBounds.All(bounds =>
                !Overlaps(candidate.Bounds, Expand(bounds, 1))))
            .OrderBy(candidate => Distance(candidate.Bounds, ownBounds))
            .ThenBy(candidate => candidate.Index)
            .FirstOrDefault();
        if (legal is null)
        {
            double fallbackX = Math.Clamp(
                centerX,
                intervalBounds.XMillimeters + estimatedWidth / 2,
                intervalBounds.XMillimeters + intervalBounds.WidthMillimeters -
                estimatedWidth / 2);
            DocumentPoint fallbackPosition = new(fallbackX, top - gap);
            DocumentRect fallbackBounds = MeasureLabel(
                fallbackPosition,
                LabelAlignment.Center,
                estimatedWidth,
                fontSize);
            return (fallbackPosition, new DocumentPoint(0, 0),
                LabelAlignment.Center, fallbackBounds);
        }

        return (legal.Position, new DocumentPoint(0, 0), legal.Alignment, legal.Bounds);
    }

    private DocumentRect CreateCableTerminalBounds(
        DocumentPoint intervalOrigin,
        RingCabinetIntervalLayout intervalLayout,
        double margin)
    {
        double centerX = intervalOrigin.XMillimeters + intervalLayout.WidthMillimeters / 2;
        double bottom = intervalOrigin.YMillimeters + intervalLayout.HeightMillimeters;
        return Expand(new DocumentRect(
            centerX - _metrics.CableTermination.TriangleWidth / 2,
            bottom - _metrics.CableTermination.TriangleHeight,
            _metrics.CableTermination.TriangleWidth,
            _metrics.CableTermination.TriangleHeight), margin);
    }

    private static DocumentRect MeasureLabel(
        DocumentPoint position,
        LabelAlignment alignment,
        double width,
        double height)
    {
        double x = alignment switch
        {
            LabelAlignment.Left => position.XMillimeters,
            LabelAlignment.Right => position.XMillimeters - width,
            _ => position.XMillimeters - width / 2
        };
        return new DocumentRect(x, position.YMillimeters - height, width, height);
    }

    private static DocumentRect Expand(DocumentRect bounds, double margin) => new(
        bounds.XMillimeters - margin,
        bounds.YMillimeters - margin,
        bounds.WidthMillimeters + margin * 2,
        bounds.HeightMillimeters + margin * 2);

    private static bool Contains(DocumentRect container, DocumentRect candidate) =>
        candidate.XMillimeters >= container.XMillimeters &&
        candidate.YMillimeters >= container.YMillimeters &&
        candidate.XMillimeters + candidate.WidthMillimeters <=
        container.XMillimeters + container.WidthMillimeters &&
        candidate.YMillimeters + candidate.HeightMillimeters <=
        container.YMillimeters + container.HeightMillimeters;

    private static bool Overlaps(DocumentRect left, DocumentRect right) =>
        left.XMillimeters < right.XMillimeters + right.WidthMillimeters &&
        left.XMillimeters + left.WidthMillimeters > right.XMillimeters &&
        left.YMillimeters < right.YMillimeters + right.HeightMillimeters &&
        left.YMillimeters + left.HeightMillimeters > right.YMillimeters;

    private static double Distance(DocumentRect first, DocumentRect second)
    {
        double dx = Math.Max(0, Math.Max(
            second.XMillimeters - (first.XMillimeters + first.WidthMillimeters),
            first.XMillimeters - (second.XMillimeters + second.WidthMillimeters)));
        double dy = Math.Max(0, Math.Max(
            second.YMillimeters - (first.YMillimeters + first.HeightMillimeters),
            first.YMillimeters - (second.YMillimeters + second.HeightMillimeters)));
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double MeasureTextWidth(string text, double fontSizeMillimeters)
    {
        const double dipsPerMillimeter = 96.0 / 25.4;
        var formatted = new FormattedText(
            text,
            CultureInfo.GetCultureInfo("zh-CN"),
            System.Windows.FlowDirection.LeftToRight,
            new Typeface("Microsoft YaHei"),
            fontSizeMillimeters * dipsPerMillimeter,
            Brushes.Black,
            1);
        return formatted.WidthIncludingTrailingWhitespace / dipsPerMillimeter;
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
