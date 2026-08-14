using DistributionDrawing.Application.Devices;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class MixedPoleRendererTests
{
    [Fact]
    public void RenderPoleWithSwitchAndCableTerminationAttachments_ProducesBothSymbols()
    {
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P-201",
            PoleType.Cement,
            null,
            [SwitchKind.IsolationSwitch],
            includeCableTerminal: true);
        SwitchDevice switchDevice = Assert.Single(result.Devices.OfType<SwitchDevice>());
        CableTermination cableTermination = Assert.Single(result.Devices.OfType<CableTermination>());
        PoleAttachment switchAttachment = Assert.Single(
            result.Attachments,
            attachment => attachment.AttachedDeviceId == switchDevice.Id);
        PoleAttachment cableAttachment = Assert.Single(
            result.Attachments,
            attachment => attachment.AttachedDeviceId == cableTermination.Id);
        var poleLayout = new PoleLayout(result.Pole.Id, new DocumentPoint(10, 20));
        var switchLayout = new AttachmentLayout(
            switchAttachment.AttachmentId,
            new DocumentPoint(12, 14));
        var cableLayout = new AttachmentLayout(
            cableAttachment.AttachmentId,
            new DocumentPoint(-22, 14));

        IReadOnlyList<SceneElement> elements = new MixedPoleRenderer().Render(
            result.Pole,
            poleLayout,
            [new SwitchAttachmentRenderInput(
                switchAttachment,
                switchDevice,
                switchLayout)],
            [new PoleAttachmentRenderInput(
                cableAttachment,
                cableTermination,
                cableLayout)]);

        Assert.Contains(elements.OfType<SceneText>(), text => text.Text == switchDevice.DisplayName);
        Assert.Contains(elements.OfType<SceneText>(), text => text.Text == cableTermination.DisplayName);
        Assert.Equal(2, elements.OfType<SceneRectangle>().Count(rectangle =>
            rectangle.Bounds.WidthMillimeters == 18));
    }

    [Fact]
    public void RenderMixedPole_DoesNotModifyDomainIdentityOrState()
    {
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P-202",
            PoleType.Cement,
            null,
            [SwitchKind.IsolationSwitch],
            includeCableTerminal: true);
        SwitchDevice switchDevice = Assert.Single(result.Devices.OfType<SwitchDevice>());
        CableTermination cableTermination = Assert.Single(result.Devices.OfType<CableTermination>());
        PoleAttachment switchAttachment = Assert.Single(
            result.Attachments,
            attachment => attachment.AttachedDeviceId == switchDevice.Id);
        PoleAttachment cableAttachment = Assert.Single(
            result.Attachments,
            attachment => attachment.AttachedDeviceId == cableTermination.Id);
        Guid poleId = result.Pole.Id;
        Guid switchId = switchDevice.Id;
        Guid cableId = cableTermination.Id;
        SwitchState? switchState = switchDevice.SwitchState;
        var poleLayout = new PoleLayout(result.Pole.Id, new DocumentPoint(10, 20));

        new MixedPoleRenderer().Render(
            result.Pole,
            poleLayout,
            [new SwitchAttachmentRenderInput(
                switchAttachment,
                switchDevice,
                new AttachmentLayout(switchAttachment.AttachmentId, new DocumentPoint(12, 14)))],
            [new PoleAttachmentRenderInput(
                cableAttachment,
                cableTermination,
                new AttachmentLayout(cableAttachment.AttachmentId, new DocumentPoint(-22, 14)))]);

        Assert.Equal(poleId, result.Pole.Id);
        Assert.Equal(switchId, switchDevice.Id);
        Assert.Equal(cableId, cableTermination.Id);
        Assert.Equal(switchState, switchDevice.SwitchState);
    }

    [Fact]
    public void RenderMixedPole_UsesOneDeterministicLabelLayoutForAllLabels()
    {
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P-203",
            PoleType.Cement,
            null,
            [SwitchKind.IsolationSwitch],
            includeCableTerminal: true);
        SwitchDevice switchDevice = Assert.Single(result.Devices.OfType<SwitchDevice>());
        CableTermination cableTermination = Assert.Single(result.Devices.OfType<CableTermination>());
        PoleAttachment switchAttachment = Assert.Single(
            result.Attachments,
            attachment => attachment.AttachedDeviceId == switchDevice.Id);
        PoleAttachment cableAttachment = Assert.Single(
            result.Attachments,
            attachment => attachment.AttachedDeviceId == cableTermination.Id);
        var poleLayout = new PoleLayout(result.Pole.Id, new DocumentPoint(10, 20));
        var switchLayout = new AttachmentLayout(
            switchAttachment.AttachmentId,
            new DocumentPoint(0, 0));
        var cableLayout = new AttachmentLayout(
            cableAttachment.AttachmentId,
            new DocumentPoint(0, 0));
        var renderer = new MixedPoleRenderer();

        IReadOnlyList<SceneElement> first = renderer.Render(
            result.Pole,
            poleLayout,
            [new SwitchAttachmentRenderInput(switchAttachment, switchDevice, switchLayout)],
            [new PoleAttachmentRenderInput(cableAttachment, cableTermination, cableLayout)]);
        IReadOnlyList<SceneElement> second = renderer.Render(
            result.Pole,
            poleLayout,
            [new SwitchAttachmentRenderInput(switchAttachment, switchDevice, switchLayout)],
            [new PoleAttachmentRenderInput(cableAttachment, cableTermination, cableLayout)]);

        SceneText[] firstLabels = first.OfType<SceneText>().ToArray();
        SceneText[] secondLabels = second.OfType<SceneText>().ToArray();
        Assert.Equal(3, firstLabels.Length);
        Assert.Contains(firstLabels, text => text.Text == "P-203");
        Assert.Contains(firstLabels, text => text.Text == switchDevice.DisplayName);
        Assert.Contains(firstLabels, text => text.Text == cableTermination.DisplayName);
        Assert.Equal(firstLabels, secondLabels);
        Assert.True(firstLabels.Select(label => label.Origin).Distinct().Count() > 1);
    }
}
