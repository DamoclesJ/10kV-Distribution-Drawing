using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Professional;

public sealed record PoleAttachmentGeometry(
    DocumentPoint FirstTerminal,
    DocumentPoint SecondTerminal,
    DocumentRect LogicalBounds,
    IReadOnlyList<DocumentPoint>? Outline = null);

/// <summary>
/// Keeps professional pole geometry and terminal anchors on one engineering baseline.
/// The dimensions are project metrics derived from the supplied drawing reference,
/// not asserted industry-standard dimensions.
/// </summary>
public static class PoleProfessionalGeometry
{
    public static DocumentPoint GetDefaultAttachmentOffset(
        SwitchKind kind,
        DrawingMetrics? metrics = null)
    {
        DrawingMetrics effectiveMetrics = metrics ?? DrawingMetrics.Default;
        double poleDiameter = effectiveMetrics.Pole.PoleRadius * 2;
        double width = effectiveMetrics.PoleAttachment.SymbolWidth;
        double height = effectiveMetrics.PoleAttachment.SymbolHeight;

        return kind == SwitchKind.DropoutFuse
            ? new DocumentPoint((poleDiameter - width) / 2, -height)
            : new DocumentPoint(poleDiameter, (poleDiameter - height) / 2);
    }

    public static DocumentRect GetPoleBounds(
        PoleLayout layout,
        DrawingMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        DrawingMetrics effectiveMetrics = metrics ?? DrawingMetrics.Default;
        double diameter = effectiveMetrics.Pole.PoleRadius * 2;
        return new DocumentRect(
            layout.Position.XMillimeters,
            layout.Position.YMillimeters,
            diameter,
            diameter);
    }

    public static DocumentPoint GetPoleCenter(
        PoleLayout layout,
        DrawingMetrics? metrics = null)
    {
        DocumentRect bounds = GetPoleBounds(layout, metrics);
        return new DocumentPoint(
            bounds.XMillimeters + bounds.WidthMillimeters / 2,
            bounds.YMillimeters + bounds.HeightMillimeters / 2);
    }

    public static PoleAttachmentGeometry GetAttachmentGeometry(
        PoleLayout poleLayout,
        AttachmentLayout attachmentLayout,
        SymbolKind kind,
        DrawingMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(poleLayout);
        ArgumentNullException.ThrowIfNull(attachmentLayout);
        DrawingMetrics effectiveMetrics = metrics ?? DrawingMetrics.Default;
        DocumentPoint origin = new(
            poleLayout.Position.XMillimeters + attachmentLayout.Offset.XMillimeters,
            poleLayout.Position.YMillimeters + attachmentLayout.Offset.YMillimeters);
        double centerX = origin.XMillimeters + attachmentLayout.WidthMillimeters / 2;
        double centerY = origin.YMillimeters + attachmentLayout.HeightMillimeters / 2;

        if (kind == SymbolKind.CableTermination)
        {
            double width = Math.Min(
                effectiveMetrics.CableTermination.TriangleWidth,
                attachmentLayout.WidthMillimeters);
            double height = Math.Min(
                effectiveMetrics.CableTermination.TriangleHeight,
                attachmentLayout.HeightMillimeters);
            DocumentPoint poleCenter = GetPoleCenter(poleLayout, effectiveMetrics);
            double deltaX = centerX - poleCenter.XMillimeters;
            double deltaY = centerY - poleCenter.YMillimeters;
            double length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            double directionX = length == 0 ? 0 : deltaX / length;
            double directionY = length == 0 ? 1 : deltaY / length;
            DocumentPoint tangent = new(
                poleCenter.XMillimeters +
                directionX * effectiveMetrics.Pole.PoleRadius,
                poleCenter.YMillimeters +
                directionY * effectiveMetrics.Pole.PoleRadius);
            DocumentPoint outerTip = new(
                tangent.XMillimeters + directionX * height,
                tangent.YMillimeters + directionY * height);
            double perpendicularX = -directionY;
            double perpendicularY = directionX;
            DocumentPoint firstBase = new(
                tangent.XMillimeters + perpendicularX * width / 2,
                tangent.YMillimeters + perpendicularY * width / 2);
            DocumentPoint secondBase = new(
                tangent.XMillimeters - perpendicularX * width / 2,
                tangent.YMillimeters - perpendicularY * width / 2);
            DocumentPoint[] outline = [outerTip, firstBase, secondBase];
            double minX = outline.Min(point => point.XMillimeters);
            double minY = outline.Min(point => point.YMillimeters);
            double maxX = outline.Max(point => point.XMillimeters);
            double maxY = outline.Max(point => point.YMillimeters);
            DocumentRect bounds = new(minX, minY, maxX - minX, maxY - minY);
            return new PoleAttachmentGeometry(
                outerTip,
                tangent,
                Expand(bounds, effectiveMetrics.CableTermination.LogicalHitPadding),
                outline);
        }

        if (kind == SymbolKind.DropoutFuse)
        {
            double width = Math.Max(
                attachmentLayout.WidthMillimeters,
                effectiveMetrics.PoleAttachment.SymbolWidth);
            double height = Math.Max(
                attachmentLayout.HeightMillimeters,
                effectiveMetrics.PoleAttachment.SymbolHeight);
            double scaledX = centerX - width / 2;
            double scaledY = centerY - height / 2;
            return RotateSwitchGeometry(
                new PoleAttachmentGeometry(
                    new DocumentPoint(centerX, scaledY),
                    new DocumentPoint(centerX, scaledY + height),
                    new DocumentRect(scaledX, scaledY, width, height)),
                GetPoleCenter(poleLayout, effectiveMetrics).XMillimeters,
                GetPoleCenter(poleLayout, effectiveMetrics).YMillimeters,
                attachmentLayout.RotationQuarterTurns);
        }

        double effectiveWidth = Math.Max(
            attachmentLayout.WidthMillimeters,
            effectiveMetrics.PoleAttachment.SymbolWidth);
        double effectiveHeight = Math.Max(
            attachmentLayout.HeightMillimeters,
            effectiveMetrics.PoleAttachment.SymbolHeight);
        double effectiveX = centerX - effectiveWidth / 2;
        double effectiveY = centerY - effectiveHeight / 2;
        DocumentPoint poleCenterForRotation = GetPoleCenter(poleLayout, effectiveMetrics);
        return RotateSwitchGeometry(
            new PoleAttachmentGeometry(
                new DocumentPoint(effectiveX, centerY),
                new DocumentPoint(effectiveX + effectiveWidth, centerY),
                new DocumentRect(effectiveX, effectiveY, effectiveWidth, effectiveHeight)),
            poleCenterForRotation.XMillimeters,
            poleCenterForRotation.YMillimeters,
            attachmentLayout.RotationQuarterTurns);
    }

    private static PoleAttachmentGeometry RotateSwitchGeometry(
        PoleAttachmentGeometry geometry,
        double centerX,
        double centerY,
        int quarterTurns)
    {
        if (quarterTurns == 0)
        {
            return geometry;
        }

        DocumentPoint first = Rotate(geometry.FirstTerminal, centerX, centerY, quarterTurns);
        DocumentPoint second = Rotate(geometry.SecondTerminal, centerX, centerY, quarterTurns);
        DocumentPoint[] corners =
        [
            Rotate(new DocumentPoint(geometry.LogicalBounds.XMillimeters, geometry.LogicalBounds.YMillimeters), centerX, centerY, quarterTurns),
            Rotate(new DocumentPoint(geometry.LogicalBounds.XMillimeters + geometry.LogicalBounds.WidthMillimeters, geometry.LogicalBounds.YMillimeters), centerX, centerY, quarterTurns),
            Rotate(new DocumentPoint(geometry.LogicalBounds.XMillimeters + geometry.LogicalBounds.WidthMillimeters, geometry.LogicalBounds.YMillimeters + geometry.LogicalBounds.HeightMillimeters), centerX, centerY, quarterTurns),
            Rotate(new DocumentPoint(geometry.LogicalBounds.XMillimeters, geometry.LogicalBounds.YMillimeters + geometry.LogicalBounds.HeightMillimeters), centerX, centerY, quarterTurns)
        ];
        double minX = corners.Min(point => point.XMillimeters);
        double minY = corners.Min(point => point.YMillimeters);
        double maxX = corners.Max(point => point.XMillimeters);
        double maxY = corners.Max(point => point.YMillimeters);
        return geometry with
        {
            FirstTerminal = first,
            SecondTerminal = second,
            LogicalBounds = new DocumentRect(minX, minY, maxX - minX, maxY - minY)
        };
    }

    private static DocumentPoint Rotate(
        DocumentPoint point,
        double centerX,
        double centerY,
        int quarterTurns)
    {
        double x = point.XMillimeters - centerX;
        double y = point.YMillimeters - centerY;
        for (int index = 0; index < quarterTurns; index++)
        {
            (x, y) = (-y, x);
        }

        return new DocumentPoint(centerX + x, centerY + y);
    }

    public static DocumentPoint GetCableTerminationOffset(
        PoleLayout poleLayout,
        AttachmentLayout attachmentLayout,
        DocumentPoint directionTarget,
        DrawingMetrics? metrics = null)
    {
        DrawingMetrics effectiveMetrics = metrics ?? DrawingMetrics.Default;
        DocumentPoint poleCenter = GetPoleCenter(poleLayout, effectiveMetrics);
        double deltaX = directionTarget.XMillimeters - poleCenter.XMillimeters;
        double deltaY = directionTarget.YMillimeters - poleCenter.YMillimeters;
        double length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        double directionX = length == 0 ? 0 : deltaX / length;
        double directionY = length == 0 ? 1 : deltaY / length;
        double triangleHeight = Math.Min(
            effectiveMetrics.CableTermination.TriangleHeight,
            attachmentLayout.HeightMillimeters);
        double centerDistance = effectiveMetrics.Pole.PoleRadius + triangleHeight / 2;
        DocumentPoint attachmentCenter = new(
            poleCenter.XMillimeters + directionX * centerDistance,
            poleCenter.YMillimeters + directionY * centerDistance);
        return new DocumentPoint(
            attachmentCenter.XMillimeters - poleLayout.Position.XMillimeters -
            attachmentLayout.WidthMillimeters / 2,
            attachmentCenter.YMillimeters - poleLayout.Position.YMillimeters -
            attachmentLayout.HeightMillimeters / 2);
    }

    public static DocumentPoint GetPoleEdgeTowards(
        PoleLayout layout,
        DocumentPoint target,
        DrawingMetrics? metrics = null)
    {
        DrawingMetrics effectiveMetrics = metrics ?? DrawingMetrics.Default;
        DocumentPoint center = GetPoleCenter(layout, effectiveMetrics);
        double deltaX = target.XMillimeters - center.XMillimeters;
        double deltaY = target.YMillimeters - center.YMillimeters;
        double length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        if (length == 0)
        {
            return center;
        }

        return new DocumentPoint(
            center.XMillimeters + deltaX / length * effectiveMetrics.Pole.PoleRadius,
            center.YMillimeters + deltaY / length * effectiveMetrics.Pole.PoleRadius);
    }

    private static DocumentRect Expand(DocumentRect bounds, double padding) =>
        new(
            bounds.XMillimeters - padding,
            bounds.YMillimeters - padding,
            bounds.WidthMillimeters + padding * 2,
            bounds.HeightMillimeters + padding * 2);
}
