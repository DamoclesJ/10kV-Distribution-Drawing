using System.Windows.Media;
using DistributionDrawing.Application.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;
using ApplicationSelectionTargetKind = DistributionDrawing.Application.Interaction.SelectionTargetKind;
using RenderingHitTestResult = DistributionDrawing.Rendering.Wpf.Interaction.HitTestResult;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class ScenePrimitiveTests
{
    [Fact]
    public void Ellipse_PreservesGeometryAndHitTestMetadata()
    {
        Guid targetId = Guid.NewGuid();
        var ellipse = new SceneEllipse(
            new DocumentRect(10, 20, 14, 14),
            Colors.Black,
            1,
            Colors.White)
        {
            TargetKind = ApplicationSelectionTargetKind.Pole,
            TargetId = targetId
        };

        Assert.Equal(new DocumentRect(10, 20, 14, 14), ellipse.Bounds);
        Assert.Equal(new DocumentRect(9.5, 19.5, 15, 15), ellipse.HitTestBounds);
        Assert.Equal(ApplicationSelectionTargetKind.Pole, ellipse.TargetKind);
        Assert.Equal(targetId, ellipse.TargetId);
        Assert.Equal(Colors.White, ellipse.Fill);
    }

    [Fact]
    public void Ellipse_DefaultHitTestBoundsCanSelectTarget()
    {
        Guid targetId = Guid.NewGuid();
        var ellipse = new SceneEllipse(
            new DocumentRect(10, 20, 14, 14),
            Colors.Black,
            1)
        {
            TargetKind = ApplicationSelectionTargetKind.Pole,
            TargetId = targetId
        };

        RenderingHitTestResult? result = new HitTestService().HitTest(
            [ellipse],
            new DocumentPoint(10, 20));

        Assert.Equal(targetId, result?.Target.TargetId);
    }

    [Fact]
    public void OpenPolyline_PreservesPointsAndCalculatesBounds()
    {
        Guid targetId = Guid.NewGuid();
        var polyline = new ScenePolyline(
            [
                new DocumentPoint(5, 8),
                new DocumentPoint(20, 2),
                new DocumentPoint(30, 12)
            ],
            isClosed: false,
            Colors.Black,
            2)
        {
            TargetKind = ApplicationSelectionTargetKind.CableSegment,
            TargetId = targetId
        };

        Assert.False(polyline.IsClosed);
        Assert.Null(polyline.Fill);
        Assert.Equal(3, polyline.Points.Count);
        Assert.Equal(new DocumentRect(5, 2, 25, 10), polyline.Bounds);
        Assert.Equal(new DocumentRect(4, 1, 27, 12), polyline.HitTestBounds);
        Assert.Equal(
            targetId,
            new HitTestService()
                .HitTest([polyline], new DocumentPoint(30.5, 12.5))?
                .Target.TargetId);
    }

    [Fact]
    public void ClosedPolyline_PreservesPolygonFillAndBounds()
    {
        var polygon = new ScenePolyline(
            [
                new DocumentPoint(10, 10),
                new DocumentPoint(20, 30),
                new DocumentPoint(0, 30)
            ],
            isClosed: true,
            Colors.Black,
            0.8,
            Colors.White);

        Assert.True(polygon.IsClosed);
        Assert.Equal(Colors.White, polygon.Fill);
        Assert.Equal(new DocumentRect(0, 10, 20, 20), polygon.Bounds);
    }

    [Fact]
    public void SceneBoundsCalculator_IncludesEllipseAndPolylineStroke()
    {
        var scene = new DrawingScene(
        [
            new SceneEllipse(
                new DocumentRect(10, 20, 8, 4),
                Colors.Black,
                2),
            new ScenePolyline(
                [new DocumentPoint(30, 10), new DocumentPoint(40, 30)],
                isClosed: false,
                Colors.Black,
                2)
        ]);

        bool calculated = DrawingSceneBoundsCalculator.TryCalculate(scene, out DocumentRect bounds);

        Assert.True(calculated);
        Assert.Equal(new DocumentRect(9, 9, 32, 22), bounds);
    }

    [Fact]
    public void Arc_PreservesStyleMetadataAndParticipatesInSceneBounds()
    {
        Guid targetId = Guid.NewGuid();
        var arc = new SceneArc(
            new DocumentPoint(20, 30),
            4,
            180,
            180,
            Colors.Black,
            2,
            SceneStrokeStyle.Dashed)
        {
            TargetKind = ApplicationSelectionTargetKind.CableSegment,
            TargetId = targetId
        };

        bool calculated = DrawingSceneBoundsCalculator.TryCalculate(
            new DrawingScene([arc]),
            out DocumentRect bounds);

        Assert.True(calculated);
        Assert.Equal(new DocumentRect(16, 26, 8, 8), arc.Bounds);
        Assert.Equal(new DocumentRect(15, 25, 10, 10), bounds);
        Assert.Equal(SceneStrokeStyle.Dashed, arc.StrokeStyle);
        Assert.Equal(targetId, arc.TargetId);
    }

    [Fact]
    public void ExistingLineAndOverheadLineRemainSolidByDefault()
    {
        var line = new SceneLine(
            new DocumentPoint(0, 0),
            new DocumentPoint(10, 0),
            Colors.Black,
            0.8);
        var overheadLine = new OverheadLineSegment(
            Guid.NewGuid(),
            new DocumentPoint(0, 0),
            new DocumentPoint(10, 0),
            Colors.Black,
            0.8);

        Assert.Equal(SceneStrokeStyle.Solid, line.StrokeStyle);
        Assert.All(
            overheadLine.CreateElements().OfType<SceneLine>(),
            element => Assert.Equal(SceneStrokeStyle.Solid, element.StrokeStyle));
    }
}
