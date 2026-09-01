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
    private const double BusinessNumberGap = 2;
    private const double BusinessNumberLocalAdjustment = 4;
    private const double BusinessNumberBoundaryInset = 1;
    private const double BusbarLabelClearance = 2;
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

            var placedBusinessNumberBounds = new List<DocumentRect>();
            SwitchDevice intervalNumberOwner = ResolveIntervalNumberOwner(interval);
            BusinessNumberPlacement intervalNumberPlacement = GetSwitchNumberPlacement(
                interval,
                intervalLayout,
                origin,
                intervalNumberOwner,
                intervalLayout.SwitchLayouts[intervalNumberOwner.Id],
                interval.BusinessNumber,
                _metrics.Typography.IntervalNumberFontSize,
                isIntervalNumber: true,
                placedBusinessNumberBounds);
            requests.Add(new LabelRequest(
                LabelTargetKind.Interval,
                interval.IntervalId,
                interval.BusinessNumber,
                intervalNumberPlacement.Position,
                new DocumentPoint(0, 0),
                intervalNumberPlacement.Alignment,
                priority: 80,
                fontSizeMillimeters: _metrics.Typography.IntervalNumberFontSize,
                allowCollisionAdjustment: false,
                measuredWidthMillimeters: intervalNumberPlacement.Bounds.WidthMillimeters));
            placedBusinessNumberBounds.Add(intervalNumberPlacement.Bounds);

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
                    BusinessNumberPlacement placement = GetSwitchNumberPlacement(
                        interval,
                        intervalLayout,
                        origin,
                        switchDevice,
                        switchLayout,
                        switchBusinessNumber,
                        _metrics.Typography.SwitchNumberFontSize,
                        isIntervalNumber: false,
                        placedBusinessNumberBounds);
                    requests.Add(new LabelRequest(
                        LabelTargetKind.SwitchDevice,
                        switchDevice.Id,
                        switchBusinessNumber,
                        placement.Position,
                        new DocumentPoint(0, 0),
                        placement.Alignment,
                        priority: 70,
                        fontSizeMillimeters: _metrics.Typography.SwitchNumberFontSize,
                        allowCollisionAdjustment: false,
                        measuredWidthMillimeters: placement.Bounds.WidthMillimeters));
                    placedBusinessNumberBounds.Add(placement.Bounds);
                }

            }
        }

        return requests;
    }

    private BusinessNumberPlacement GetSwitchNumberPlacement(
            RingCabinetInterval interval,
            RingCabinetIntervalLayout intervalLayout,
            DocumentPoint intervalOrigin,
            SwitchDevice switchDevice,
            RingCabinetSwitchLayout switchLayout,
            string label,
            double fontSize,
            bool isIntervalNumber,
            IReadOnlyList<DocumentRect> placedLabelBounds)
    {
        double estimatedWidth = MeasureTextWidth(label, fontSize);
        double busbarY = intervalOrigin.YMillimeters +
                         _metrics.RingCabinet.BusbarOffset -
                         _metrics.RingCabinet.CabinetPadding;
        var allowedBounds = new DocumentRect(
            intervalOrigin.XMillimeters + BusinessNumberBoundaryInset,
            busbarY + BusbarLabelClearance,
            intervalLayout.WidthMillimeters - BusinessNumberBoundaryInset * 2,
            intervalOrigin.YMillimeters + intervalLayout.HeightMillimeters -
            BusinessNumberBoundaryInset - busbarY - BusbarLabelClearance);
        DocumentRect ownBounds = CreateSwitchVisualBounds(
            interval,
            intervalLayout,
            intervalOrigin,
            switchDevice,
            switchLayout);
        SemanticLabelSide side = ResolveSemanticLabelSide(
            interval,
            switchDevice,
            isIntervalNumber);
        double circuitX = intervalOrigin.XMillimeters + intervalLayout.WidthMillimeters / 2;
        var obstacles = interval.SwitchDevices
            .Select(device => CreateSwitchVisualBounds(
                interval,
                intervalLayout,
                intervalOrigin,
                device,
                intervalLayout.SwitchLayouts[device.Id]))
            .Select(bounds => Expand(bounds, _metrics.General.StandardStrokeThickness / 2))
            .ToList();
        obstacles.Add(CreateCableTerminalBounds(
            intervalOrigin,
            intervalLayout,
            BusinessNumberBoundaryInset));
        if (intervalLayout.PTSymbolPosition is DocumentPoint pt)
        {
            obstacles.Add(Expand(new DocumentRect(
                intervalOrigin.XMillimeters + pt.XMillimeters,
                intervalOrigin.YMillimeters + pt.YMillimeters,
                _metrics.PT.CoilRadius * 2,
                _metrics.PT.CoilRadius * 4 - _metrics.PT.CoilSpacing),
                BusinessNumberBoundaryInset));
        }

        BusinessNumberPlacement? legal = CreateLocalCandidates(
                side,
                ownBounds,
                circuitX,
                fontSize)
            .Select(candidate => new BusinessNumberPlacement(
                candidate.Position,
                candidate.Alignment,
                MeasureLabel(
                    candidate.Position,
                    candidate.Alignment,
                    estimatedWidth,
                    fontSize)))
            .Where(candidate => Contains(allowedBounds, candidate.Bounds))
            .Where(candidate => obstacles.All(obstacle =>
                !Overlaps(candidate.Bounds, obstacle)))
            .Where(candidate => placedLabelBounds.All(bounds =>
                !Overlaps(candidate.Bounds, Expand(bounds, BusinessNumberBoundaryInset))))
            .FirstOrDefault();
        return legal ?? throw new InvalidOperationException(
            $"No semantic business-number placement exists for switch '{switchDevice.Id}'.");
    }

    private IEnumerable<(DocumentPoint Position, LabelAlignment Alignment)>
        CreateLocalCandidates(
            SemanticLabelSide side,
            DocumentRect ownerBounds,
            double circuitX,
            double height)
    {
        (DocumentPoint nominal, LabelAlignment alignment) = side switch
        {
            SemanticLabelSide.Left => (
                new DocumentPoint(
                    ownerBounds.XMillimeters - BusinessNumberGap,
                    ownerBounds.YMillimeters + ownerBounds.HeightMillimeters / 2 + height / 2),
                LabelAlignment.Right),
            SemanticLabelSide.Right => (
                new DocumentPoint(
                    ownerBounds.XMillimeters + ownerBounds.WidthMillimeters +
                    BusinessNumberGap,
                    ownerBounds.YMillimeters + ownerBounds.HeightMillimeters / 2 + height / 2),
                LabelAlignment.Left),
            SemanticLabelSide.Above => (
                new DocumentPoint(
                    circuitX - BusinessNumberGap,
                    ownerBounds.YMillimeters - BusinessNumberGap),
                LabelAlignment.Right),
            SemanticLabelSide.Below => (
                new DocumentPoint(
                    circuitX - BusinessNumberGap,
                    ownerBounds.YMillimeters + ownerBounds.HeightMillimeters +
                    BusinessNumberGap + height),
                LabelAlignment.Right),
            _ => throw new ArgumentOutOfRangeException(nameof(side))
        };

        yield return (nominal, alignment);
        foreach (double adjustment in new[] { 2d, BusinessNumberLocalAdjustment })
        {
            if (side is SemanticLabelSide.Left or SemanticLabelSide.Right)
            {
                yield return (
                    new DocumentPoint(
                        nominal.XMillimeters,
                        nominal.YMillimeters - adjustment),
                    alignment);
                yield return (
                    new DocumentPoint(
                        nominal.XMillimeters,
                        nominal.YMillimeters + adjustment),
                    alignment);
            }
            else
            {
                double direction = side == SemanticLabelSide.Above ? -1 : 1;
                yield return (
                    new DocumentPoint(
                        nominal.XMillimeters,
                        nominal.YMillimeters + direction * adjustment),
                    alignment);
            }
        }
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

    private SwitchDevice ResolveIntervalNumberOwner(RingCabinetInterval interval)
    {
        SwitchKind ownerKind = interval.IntervalKind switch
        {
            IntervalKind.LoadSwitchInterval => SwitchKind.LoadSwitch,
            IntervalKind.IntegratedFeederInterval => SwitchKind.CircuitBreaker,
            IntervalKind.PTInterval => SwitchKind.IsolationSwitch,
            _ => throw new ArgumentOutOfRangeException(nameof(interval))
        };
        return interval.SwitchDevices.Single(device => device.SwitchKind == ownerKind);
    }

    private static SemanticLabelSide ResolveSemanticLabelSide(
        RingCabinetInterval interval,
        SwitchDevice switchDevice,
        bool isIntervalNumber)
    {
        if (isIntervalNumber)
        {
            return SemanticLabelSide.Right;
        }

        return switchDevice.SwitchKind switch
        {
            SwitchKind.IsolationSwitch when interval.GroundingStructureKind ==
                GroundingStructureKind.UpperLowerGrounding => SemanticLabelSide.Left,
            SwitchKind.IsolationSwitch => SemanticLabelSide.Above,
            SwitchKind.GroundSwitch when interval.GroundingStructureKind ==
                GroundingStructureKind.UpperLowerGrounding =>
                SemanticLabelSide.Above,
            SwitchKind.GroundSwitch => SemanticLabelSide.Below,
            SwitchKind.LoadSwitch or SwitchKind.CircuitBreaker => SemanticLabelSide.Right,
            _ => throw new InvalidOperationException(
                $"Switch kind '{switchDevice.SwitchKind}' has no business-number anchor.")
        };
    }

    private DocumentRect CreateSwitchVisualBounds(
        RingCabinetInterval interval,
        RingCabinetIntervalLayout intervalLayout,
        DocumentPoint intervalOrigin,
        SwitchDevice switchDevice,
        RingCabinetSwitchLayout switchLayout)
    {
        double scale = _metrics.RingCabinet.SwitchSymbolScale;
        double contactRadius = _metrics.Switch.ContactRadius * scale;
        double layoutLeft = intervalOrigin.XMillimeters +
                            switchLayout.RelativePosition.XMillimeters;
        double layoutTop = intervalOrigin.YMillimeters +
                           switchLayout.RelativePosition.YMillimeters;
        double circuitX = intervalOrigin.XMillimeters + intervalLayout.WidthMillimeters / 2;

        if (switchDevice.SwitchKind == SwitchKind.GroundSwitch)
        {
            double centerY = layoutTop + switchLayout.HeightMillimeters / 2;
            double left;
            if (interval.GroundingStructureKind == GroundingStructureKind.UpperLowerGrounding)
            {
                double right = circuitX - switchLayout.WidthMillimeters * 3 / 16;
                double contact = right - switchLayout.WidthMillimeters / 4;
                left = contact - contactRadius * 3.5;
            }
            else
            {
                double groundContactInset = Math.Max(
                    contactRadius,
                    Math.Min(
                        switchLayout.WidthMillimeters / 4,
                        _metrics.Switch.GroundSwitchLength * scale / 4));
                double contact = layoutLeft + switchLayout.WidthMillimeters -
                                 groundContactInset;
                left = contact - contactRadius * 5;
            }

            double verticalRadius = contactRadius * 3;
            return new DocumentRect(
                left,
                centerY - verticalRadius,
                circuitX - left,
                verticalRadius * 2);
        }

        double centerX = layoutLeft + switchLayout.WidthMillimeters / 2;
        double contactInset = Math.Max(
            contactRadius,
            Math.Min(
                switchLayout.HeightMillimeters / 4,
                _metrics.Switch.StandardSwitchLength * scale / 4));
        double top = layoutTop + contactInset;
        double bottom = layoutTop + switchLayout.HeightMillimeters - contactInset;
        double leftRadius = switchDevice.SwitchKind == SwitchKind.CircuitBreaker
            ? Math.Max(
                contactRadius,
                Math.Min(
                    switchLayout.WidthMillimeters / 4,
                    _metrics.PoleAttachment.ContactCrossSize / 2))
            : contactRadius;
        double rightRadius = Math.Max(contactRadius, contactRadius * 2);
        double topRadius = switchDevice.SwitchKind == SwitchKind.CircuitBreaker
            ? leftRadius
            : 0;
        return new DocumentRect(
            centerX - leftRadius,
            top - topRadius,
            leftRadius + rightRadius,
            bottom - top + topRadius);
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

    private sealed record BusinessNumberPlacement(
        DocumentPoint Position,
        LabelAlignment Alignment,
        DocumentRect Bounds);

    private enum SemanticLabelSide
    {
        Left,
        Right,
        Above,
        Below
    }
}
