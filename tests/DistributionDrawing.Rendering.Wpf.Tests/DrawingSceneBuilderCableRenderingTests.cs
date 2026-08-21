using DistributionDrawing.Application.Devices;
using DistributionDrawing.Application.Interaction;
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

        SceneLine[] cableLines = scene.Elements.OfType<SceneLine>()
            .Where(line => line.TargetKind == SelectionTargetKind.CableSegment)
            .ToArray();
        Assert.True(cableLines.Length >= 2);
        Assert.All(cableLines, line =>
        {
            Assert.Equal(SceneStrokeStyle.Dashed, line.StrokeStyle);
            Assert.True(
                line.Start.XMillimeters == line.End.XMillimeters ||
                line.Start.YMillimeters == line.End.YMillimeters);
            Assert.Equal(fixture.Cable.Id, line.TargetId);
        });
        Assert.True(scene.HitTestIndex.FindAll(new(
            DistributionDrawing.Rendering.Wpf.Interaction.SelectionTargetKind.CableSegment,
            fixture.Cable.Id)).Count >= 2);
        Assert.Single(
            scene.Elements.OfType<SceneText>(),
            text => text.Text == "YJV22-8.7/15kV 120m");
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

        SceneElementAssertions.Equal(first.Elements, second.Elements);
    }

    [Fact]
    public void PoleMove_ReroutesCableAndUndoRedoRestoreDeterministicRoute()
    {
        CableSceneFixture fixture = CreateFixture();
        PoleLayout before = fixture.Layout.DrawingLayout.Poles.Values.Single(
            pole => pole.Position == new DocumentPoint(100, 70));
        PoleLayout after = before.MoveTo(new DocumentPoint(130, 95));
        string initial = CableGeometryKey(fixture.Builder.Build(fixture.Document, fixture.Layout));
        var command = new DistributionDrawing.Rendering.Wpf.Interaction.MoveCommand(
            fixture.Layout.DrawingLayout,
            before,
            after);

        command.Execute();
        string moved = CableGeometryKey(fixture.Builder.Build(fixture.Document, fixture.Layout));
        command.Undo();
        string undone = CableGeometryKey(fixture.Builder.Build(fixture.Document, fixture.Layout));
        command.Redo();
        string redone = CableGeometryKey(fixture.Builder.Build(fixture.Document, fixture.Layout));

        Assert.NotEqual(initial, moved);
        Assert.Equal(initial, undone);
        Assert.Equal(moved, redone);
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
        drawingLayout.Add(new PoleLayout(secondPole.Pole.Id, new DocumentPoint(100, 70)));
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

    private static string CableGeometryKey(DrawingScene scene)
    {
        return string.Join(';', scene.Elements.OfType<SceneLine>()
            .Where(line => line.TargetKind == SelectionTargetKind.CableSegment)
            .Select(line =>
                $"{line.Start.XMillimeters:R},{line.Start.YMillimeters:R}-" +
                $"{line.End.XMillimeters:R},{line.End.YMillimeters:R}"));
    }

    private sealed record CableSceneFixture(
        DrawingDocument Document,
        CableSegment Cable,
        RuntimeLayoutDocument Layout,
        DrawingSceneBuilder Builder);
}
