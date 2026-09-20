using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Routing;

internal sealed class VisibilityObstacleIndex
{
    private readonly IReadOnlyDictionary<double, OpenIntervalIndex> _horizontalByY;
    private readonly IReadOnlyDictionary<double, OpenIntervalIndex> _verticalByX;

    public VisibilityObstacleIndex(
        IEnumerable<double> xCoordinates,
        IEnumerable<double> yCoordinates,
        IReadOnlyList<RoutingObstacle> obstacles)
    {
        ArgumentNullException.ThrowIfNull(xCoordinates);
        ArgumentNullException.ThrowIfNull(yCoordinates);
        ArgumentNullException.ThrowIfNull(obstacles);

        _horizontalByY = yCoordinates.ToDictionary(
            y => y,
            y => OpenIntervalIndex.Create(obstacles
                .Where(obstacle =>
                    obstacle.Bounds.YMillimeters < y &&
                    y < obstacle.Bounds.YMillimeters + obstacle.Bounds.HeightMillimeters)
                .Select(obstacle => new OpenInterval(
                    obstacle.Bounds.XMillimeters,
                    obstacle.Bounds.XMillimeters + obstacle.Bounds.WidthMillimeters))));
        _verticalByX = xCoordinates.ToDictionary(
            x => x,
            x => OpenIntervalIndex.Create(obstacles
                .Where(obstacle =>
                    obstacle.Bounds.XMillimeters < x &&
                    x < obstacle.Bounds.XMillimeters + obstacle.Bounds.WidthMillimeters)
                .Select(obstacle => new OpenInterval(
                    obstacle.Bounds.YMillimeters,
                    obstacle.Bounds.YMillimeters + obstacle.Bounds.HeightMillimeters))));
    }

    internal int IndexCount => _horizontalByY.Count + _verticalByX.Count;

    internal int IntervalCount =>
        _horizontalByY.Values.Sum(index => index.Count) +
        _verticalByX.Values.Sum(index => index.Count);

    public bool ContainsInterior(DocumentPoint point) =>
        _horizontalByY.TryGetValue(point.YMillimeters, out OpenIntervalIndex? index) &&
        index.Contains(point.XMillimeters);

    public bool IntersectsInterior(OrthogonalRouteSegment segment)
    {
        if (segment.IsHorizontal)
        {
            return _horizontalByY.TryGetValue(
                       segment.Start.YMillimeters,
                       out OpenIntervalIndex? horizontal) &&
                   horizontal.Overlaps(
                       segment.Start.XMillimeters,
                       segment.End.XMillimeters);
        }

        return _verticalByX.TryGetValue(
                   segment.Start.XMillimeters,
                   out OpenIntervalIndex? vertical) &&
               vertical.Overlaps(
                   segment.Start.YMillimeters,
                   segment.End.YMillimeters);
    }

    private readonly record struct OpenInterval(double Start, double End);

    private sealed class OpenIntervalIndex
    {
        private readonly double[] _starts;
        private readonly double[] _prefixMaximumEnds;

        private OpenIntervalIndex(OpenInterval[] intervals)
        {
            _starts = new double[intervals.Length];
            _prefixMaximumEnds = new double[intervals.Length];
            double maximumEnd = double.NegativeInfinity;
            for (int index = 0; index < intervals.Length; index++)
            {
                OpenInterval interval = intervals[index];
                _starts[index] = interval.Start;
                maximumEnd = Math.Max(maximumEnd, interval.End);
                _prefixMaximumEnds[index] = maximumEnd;
            }
        }

        public int Count => _starts.Length;

        public static OpenIntervalIndex Create(IEnumerable<OpenInterval> intervals)
        {
            OpenInterval[] ordered = intervals
                .Where(interval => interval.Start < interval.End)
                .OrderBy(interval => interval.Start)
                .ToArray();
            return new OpenIntervalIndex(ordered);
        }

        public bool Contains(double value)
        {
            int lastCandidate = LastStartStrictlyLessThan(value);
            return lastCandidate >= 0 && _prefixMaximumEnds[lastCandidate] > value;
        }

        public bool Overlaps(double first, double second)
        {
            double minimum = Math.Min(first, second);
            double maximum = Math.Max(first, second);
            if (minimum == maximum)
            {
                return false;
            }

            int lastCandidate = LastStartStrictlyLessThan(maximum);
            return lastCandidate >= 0 && _prefixMaximumEnds[lastCandidate] > minimum;
        }

        private int LastStartStrictlyLessThan(double value)
        {
            int lower = 0;
            int upper = _starts.Length;
            while (lower < upper)
            {
                int middle = lower + (upper - lower) / 2;
                if (_starts[middle] < value)
                {
                    lower = middle + 1;
                }
                else
                {
                    upper = middle;
                }
            }

            return lower - 1;
        }
    }
}
