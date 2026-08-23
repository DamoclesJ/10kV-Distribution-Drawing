using System.Windows.Media;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Symbols;

internal static class RingCabinetProfessionalGeometry
{
    public static (DocumentPoint Top, DocumentPoint Bottom) AddVerticalSwitch(
        ICollection<SceneElement> elements,
        SwitchDevice switchDevice,
        RingCabinetSwitchLayout layout,
        DocumentPoint intervalOrigin,
        DrawingMetrics metrics)
    {
        metrics = WithRingCabinetSwitchScale(metrics);
        DocumentRect bounds = GetBounds(layout, intervalOrigin);
        double centerX = bounds.XMillimeters + bounds.WidthMillimeters / 2;
        double contactInset = Math.Max(
            metrics.Switch.ContactRadius,
            Math.Min(bounds.HeightMillimeters / 4, metrics.Switch.StandardSwitchLength / 4));
        DocumentPoint top = new(centerX, bounds.YMillimeters + contactInset);
        DocumentPoint bottom = new(
            centerX,
            bounds.YMillimeters + bounds.HeightMillimeters - contactInset);

        if (switchDevice.SwitchKind == SwitchKind.CircuitBreaker)
        {
            AddCircuitBreaker(elements, top, bottom, bounds.WidthMillimeters, switchDevice, metrics);
        }
        else if (switchDevice.SwitchKind == SwitchKind.LoadSwitch)
        {
            AddLoadSwitch(elements, top, bottom, switchDevice, metrics);
        }
        else
        {
            AddFixedContact(elements, top, horizontalBlade: false, metrics);
            AddKnifeSwitch(
                elements,
                top,
                bottom,
                switchDevice,
                metrics);
        }

        return (top, bottom);
    }

    public static (DocumentPoint Left, DocumentPoint Right) AddGroundSwitch(
        ICollection<SceneElement> elements,
        SwitchDevice switchDevice,
        RingCabinetSwitchLayout layout,
        DocumentPoint intervalOrigin,
        DocumentPoint circuitNode,
        DrawingMetrics metrics)
    {
        metrics = WithRingCabinetSwitchScale(metrics);
        DocumentRect bounds = GetBounds(layout, intervalOrigin);
        // The grounding blade is a side branch from the actual electrical
        // node. Align its branch with that node instead of using an
        // independent layout center, which would create an unintended slope.
        double centerY = circuitNode.YMillimeters;
        double contactInset = Math.Max(
            metrics.Switch.ContactRadius,
            Math.Min(bounds.WidthMillimeters / 4, metrics.Switch.GroundSwitchLength / 4));
        DocumentPoint left = new(bounds.XMillimeters + contactInset, centerY);
        DocumentPoint right = new(
            bounds.XMillimeters + bounds.WidthMillimeters - contactInset,
            centerY);

        elements.Add(new SceneLine(
            circuitNode,
            right,
            Colors.Black,
            metrics.General.StandardStrokeThickness));
        AddFixedContact(elements, left, horizontalBlade: true, metrics);
        double openOffset = Math.Max(3, metrics.Switch.ContactRadius * 2);
        DocumentPoint bladeEnd = switchDevice.SwitchState == SwitchState.Closed
            ? left
            : new DocumentPoint(left.XMillimeters, left.YMillimeters - openOffset);
        elements.Add(Line(right, bladeEnd, metrics));
        AddLeftFacingEarth(elements, left, metrics);
        return (left, right);
    }

    public static (DocumentPoint Top, DocumentPoint Common) AddThreePositionSwitch(
        ICollection<SceneElement> elements,
        SwitchDevice mainSwitch,
        SwitchDevice groundSwitch,
        RingCabinetSwitchLayout mainLayout,
        RingCabinetSwitchLayout groundLayout,
        DocumentPoint intervalOrigin,
        DrawingMetrics metrics)
    {
        metrics = WithRingCabinetSwitchScale(metrics);
        if (mainSwitch.SwitchKind is not (
                SwitchKind.IsolationSwitch or SwitchKind.LoadSwitch) ||
            groundSwitch.SwitchKind != SwitchKind.GroundSwitch)
        {
            throw new ArgumentException(
                "The three-position symbol requires a main switch and grounding switch.");
        }

        if (mainSwitch.SwitchState == SwitchState.Closed &&
            groundSwitch.SwitchState == SwitchState.Closed)
        {
            throw new InvalidOperationException(
                "Isolation and grounding switches cannot both be closed.");
        }

        DocumentRect mainBounds = GetBounds(mainLayout, intervalOrigin);
        double centerX = mainBounds.XMillimeters +
                         mainBounds.WidthMillimeters / 2;
        double verticalInset = Math.Max(
            metrics.Switch.ContactRadius,
            Math.Min(
                mainBounds.HeightMillimeters / 4,
                metrics.Switch.StandardSwitchLength / 4));
        DocumentPoint top = new(
            centerX,
            mainBounds.YMillimeters + verticalInset);
        DocumentPoint common = new(
            centerX,
            mainBounds.YMillimeters +
            mainBounds.HeightMillimeters -
            verticalInset);

        DocumentRect groundBounds = GetBounds(groundLayout, intervalOrigin);
        double horizontalInset = Math.Max(
            metrics.Switch.ContactRadius,
            Math.Min(
                groundBounds.WidthMillimeters / 4,
                metrics.Switch.GroundSwitchLength / 4));
        DocumentPoint groundContact = new(
            groundBounds.XMillimeters +
            groundBounds.WidthMillimeters -
            horizontalInset,
            common.YMillimeters);

        if (mainSwitch.SwitchKind == SwitchKind.LoadSwitch)
        {
            AddLoadSwitchFixedContact(elements, top, metrics);
        }
        else
        {
            AddFixedContact(elements, top, horizontalBlade: false, metrics);
        }
        AddFixedContact(elements, groundContact, horizontalBlade: true, metrics);
        AddLeftFacingEarth(elements, groundContact, metrics);

        DocumentPoint bladeEnd;
        if (mainSwitch.SwitchState == SwitchState.Closed)
        {
            bladeEnd = top;
        }
        else if (groundSwitch.SwitchState == SwitchState.Closed)
        {
            bladeEnd = groundContact;
        }
        else
        {
            double bladeLength = Math.Max(
                metrics.Switch.ContactRadius * 2,
                common.YMillimeters - top.YMillimeters);
            double diagonal = bladeLength / Math.Sqrt(2);
            bladeEnd = new DocumentPoint(
                common.XMillimeters - diagonal,
                common.YMillimeters - diagonal);
        }

        elements.Add(Line(common, bladeEnd, metrics));
        return (top, common);
    }

    private static void AddLoadSwitch(
        ICollection<SceneElement> elements,
        DocumentPoint top,
        DocumentPoint bottom,
        SwitchDevice switchDevice,
        DrawingMetrics metrics)
    {
        AddLoadSwitchFixedContact(elements, top, metrics);
        AddKnifeSwitch(elements, top, bottom, switchDevice, metrics);
    }

    private static void AddLoadSwitchFixedContact(
        ICollection<SceneElement> elements,
        DocumentPoint top,
        DrawingMetrics metrics)
    {
        double contactRadius = metrics.Switch.ContactRadius / 2;
        elements.Add(new SceneEllipse(
            new DocumentRect(
                top.XMillimeters - contactRadius,
                top.YMillimeters - contactRadius,
                contactRadius * 2,
                contactRadius * 2),
            Colors.Black,
            metrics.General.StandardStrokeThickness,
            Colors.White));
        elements.Add(Line(
            new DocumentPoint(
                top.XMillimeters - metrics.Switch.ContactRadius,
                top.YMillimeters),
            new DocumentPoint(
                top.XMillimeters + metrics.Switch.ContactRadius,
                top.YMillimeters),
            metrics));
    }

    public static void AddCableTerminationMarker(
        ICollection<SceneElement> elements,
        DocumentPoint tip,
        DrawingMetrics metrics)
    {
        double halfWidth = metrics.CableTermination.TriangleWidth / 2;
        double topY = tip.YMillimeters - metrics.CableTermination.TriangleHeight;
        elements.Add(new ScenePolyline(
            [
                new DocumentPoint(tip.XMillimeters - halfWidth, topY),
                new DocumentPoint(tip.XMillimeters + halfWidth, topY),
                tip
            ],
            isClosed: true,
            Colors.Black,
            metrics.General.StandardStrokeThickness,
            Colors.White));
    }

    public static DocumentRect GetBounds(
        RingCabinetSwitchLayout layout,
        DocumentPoint intervalOrigin) =>
        new(
            intervalOrigin.XMillimeters + layout.RelativePosition.XMillimeters,
            intervalOrigin.YMillimeters + layout.RelativePosition.YMillimeters,
            layout.WidthMillimeters,
            layout.HeightMillimeters);

    private static void AddKnifeSwitch(
        ICollection<SceneElement> elements,
        DocumentPoint first,
        DocumentPoint second,
        SwitchDevice switchDevice,
        DrawingMetrics metrics)
    {
        DocumentPoint bladeEnd = switchDevice.SwitchState == SwitchState.Closed
            ? second
            : new DocumentPoint(
                first.XMillimeters + Math.Max(3, metrics.Switch.ContactRadius * 2),
                first.YMillimeters);
        DocumentPoint bladeStart = switchDevice.SwitchState == SwitchState.Closed
            ? first
            : second;
        elements.Add(new SceneLine(
            bladeStart,
            bladeEnd,
            Colors.Black,
            metrics.General.StandardStrokeThickness));
    }

    private static void AddFixedContact(
        ICollection<SceneElement> elements,
        DocumentPoint center,
        bool horizontalBlade,
        DrawingMetrics metrics)
    {
        double halfLength = metrics.Switch.ContactRadius;
        DocumentPoint start = horizontalBlade
            ? new DocumentPoint(center.XMillimeters, center.YMillimeters - halfLength)
            : new DocumentPoint(center.XMillimeters - halfLength, center.YMillimeters);
        DocumentPoint end = horizontalBlade
            ? new DocumentPoint(center.XMillimeters, center.YMillimeters + halfLength)
            : new DocumentPoint(center.XMillimeters + halfLength, center.YMillimeters);
        elements.Add(Line(start, end, metrics));
    }

    private static void AddCircuitBreaker(
        ICollection<SceneElement> elements,
        DocumentPoint top,
        DocumentPoint bottom,
        double availableWidth,
        SwitchDevice switchDevice,
        DrawingMetrics metrics)
    {
        double crossHalf = Math.Max(
            metrics.Switch.ContactRadius,
            Math.Min(availableWidth / 4, metrics.PoleAttachment.ContactCrossSize / 2));
        elements.Add(Line(
            new DocumentPoint(top.XMillimeters - crossHalf, top.YMillimeters - crossHalf),
            new DocumentPoint(top.XMillimeters + crossHalf, top.YMillimeters + crossHalf),
            metrics));
        elements.Add(Line(
            new DocumentPoint(top.XMillimeters - crossHalf, top.YMillimeters + crossHalf),
            new DocumentPoint(top.XMillimeters + crossHalf, top.YMillimeters - crossHalf),
            metrics));

        DocumentPoint end = switchDevice.SwitchState == SwitchState.Closed
            ? bottom
            : new DocumentPoint(
                top.XMillimeters + Math.Max(3, metrics.Switch.ContactRadius * 2),
                top.YMillimeters);
        DocumentPoint start = switchDevice.SwitchState == SwitchState.Closed
            ? top
            : bottom;
        elements.Add(new SceneLine(
            start,
            end,
            Colors.Black,
            metrics.General.StandardStrokeThickness));
    }

    private static SceneLine Line(
        DocumentPoint start,
        DocumentPoint end,
        DrawingMetrics metrics) =>
        new(start, end, Colors.Black, metrics.General.StandardStrokeThickness);

    private static DrawingMetrics WithRingCabinetSwitchScale(DrawingMetrics metrics)
    {
        double scale = metrics.RingCabinet.SwitchSymbolScale;
        return metrics with
        {
            Switch = metrics.Switch with
            {
                StandardSwitchLength = metrics.Switch.StandardSwitchLength * scale,
                GroundSwitchLength = metrics.Switch.GroundSwitchLength * scale,
                ContactRadius = metrics.Switch.ContactRadius * scale,
                LogicalHitHeight = metrics.Switch.LogicalHitHeight * scale
            }
        };
    }

    private static void AddLeftFacingEarth(
        ICollection<SceneElement> elements,
        DocumentPoint connection,
        DrawingMetrics metrics)
    {
        double lead = metrics.Switch.ContactRadius * 3;
        DocumentPoint basePoint = new(connection.XMillimeters - lead, connection.YMillimeters);
        elements.Add(new SceneLine(
            connection,
            basePoint,
            Colors.Black,
            metrics.General.StandardStrokeThickness));

        for (int index = 0; index < 3; index++)
        {
            double halfHeight = (3 - index) * metrics.Switch.ContactRadius;
            double x = basePoint.XMillimeters - index * metrics.Switch.ContactRadius;
            elements.Add(new SceneLine(
                new DocumentPoint(x, basePoint.YMillimeters - halfHeight),
                new DocumentPoint(x, basePoint.YMillimeters + halfHeight),
                Colors.Black,
                metrics.General.StandardStrokeThickness));
        }
    }

}
