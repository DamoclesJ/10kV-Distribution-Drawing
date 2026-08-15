using ApplicationSelectionTargetKind = DistributionDrawing.Application.Interaction.SelectionTargetKind;
using DistributionDrawing.Application.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class MoveRuntimeTests
{
    [Fact]
    public void MoveRingCabinet_ExecuteUndoRedo_PreservesDomainBoundary()
    {
        Guid cabinetId = Guid.NewGuid();
        RuntimeLayoutDocument layout = CreateLayout(cabinetId, Guid.NewGuid());
        SelectionService selection = new();
        CommandStack commands = new();
        MoveController controller = new(selection);
        SelectionTarget target = new(ApplicationSelectionTargetKind.RingCabinet, cabinetId);

        Assert.True(controller.MouseDown(target, new DocumentPoint(10, 20), layout));
        Assert.True(controller.MouseMove(new DocumentPoint(15, 28)));
        Assert.True(controller.MouseUp(commands));
        Assert.Equal(new DocumentPoint(5, 8), layout.RingCabinetLayouts[cabinetId].Position);
        Assert.Equal(target, selection.CurrentSelection);

        Assert.True(commands.Undo());
        Assert.Equal(new DocumentPoint(0, 0), layout.RingCabinetLayouts[cabinetId].Position);
        Assert.True(commands.Redo());
        Assert.Equal(new DocumentPoint(5, 8), layout.RingCabinetLayouts[cabinetId].Position);
    }

    [Fact]
    public void MovePole_UpdatesOnlyPoleLayout()
    {
        Guid poleId = Guid.NewGuid();
        RuntimeLayoutDocument layout = CreateLayout(Guid.NewGuid(), poleId);
        CommandStack commands = new();
        MoveController controller = new(new SelectionService());

        Assert.True(controller.MouseDown(
            new SelectionTarget(ApplicationSelectionTargetKind.Pole, poleId),
            new DocumentPoint(2, 3),
            layout));
        Assert.True(controller.MouseMove(new DocumentPoint(8, 11)));
        Assert.True(controller.MouseUp(commands));

        Assert.Equal(new DocumentPoint(6, 8), layout.DrawingLayout.Poles[poleId].Position);
        Assert.Single(layout.DrawingLayout.Poles);
    }

    private static RuntimeLayoutDocument CreateLayout(Guid cabinetId, Guid poleId)
    {
        RingCabinetLayout cabinet = new(
            cabinetId,
            new DocumentPoint(0, 0),
            100,
            50,
            10,
            []);
        var drawing = new DrawingLayout();
        drawing.Add(new PoleLayout(poleId, new DocumentPoint(0, 0)));
        return new RuntimeLayoutDocument(drawing, new Dictionary<Guid, RingCabinetLayout>
        {
            [cabinetId] = cabinet
        });
    }
}
