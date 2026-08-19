using System.Windows.Media;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Symbols;

public sealed class PTIntervalSymbol : IIntervalSymbolDefinition
{
    private readonly DrawingMetrics _metrics = DrawingMetrics.Default;

    public IntervalKind Kind => IntervalKind.PTInterval;

    public IReadOnlyList<SceneElement> Create(
        RingCabinetInterval interval,
        RingCabinetIntervalLayout layout,
        DocumentPoint cabinetPosition,
        SymbolLibrary symbolLibrary,
        bool includeLabels = true,
        double? busbarYMillimeters = null)
    {
        ArgumentNullException.ThrowIfNull(interval);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(symbolLibrary);

        if (interval.IntervalKind != Kind)
        {
            throw new InvalidOperationException("The PT symbol requires a PT interval.");
        }

        DocumentPoint origin = new(
            cabinetPosition.XMillimeters + layout.RelativePosition.XMillimeters,
            cabinetPosition.YMillimeters + layout.RelativePosition.YMillimeters);
        DocumentPoint ptOrigin = layout.PTSymbolPosition is DocumentPoint position
            ? new DocumentPoint(
                origin.XMillimeters + position.XMillimeters,
                origin.YMillimeters + position.YMillimeters)
            : throw new InvalidOperationException(
                $"PT interval '{interval.IntervalId}' has no PT symbol position.");
        double busY = busbarYMillimeters ?? origin.YMillimeters;
        var elements = new List<SceneElement>();

        SwitchDevice isolation = GetSingleSwitch(interval, SwitchKind.IsolationSwitch);
        SwitchDevice ground = GetSingleSwitch(interval, SwitchKind.GroundSwitch);
        RingCabinetSwitchLayout isolationLayout = GetLayout(layout, isolation);
        RingCabinetSwitchLayout groundLayout = GetLayout(layout, ground);
        (DocumentPoint isolationTop, DocumentPoint isolationBottom) =
            RingCabinetProfessionalGeometry.AddVerticalSwitch(
                elements,
                isolation,
                isolationLayout,
                origin,
                _metrics);
        elements.Add(new SceneLine(
            new DocumentPoint(isolationTop.XMillimeters, busY),
            isolationTop,
            Colors.Black,
            _metrics.General.ThinStrokeThickness));

        AddPTCoils(elements, ptOrigin, out DocumentPoint coilTop, out DocumentPoint coilBottom);
        elements.Add(new SceneLine(
            isolationBottom,
            coilTop,
            Colors.Black,
            _metrics.General.ThinStrokeThickness));
        RingCabinetProfessionalGeometry.AddGroundSwitch(
            elements,
            ground,
            groundLayout,
            origin,
            isolationBottom,
            _metrics);

        DocumentPoint terminalTip = new(
            coilBottom.XMillimeters,
            origin.YMillimeters + layout.HeightMillimeters);
        double terminalTop = terminalTip.YMillimeters -
                             _metrics.CableTermination.TriangleHeight;
        elements.Add(new SceneLine(
            coilBottom,
            new DocumentPoint(terminalTip.XMillimeters, terminalTop),
            Colors.Black,
            _metrics.General.ThinStrokeThickness));
        RingCabinetProfessionalGeometry.AddCableTerminationMarker(
            elements,
            terminalTip,
            _metrics);

        return elements;
    }

    private void AddPTCoils(
        ICollection<SceneElement> elements,
        DocumentPoint origin,
        out DocumentPoint top,
        out DocumentPoint bottom)
    {
        double radius = _metrics.PT.CoilRadius;
        double diameter = radius * 2;
        double secondOffset = diameter - _metrics.PT.CoilSpacing;
        elements.Add(new SceneEllipse(
            new DocumentRect(origin.XMillimeters, origin.YMillimeters, diameter, diameter),
            Colors.Black,
            _metrics.General.StandardStrokeThickness));
        elements.Add(new SceneEllipse(
            new DocumentRect(
                origin.XMillimeters,
                origin.YMillimeters + secondOffset,
                diameter,
                diameter),
            Colors.Black,
            _metrics.General.StandardStrokeThickness));

        double centerX = origin.XMillimeters + radius;
        top = new DocumentPoint(centerX, origin.YMillimeters);
        bottom = new DocumentPoint(centerX, origin.YMillimeters + secondOffset + diameter);
        elements.Add(new SceneText(
            new DocumentPoint(
                origin.XMillimeters + diameter + 2,
                origin.YMillimeters + secondOffset / 2 + radius),
            "PT",
            Colors.Black,
            _metrics.General.SmallFontSize));
    }

    private static RingCabinetSwitchLayout GetLayout(
        RingCabinetIntervalLayout intervalLayout,
        SwitchDevice switchDevice) =>
        intervalLayout.SwitchLayouts.GetValueOrDefault(switchDevice.Id)
        ?? throw new InvalidOperationException(
            $"No layout exists for switch '{switchDevice.Id}' in interval '{intervalLayout.IntervalId}'.");

    private static SwitchDevice GetSingleSwitch(
        RingCabinetInterval interval,
        SwitchKind kind) =>
        interval.SwitchDevices.SingleOrDefault(device => device.SwitchKind == kind)
        ?? throw new InvalidOperationException(
            $"Interval '{interval.IntervalId}' does not contain exactly one '{kind}'.");
}
