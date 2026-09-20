using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class VisibilityGraphConstructionDifferentialTests
{
    [Fact]
    public void AxisIntervalIndex_MatchesLegacyGraphAcrossBoundaryMatrix()
    {
        foreach (GraphFixture fixture in BoundaryFixtures())
        {
            AssertEquivalent(fixture);
        }
    }

    [Fact]
    public void AxisIntervalIndex_MatchesLegacyGraphForFixedSeedRectangles()
    {
        const int seed = 731_901;
        var random = new Random(seed);
        for (int fixtureIndex = 0; fixtureIndex < 80; fixtureIndex++)
        {
            int obstacleCount = random.Next(0, 13);
            var obstacles = new RoutingObstacle[obstacleCount];
            for (int obstacleIndex = 0; obstacleIndex < obstacleCount; obstacleIndex++)
            {
                double left = random.Next(-8, 18) * 5;
                double top = random.Next(-8, 18) * 5;
                double width = random.Next(0, 8) * 5;
                double height = random.Next(0, 8) * 5;
                obstacles[obstacleIndex] = Obstacle(
                    fixtureIndex * 100 + obstacleIndex + 1,
                    left,
                    top,
                    width,
                    height);
            }

            var fixture = new GraphFixture(
                $"seed={seed}; fixture={fixtureIndex}; " +
                $"start=(-20,-15); end=(105,95); obstacles={Format(obstacles)}",
                new DocumentPoint(-20, -15),
                new DocumentPoint(105, 95),
                obstacles);
            AssertEquivalent(fixture);
        }
    }

    [Fact]
    public void AxisIntervalIndex_PreservesOpenBoundaryAndTouchingIntervalSemantics()
    {
        RoutingObstacle[] obstacles =
        [
            Obstacle(1, 0, 0, 10, 10),
            Obstacle(2, 10, 0, 10, 10)
        ];
        double[] axes = [0, 5, 10, 15, 20];
        var index = new VisibilityObstacleIndex(axes, axes, obstacles);

        Assert.False(index.ContainsInterior(new DocumentPoint(10, 5)));
        Assert.True(index.ContainsInterior(new DocumentPoint(5, 5)));
        Assert.True(index.ContainsInterior(new DocumentPoint(15, 5)));
        Assert.False(index.ContainsInterior(new DocumentPoint(5, 0)));
        Assert.False(index.IntersectsInterior(Segment(0, 0, 20, 0)));
        Assert.False(index.IntersectsInterior(Segment(0, 10, 20, 10)));
        Assert.True(index.IntersectsInterior(Segment(0, 5, 20, 5)));
        Assert.False(index.IntersectsInterior(Segment(10, -5, 10, 15)));
        Assert.False(index.IntersectsInterior(Segment(5, 5, 5, 5)));
        Assert.Equal(axes.Length * 2, index.IndexCount);
        Assert.True(index.IntervalCount > 0);
    }

    private static void AssertEquivalent(GraphFixture fixture)
    {
        GraphResult legacy = Build(fixture, useIntervalIndex: false);
        GraphResult indexed = Build(fixture, useIntervalIndex: true);
        string context = fixture.Name;

        AssertSequence(legacy.XCoordinates, indexed.XCoordinates, context, "X axes");
        AssertSequence(legacy.YCoordinates, indexed.YCoordinates, context, "Y axes");
        AssertSequence(legacy.Nodes, indexed.Nodes, context, "ordered nodes");
        AssertSequence(legacy.PotentialPairs, indexed.PotentialPairs, context, "potential pairs");
        AssertSequence(legacy.AcceptedEdges, indexed.AcceptedEdges, context, "accepted edges");
        Assert.Equal(legacy.Nodes.Length, indexed.Nodes.Length);
        foreach (DocumentPoint node in legacy.Nodes)
        {
            AssertSequence(
                legacy.Adjacency[node],
                indexed.Adjacency[node],
                context,
                $"raw adjacency at {node}");
            AssertSequence(
                legacy.DijkstraNeighborOrder[node],
                indexed.DijkstraNeighborOrder[node],
                context,
                $"Dijkstra neighbors at {node}");
        }

        AssertNullableSequence(legacy.DijkstraPath, indexed.DijkstraPath, context, "Dijkstra path");
        AssertNullableSequence(legacy.NormalizedPath, indexed.NormalizedPath, context, "normalized path");
    }

    private static GraphResult Build(GraphFixture fixture, bool useIntervalIndex)
    {
        var xCoordinates = new SortedSet<double>
        {
            fixture.Start.XMillimeters,
            fixture.End.XMillimeters
        };
        var yCoordinates = new SortedSet<double>
        {
            fixture.Start.YMillimeters,
            fixture.End.YMillimeters
        };
        RoutingObstacle[] pathfindingObstacles = fixture.Obstacles
            .Where(obstacle =>
                obstacle.Kind is RoutingObstacleKind.Transformer or
                    RoutingObstacleKind.CustomerStation ||
                !obstacle.Contains(fixture.Start) && !obstacle.Contains(fixture.End))
            .ToArray();
        foreach (RoutingObstacle obstacle in pathfindingObstacles)
        {
            xCoordinates.Add(obstacle.Bounds.XMillimeters);
            xCoordinates.Add(obstacle.Bounds.XMillimeters + obstacle.Bounds.WidthMillimeters);
            yCoordinates.Add(obstacle.Bounds.YMillimeters);
            yCoordinates.Add(obstacle.Bounds.YMillimeters + obstacle.Bounds.HeightMillimeters);
        }

        VisibilityObstacleIndex? index = useIntervalIndex
            ? new VisibilityObstacleIndex(xCoordinates, yCoordinates, pathfindingObstacles)
            : null;
        bool PointBlocked(DocumentPoint point) => index is null
            ? pathfindingObstacles.Any(obstacle => ContainsInterior(obstacle.Bounds, point))
            : index.ContainsInterior(point);
        bool EdgeBlocked(OrthogonalRouteSegment segment) => index is null
            ? pathfindingObstacles.Any(obstacle => IntersectsInterior(segment, obstacle.Bounds))
            : index.IntersectsInterior(segment);

        DocumentPoint[] nodes = xCoordinates
            .SelectMany(x => yCoordinates.Select(y => new DocumentPoint(x, y)))
            .Where(point => !PointBlocked(point))
            .OrderBy(point => point.XMillimeters)
            .ThenBy(point => point.YMillimeters)
            .ToArray();
        var adjacency = nodes.ToDictionary(point => point, _ => new List<DocumentPoint>());
        var potentialPairs = new List<GraphEdge>();
        var acceptedEdges = new List<GraphEdge>();

        foreach (IGrouping<double, DocumentPoint> column in
                 nodes.GroupBy(point => point.XMillimeters))
        {
            Connect(
                column.OrderBy(point => point.YMillimeters).ToArray(),
                adjacency,
                potentialPairs,
                acceptedEdges,
                EdgeBlocked);
        }

        foreach (IGrouping<double, DocumentPoint> row in
                 nodes.GroupBy(point => point.YMillimeters))
        {
            Connect(
                row.OrderBy(point => point.XMillimeters).ToArray(),
                adjacency,
                potentialPairs,
                acceptedEdges,
                EdgeBlocked);
        }

        var neighborOrder = adjacency.ToDictionary(
            item => item.Key,
            item => item.Value
                .OrderBy(point => point.XMillimeters)
                .ThenBy(point => point.YMillimeters)
                .ToArray());
        DocumentPoint[]? path = FindPath(fixture.Start, fixture.End, nodes, adjacency);
        return new GraphResult(
            xCoordinates.ToArray(),
            yCoordinates.ToArray(),
            nodes,
            potentialPairs.ToArray(),
            acceptedEdges.ToArray(),
            adjacency,
            neighborOrder,
            path,
            path is null ? null : Normalize(path).ToArray());
    }

    private static void Connect(
        IReadOnlyList<DocumentPoint> ordered,
        IDictionary<DocumentPoint, List<DocumentPoint>> adjacency,
        ICollection<GraphEdge> potentialPairs,
        ICollection<GraphEdge> acceptedEdges,
        Func<OrthogonalRouteSegment, bool> blocked)
    {
        for (int index = 1; index < ordered.Count; index++)
        {
            DocumentPoint previous = ordered[index - 1];
            DocumentPoint current = ordered[index];
            var edge = new GraphEdge(previous, current);
            potentialPairs.Add(edge);
            if (blocked(new OrthogonalRouteSegment(previous, current, 0)))
            {
                continue;
            }

            acceptedEdges.Add(edge);
            adjacency[previous].Add(current);
            adjacency[current].Add(previous);
        }
    }

    private static DocumentPoint[]? FindPath(
        DocumentPoint start,
        DocumentPoint end,
        IReadOnlyList<DocumentPoint> nodes,
        IReadOnlyDictionary<DocumentPoint, List<DocumentPoint>> adjacency)
    {
        if (!adjacency.ContainsKey(start) || !adjacency.ContainsKey(end))
        {
            return null;
        }

        var distances = nodes.ToDictionary(point => point, _ => double.PositiveInfinity);
        var previous = new Dictionary<DocumentPoint, DocumentPoint>();
        var queue = new PriorityQueue<DocumentPoint, (double Distance, double X, double Y)>();
        distances[start] = 0;
        queue.Enqueue(start, (0, start.XMillimeters, start.YMillimeters));
        while (queue.TryDequeue(out DocumentPoint current, out var priority))
        {
            if (priority.Distance > distances[current])
            {
                continue;
            }

            if (current == end)
            {
                break;
            }

            foreach (DocumentPoint neighbor in adjacency[current]
                         .OrderBy(point => point.XMillimeters)
                         .ThenBy(point => point.YMillimeters))
            {
                double distance = distances[current] + ManhattanDistance(current, neighbor);
                if (distance >= distances[neighbor])
                {
                    continue;
                }

                distances[neighbor] = distance;
                previous[neighbor] = current;
                queue.Enqueue(neighbor, (distance, neighbor.XMillimeters, neighbor.YMillimeters));
            }
        }

        if (!previous.ContainsKey(end))
        {
            return null;
        }

        var path = new List<DocumentPoint> { end };
        while (path[^1] != start)
        {
            path.Add(previous[path[^1]]);
        }

        path.Reverse();
        return path.ToArray();
    }

    private static IReadOnlyList<DocumentPoint> Normalize(IEnumerable<DocumentPoint> points)
    {
        var values = new List<DocumentPoint>();
        foreach (DocumentPoint point in points)
        {
            if (values.Count == 0 || values[^1] != point)
            {
                values.Add(point);
            }
        }

        var result = new List<DocumentPoint>();
        foreach (DocumentPoint point in values)
        {
            result.Add(point);
            while (result.Count >= 3 &&
                   (result[^3].XMillimeters == result[^2].XMillimeters &&
                    result[^2].XMillimeters == result[^1].XMillimeters ||
                    result[^3].YMillimeters == result[^2].YMillimeters &&
                    result[^2].YMillimeters == result[^1].YMillimeters))
            {
                result.RemoveAt(result.Count - 2);
            }
        }

        return result;
    }

    private static IEnumerable<GraphFixture> BoundaryFixtures()
    {
        yield return Fixture("no obstacles", new(0, 0), new(100, 60));
        yield return Fixture("single obstacle", new(0, 20), new(100, 60),
            Obstacle(1, 35, 15, 20, 30));
        yield return Fixture("multiple obstacles", new(-10, 5), new(110, 75),
            Obstacle(1, 20, 0, 20, 35), Obstacle(2, 65, 30, 25, 35));
        yield return Fixture("overlapping obstacles", new(-10, 0), new(100, 80),
            Obstacle(1, 20, 20, 40, 35), Obstacle(2, 45, 35, 35, 30));
        yield return Fixture("nested obstacles", new(-10, 0), new(100, 80),
            Obstacle(1, 20, 15, 60, 50), Obstacle(2, 35, 30, 20, 15));
        yield return Fixture("touching obstacles", new(-10, 5), new(30, 15),
            Obstacle(1, 0, 0, 10, 20), Obstacle(2, 10, 0, 10, 20));
        yield return Fixture("duplicate obstacle boundaries", new(-10, -10), new(80, 70),
            Obstacle(1, 10, 10, 30, 30), Obstacle(2, 10, 20, 30, 20));
        yield return Fixture("duplicate axes", new(10, 10), new(40, 40),
            Obstacle(1, 10, 0, 30, 50), Obstacle(2, 10, 10, 30, 30));
        yield return Fixture("point on every obstacle edge", new(0, 5), new(10, 5),
            Obstacle(1, 0, 0, 10, 10));
        yield return Fixture("point on obstacle corner", new(0, 0), new(20, 20),
            Obstacle(1, 0, 0, 10, 10));
        yield return Fixture("edge along obstacle boundary", new(-10, 0), new(20, 0),
            Obstacle(1, 0, 0, 10, 10));
        yield return Fixture("edge touches obstacle corner", new(-10, 0), new(0, 0),
            Obstacle(1, 0, 0, 10, 10));
        yield return Fixture("edge ends at obstacle boundary", new(-10, 5), new(0, 5),
            Obstacle(1, 0, 0, 10, 10));
        yield return Fixture("start and end axes equal obstacle axes", new(0, 0), new(10, 10),
            Obstacle(1, 0, 0, 10, 10));
        yield return Fixture("start and end axes inserted between obstacle axes", new(5, 7), new(25, 23),
            Obstacle(1, 0, 0, 10, 10), Obstacle(2, 20, 20, 10, 10));
        yield return Fixture("horizontal route", new(-10, 5), new(40, 5),
            Obstacle(1, 10, 0, 10, 10));
        yield return Fixture("vertical route", new(5, -10), new(5, 40),
            Obstacle(1, 0, 10, 10, 10));
        yield return Fixture("aligned start and end", new(5, 5), new(5, 25),
            Obstacle(1, 10, 10, 10, 10));
        RoutingObstacle endpointObstacle = Obstacle(1, 0, 0, 10, 10);
        yield return Fixture(
            "endpoint inside ordinary obstacle excluded by pathfinding filter",
            new(5, 5),
            new(50, 50),
            endpointObstacle);
        RoutingObstacle transformerOwner = Obstacle(
            1, 0, 0, 10, 10, RoutingObstacleKind.Transformer);
        RoutingObstacle customerStationOwner = Obstacle(
            2, 45, 45, 10, 10, RoutingObstacleKind.CustomerStation);
        RoutingObstacle unrelated = Obstacle(3, 20, 20, 10, 10);
        yield return Fixture(
            "Transformer and CustomerStation stable owner obstacles retained",
            new(5, 5),
            new(50, 50),
            transformerOwner,
            customerStationOwner,
            unrelated);
        RoutingObstacle[] reversed =
        [Obstacle(1, 10, 10, 15, 20), Obstacle(2, 40, 25, 20, 15)];
        yield return Fixture("reversed obstacle input order", new(0, 0), new(80, 60),
            reversed.Reverse().ToArray());
    }

    private static GraphFixture Fixture(
        string name,
        DocumentPoint start,
        DocumentPoint end,
        params RoutingObstacle[] obstacles) => new(name, start, end, obstacles);

    private static RoutingObstacle Obstacle(
        int id,
        double x,
        double y,
        double width,
        double height,
        RoutingObstacleKind kind = RoutingObstacleKind.RingCabinet) => new(
        Guid.Parse($"83000000-0000-0000-0000-{id:D12}"),
        kind,
        new DocumentRect(x, y, width, height));

    private static OrthogonalRouteSegment Segment(
        double x1,
        double y1,
        double x2,
        double y2) => new(new DocumentPoint(x1, y1), new DocumentPoint(x2, y2), 0);

    private static bool ContainsInterior(DocumentRect bounds, DocumentPoint point) =>
        point.XMillimeters > bounds.XMillimeters &&
        point.XMillimeters < bounds.XMillimeters + bounds.WidthMillimeters &&
        point.YMillimeters > bounds.YMillimeters &&
        point.YMillimeters < bounds.YMillimeters + bounds.HeightMillimeters;

    private static bool IntersectsInterior(OrthogonalRouteSegment segment, DocumentRect bounds)
    {
        if (segment.IsHorizontal)
        {
            double y = segment.Start.YMillimeters;
            return y > bounds.YMillimeters &&
                   y < bounds.YMillimeters + bounds.HeightMillimeters &&
                   Math.Max(Math.Min(segment.Start.XMillimeters, segment.End.XMillimeters),
                       bounds.XMillimeters) <
                   Math.Min(Math.Max(segment.Start.XMillimeters, segment.End.XMillimeters),
                       bounds.XMillimeters + bounds.WidthMillimeters);
        }

        double x = segment.Start.XMillimeters;
        return x > bounds.XMillimeters &&
               x < bounds.XMillimeters + bounds.WidthMillimeters &&
               Math.Max(Math.Min(segment.Start.YMillimeters, segment.End.YMillimeters),
                   bounds.YMillimeters) <
               Math.Min(Math.Max(segment.Start.YMillimeters, segment.End.YMillimeters),
                   bounds.YMillimeters + bounds.HeightMillimeters);
    }

    private static double ManhattanDistance(DocumentPoint first, DocumentPoint second) =>
        Math.Abs(first.XMillimeters - second.XMillimeters) +
        Math.Abs(first.YMillimeters - second.YMillimeters);

    private static void AssertSequence<T>(
        IEnumerable<T> expected,
        IEnumerable<T> actual,
        string context,
        string field) => Assert.True(
        expected.SequenceEqual(actual),
        $"{context}: {field} differ. Expected [{string.Join(", ", expected)}], " +
        $"actual [{string.Join(", ", actual)}].");

    private static void AssertNullableSequence<T>(
        IEnumerable<T>? expected,
        IEnumerable<T>? actual,
        string context,
        string field)
    {
        Assert.Equal(expected is null, actual is null);
        if (expected is not null && actual is not null)
        {
            AssertSequence(expected, actual, context, field);
        }
    }

    private static string Format(IEnumerable<RoutingObstacle> obstacles) => string.Join(
        ";",
        obstacles.Select(obstacle =>
            $"({obstacle.Bounds.XMillimeters},{obstacle.Bounds.YMillimeters}," +
            $"{obstacle.Bounds.WidthMillimeters},{obstacle.Bounds.HeightMillimeters})"));

    private sealed record GraphFixture(
        string Name,
        DocumentPoint Start,
        DocumentPoint End,
        RoutingObstacle[] Obstacles);

    private readonly record struct GraphEdge(DocumentPoint Start, DocumentPoint End);

    private sealed record GraphResult(
        double[] XCoordinates,
        double[] YCoordinates,
        DocumentPoint[] Nodes,
        GraphEdge[] PotentialPairs,
        GraphEdge[] AcceptedEdges,
        IReadOnlyDictionary<DocumentPoint, List<DocumentPoint>> Adjacency,
        IReadOnlyDictionary<DocumentPoint, DocumentPoint[]> DijkstraNeighborOrder,
        DocumentPoint[]? DijkstraPath,
        DocumentPoint[]? NormalizedPath);
}
