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

public sealed class DrawingSceneBuilderCableRenderingTests
{
    [Fact]
    public void BuildDocumentScene_RendersCableSegmentAndBusinessLabel()
    {
        CableSceneFixture fixture = CreateFixture();
        DrawingScene scene = fixture.Builder.Build(fixture.Document, fixture.Layout);

        Assert.Single(scene.Elements.OfType<SceneLine>());
        SceneText label = Assert.Single(scene.Elements.OfType<SceneText>());
        Assert.Contains(fixture.Cable.CableType, label.Text, StringComparison.Ordinal);
        Assert.Contains("120", label.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDocumentScene_DoesNotModifyCableDomain()
    {
        CableSceneFixture fixture = CreateFixture();
        Guid cableId = fixture.Cable.Id;
        Guid connectionId = fixture.Cable.ConnectionId;
        Guid startTerminalId = fixture.Cable.StartTerminalId;
        Guid endTerminalId = fixture.Cable.EndTerminalId;
        int connectionCount = fixture.Document.Connections.Count;
        int terminalCount = fixture.Document.Terminals.Count;

        _ = fixture.Builder.Build(fixture.Document, fixture.Layout);

        Assert.Equal(cableId, fixture.Cable.Id);
        Assert.Equal(connectionId, fixture.Cable.ConnectionId);
        Assert.Equal(startTerminalId, fixture.Cable.StartTerminalId);
        Assert.Equal(endTerminalId, fixture.Cable.EndTerminalId);
        Assert.Equal(connectionCount, fixture.Document.Connections.Count);
        Assert.Equal(terminalCount, fixture.Document.Terminals.Count);
    }

    [Fact]
    public void BuildDocumentScene_WithSameInputProducesSameCableElements()
    {
        CableSceneFixture fixture = CreateFixture();

        DrawingScene first = fixture.Builder.Build(fixture.Document, fixture.Layout);
        DrawingScene second = fixture.Builder.Build(fixture.Document, fixture.Layout);

        Assert.Equal(first.Elements, second.Elements);
    }

    private static CableSceneFixture CreateFixture()
    {
        PoleCreationFactory factory = new();
        PoleCreationResult firstPole = factory.CreateWithAttachments(
            "P-401",
            PoleType.Cement,
            null,
            switchKinds: null,
            includeCableTerminal: true);
        PoleCreationResult secondPole = factory.CreateWithAttachments(
            "P-402",
            PoleType.Cement,
            null,
            switchKinds: null,
            includeCableTerminal: true);

        var document = new DrawingDocument(Guid.NewGuid(), "Scene builder cable test");
        AddPoleAggregate(document, firstPole);
        AddPoleAggregate(document, secondPole);

        CableTermination firstTermination = Assert.Single(
            firstPole.Devices.OfType<CableTermination>());
        CableTermination secondTermination = Assert.Single(
            secondPole.Devices.OfType<CableTermination>());
        Guid connectionId = Guid.NewGuid();
        var connection = new Connection(
            connectionId,
            ConnectionType.Cable,
            firstTermination.CableSideTerminalId,
            secondTermination.CableSideTerminalId,
            "P-401至P-402电缆",
            "10kV");
        document.AddConnection(connection);

        var cable = new CableSegment(
            Guid.NewGuid(),
            "Cable-401",
            "YJV22-8.7/15kV",
            120,
            "10kV",
            connectionId,
            connection.StartTerminalId,
            connection.EndTerminalId);
        document.AddCableSegment(cable, connection);

        var drawingLayout = new DrawingLayout();
        drawingLayout.Add(new PoleLayout(firstPole.Pole.Id, new DocumentPoint(10, 20)));
        drawingLayout.Add(new PoleLayout(secondPole.Pole.Id, new DocumentPoint(100, 20)));
        foreach (PoleAttachment attachment in firstPole.Attachments.Concat(secondPole.Attachments))
        {
            drawingLayout.Add(new AttachmentLayout(
                attachment.AttachmentId,
                new DocumentPoint(0, 0)));
        }

        return new CableSceneFixture(
            document,
            cable,
            new RuntimeLayoutDocument(
                drawingLayout,
                new Dictionary<Guid, RingCabinetLayout>()),
            new DrawingSceneBuilder());
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

    private sealed record CableSceneFixture(
        DrawingDocument Document,
        CableSegment Cable,
        RuntimeLayoutDocument Layout,
        DrawingSceneBuilder Builder);
}
