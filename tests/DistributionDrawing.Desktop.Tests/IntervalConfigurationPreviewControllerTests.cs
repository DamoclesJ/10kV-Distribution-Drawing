using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;
using DistributionDrawing.Desktop.RingCabinetEditing;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class IntervalConfigurationPreviewControllerTests
{
    [Fact]
    public void DraftPreviewChangesOnlyTheTransientCloneAndCancelRestoresFormalRendering()
    {
        RingCabinet cabinet = CreateCabinet(RingCabinetPTPlacement.Right);
        RingCabinetInterval target = cabinet.Intervals.Single(interval => interval.BayIndex == 3);
        RingCabinetInterval formalPT = Assert.Single(cabinet.Intervals, interval =>
            interval.IntervalKind == IntervalKind.PTInterval);
        Guid[] formalSwitchIds = cabinet.Intervals
            .SelectMany(interval => interval.SwitchDevices)
            .Select(device => device.Id)
            .ToArray();
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(20, 30));
        var stack = new CommandStack();
        var controller = new IntervalConfigurationPreviewController();

        Assert.True(controller.Update(
            cabinet,
            layout,
            target.IntervalId,
            IntervalKind.PTInterval,
            null));

        Assert.False(stack.IsDirty);
        Assert.Empty(stack.History);
        Assert.Equal(IntervalKind.IntegratedFeederInterval, target.IntervalKind);
        Assert.Equal(IntervalKind.PTInterval, formalPT.IntervalKind);
        Assert.Equal(formalSwitchIds, cabinet.Intervals
            .SelectMany(interval => interval.SwitchDevices)
            .Select(device => device.Id));
        Assert.NotNull(controller.PreviewCabinet);
        Assert.Equal(IntervalKind.PTInterval, controller.PreviewCabinet!.Intervals
            .Single(interval => interval.IntervalId == target.IntervalId).IntervalKind);
        Assert.Equal(IntervalKind.IntegratedFeederInterval,
            controller.PreviewCabinet.Intervals
                .Single(interval => interval.IntervalId == formalPT.IntervalId).IntervalKind);
        Assert.Single(controller.PreviewCabinet.Intervals, interval =>
            interval.IntervalKind == IntervalKind.PTInterval);
        Assert.NotEmpty(controller.Elements);

        Assert.True(controller.Cancel());
        Assert.False(controller.IsActive);
        Assert.Empty(controller.Elements);
        Assert.Equal(IntervalKind.IntegratedFeederInterval, target.IntervalKind);
        Assert.Equal(IntervalKind.PTInterval, formalPT.IntervalKind);
    }

    [Fact]
    public void UnchangedDraftDoesNotCreatePreview()
    {
        RingCabinet cabinet = CreateCabinet(RingCabinetPTPlacement.Left);
        RingCabinetInterval target = cabinet.Intervals.Single(interval => interval.BayIndex == 3);
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(0, 0));
        var controller = new IntervalConfigurationPreviewController();

        Assert.False(controller.Update(
            cabinet,
            layout,
            target.IntervalId,
            target.IntervalKind,
            target.GroundingStructureKind));

        Assert.False(controller.IsActive);
        Assert.Empty(controller.Elements);
    }

    private static RingCabinet CreateCabinet(RingCabinetPTPlacement placement)
    {
        RingCabinetTemplate template = new RingCabinetCreationTemplateFactory().Create(
            RingCabinetTemplateType.PrimarySecondaryIntegrated,
            5,
            includePTInterval: true,
            ptPlacement: placement);
        return new RingCabinetCreationFactory().Create(
            new RingCabinetCreationConfiguration("Preview cabinet", template));
    }
}
