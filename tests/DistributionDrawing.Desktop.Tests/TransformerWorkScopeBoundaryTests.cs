using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class TransformerWorkScopeBoundaryTests
{
    [Fact]
    public void ConfirmedMembershipCanIncludeTransformerHvAndPoleTerminals()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "WorkScope eligibility");
        var runtime = new RuntimeLayoutDocument(
            new DrawingLayout(),
            new Dictionary<Guid, RingCabinetLayout>());
        TransformerCreation transformer = new TransformerCreationFactory().Create(
            TransformerKind.PublicIndoor,
            new DocumentPoint(80, 40),
            "测试变压器");
        new AddTransformerCommand(document, runtime, transformer).Execute();
        AddPoleCommand pole = new DeviceCommandFactory().CreateAddPole(
            document,
            runtime,
            new DocumentPoint(20, 40));
        pole.Execute();

        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            runtime.DrawingLayout,
            runtime.RingCabinetLayouts,
            document.Connections,
            document.CableSegments,
            runtime.TransformerLayouts);

        Assert.True(anchors.TryGet(transformer.HvTerminal.Id, out _));
        WorkScope scope = document.CreateWorkScope(Guid.NewGuid(),
            [new WorkScopeRegion([transformer.HvTerminal.Id, pole.Terminal.Id], [])], [], null);
        Assert.Equal(new[] { transformer.HvTerminal.Id, pole.Terminal.Id }, scope.Regions[0].TerminalIds);
        Assert.Empty(scope.Boundaries);

    }
}
