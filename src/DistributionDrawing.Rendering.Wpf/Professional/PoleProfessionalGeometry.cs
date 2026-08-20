using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Professional;

public sealed record PoleAttachmentGeometry(
    DocumentPoint FirstTerminal,
    DocumentPoint SecondTerminal,
    DocumentRect LogicalBounds);

/// <summary>
/// Keeps professional pole geometry and terminal anchors on one engineering baseline.
/// The dimensions are project metrics derived from the supplied drawing reference,
/// not asserted industry-standard dimensions.
/// </summary>
public static class PoleProfessionalGeometry
{
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
            DocumentRect bounds = new(
                centerX - width / 2,
                centerY - height / 2,
                width,
                height);
            return new PoleAttachmentGeometry(
                new DocumentPoint(centerX, bounds.YMillimeters),
                new DocumentPoint(centerX, bounds.YMillimeters + bounds.HeightMillimeters),
                Expand(bounds, effectiveMetrics.CableTermination.LogicalHitPadding));
        }

        if (kind == SymbolKind.DropoutFuse)
        {
            return new PoleAttachmentGeometry(
                new DocumentPoint(centerX, origin.YMillimeters),
                new DocumentPoint(centerX, origin.YMillimeters + attachmentLayout.HeightMillimeters),
                new DocumentRect(
                    origin.XMillimeters,
                    origin.YMillimeters,
                    attachmentLayout.WidthMillimeters,
                    attachmentLayout.HeightMillimeters));
        }

        return new PoleAttachmentGeometry(
            new DocumentPoint(origin.XMillimeters, centerY),
            new DocumentPoint(origin.XMillimeters + attachmentLayout.WidthMillimeters, centerY),
            new DocumentRect(
                origin.XMillimeters,
                origin.YMillimeters,
                attachmentLayout.WidthMillimeters,
                attachmentLayout.HeightMillimeters));
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
