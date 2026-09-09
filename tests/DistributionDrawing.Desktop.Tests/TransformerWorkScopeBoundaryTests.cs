using DistributionDrawing.Desktop.WorkScopeCreation;
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
    public void WorkScopePicker_ExcludesTransformerHvTerminalButKeepsPoleTerminalEligible()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "WorkScope eligibility");
        var runtime = new RuntimeLayoutDocument(
            new DrawingLayout(),
            new Dictionary<Guid, RingCabinetLayout>());
        TransformerCreation transformer = new TransformerCreationFactory().Create(
            TransformerKind.PublicIndoor,
            new DocumentPoint(80, 40));
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
        Assert.False(WorkScopeBoundaryTerminalEligibility.IsEligible(
            document,
            transformer.HvTerminal.Id));
        Assert.True(anchors.TryGet(pole.Terminal.Id, out _));
        Assert.True(WorkScopeBoundaryTerminalEligibility.IsEligible(
            document,
            pole.Terminal.Id));
    }
}
