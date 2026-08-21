using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

internal static class SceneElementAssertions
{
    public static void Equal(
        IReadOnlyList<SceneElement> expected,
        IReadOnlyList<SceneElement> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            SceneElement left = expected[index];
            SceneElement right = actual[index];
            Assert.Equal(left.GetType(), right.GetType());
            Assert.Equal(left.TargetKind, right.TargetKind);
            Assert.Equal(left.TargetId, right.TargetId);
            Assert.Equal(left.HitTestBounds, right.HitTestBounds);

            if (left is ScenePolyline leftPolyline && right is ScenePolyline rightPolyline)
            {
                Assert.Equal(leftPolyline.Points, rightPolyline.Points);
                Assert.Equal(leftPolyline.IsClosed, rightPolyline.IsClosed);
                Assert.Equal(leftPolyline.Bounds, rightPolyline.Bounds);
                Assert.Equal(leftPolyline.Stroke, rightPolyline.Stroke);
                Assert.Equal(leftPolyline.ThicknessMillimeters, rightPolyline.ThicknessMillimeters);
                Assert.Equal(leftPolyline.Fill, rightPolyline.Fill);
                Assert.Equal(leftPolyline.StrokeStyle, rightPolyline.StrokeStyle);
                continue;
            }

            Assert.Equal(left, right);
        }
    }
}
