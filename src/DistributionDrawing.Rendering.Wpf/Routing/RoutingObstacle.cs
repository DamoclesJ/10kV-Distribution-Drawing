using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Routing;

public enum RoutingObstacleKind
{
    RingCabinet,
    Pole,
    PoleAttachment,
    IntermediateTerminal
}

public sealed record RoutingObstacle(
    Guid SourceId,
    RoutingObstacleKind Kind,
    DocumentRect Bounds)
{
    public RoutingObstacle Expand(double clearance)
    {
        return this with
        {
            Bounds = new DocumentRect(
                Bounds.XMillimeters - clearance,
                Bounds.YMillimeters - clearance,
                Bounds.WidthMillimeters + clearance * 2,
                Bounds.HeightMillimeters + clearance * 2)
        };
    }

    public bool Contains(DocumentPoint point, double tolerance = 0)
    {
        return point.XMillimeters >= Bounds.XMillimeters - tolerance &&
               point.XMillimeters <= Bounds.XMillimeters + Bounds.WidthMillimeters + tolerance &&
               point.YMillimeters >= Bounds.YMillimeters - tolerance &&
               point.YMillimeters <= Bounds.YMillimeters + Bounds.HeightMillimeters + tolerance;
    }
}
