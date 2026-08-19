using System.Windows.Media;
using DistributionDrawing.Application.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction;
using ApplicationSelectionTargetKind = DistributionDrawing.Application.Interaction.SelectionTargetKind;

namespace DistributionDrawing.Rendering.Wpf.Scene;

public sealed class DrawingScene
{
    public DrawingScene(
        IEnumerable<SceneElement> elements,
        SelectionHitTestIndex? hitTestIndex = null)
    {
        Elements = elements.ToArray();
        HitTestIndex = hitTestIndex ?? new SelectionHitTestIndex();
    }

    public IReadOnlyList<SceneElement> Elements { get; }

    public SelectionHitTestIndex HitTestIndex { get; }
}

public abstract record SceneElement
{
    public ApplicationSelectionTargetKind? TargetKind { get; init; }

    public Guid? TargetId { get; init; }

    public DocumentRect? HitTestBounds { get; init; }
}

/// <summary>
/// Preserves a logical scene extent without producing visible geometry.
/// Selection remains owned by <see cref="SelectionHitTestIndex"/>.
/// </summary>
public sealed record SceneLogicalBounds(DocumentRect Bounds) : SceneElement;

public sealed record SceneLine(
    DocumentPoint Start,
    DocumentPoint End,
    Color Stroke,
    double ThicknessMillimeters,
    SceneStrokeStyle StrokeStyle = SceneStrokeStyle.Solid) : SceneElement;

public sealed record SceneRectangle(
    DocumentRect Bounds,
    Color Stroke,
    double ThicknessMillimeters,
    Color? Fill = null,
    SceneStrokeStyle StrokeStyle = SceneStrokeStyle.Solid) : SceneElement;

public sealed record SceneText(
    DocumentPoint Origin,
    string Text,
    Color Foreground,
    double FontSizeMillimeters) : SceneElement;
