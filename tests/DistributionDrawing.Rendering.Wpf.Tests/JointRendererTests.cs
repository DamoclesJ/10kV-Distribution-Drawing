using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class JointRendererTests
{
    [Fact]
    public void RenderIntermediateTerminal_ProducesJointSymbol()
    {
        IntermediateTerminal intermediateTerminal = CreateIntermediateTerminal();
        JointLayout layout = new(
            intermediateTerminal.Id,
            new DocumentPoint(30, 20));

        IReadOnlyList<SceneElement> elements = new JointRenderer().Render(
            intermediateTerminal,
            layout);

        Assert.Single(elements.OfType<SceneRectangle>());
        Assert.Contains(elements.OfType<SceneText>(), text =>
            text.Text == intermediateTerminal.DisplayName);
    }

    [Fact]
    public void RenderDoesNotCreateOrModifyConnections()
    {
        IntermediateTerminal intermediateTerminal = CreateIntermediateTerminal();
        Guid terminalId = intermediateTerminal.TerminalId;
        JointLayout layout = new(
            intermediateTerminal.Id,
            new DocumentPoint(30, 20));

        IReadOnlyList<SceneElement> elements = new JointRenderer().Render(
            intermediateTerminal,
            layout);

        Assert.NotEmpty(elements);
        Assert.Equal(terminalId, intermediateTerminal.TerminalId);
    }

    [Fact]
    public void RenderPreservesIntermediateTerminalDomainIdentity()
    {
        IntermediateTerminal intermediateTerminal = CreateIntermediateTerminal();
        Guid id = intermediateTerminal.Id;
        JointLayout layout = new(id, new DocumentPoint(30, 20));

        _ = new JointRenderer().Render(intermediateTerminal, layout);

        Assert.Equal(id, intermediateTerminal.Id);
    }

    private static IntermediateTerminal CreateIntermediateTerminal()
    {
        return new IntermediateTerminal(
            Guid.NewGuid(),
            "Joint-X",
            Guid.NewGuid());
    }
}
