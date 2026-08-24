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

        SceneEllipse pole = Assert.Single(elements.OfType<SceneEllipse>());
        Assert.Equal(pole.Bounds.WidthMillimeters, pole.Bounds.HeightMillimeters);
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
            includeCableTerminal: true,
            cableTerminalDisplayName: "电缆终端-002");
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

        Assert.Empty(elements.OfType<SceneLine>());
        Assert.Single(elements.OfType<SceneEllipse>());
        Assert.Single(elements.OfType<ScenePolyline>(), polyline => polyline.IsClosed);
        Assert.Empty(elements.OfType<SceneRectangle>());
        Assert.Contains(elements.OfType<SceneText>(), text => text.Text == "电缆终端");
    }

    [Fact]
    public void RenderDoesNotModifyPoleOrCableTerminationDomainObjects()
    {
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P-003",
            PoleType.Cement,
            null,
            switchKinds: null,
            includeCableTerminal: true,
            cableTerminalDisplayName: "电缆终端-003");
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

    [Fact]
    public void RenderPoleLabelUsesLayoutAndUpdatesWhenPoleMoves()
    {
        PoleCreationResult result = new PoleCreationFactory().Create(
            "P-004",
            PoleType.Cement,
            null);
        var renderer = new PoleRenderer();

        SceneText first = Assert.Single(
            renderer.Render(
                result.Pole,
                new PoleLayout(result.Pole.Id, new DocumentPoint(10, 20)))
                .OfType<SceneText>());
        SceneText second = Assert.Single(
            renderer.Render(
                result.Pole,
                new PoleLayout(result.Pole.Id, new DocumentPoint(40, 50)))
                .OfType<SceneText>());

        Assert.Equal("P-004", first.Text);
        Assert.Equal("P-004", second.Text);
        Assert.NotEqual(first.Origin, second.Origin);
    }

    [Fact]
    public void RenderAttachmentLabelUsesLatestAttachmentLayout()
    {
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P-005",
            PoleType.Cement,
            null,
            switchKinds: null,
            includeCableTerminal: true,
            cableTerminalDisplayName: "电缆终端-005");
        CableTermination cableTermination = Assert.IsType<CableTermination>(
            Assert.Single(result.Devices));
        PoleAttachment attachment = Assert.Single(result.Attachments);
        var renderer = new PoleRenderer();
        var poleLayout = new PoleLayout(result.Pole.Id, new DocumentPoint(10, 20));

        SceneText first = Assert.Single(
            renderer.Render(
                result.Pole,
                poleLayout,
                [new PoleAttachmentRenderInput(
                    attachment,
                    cableTermination,
                    new AttachmentLayout(attachment.AttachmentId, new DocumentPoint(5, 5)))])
                .OfType<SceneText>(),
            text => text.Text == "电缆终端");
        SceneText second = Assert.Single(
            renderer.Render(
                result.Pole,
                poleLayout,
                [new PoleAttachmentRenderInput(
                    attachment,
                    cableTermination,
                    new AttachmentLayout(attachment.AttachmentId, new DocumentPoint(25, 5)))])
                .OfType<SceneText>(),
            text => text.Text == "电缆终端");

        Assert.NotEqual(first.Origin, second.Origin);
    }
}
