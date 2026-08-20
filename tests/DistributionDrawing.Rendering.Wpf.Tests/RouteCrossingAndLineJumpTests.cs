using System.Windows.Media;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class RouteCrossingAndLineJumpTests
{
    [Fact]
    public void Detector_ClassifiesIndependentPerpendicularCrossing()
    {
        (OrthogonalRoute lower, OrthogonalRoute upper) = CreateCrossingRoutes();

        RouteIntersection intersection = Assert.Single(
            new RouteCrossingDetector().Detect([upper, lower]));

        Assert.Equal(RouteIntersectionKind.PerpendicularInterior, intersection.Kind);
        Assert.Equal(new DocumentPoint(20, 20), intersection.Position);
        Assert.False(intersection.SharesTopologyTerminal);
    }

    [Fact]
    public void Decorator_PlacesJumpOnLargerConnectionIdAndInheritsCableStyle()
    {
        (OrthogonalRoute lower, OrthogonalRoute upper) = CreateCrossingRoutes();
        IReadOnlyList<RouteIntersection> intersections =
            new RouteCrossingDetector().Detect([lower, upper]);
        var decorator = new LineJumpDecorator();

        IReadOnlyList<SceneElement> lowerElements = decorator.Project(
            lower, intersections, Colors.Black, SceneStrokeStyle.Solid);
        IReadOnlyList<SceneElement> upperElements = decorator.Project(
            upper, intersections, Colors.Black, SceneStrokeStyle.Dashed);

        Assert.Empty(lowerElements.OfType<SceneArc>());
        SceneArc jump = Assert.Single(upperElements.OfType<SceneArc>());
        Assert.Equal(SceneStrokeStyle.Dashed, jump.StrokeStyle);
        Assert.Equal(new DocumentPoint(20, 20), jump.Center);
    }

    [Fact]
    public void Detector_DoesNotCreateJumpForSharedTopologyTerminalOrEndpointTouch()
    {
        Guid sharedTerminal = Guid.NewGuid();
        OrthogonalRoute first = new(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            ConnectionType.Cable,
            sharedTerminal,
            Guid.NewGuid(),
            [new DocumentPoint(0, 0), new DocumentPoint(20, 0)]);
        OrthogonalRoute second = new(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            ConnectionType.OverheadLine,
            sharedTerminal,
            Guid.NewGuid(),
            [new DocumentPoint(20, 0), new DocumentPoint(20, 20)]);
        IReadOnlyList<RouteIntersection> intersections =
            new RouteCrossingDetector().Detect([first, second]);

        Assert.Contains(intersections, intersection =>
            intersection.Kind == RouteIntersectionKind.EndpointTouch &&
            intersection.SharesTopologyTerminal);
        Assert.Empty(new LineJumpDecorator()
            .Project(second, intersections, Colors.Black, SceneStrokeStyle.Solid)
            .OfType<SceneArc>());
    }

    [Fact]
    public void Detector_ClassifiesCollinearOverlapAndIgnoresParallelSeparatedSegments()
    {
        OrthogonalRoute first = new(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            ConnectionType.Cable,
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new DocumentPoint(0, 0), new DocumentPoint(30, 0)]);
        OrthogonalRoute overlap = new(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            ConnectionType.Cable,
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new DocumentPoint(10, 0), new DocumentPoint(40, 0)]);
        OrthogonalRoute separated = new(
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            ConnectionType.Cable,
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new DocumentPoint(0, 5), new DocumentPoint(30, 5)]);

        IReadOnlyList<RouteIntersection> intersections =
            new RouteCrossingDetector().Detect([first, overlap, separated]);

        Assert.Single(intersections, intersection =>
            intersection.Kind == RouteIntersectionKind.CollinearOverlap);
        Assert.DoesNotContain(intersections, intersection =>
            intersection.FirstConnectionId == separated.ConnectionId ||
            intersection.SecondConnectionId == separated.ConnectionId);
    }

    [Fact]
    public void Decorator_DoesNotJumpTooCloseToEndpoint()
    {
        OrthogonalRoute horizontal = new(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            ConnectionType.OverheadLine,
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new DocumentPoint(0, 5), new DocumentPoint(40, 5)]);
        OrthogonalRoute vertical = new(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            ConnectionType.OverheadLine,
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new DocumentPoint(5, 0), new DocumentPoint(5, 20)]);
        IReadOnlyList<RouteIntersection> intersections =
            new RouteCrossingDetector().Detect([horizontal, vertical]);

        Assert.Empty(new LineJumpDecorator()
            .Project(vertical, intersections, Colors.Black, SceneStrokeStyle.Solid)
            .OfType<SceneArc>());
    }

    [Fact]
    public void Detector_DoesNotReportAdjacentSegmentsWithinOneRoute()
    {
        OrthogonalRoute route = new(
            Guid.NewGuid(),
            ConnectionType.Cable,
            Guid.NewGuid(),
            Guid.NewGuid(),
            [
                new DocumentPoint(0, 0),
                new DocumentPoint(20, 0),
                new DocumentPoint(20, 20)
            ]);

        Assert.Empty(new RouteCrossingDetector().Detect([route]));
    }

    [Fact]
    public void Decorator_SuppressesJumpsThatAreTooCloseToEachOther()
    {
        OrthogonalRoute horizontal = new(
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            ConnectionType.Cable,
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new DocumentPoint(0, 20), new DocumentPoint(50, 20)]);
        OrthogonalRoute firstVertical = new(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            ConnectionType.OverheadLine,
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new DocumentPoint(20, 0), new DocumentPoint(20, 40)]);
        OrthogonalRoute secondVertical = new(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            ConnectionType.OverheadLine,
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new DocumentPoint(25, 0), new DocumentPoint(25, 40)]);
        IReadOnlyList<RouteIntersection> intersections = new RouteCrossingDetector()
            .Detect([horizontal, firstVertical, secondVertical]);

        SceneArc[] jumps = new LineJumpDecorator()
            .Project(horizontal, intersections, Colors.Black, SceneStrokeStyle.Dashed)
            .OfType<SceneArc>()
            .ToArray();

        Assert.Single(jumps);
    }

    private static (OrthogonalRoute Lower, OrthogonalRoute Upper) CreateCrossingRoutes()
    {
        OrthogonalRoute lower = new(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            ConnectionType.OverheadLine,
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new DocumentPoint(0, 20), new DocumentPoint(40, 20)]);
        OrthogonalRoute upper = new(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            ConnectionType.Cable,
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new DocumentPoint(20, 0), new DocumentPoint(20, 40)]);
        return (lower, upper);
    }
}
