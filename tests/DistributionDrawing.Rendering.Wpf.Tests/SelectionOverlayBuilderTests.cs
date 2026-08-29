using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class SelectionOverlayBuilderTests
{
    [Fact]
    public void MultipleSelectionsCreatePrimaryAndSecondaryOverlays()
    {
        SelectionReference first = new(SelectionTargetKind.Device, Guid.NewGuid());
        SelectionReference second = new(SelectionTargetKind.Connection, Guid.NewGuid());
        var manager = new SelectionManager();
        manager.Replace([first, second]);
        var index = new SelectionHitTestIndex(
        [
            new SelectionHitTestEntry(first, new DocumentRect(0, 0, 10, 10), 10),
            new SelectionHitTestEntry(second, new DocumentRect(20, 0, 10, 10), 10)
        ]);

        SceneRectangle[] overlays = SelectionOverlayBuilder.CreateElements(
                index,
                manager.SelectionSet)
            .Cast<SceneRectangle>()
            .ToArray();

        Assert.Equal(2, overlays.Length);
        Assert.Equal(0.8, overlays[0].ThicknessMillimeters);
        Assert.Equal(1.2, overlays[1].ThicknessMillimeters);
    }
}
