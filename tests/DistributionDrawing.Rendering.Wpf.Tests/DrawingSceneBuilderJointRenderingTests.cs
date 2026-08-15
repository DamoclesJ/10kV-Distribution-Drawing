using DistributionDrawing.Application.Devices;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class DrawingSceneBuilderJointRenderingTests
{
    [Fact]
    public void BuildDocumentScene_RendersCableJointCableAndLabels()
    {
        JointSceneFixture fixture = CreateFixture();

        DrawingScene scene = fixture.Builder.Build(fixture.Document, fixture.Layout);

        Assert.Equal(2, scene.Elements.OfType<SceneLine>().Count(line =>
            line.Start.YMillimeters == 25 && line.End.YMillimeters == 25));
        Assert.Single(scene.Elements.OfType<SceneRectangle>());
        Assert.Equal(2, scene.Elements.OfType<SceneText>().Count(
            text => text.Text.Contains(fixture.CableType, StringComparison.Ordinal)));
        Assert.Contains(scene.Elements.OfType<SceneText>(), text => text.Text == "Joint-X");
    }

    [Fact]
    public void BuildDocumentScene_DoesNotModifyJointCableOrTopology()
    {
        JointSceneFixture fixture = CreateFixture();
        Guid jointId = fixture.IntermediateTerminal.Id;
        Guid jointTerminalId = fixture.IntermediateTerminal.TerminalId;
        Guid[] connectionIds = fixture.Document.Connections.Select(connection => connection.Id).ToArray();
        Guid[] cableIds = fixture.Document.CableSegments.Select(cable => cable.Id).ToArray();

        _ = fixture.Builder.Build(fixture.Document, fixture.Layout);

        Assert.Equal(jointId, fixture.IntermediateTerminal.Id);
        Assert.Equal(jointTerminalId, fixture.IntermediateTerminal.TerminalId);
        Assert.Equal(connectionIds, fixture.Document.Connections.Select(connection => connection.Id));
        Assert.Equal(cableIds, fixture.Document.CableSegments.Select(cable => cable.Id));
    }

    [Fact]
    public void BuildDocumentScene_WithSameInputProducesSameElements()
    {
        JointSceneFixture fixture = CreateFixture();

        DrawingScene first = fixture.Builder.Build(fixture.Document, fixture.Layout);
        DrawingScene second = fixture.Builder.Build(fixture.Document, fixture.Layout);

        Assert.Equal(first.Elements, second.Elements);
    }

    [Fact]
    public void BuildDocumentScene_WithIncompleteJointTopologyFailsClearly()
    {
        JointSceneFixture fixture = CreateFixture(includeSecondCable: false);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => fixture.Builder.Build(fixture.Document, fixture.Layout));

        Assert.Contains("must connect exactly two cable connections", exception.Message);
    }

    private static JointSceneFixture CreateFixture(bool includeSecondCable = true)
    {
        PoleCreationFactory poleFactory = new();
        PoleCreationResult firstPole = poleFactory.CreateWithAttachments(
            "P-501",
            PoleType.Cement,
            null,
            switchKinds: null,
            includeCableTerminal: true);
        PoleCreationResult secondPole = poleFactory.CreateWithAttachments(
            "P-502",
            PoleType.Cement,
            null,
            switchKinds: null,
            includeCableTerminal: true);
        var document = new DrawingDocument(Guid.NewGuid(), "Scene builder joint test");
        AddPoleAggregate(document, firstPole);
        AddPoleAggregate(document, secondPole);

        IntermediateTerminalCreationResult joint =
            new IntermediateTerminalCreationFactory().Create("Joint-X");
        document.AddIntermediateTerminal(joint.IntermediateTerminal, joint.Terminal);

        CableTermination firstTermination = Assert.Single(
            firstPole.Devices.OfType<CableTermination>());
        CableTermination secondTermination = Assert.Single(
            secondPole.Devices.OfType<CableTermination>());
        AddCable(
            document,
            firstTermination.CableSideTerminalId,
            joint.Terminal.Id,
            "Cable-501");
        if (includeSecondCable)
        {
            AddCable(
                document,
                joint.Terminal.Id,
                secondTermination.CableSideTerminalId,
                "Cable-502");
        }

        var drawingLayout = new DrawingLayout();
        drawingLayout.Add(new PoleLayout(firstPole.Pole.Id, new DocumentPoint(10, 20)));
        drawingLayout.Add(new PoleLayout(secondPole.Pole.Id, new DocumentPoint(110, 20)));
        foreach (PoleAttachment attachment in firstPole.Attachments.Concat(secondPole.Attachments))
        {
            drawingLayout.Add(new AttachmentLayout(
                attachment.AttachmentId,
                new DocumentPoint(0, 0)));
        }

        return new JointSceneFixture(
            document,
            joint.IntermediateTerminal,
            "YJV22-8.7/15kV",
            new RuntimeLayoutDocument(
                drawingLayout,
                new Dictionary<Guid, RingCabinetLayout>()),
            new DrawingSceneBuilder());
    }

    private static void AddCable(
        DrawingDocument document,
        Guid startTerminalId,
        Guid endTerminalId,
        string name)
    {
        var connection = new Connection(
            Guid.NewGuid(),
            ConnectionType.Cable,
            startTerminalId,
            endTerminalId,
            name,
            "10kV");
        var cable = new CableSegment(
            Guid.NewGuid(),
            name,
            "YJV22-8.7/15kV",
            120,
            "10kV",
            connection.Id,
            startTerminalId,
            endTerminalId);
        document.AddCableSegment(cable, connection);
    }

    private static void AddPoleAggregate(
        DrawingDocument document,
        PoleCreationResult result)
    {
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
    }

    private sealed record JointSceneFixture(
        DrawingDocument Document,
        IntermediateTerminal IntermediateTerminal,
        string CableType,
        RuntimeLayoutDocument Layout,
        DrawingSceneBuilder Builder);
}
