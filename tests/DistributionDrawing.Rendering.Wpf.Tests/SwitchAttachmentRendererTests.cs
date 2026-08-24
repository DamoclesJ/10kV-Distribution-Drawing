using DistributionDrawing.Application.Devices;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class SwitchAttachmentRendererTests
{
    [Theory]
    [InlineData(SwitchKind.IsolationSwitch)]
    [InlineData(SwitchKind.CircuitBreaker)]
    [InlineData(SwitchKind.LoadSwitch)]
    [InlineData(SwitchKind.DropoutFuse)]
    public void RenderPoleSwitchAttachment_ProducesPoleAndSwitchSymbol(SwitchKind switchKind)
    {
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P-101",
            PoleType.Cement,
            null,
            [switchKind],
            includeCableTerminal: false);
        SwitchDevice switchDevice = Assert.IsType<SwitchDevice>(
            Assert.Single(result.Devices));
        PoleAttachment attachment = Assert.Single(result.Attachments);
        var poleLayout = new PoleLayout(result.Pole.Id, new DocumentPoint(10, 20));
        var attachmentLayout = new AttachmentLayout(
            attachment.AttachmentId,
            new DocumentPoint(12, 14));

        IReadOnlyList<SceneElement> elements = new SwitchAttachmentRenderer().Render(
            result.Pole,
            poleLayout,
            [new SwitchAttachmentRenderInput(attachment, switchDevice, attachmentLayout)]);

        Assert.NotEmpty(elements.OfType<SceneLine>());
        Assert.Single(elements.OfType<SceneEllipse>(), ellipse =>
            ellipse.Bounds.WidthMillimeters == ellipse.Bounds.HeightMillimeters &&
            ellipse.Bounds.WidthMillimeters ==
            DrawingMetrics.Default.Pole.PoleRadius * 2);
        Assert.True(elements.OfType<SceneLine>().Any() || elements.OfType<ScenePolyline>().Any());
    }

    [Fact]
    public void RenderPoleSwitchAttachment_ReflectsStateWithoutChangingDomain()
    {
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P-102",
            PoleType.Cement,
            null,
            [SwitchKind.IsolationSwitch],
            includeCableTerminal: false);
        SwitchDevice switchDevice = Assert.IsType<SwitchDevice>(
            Assert.Single(result.Devices));
        PoleAttachment attachment = Assert.Single(result.Attachments);
        var document = new DrawingDocument(Guid.NewGuid(), "Switch rendering test");
        document.AddDevice(result.Pole);
        document.AddPoleSwitchAttachment(
            switchDevice,
            result.Terminals[0],
            result.Terminals[1],
            attachment);
        var poleLayout = new PoleLayout(result.Pole.Id, new DocumentPoint(10, 20));
        var attachmentLayout = new AttachmentLayout(
            attachment.AttachmentId,
            new DocumentPoint(12, 14));
        var input = new SwitchAttachmentRenderInput(
            attachment,
            switchDevice,
            attachmentLayout);
        var renderer = new SwitchAttachmentRenderer();

        IReadOnlyList<SceneElement> open = renderer.Render(
            result.Pole,
            poleLayout,
            [input]);
        document.ChangeSwitchState(switchDevice.Id, SwitchState.Closed);
        IReadOnlyList<SceneElement> closed = renderer.Render(
            result.Pole,
            poleLayout,
            [input]);

        Assert.DoesNotContain(open.OfType<SceneText>(), text => text.Text is "合" or "分");
        Assert.DoesNotContain(closed.OfType<SceneText>(), text => text.Text is "合" or "分");
        Assert.Equal(SwitchState.Closed, switchDevice.SwitchState);
        Assert.Equal(attachment.AttachmentId, input.Attachment.AttachmentId);
    }

    [Fact]
    public void RenderPoleWithCableTerminationAndSwitchAttachments_ComposesBothRenderers()
    {
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P-103",
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

        IReadOnlyList<SceneElement> switchElements = new SwitchAttachmentRenderer().Render(
            result.Pole,
            poleLayout,
            [new SwitchAttachmentRenderInput(
                switchAttachment,
                switchDevice,
                new AttachmentLayout(switchAttachment.AttachmentId, new DocumentPoint(12, 14)))]);
        IReadOnlyList<SceneElement> cableElements = new PoleRenderer().Render(
            result.Pole,
            poleLayout,
            [new PoleAttachmentRenderInput(
                cableAttachment,
                cableTermination,
                new AttachmentLayout(cableAttachment.AttachmentId, new DocumentPoint(-22, 14)))]);

        Assert.NotEmpty(switchElements.OfType<SceneLine>());
        Assert.DoesNotContain(switchElements.OfType<SceneText>(), text =>
            text.Text is "合" or "分");
        Assert.Contains(cableElements.OfType<ScenePolyline>(), polyline => polyline.IsClosed);
        Assert.Equal(SwitchState.Open, switchDevice.SwitchState);
        Assert.Equal(cableTermination.Id, cableAttachment.AttachedDeviceId);
    }
}
