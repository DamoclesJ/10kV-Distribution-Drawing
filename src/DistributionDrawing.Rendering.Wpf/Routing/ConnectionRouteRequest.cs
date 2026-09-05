using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Routing;

public sealed record ConnectionRouteRequest(
    Guid ConnectionId,
    ConnectionType ConnectionType,
    Guid StartTerminalId,
    Guid EndTerminalId,
    TerminalAnchor Start,
    TerminalAnchor End,
    double? PreferredHorizontalY = null,
    IReadOnlyList<RequiredRouteWaypoint>? RequiredWaypoints = null);

public readonly record struct RequiredRouteWaypoint(
    Guid SourceId,
    DocumentPoint Position);
