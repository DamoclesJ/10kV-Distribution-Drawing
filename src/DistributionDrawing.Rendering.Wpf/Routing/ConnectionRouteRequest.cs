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
    IReadOnlyList<RequiredRouteWaypoint>? RequiredWaypoints = null,
    bool EnforceRequiredStubConstraints = false);

public readonly record struct RequiredRouteWaypoint(
    Guid SourceId,
    DocumentPoint Position,
    double MinimumStubLength = 0,
    IReadOnlyList<Guid>? CompositeSourceIds = null,
    double PredecessorMinimumStubLength = 0,
    double SuccessorMinimumStubLength = 0);
