using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class CableRendererTests
{
    [Fact]
    public void RenderCableSegment_ProducesCableLineAndBusinessLabel()
    {
        CableSegment cable = CreateCable();
        CableLayout layout = new(
            cable.Id,
            [new DocumentPoint(10, 20), new DocumentPoint(50, 20)]);

        IReadOnlyList<SceneElement> elements = new CableRenderer().Render(cable, layout);

        Assert.Single(elements.OfType<SceneLine>());
        Assert.Contains(elements.OfType<SceneText>(), text =>
            text.Text.Contains(cable.CableType, StringComparison.Ordinal) &&
            text.Text.Contains("120", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderCableLayoutWithPath_ProducesOneLinePerPathSegment()
    {
        CableSegment cable = CreateCable();
        CableLayout layout = new(
            cable.Id,
            [
                new DocumentPoint(10, 20),
                new DocumentPoint(30, 20),
                new DocumentPoint(30, 40)
            ]);

        IReadOnlyList<SceneElement> elements = new CableRenderer().Render(cable, layout);

        Assert.Equal(2, elements.OfType<SceneLine>().Count());
    }

    [Fact]
    public void RenderDoesNotModifyCableDomainObject()
    {
        CableSegment cable = CreateCable();
        Guid cableId = cable.Id;
        Guid connectionId = cable.ConnectionId;
        Guid startTerminalId = cable.StartTerminalId;
        Guid endTerminalId = cable.EndTerminalId;
        CableLayout layout = new(
            cable.Id,
            [new DocumentPoint(10, 20), new DocumentPoint(50, 20)]);

        new CableRenderer().Render(cable, layout);

        Assert.Equal(cableId, cable.Id);
        Assert.Equal(connectionId, cable.ConnectionId);
        Assert.Equal(startTerminalId, cable.StartTerminalId);
        Assert.Equal(endTerminalId, cable.EndTerminalId);
    }

    [Fact]
    public void RenderPreservesCableStableIdInLayoutContract()
    {
        CableSegment cable = CreateCable();
        CableLayout layout = new(
            cable.Id,
            [new DocumentPoint(10, 20), new DocumentPoint(50, 20)]);

        _ = new CableRenderer().Render(cable, layout);

        Assert.Equal(cable.Id, layout.CableSegmentId);
    }

    [Fact]
    public void RenderUsesCableLayoutLabelPositionAsLabelAnchor()
    {
        CableSegment cable = CreateCable();
        CableLayout layout = new(
            cable.Id,
            [new DocumentPoint(10, 20), new DocumentPoint(50, 20)],
            new DocumentPoint(37, 14));

        SceneText label = Assert.Single(new CableRenderer().Render(cable, layout).OfType<SceneText>());

        Assert.Equal(new DocumentPoint(37, 14), label.Origin);
    }

    [Fact]
    public void RenderBatchUsesLabelEngineToAvoidInitialCableLabelCollision()
    {
        CableSegment firstCable = CreateCable();
        CableSegment secondCable = CreateCable();
        CableLayout firstLayout = CreateLayout(firstCable, new DocumentPoint(30, 20));
        CableLayout secondLayout = CreateLayout(secondCable, new DocumentPoint(30, 20));

        IReadOnlyList<SceneText> labels = new CableRenderer()
            .Render([(firstCable, firstLayout), (secondCable, secondLayout)])
            .OfType<SceneText>()
            .ToArray();

        Assert.Equal(2, labels.Count);
        Assert.NotEqual(labels[0].Origin, labels[1].Origin);
    }

    [Fact]
    public void RenderBatchProducesStableLabelPositions()
    {
        CableSegment firstCable = CreateCable();
        CableSegment secondCable = CreateCable();
        CableLayout firstLayout = CreateLayout(firstCable, new DocumentPoint(30, 20));
        CableLayout secondLayout = CreateLayout(secondCable, new DocumentPoint(30, 20));
        (CableSegment CableSegment, CableLayout Layout)[] inputs =
        [
            (firstCable, firstLayout),
            (secondCable, secondLayout)
        ];

        CableRenderer renderer = new();
        IReadOnlyList<DocumentPoint> first = renderer.Render(inputs)
            .OfType<SceneText>()
            .Select(label => label.Origin)
            .ToArray();
        IReadOnlyList<DocumentPoint> second = renderer.Render(inputs)
            .OfType<SceneText>()
            .Select(label => label.Origin)
            .ToArray();

        Assert.Equal(first, second);
    }

    private static CableSegment CreateCable()
    {
        return new CableSegment(
            Guid.NewGuid(),
            "Cable-001",
            "YJV22-8.7/15kV",
            120,
            "10kV",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
    }

    private static CableLayout CreateLayout(CableSegment cable, DocumentPoint labelPosition)
    {
        return new CableLayout(
            cable.Id,
            [new DocumentPoint(10, 20), new DocumentPoint(50, 20)],
            labelPosition);
    }
}
