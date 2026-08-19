using System.Windows.Media;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Symbols;

/// <summary>
/// Projects the existing integrated-feeder switch composition into professional geometry.
/// It does not derive or mutate electrical state.
/// </summary>
public sealed class IntegratedFeederIntervalSymbol : IIntervalSymbolDefinition
{
    private readonly DrawingMetrics _metrics = DrawingMetrics.Default;

    public IntervalKind Kind => IntervalKind.IntegratedFeederInterval;

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
            throw new InvalidOperationException(
                "The integrated-feeder symbol requires an integrated-feeder interval.");
        }

        GroundingStructureKind structureKind = interval.GroundingStructureKind
            ?? throw new InvalidOperationException(
                $"Interval '{interval.IntervalId}' has no grounding structure.");
        DocumentPoint origin = new(
            cabinetPosition.XMillimeters + layout.RelativePosition.XMillimeters,
            cabinetPosition.YMillimeters + layout.RelativePosition.YMillimeters);
        double centerX = origin.XMillimeters + layout.WidthMillimeters / 2;
        double busY = busbarYMillimeters ?? origin.YMillimeters;
        var elements = new List<SceneElement>();

        SwitchDevice isolation = GetSingleSwitch(interval, SwitchKind.IsolationSwitch);
        SwitchDevice breaker = GetSingleSwitch(interval, SwitchKind.CircuitBreaker);
        SwitchDevice ground = GetSingleSwitch(interval, SwitchKind.GroundSwitch);
        SwitchDevice upper = structureKind == GroundingStructureKind.LowerLowerGrounding
            ? breaker
            : isolation;
        SwitchDevice lower = structureKind == GroundingStructureKind.LowerLowerGrounding
            ? isolation
            : breaker;

        RingCabinetSwitchLayout upperLayout = GetLayout(layout, upper);
        RingCabinetSwitchLayout lowerLayout = GetLayout(layout, lower);
        RingCabinetSwitchLayout groundLayout = GetLayout(layout, ground);
        (DocumentPoint upperTop, DocumentPoint upperBottom) =
            RingCabinetProfessionalGeometry.AddVerticalSwitch(
                elements,
                upper,
                upperLayout,
                origin,
                _metrics);
        (DocumentPoint lowerTop, DocumentPoint lowerBottom) =
            RingCabinetProfessionalGeometry.AddVerticalSwitch(
                elements,
                lower,
                lowerLayout,
                origin,
                _metrics);

        elements.Add(new SceneLine(
            new DocumentPoint(centerX, busY),
            upperTop,
            Colors.Black,
            _metrics.General.ThinStrokeThickness));
        elements.Add(new SceneLine(
            upperBottom,
            lowerTop,
            Colors.Black,
            _metrics.General.ThinStrokeThickness));

        DocumentPoint groundingNode = structureKind switch
        {
            GroundingStructureKind.UpperIsolationGrounding => upperBottom,
            GroundingStructureKind.UpperLowerGrounding => lowerBottom,
            GroundingStructureKind.LowerLowerGrounding => lowerBottom,
            _ => throw new ArgumentOutOfRangeException(nameof(structureKind))
        };
        RingCabinetProfessionalGeometry.AddGroundSwitch(
            elements,
            ground,
            groundLayout,
            origin,
            groundingNode,
            _metrics);

        DocumentPoint terminalTip = new(
            centerX,
            origin.YMillimeters + layout.HeightMillimeters);
        double terminalTop = terminalTip.YMillimeters -
                             _metrics.CableTermination.TriangleHeight;
        elements.Add(new SceneLine(
            lowerBottom,
            new DocumentPoint(centerX, terminalTop),
            Colors.Black,
            _metrics.General.ThinStrokeThickness));
        RingCabinetProfessionalGeometry.AddCableTerminationMarker(
            elements,
            terminalTip,
            _metrics);

        return elements;
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
