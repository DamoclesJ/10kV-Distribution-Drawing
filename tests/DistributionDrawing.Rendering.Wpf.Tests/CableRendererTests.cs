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
}
