using System.Globalization;
using System.Windows;
using System.Windows.Media;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

public sealed class DrawingSceneRenderer
{
    private readonly DocumentCoordinateSystem _coordinates;
    private readonly DrawingMetrics _metrics;

    public DrawingSceneRenderer(
        DocumentCoordinateSystem? coordinates = null,
        DrawingMetrics? metrics = null)
    {
        _coordinates = coordinates ?? new DocumentCoordinateSystem();
        _metrics = metrics ?? DrawingMetrics.Default;
    }

    public DrawingVisual Render(DrawingScene scene, double pixelsPerDip)
    {
        var visual = new DrawingVisual();
        using DrawingContext context = visual.RenderOpen();
        context.DrawDrawing(RenderDrawing(scene, pixelsPerDip));
        return visual;
    }

    public DrawingGroup RenderDrawing(DrawingScene scene, double pixelsPerDip)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var drawing = new DrawingGroup();
        using (DrawingContext context = drawing.Open())
        {
            foreach (SceneElement element in scene.Elements)
            {
                switch (element)
                {
                    case SceneLine line:
                        DrawLine(context, line);
                        break;
                    case SceneEllipse ellipse:
                        DrawEllipse(context, ellipse);
                        break;
                    case ScenePolyline polyline:
                        DrawPolyline(context, polyline);
                        break;
                    case SceneArc arc:
                        DrawArc(context, arc);
                        break;
                    case SceneRectangle rectangle:
                        DrawRectangle(context, rectangle);
                        break;
                    case SceneText text:
                        DrawText(context, text, pixelsPerDip);
                        break;
                }
            }
        }

        drawing.Freeze();
        return drawing;
    }

    private void DrawLine(DrawingContext context, SceneLine line)
    {
        var geometry = new StreamGeometry();

        using (StreamGeometryContext geometryContext = geometry.Open())
        {
            geometryContext.BeginFigure(_coordinates.ToPoint(line.Start), false, false);
            geometryContext.LineTo(_coordinates.ToPoint(line.End), true, false);
        }

        geometry.Freeze();
        context.DrawGeometry(
            null,
            CreatePen(line.Stroke, line.ThicknessMillimeters, line.StrokeStyle),
            geometry);
    }

    private void DrawEllipse(DrawingContext context, SceneEllipse ellipse)
    {
        Geometry geometry = new EllipseGeometry(_coordinates.ToRect(ellipse.Bounds));
        geometry.Freeze();

        context.DrawGeometry(
            CreateOptionalBrush(ellipse.Fill),
            CreatePen(ellipse.Stroke, ellipse.ThicknessMillimeters, ellipse.StrokeStyle),
            geometry);
    }

    private void DrawPolyline(DrawingContext context, ScenePolyline polyline)
    {
        var geometry = new StreamGeometry();

        using (StreamGeometryContext geometryContext = geometry.Open())
        {
            geometryContext.BeginFigure(
                _coordinates.ToPoint(polyline.Points[0]),
                polyline.Fill is not null,
                polyline.IsClosed);
            foreach (DocumentPoint point in polyline.Points.Skip(1))
            {
                geometryContext.LineTo(_coordinates.ToPoint(point), true, false);
            }
        }

        geometry.Freeze();
        context.DrawGeometry(
            CreateOptionalBrush(polyline.Fill),
            CreatePen(polyline.Stroke, polyline.ThicknessMillimeters, polyline.StrokeStyle),
            geometry);
    }

    private void DrawArc(DrawingContext context, SceneArc arc)
    {
        double startRadians = arc.StartAngleDegrees * Math.PI / 180;
        double endRadians =
            (arc.StartAngleDegrees + arc.SweepAngleDegrees) * Math.PI / 180;
        DocumentPoint start = new(
            arc.Center.XMillimeters + Math.Cos(startRadians) * arc.RadiusMillimeters,
            arc.Center.YMillimeters + Math.Sin(startRadians) * arc.RadiusMillimeters);
        DocumentPoint end = new(
            arc.Center.XMillimeters + Math.Cos(endRadians) * arc.RadiusMillimeters,
            arc.Center.YMillimeters + Math.Sin(endRadians) * arc.RadiusMillimeters);
        var figure = new PathFigure
        {
            StartPoint = _coordinates.ToPoint(start),
            IsClosed = false,
            IsFilled = false
        };
        figure.Segments.Add(new ArcSegment(
            _coordinates.ToPoint(end),
            new Size(
                _coordinates.MillimetersToDip(arc.RadiusMillimeters),
                _coordinates.MillimetersToDip(arc.RadiusMillimeters)),
            0,
            Math.Abs(arc.SweepAngleDegrees) > 180,
            arc.SweepAngleDegrees > 0
                ? SweepDirection.Clockwise
                : SweepDirection.Counterclockwise,
            true));
        Geometry geometry = new PathGeometry([figure]);
        geometry.Freeze();
        context.DrawGeometry(
            null,
            CreatePen(arc.Stroke, arc.ThicknessMillimeters, arc.StrokeStyle),
            geometry);
    }

    private void DrawRectangle(DrawingContext context, SceneRectangle rectangle)
    {
        Geometry geometry = new RectangleGeometry(_coordinates.ToRect(rectangle.Bounds));
        geometry.Freeze();

        context.DrawGeometry(
            CreateOptionalBrush(rectangle.Fill),
            CreatePen(
                rectangle.Stroke,
                rectangle.ThicknessMillimeters,
                rectangle.StrokeStyle),
            geometry);
    }

    private void DrawText(DrawingContext context, SceneText text, double pixelsPerDip)
    {
        var formattedText = new FormattedText(
            text.Text,
            CultureInfo.GetCultureInfo("zh-CN"),
            FlowDirection.LeftToRight,
            new Typeface("Microsoft YaHei"),
            _coordinates.MillimetersToDip(text.FontSizeMillimeters),
            CreateBrush(text.Foreground),
            pixelsPerDip);

        context.DrawText(formattedText, _coordinates.ToPoint(text.Origin));
    }

    private Pen CreatePen(
        Color color,
        double thicknessMillimeters,
        SceneStrokeStyle strokeStyle)
    {
        var pen = new Pen(
            CreateBrush(color),
            _coordinates.MillimetersToDip(thicknessMillimeters));
        if (strokeStyle == SceneStrokeStyle.Dashed)
        {
            pen.DashStyle = new DashStyle(
                [
                    _metrics.Line.CableDashLength / thicknessMillimeters,
                    _metrics.Line.CableDashGap / thicknessMillimeters
                ],
                0);
        }

        pen.Freeze();
        return pen;
    }

    private static Brush? CreateOptionalBrush(Color? color)
    {
        return color is Color fillColor ? CreateBrush(fillColor) : null;
    }

    private static Brush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
