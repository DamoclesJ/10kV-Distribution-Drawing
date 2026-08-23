using DistributionDrawing.Application.Devices;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class DrawingSceneBuilderPoleRenderingTests
{
    [Fact]
    public void BuildMixedPole_UsesUnifiedLabelsAndPreservesSelectionAndDomain()
    {
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P-301",
            PoleType.Cement,
            null,
            [SwitchKind.IsolationSwitch],
            includeCableTerminal: true,
            cableTerminalDisplayName: "电缆终端-301");
        var document = new DrawingDocument(Guid.NewGuid(), "Scene builder pole test");
        document.AddDevice(result.Pole);
        foreach (Device device in result.Devices)
        {
            document.AddDevice(device);
        }

        foreach (ElectricalNode node in result.ElectricalNodes)
        {
            document.AddElectricalNode(node);
        }

        foreach (Terminal terminal in result.Terminals)
        {
            document.AddTerminal(terminal);
        }

        foreach (PoleAttachment attachment in result.Attachments)
        {
            document.AddPoleAttachment(attachment);
        }

        SwitchDevice switchDevice = Assert.Single(result.Devices.OfType<SwitchDevice>());
        CableTermination cableTermination = Assert.Single(result.Devices.OfType<CableTermination>());
        PoleAttachment switchAttachment = Assert.Single(
            result.Attachments,
            attachment => attachment.AttachedDeviceId == switchDevice.Id);
        PoleAttachment cableAttachment = Assert.Single(
            result.Attachments,
            attachment => attachment.AttachedDeviceId == cableTermination.Id);
        var drawingLayout = new DrawingLayout();
        drawingLayout.Add(new PoleLayout(result.Pole.Id, new DocumentPoint(10, 20)));
        drawingLayout.Add(new AttachmentLayout(
            switchAttachment.AttachmentId,
            new DocumentPoint(0, 0)));
        drawingLayout.Add(new AttachmentLayout(
            cableAttachment.AttachmentId,
            new DocumentPoint(0, 0)));
        var runtimeLayout = new RuntimeLayoutDocument(
            drawingLayout,
            new Dictionary<Guid, RingCabinetLayout>());
        var builder = new DrawingSceneBuilder();
        Guid poleId = result.Pole.Id;
        Guid switchId = switchDevice.Id;
        Guid cableId = cableTermination.Id;
        SwitchState switchState = switchDevice.SwitchState!.Value;

        DrawingScene scene = builder.Build(document, runtimeLayout);
        SceneText[] labels = scene.Elements.OfType<SceneText>().ToArray();

        Assert.Equal(1, labels.Count(text => text.Text == "P-301"));
        Assert.Equal(1, labels.Count(text => text.Text == "柱上隔离开关"));
        Assert.Equal(1, labels.Count(text => text.Text == "电缆终端"));
        Assert.DoesNotContain(labels, text => text.Text is "合" or "分");
        Assert.Single(scene.Elements.OfType<SceneEllipse>(), ellipse =>
            ellipse.Bounds.WidthMillimeters == ellipse.Bounds.HeightMillimeters &&
            ellipse.Bounds.WidthMillimeters == 14);
        Assert.Single(scene.Elements.OfType<ScenePolyline>(), polyline => polyline.IsClosed);
        Assert.Contains(scene.HitTestIndex.Entries, entry =>
            entry.Target.ObjectId == poleId);
        Assert.Contains(scene.HitTestIndex.Entries, entry =>
            entry.Target.Kind == DistributionDrawing.Rendering.Wpf.Interaction.SelectionTargetKind.Device &&
            entry.Target.ObjectId == switchId);
        Assert.Contains(scene.HitTestIndex.Entries, entry =>
            entry.Target.ObjectId == switchAttachment.AttachmentId);
        Assert.Contains(scene.HitTestIndex.Entries, entry =>
            entry.Target.ObjectId == cableAttachment.AttachmentId);
        Assert.Equal(poleId, result.Pole.Id);
        Assert.Equal(switchId, switchDevice.Id);
        Assert.Equal(cableId, cableTermination.Id);
        Assert.Equal(switchState, switchDevice.SwitchState);
    }

    [Fact]
    public void BuildPoleAgainAfterLayoutMove_UpdatesPoleLabelPosition()
    {
        PoleCreationResult result = new PoleCreationFactory().Create(
            "P-302",
            PoleType.Cement,
            null);
        var document = new DrawingDocument(Guid.NewGuid(), "Scene builder move test");
        document.AddDevice(result.Pole);
        foreach (Terminal terminal in result.Terminals)
        {
            document.AddTerminal(terminal);
        }

        var layout = new DrawingLayout();
        layout.Add(new PoleLayout(result.Pole.Id, new DocumentPoint(10, 20)));
        var runtimeLayout = new RuntimeLayoutDocument(
            layout,
            new Dictionary<Guid, RingCabinetLayout>());
        var builder = new DrawingSceneBuilder();

        SceneText first = Assert.Single(
            builder.Build(document, runtimeLayout).Elements.OfType<SceneText>());
        layout.Replace(new PoleLayout(result.Pole.Id, new DocumentPoint(40, 50)));
        SceneText second = Assert.Single(
            builder.Build(document, runtimeLayout).Elements.OfType<SceneText>());

        Assert.Equal("P-302", first.Text);
        Assert.Equal("P-302", second.Text);
        Assert.NotEqual(first.Origin, second.Origin);
    }
}
