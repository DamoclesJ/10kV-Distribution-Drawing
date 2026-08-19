namespace DistributionDrawing.Rendering.Wpf.Scene;

public static class DrawingSceneBoundsCalculator
{
    public static bool TryCalculate(DrawingScene scene, out DocumentRect bounds)
    {
        ArgumentNullException.ThrowIfNull(scene);

        BoundsAccumulator accumulator = new();
        foreach (SceneElement element in scene.Elements)
        {
            switch (element)
            {
                case SceneLogicalBounds logicalBounds:
                    accumulator.Include(logicalBounds.Bounds, 0);
                    break;
                case SceneLine line:
                    double halfThickness = Math.Max(0, line.ThicknessMillimeters) / 2;
                    accumulator.Include(line.Start, halfThickness);
                    accumulator.Include(line.End, halfThickness);
                    break;
                case SceneEllipse ellipse:
                    double ellipsePadding = Math.Max(0, ellipse.ThicknessMillimeters) / 2;
                    accumulator.Include(ellipse.Bounds, ellipsePadding);
                    break;
                case ScenePolyline polyline:
                    double polylinePadding = Math.Max(0, polyline.ThicknessMillimeters) / 2;
                    accumulator.Include(polyline.Bounds, polylinePadding);
                    break;
                case SceneRectangle rectangle:
                    double rectanglePadding = Math.Max(0, rectangle.ThicknessMillimeters) / 2;
                    accumulator.Include(rectangle.Bounds, rectanglePadding);
                    break;
                case SceneText text:
                    double textWidth = Math.Max(
                        text.FontSizeMillimeters,
                        text.Text.Length * text.FontSizeMillimeters * 0.65);
                    double textHeight = Math.Max(1, text.FontSizeMillimeters * 1.3);
                    accumulator.Include(
                        new DocumentRect(
                            text.Origin.XMillimeters,
                            text.Origin.YMillimeters,
                            textWidth,
                            textHeight),
                        0);
                    break;
            }
        }

        return accumulator.TryCreate(out bounds);
    }

    private sealed class BoundsAccumulator
    {
        private double _minimumX = double.PositiveInfinity;
        private double _minimumY = double.PositiveInfinity;
        private double _maximumX = double.NegativeInfinity;
        private double _maximumY = double.NegativeInfinity;

        public void Include(DocumentPoint point, double padding)
        {
            Include(
                new DocumentRect(
                    point.XMillimeters,
                    point.YMillimeters,
                    0,
                    0),
                padding);
        }

        public void Include(DocumentRect rect, double padding)
        {
            double left = rect.XMillimeters - padding;
            double top = rect.YMillimeters - padding;
            double right = rect.XMillimeters + rect.WidthMillimeters + padding;
            double bottom = rect.YMillimeters + rect.HeightMillimeters + padding;
            if (!IsFinite(left) || !IsFinite(top) || !IsFinite(right) || !IsFinite(bottom))
            {
                return;
            }

            _minimumX = Math.Min(_minimumX, left);
            _minimumY = Math.Min(_minimumY, top);
            _maximumX = Math.Max(_maximumX, right);
            _maximumY = Math.Max(_maximumY, bottom);
        }

        public bool TryCreate(out DocumentRect bounds)
        {
            if (double.IsPositiveInfinity(_minimumX))
            {
                bounds = default;
                return false;
            }

            bounds = new DocumentRect(
                _minimumX,
                _minimumY,
                _maximumX - _minimumX,
                _maximumY - _minimumY);
            return true;
        }

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
