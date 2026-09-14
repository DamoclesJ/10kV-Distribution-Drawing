namespace DistributionDrawing.Rendering.Wpf.Routing;

/// <summary>
/// Identifies a structurally valid route request that cannot satisfy its
/// existing geometric routing constraints.
/// </summary>
public sealed class RoutingConstraintException : InvalidOperationException
{
    public RoutingConstraintException(string message)
        : base(message)
    {
    }
}
