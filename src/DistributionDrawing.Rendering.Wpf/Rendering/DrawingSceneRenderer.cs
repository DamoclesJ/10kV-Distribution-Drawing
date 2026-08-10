using System.Globalization;
using System.Windows;
using System.Windows.Media;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

public sealed class DrawingSceneRenderer
{
    private readonly DocumentCoordinateSystem _coordinates;

    public DrawingSceneRenderer(DocumentCoordinateSystem? coordinates = null)
    {
        _coordinates = coordinates ?? new DocumentCoordinateSystem();
    }

    public DrawingVisual Render(DrawingScene scene, double pixelsPerDip)
    {
        var visual = new DrawingVisual();

        using DrawingContext context = visual.RenderOpen();

        foreach (SceneElement element in scene.Elements)
        {
            switch (element)
            {
                case SceneLine line:
                    DrawLine(context, line);
                    break;
                case SceneRectangle rectangle:
                    DrawRectangle(context, rectangle);
                    break;
                case SceneText text:
                    DrawText(context, text, pixelsPerDip);
                    break;
            }
        }

        return visual;
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
        context.DrawGeometry(null, CreatePen(line.Stroke, line.ThicknessMillimeters), geometry);
    }

    private void DrawRectangle(DrawingContext context, SceneRectangle rectangle)
    {
        Geometry geometry = new RectangleGeometry(_coordinates.ToRect(rectangle.Bounds));
        geometry.Freeze();

        Brush? fill = rectangle.Fill is Color fillColor
            ? CreateBrush(fillColor)
            : null;

        context.DrawGeometry(
            fill,
            CreatePen(rectangle.Stroke, rectangle.ThicknessMillimeters),
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

    private Pen CreatePen(Color color, double thicknessMillimeters)
    {
        var pen = new Pen(
            CreateBrush(color),
            _coordinates.MillimetersToDip(thicknessMillimeters));
        pen.Freeze();
        return pen;
    }

    private static Brush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
