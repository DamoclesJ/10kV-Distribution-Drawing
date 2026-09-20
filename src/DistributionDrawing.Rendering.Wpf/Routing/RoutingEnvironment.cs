using System.Globalization;
using System.Collections.ObjectModel;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Routing;

internal sealed class RoutingEnvironment
{
    private readonly RoutingObstacle[] _sourceObstacles;
    private readonly IReadOnlyList<RoutingObstacle> _sourceObstacleView;
    private readonly RoutingEnvironmentCounters? _counters;
    private readonly string _environmentSignature;
    private readonly IReadOnlyDictionary<Guid, string> _sourceIdText;
    private readonly Dictionary<double, IReadOnlyList<RoutingObstacle>> _expandedObstacles = [];
    private readonly Dictionary<RoutingObstacle, string> _obstacleIdentityText = [];
    private readonly Dictionary<EffectiveRoutingSignature, EffectiveRoutingView> _effectiveViews = [];
    private readonly Dictionary<VisibilityAxisSignature, VisibilityAxisBasis> _visibilityAxisBases = [];

    public RoutingEnvironment(
        IEnumerable<RoutingObstacle> obstacles,
        DrawingMetrics metrics,
        RoutingEnvironmentCounters? counters = null)
    {
        ArgumentNullException.ThrowIfNull(obstacles);
        ArgumentNullException.ThrowIfNull(metrics);
        _counters = counters;
        _sourceObstacles = obstacles.OrderBy(obstacle => obstacle.SourceId).ToArray();
        _sourceObstacleView = Array.AsReadOnly(_sourceObstacles);
        _sourceIdText = new ReadOnlyDictionary<Guid, string>(_sourceObstacles
            .Select(obstacle => obstacle.SourceId)
            .Distinct()
            .ToDictionary(id => id, id => id.ToString("N")));
        foreach (RoutingObstacle obstacle in _sourceObstacles)
        {
            _obstacleIdentityText.TryAdd(obstacle, FormatObstacleIdentity(obstacle));
        }
        if (_counters is not null)
        {
            _counters.ObstacleSortCount++;
        }

        _environmentSignature = BuildEnvironmentSignature(_sourceObstacles, metrics);
    }

    public IReadOnlyList<RoutingObstacle> SourceObstacles => _sourceObstacleView;

    public IReadOnlyDictionary<Guid, string> SourceIdText => _sourceIdText;

    public IReadOnlyList<RoutingObstacle> GetExpandedObstacles(double clearance)
    {
        if (_expandedObstacles.TryGetValue(clearance, out IReadOnlyList<RoutingObstacle>? expanded))
        {
            return expanded;
        }

        RoutingObstacle[] expandedArray = _sourceObstacles
            .Select(obstacle => obstacle.Expand(clearance))
            .ToArray();
        foreach (RoutingObstacle obstacle in expandedArray)
        {
            _obstacleIdentityText.TryAdd(obstacle, FormatObstacleIdentity(obstacle));
        }
        expanded = Array.AsReadOnly(expandedArray);
        _expandedObstacles.Add(clearance, expanded);
        if (_counters is not null)
        {
            _counters.ObstacleExpansionCount += expanded.Count;
        }

        return expanded;
    }

    public EffectiveRoutingView GetEffectiveView(
        IEnumerable<Guid>? excludedSourceIds,
        double clearance,
        double parallelSpacing)
    {
        Guid[] normalizedExclusions = excludedSourceIds?
            .Distinct()
            .OrderBy(id => id)
            .ToArray() ?? [];
        string exclusionKey = string.Join(",", normalizedExclusions.Select(id => id.ToString("N")));
        var signature = new EffectiveRoutingSignature(
            _environmentSignature,
            Format(clearance),
            Format(parallelSpacing),
            exclusionKey);
        if (_effectiveViews.TryGetValue(signature, out EffectiveRoutingView? cached))
        {
            if (_counters is not null)
            {
                _counters.CacheHitCount++;
            }

            return cached;
        }

        if (_counters is not null)
        {
            _counters.CacheMissCount++;
            _counters.EffectiveViewBuildCount++;
        }

        HashSet<Guid> exclusionSet = normalizedExclusions.ToHashSet();
        IReadOnlyList<RoutingObstacle> effectiveObstacles = Array.AsReadOnly(GetExpandedObstacles(clearance)
            .Where(obstacle => !exclusionSet.Contains(obstacle.SourceId))
            .ToArray());
        double[] candidateX = effectiveObstacles
            .SelectMany(obstacle => new[]
            {
                obstacle.Bounds.XMillimeters - parallelSpacing,
                obstacle.Bounds.XMillimeters + obstacle.Bounds.WidthMillimeters + parallelSpacing
            })
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        double[] candidateY = effectiveObstacles
            .SelectMany(obstacle => new[]
            {
                obstacle.Bounds.YMillimeters - parallelSpacing,
                obstacle.Bounds.YMillimeters + obstacle.Bounds.HeightMillimeters + parallelSpacing
            })
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        var view = new EffectiveRoutingView(
            signature,
            effectiveObstacles,
            new CandidateAxisBasis(Array.AsReadOnly(candidateX), Array.AsReadOnly(candidateY)));
        _effectiveViews.Add(signature, view);
        if (_counters is not null)
        {
            _counters.CandidateAxisBasisBuildCount++;
        }

        return view;
    }

    public VisibilityAxisBasis GetVisibilityAxisBasis(
        EffectiveRoutingView effectiveView,
        IReadOnlyList<RoutingObstacle> pathfindingObstacles)
    {
        var signature = new VisibilityAxisSignature(
            effectiveView.Signature,
            BuildObstacleListSignature(pathfindingObstacles));
        if (_visibilityAxisBases.TryGetValue(signature, out VisibilityAxisBasis? cached))
        {
            if (_counters is not null)
            {
                _counters.CacheHitCount++;
            }

            return cached;
        }

        if (_counters is not null)
        {
            _counters.CacheMissCount++;
        }

        double[] xCoordinates = pathfindingObstacles
            .SelectMany(obstacle => new[]
            {
                obstacle.Bounds.XMillimeters,
                obstacle.Bounds.XMillimeters + obstacle.Bounds.WidthMillimeters
            })
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        double[] yCoordinates = pathfindingObstacles
            .SelectMany(obstacle => new[]
            {
                obstacle.Bounds.YMillimeters,
                obstacle.Bounds.YMillimeters + obstacle.Bounds.HeightMillimeters
            })
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        var basis = new VisibilityAxisBasis(
            Array.AsReadOnly(xCoordinates),
            Array.AsReadOnly(yCoordinates));
        _visibilityAxisBases.Add(signature, basis);
        if (_counters is not null)
        {
            _counters.VisibilityAxisBasisBuildCount++;
        }

        return basis;
    }

    private string BuildEnvironmentSignature(
        IReadOnlyList<RoutingObstacle> obstacles,
        DrawingMetrics metrics)
    {
        return string.Concat(
            "metrics=", Format(metrics.Routing.PortStubLength), ",",
            Format(metrics.Routing.ObstacleClearance), ",",
            Format(metrics.Routing.ParallelSpacing), ",",
            Format(metrics.Routing.MinimumDoglegLength), ",",
            Format(metrics.Routing.CrossingTolerance),
            "|obstacles=", BuildObstacleListSignature(obstacles));
    }

    private string BuildObstacleListSignature(IReadOnlyList<RoutingObstacle> obstacles) =>
        BuildObstacleListSignature(obstacles, obstacle => _obstacleIdentityText[obstacle]);

    private static string BuildObstacleListSignature(
        IReadOnlyList<RoutingObstacle> obstacles,
        Func<RoutingObstacle, string> identity) =>
        string.Join(";", obstacles.Select(identity));

    private string FormatObstacleIdentity(RoutingObstacle obstacle) => string.Concat(
            _sourceIdText[obstacle.SourceId], ":",
            ((int)obstacle.Kind).ToString(CultureInfo.InvariantCulture), ":",
            Format(obstacle.Bounds.XMillimeters), ",",
            Format(obstacle.Bounds.YMillimeters), ",",
            Format(obstacle.Bounds.WidthMillimeters), ",",
            Format(obstacle.Bounds.HeightMillimeters));

    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}

internal sealed record EffectiveRoutingView(
    EffectiveRoutingSignature Signature,
    IReadOnlyList<RoutingObstacle> Obstacles,
    CandidateAxisBasis CandidateAxes);

internal readonly record struct EffectiveRoutingSignature(
    string Environment,
    string Clearance,
    string ParallelSpacing,
    string ExcludedSourceIds);

internal readonly record struct VisibilityAxisSignature(
    EffectiveRoutingSignature EffectiveObstacles,
    string PathfindingObstacles);

internal sealed record CandidateAxisBasis(
    IReadOnlyList<double> XCoordinates,
    IReadOnlyList<double> YCoordinates);

internal sealed record VisibilityAxisBasis(
    IReadOnlyList<double> XCoordinates,
    IReadOnlyList<double> YCoordinates);

internal sealed class RoutingEnvironmentCounters
{
    public int ObstacleSortCount { get; set; }
    public int ObstacleExpansionCount { get; set; }
    public int EffectiveViewBuildCount { get; set; }
    public int CandidateAxisBasisBuildCount { get; set; }
    public int VisibilityAxisBasisBuildCount { get; set; }
    public int CacheHitCount { get; set; }
    public int CacheMissCount { get; set; }
}
