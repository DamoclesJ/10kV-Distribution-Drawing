using DistributionDrawing.Rendering.Wpf.Labels;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class LabelLayoutEngineTests
{
    [Fact]
    public void LayoutSingleLabelAppliesAnchorAndOffset()
    {
        Guid targetId = Guid.NewGuid();
        LabelRequest request = new(
            LabelTargetKind.Pole,
            targetId,
            "P-001",
            new DocumentPoint(10, 20),
            new DocumentPoint(3, -2));

        LabelLayoutResult result = Assert.Single(new LabelLayoutEngine().Layout([request]));

        Assert.Equal(new DocumentPoint(13, 18), result.Position);
        Assert.Equal(targetId, result.TargetId);
        Assert.Equal("P-001", result.Text);
        Assert.False(result.WasAdjusted);
        Assert.False(result.HasCollision);
    }

    [Fact]
    public void LayoutMultipleLabelsAvoidsCollisionWhenCandidateOffsetIsAvailable()
    {
        LabelRequest first = CreateRequest(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        LabelRequest second = CreateRequest(Guid.Parse("00000000-0000-0000-0000-000000000002"));

        IReadOnlyList<LabelLayoutResult> results = new LabelLayoutEngine().Layout([first, second]);

        Assert.Equal(2, results.Count);
        Assert.True(results[1].WasAdjusted);
        Assert.False(results[1].HasCollision);
        Assert.False(Overlaps(results[0].Bounds, results[1].Bounds));
    }

    [Fact]
    public void LayoutProducesStableResultsForSameRequests()
    {
        LabelRequest[] requests =
        [
            CreateRequest(Guid.Parse("00000000-0000-0000-0000-000000000002")),
            CreateRequest(Guid.Parse("00000000-0000-0000-0000-000000000001"))
        ];

        IReadOnlyList<LabelLayoutResult> first = new LabelLayoutEngine().Layout(requests);
        IReadOnlyList<LabelLayoutResult> second = new LabelLayoutEngine().Layout(requests);

        Assert.Equal(first.Select(result => result.Position), second.Select(result => result.Position));
        Assert.Equal(first.Select(result => result.Bounds), second.Select(result => result.Bounds));
    }

    private static LabelRequest CreateRequest(Guid targetId)
    {
        return new LabelRequest(
            LabelTargetKind.CableSegment,
            targetId,
            "Cable",
            new DocumentPoint(10, 20),
            new DocumentPoint(0, 0));
    }

    private static bool Overlaps(DocumentRect left, DocumentRect right)
    {
        return left.XMillimeters < right.XMillimeters + right.WidthMillimeters &&
               left.XMillimeters + left.WidthMillimeters > right.XMillimeters &&
               left.YMillimeters < right.YMillimeters + right.HeightMillimeters &&
               left.YMillimeters + left.HeightMillimeters > right.YMillimeters;
    }
}
