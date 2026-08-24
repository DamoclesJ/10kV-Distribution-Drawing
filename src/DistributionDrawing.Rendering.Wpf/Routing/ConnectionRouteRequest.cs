using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Professional;

namespace DistributionDrawing.Rendering.Wpf.Routing;

public sealed record ConnectionRouteRequest(
    Guid ConnectionId,
    ConnectionType ConnectionType,
    Guid StartTerminalId,
    Guid EndTerminalId,
    TerminalAnchor Start,
    TerminalAnchor End,
    double? PreferredHorizontalY = null);
