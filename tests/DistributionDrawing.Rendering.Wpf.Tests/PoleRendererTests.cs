using DistributionDrawing.Application.Devices;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class PoleRendererTests
{
    [Fact]
    public void Render普通Pole_ProducesPoleSymbol()
    {
        PoleCreationResult result = new PoleCreationFactory().Create(
            "P-001",
            PoleType.Cement,
            null);
        PoleLayout layout = new PoleLayout(result.Pole.Id, new DocumentPoint(10, 20));

        IReadOnlyList<SceneElement> elements = new PoleRenderer().Render(
            result.Pole,
            layout);

        Assert.NotEmpty(elements.OfType<SceneLine>());
        Assert.Contains(elements.OfType<SceneText>(), text => text.Text == "P-001");
    }

    [Fact]
    public void RenderCableTerminationPole_ProducesPoleAndOneAttachment()
    {
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P-002",
            PoleType.Cement,
            null,
            switchKinds: null,
            includeCableTerminal: true);
        CableTermination cableTermination = Assert.IsType<CableTermination>(
            Assert.Single(result.Devices));
        PoleAttachment attachment = Assert.Single(result.Attachments);
        PoleLayout poleLayout = new PoleLayout(result.Pole.Id, new DocumentPoint(10, 20));
        var attachmentLayout = new AttachmentLayout(
            attachment.AttachmentId,
            new DocumentPoint(12, 14));

        IReadOnlyList<SceneElement> elements = new PoleRenderer().Render(
            result.Pole,
            poleLayout,
            [new PoleAttachmentRenderInput(attachment, cableTermination, attachmentLayout)]);

        Assert.NotEmpty(elements.OfType<SceneLine>());
        Assert.Contains(elements.OfType<SceneRectangle>(), rectangle =>
            rectangle.Bounds.WidthMillimeters == attachmentLayout.WidthMillimeters);
        Assert.Contains(elements.OfType<SceneText>(), text =>
            text.Text == cableTermination.DisplayName);
    }

    [Fact]
    public void RenderDoesNotModifyPoleOrCableTerminationDomainObjects()
    {
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P-003",
            PoleType.Cement,
            null,
            switchKinds: null,
            includeCableTerminal: true);
        CableTermination cableTermination = Assert.IsType<CableTermination>(
            Assert.Single(result.Devices));
        PoleAttachment attachment = Assert.Single(result.Attachments);
        PoleLayout poleLayout = new PoleLayout(result.Pole.Id, new DocumentPoint(10, 20));
        var attachmentLayout = new AttachmentLayout(
            attachment.AttachmentId,
            new DocumentPoint(12, 14));
        Guid poleId = result.Pole.Id;
        Guid attachmentId = attachment.AttachmentId;
        Guid terminationId = cableTermination.Id;

        new PoleRenderer().Render(
            result.Pole,
            poleLayout,
            [new PoleAttachmentRenderInput(attachment, cableTermination, attachmentLayout)]);

        Assert.Equal(poleId, result.Pole.Id);
        Assert.Equal(attachmentId, attachment.AttachmentId);
        Assert.Equal(terminationId, cableTermination.Id);
    }
}
