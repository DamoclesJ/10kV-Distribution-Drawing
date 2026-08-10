using System.Windows.Media;

namespace DistributionDrawing.Rendering.Wpf.Scene;

public sealed class DrawingScene
{
    public DrawingScene(IEnumerable<SceneElement> elements)
    {
        Elements = elements.ToArray();
    }

    public IReadOnlyList<SceneElement> Elements { get; }
}

public abstract record SceneElement;

public sealed record SceneLine(
    DocumentPoint Start,
    DocumentPoint End,
    Color Stroke,
    double ThicknessMillimeters) : SceneElement;

public sealed record SceneRectangle(
    DocumentRect Bounds,
    Color Stroke,
    double ThicknessMillimeters,
    Color? Fill = null) : SceneElement;

public sealed record SceneText(
    DocumentPoint Origin,
    string Text,
    Color Foreground,
    double FontSizeMillimeters) : SceneElement;
