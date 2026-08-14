using DistributionDrawing.Application.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Scene;
using System.Windows.Media;
using ApplicationSelectionTargetKind = DistributionDrawing.Application.Interaction.SelectionTargetKind;
using RenderingHitTestResult = DistributionDrawing.Rendering.Wpf.Interaction.HitTestResult;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class HitTestingTests
{
    [Fact]
    public void HitTest_RingCabinet_ReturnsSelectionTarget()
    {
        Guid id = Guid.NewGuid();
        RenderingHitTestResult? result = HitTest(new SceneRectangle(
            new DocumentRect(0, 0, 20, 20),
            Colors.Black,
            0.2)
        {
            TargetKind = ApplicationSelectionTargetKind.RingCabinet,
            TargetId = id,
            HitTestBounds = new DocumentRect(0, 0, 20, 20)
        });

        Assert.Equal(ApplicationSelectionTargetKind.RingCabinet, result?.Target.TargetKind);
        Assert.Equal(id, result?.Target.TargetId);
    }

    [Fact]
    public void HitTest_Pole_ReturnsSelectionTarget()
    {
        RenderingHitTestResult? result = HitTarget(ApplicationSelectionTargetKind.Pole);

        Assert.Equal(ApplicationSelectionTargetKind.Pole, result?.Target.TargetKind);
    }

    [Fact]
    public void HitTest_CableSegment_ReturnsSelectionTarget()
    {
        RenderingHitTestResult? result = HitTarget(ApplicationSelectionTargetKind.CableSegment);

        Assert.Equal(ApplicationSelectionTargetKind.CableSegment, result?.Target.TargetKind);
    }

    [Fact]
    public void HitTest_IntermediateTerminal_ReturnsSelectionTarget()
    {
        RenderingHitTestResult? result = HitTarget(ApplicationSelectionTargetKind.IntermediateTerminal);

        Assert.Equal(ApplicationSelectionTargetKind.IntermediateTerminal, result?.Target.TargetKind);
    }

    [Fact]
    public void HitTest_OutsideAllElements_ReturnsNull()
    {
        HitTestService service = new();
        SceneElement element = CreateElement(ApplicationSelectionTargetKind.Pole);

        Assert.Null(service.HitTest([element], new DocumentPoint(100, 100)));
    }

    [Fact]
    public void HitTestAndSelect_UpdatesSelectionService()
    {
        SelectionService selectionService = new();
        SceneElement element = CreateElement(ApplicationSelectionTargetKind.Pole);

        RenderingHitTestResult? result = new HitTestService().HitTestAndSelect(
            [element],
            new DocumentPoint(5, 5),
            selectionService);

        Assert.Equal(result?.Target, selectionService.CurrentSelection);
    }

    private static RenderingHitTestResult? HitTest(SceneElement element)
    {
        return new HitTestService().HitTest([element], new DocumentPoint(5, 5));
    }

    private static RenderingHitTestResult? HitTarget(ApplicationSelectionTargetKind kind)
    {
        return HitTest(CreateElement(kind));
    }

    private static SceneElement CreateElement(ApplicationSelectionTargetKind kind)
    {
        return new SceneRectangle(
            new DocumentRect(0, 0, 10, 10),
            Colors.Black,
            0.2)
        {
            TargetKind = kind,
            TargetId = Guid.NewGuid(),
            HitTestBounds = new DocumentRect(0, 0, 10, 10)
        };
    }
}
