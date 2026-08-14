using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class CableTopologyRenderingTests
{
    [Fact]
    public void RenderOrdinaryCable_ProjectsCurrentSegment()
    {
        (DrawingDocument document, CableSegment cable) = CreateCableScenario();
        CableLayout layout = new(
            cable.Id,
            [new DocumentPoint(10, 20), new DocumentPoint(50, 20)]);

        IReadOnlyList<SceneElement> elements = new CableRenderer().Render(cable, layout);

        Assert.Single(elements.OfType<SceneLine>());
        Assert.Contains(elements.OfType<SceneText>(), text =>
            text.Text.Contains(cable.CableType, StringComparison.Ordinal));
        Assert.Single(document.CableSegments);
    }

    [Fact]
    public void RenderSplitCable_ProjectsTwoCurrentSegmentsAndOneJoint()
    {
        (DrawingDocument document, CableSegment original) = CreateCableScenario();
        var split = new SplitCableCommand(document, original.Id, "中间接头");
        split.Execute();

        CableSplitResult result = Assert.IsType<CableSplitResult>(split.Result);
        var cableRenderer = new CableRenderer();
        var jointRenderer = new JointRenderer();
        var elements = new List<SceneElement>();
        elements.AddRange(cableRenderer.Render(
            result.FirstCableSegment,
            new CableLayout(
                result.FirstCableSegment.Id,
                [new DocumentPoint(10, 20), new DocumentPoint(30, 20)])));
        elements.AddRange(cableRenderer.Render(
            result.SecondCableSegment,
            new CableLayout(
                result.SecondCableSegment.Id,
                [new DocumentPoint(30, 20), new DocumentPoint(50, 20)])));
        elements.AddRange(jointRenderer.Render(
            result.IntermediateTerminal.IntermediateTerminal,
            new JointLayout(
                result.IntermediateTerminal.IntermediateTerminal.Id,
                new DocumentPoint(30, 20))));

        Assert.Equal(2, elements.OfType<SceneLine>().Count());
        Assert.Single(elements.OfType<SceneRectangle>());
        Assert.DoesNotContain(elements, element =>
            element is SceneText text && text.Text == original.Name);
        Assert.DoesNotContain(document.CableSegments, segment => segment.Id == original.Id);
        Assert.True(CreateQuery(document).IsConnected(
            original.StartTerminalId,
            original.EndTerminalId));
    }

    [Fact]
    public void RenderReconnect_UsesNewEndpointsAndPreservesCableSegmentId()
    {
        (DrawingDocument document, CableSegment cable, Guid startId, Guid endId, Guid newEndId) =
            CreateReconnectScenario();
        Guid cableId = cable.Id;
        var reconnect = new ReconnectCableCommand(document, cable.Id, startId, newEndId);
        reconnect.Execute();

        CableSegment currentCable = Assert.Single(document.CableSegments);
        CableLayout layout = new(
            currentCable.Id,
            [new DocumentPoint(10, 20), new DocumentPoint(70, 20)]);
        IReadOnlyList<SceneElement> elements = new CableRenderer().Render(
            currentCable,
            layout);

        SceneLine line = Assert.Single(elements.OfType<SceneLine>());
        Assert.Equal(layout.Start, line.Start);
        Assert.Equal(layout.End, line.End);
        Assert.Equal(cableId, currentCable.Id);
        Assert.Equal(newEndId, currentCable.EndTerminalId);
        Assert.False(CreateQuery(document).IsConnected(startId, endId));
        Assert.True(CreateQuery(document).IsConnected(startId, newEndId));
    }

    private static ElectricalConnectivityQuery CreateQuery(DrawingDocument document)
    {
        return new ElectricalConnectivityQuery(
            new ElectricalConnectivityGraphBuilder().Build(document));
    }

    private static (DrawingDocument Document, CableSegment CableSegment)
        CreateCableScenario()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Cable topology rendering test");
        var terminalFactory = new IntermediateTerminalCreationFactory();
        IntermediateTerminalCreationResult start = terminalFactory.Create("起点");
        IntermediateTerminalCreationResult end = terminalFactory.Create("终点");
        new CreateIntermediateTerminalCommand(document, start).Execute();
        new CreateIntermediateTerminalCommand(document, end).Execute();

        CableSegmentCreationResult cable = new CableSegmentCreationFactory().Create(
            document,
            start.Terminal.Id,
            end.Terminal.Id,
            "工作票电缆",
            "10kV-Cable",
            25);
        new CreateCableSegmentCommand(document, cable).Execute();
        return (document, cable.CableSegment);
    }

    private static (
        DrawingDocument Document,
        CableSegment Cable,
        Guid StartId,
        Guid EndId,
        Guid NewEndId)
        CreateReconnectScenario()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Cable reconnect rendering test");
        var terminalFactory = new IntermediateTerminalCreationFactory();
        IntermediateTerminalCreationResult start = terminalFactory.Create("起点");
        IntermediateTerminalCreationResult end = terminalFactory.Create("原终点");
        IntermediateTerminalCreationResult newEnd = terminalFactory.Create("新终点");
        foreach (IntermediateTerminalCreationResult result in new[] { start, end, newEnd })
        {
            new CreateIntermediateTerminalCommand(document, result).Execute();
        }

        CableSegmentCreationResult cable = new CableSegmentCreationFactory().Create(
            document,
            start.Terminal.Id,
            end.Terminal.Id,
            "工作票电缆",
            "10kV-Cable",
            25);
        new CreateCableSegmentCommand(document, cable).Execute();
        return (
            document,
            cable.CableSegment,
            start.Terminal.Id,
            end.Terminal.Id,
            newEnd.Terminal.Id);
    }
}
