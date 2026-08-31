using DistributionDrawing.Desktop.Actions;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class DesktopContextMenuResolverTests
{
    private readonly DesktopContextMenuResolver _resolver = new();

    [Fact]
    public void ActiveInteractionDoesNotOpenContextMenu()
    {
        Assert.Empty(_resolver.Resolve(false, true, 0, false, false, false));
    }

    [Fact]
    public void IdleBlankCanvasUsesOnlyCanvasActions()
    {
        Assert.Equal(
            [
                DesktopContextActionKind.PasteAtCursor,
                DesktopContextActionKind.SelectAll,
                DesktopContextActionKind.FitDrawing,
                DesktopContextActionKind.ToggleGrid
            ],
            _resolver.Resolve(true, true, 0, false, false, false));
    }

    [Fact]
    public void IdleMultiSelectionUsesCopyAndDeleteOnly()
    {
        Assert.Equal(
            [DesktopContextActionKind.Copy, DesktopContextActionKind.Delete],
            _resolver.Resolve(true, false, 3, true, true, true));
    }

    [Fact]
    public void SingleSelectionAddsOnlyApplicableObjectActions()
    {
        IReadOnlyList<DesktopContextActionKind> attachment =
            _resolver.Resolve(true, false, 1, true, false, false);
        IReadOnlyList<DesktopContextActionKind> switchDevice =
            _resolver.Resolve(true, false, 1, false, true, false);
        IReadOnlyList<DesktopContextActionKind> cable =
            _resolver.Resolve(true, false, 1, false, false, true);

        Assert.Contains(DesktopContextActionKind.RotateLeft, attachment);
        Assert.Contains(DesktopContextActionKind.RotateRight, attachment);
        Assert.DoesNotContain(DesktopContextActionKind.SwitchOperation, attachment);
        Assert.Contains(DesktopContextActionKind.SwitchOperation, switchDevice);
        Assert.Contains(DesktopContextActionKind.ReconnectCableStart, cable);
        Assert.Contains(DesktopContextActionKind.ReconnectCableEnd, cable);
    }
}
