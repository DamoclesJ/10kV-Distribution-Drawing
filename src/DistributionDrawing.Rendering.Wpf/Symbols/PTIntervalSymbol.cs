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
        (DocumentPoint isolationTop, DocumentPoint common) =
            RingCabinetProfessionalGeometry.AddThreePositionSwitch(
                elements,
                isolation,
                ground,
                isolationLayout,
                groundLayout,
                origin,
                _metrics);
        elements.Add(new SceneLine(
            new DocumentPoint(isolationTop.XMillimeters, busY),
            isolationTop,
            Colors.Black,
            _metrics.General.StandardStrokeThickness));

        AddPTCoils(elements, ptOrigin, out DocumentPoint coilTop);
        DocumentPoint terminalTip = new(
            coilTop.XMillimeters,
            origin.YMillimeters + layout.HeightMillimeters);
        DocumentPoint terminalBase = new(
            terminalTip.XMillimeters,
            terminalTip.YMillimeters - _metrics.CableTermination.TriangleHeight);
        elements.Add(new SceneLine(
            common,
            terminalBase,
            Colors.Black,
            _metrics.General.StandardStrokeThickness));

        RingCabinetProfessionalGeometry.AddCableTerminationMarker(
            elements,
            terminalTip,
            _metrics);
        elements.Add(new SceneLine(
            terminalTip,
            coilTop,
            Colors.Black,
            _metrics.General.StandardStrokeThickness));

        return elements;
    }

    private void AddPTCoils(
        ICollection<SceneElement> elements,
        DocumentPoint origin,
        out DocumentPoint top)
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
        double totalHeight = diameter * 2 - _metrics.PT.CoilSpacing;
        elements.Add(new SceneText(
            new DocumentPoint(
                centerX - _metrics.Typography.PTLabelFontSize * 0.6,
                origin.YMillimeters + totalHeight +
                _metrics.Typography.PTLabelFontSize + 2),
            "PT",
            Colors.Black,
            _metrics.Typography.PTLabelFontSize));
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
