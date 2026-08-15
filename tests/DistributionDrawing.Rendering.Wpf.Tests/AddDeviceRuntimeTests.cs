using DistributionDrawing.Application.Interaction;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.Building;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class AddDeviceRuntimeTests
{
    [Fact]
    public void AddRingCabinet_ExecuteUndoRedo_SelectsAndPreservesStableId()
    {
        DrawingDocument document = new(Guid.NewGuid(), "Test");
        var runtime = new RuntimeLayoutDocument(new DrawingLayout(), new Dictionary<Guid, RingCabinetLayout>());
        SelectionService selection = new();
        RingCabinet cabinet = CreateCabinet();
        RingCabinetLayout layout = new(cabinet.Id, new DocumentPoint(1, 2), 100, 50, 10, []);
        var command = new AddRingCabinetCommand(document, runtime, cabinet, layout, selection);

        command.Execute();
        Assert.Same(cabinet, Assert.Single(document.Devices));
        Assert.Equal(cabinet.Id, selection.CurrentSelection?.TargetId);
        command.Undo();
        Assert.Empty(document.Devices);
        Assert.Null(selection.CurrentSelection);
        command.Redo();
        Assert.Same(cabinet, Assert.Single(document.Devices));
        Assert.Equal(cabinet.Id, selection.CurrentSelection?.TargetId);
    }

    [Fact]
    public void AddPole_ExecuteUndoRedo_CreatesLayoutAndPreservesStableId()
    {
        DrawingDocument document = new(Guid.NewGuid(), "Test");
        var runtime = new RuntimeLayoutDocument(new DrawingLayout(), new Dictionary<Guid, RingCabinetLayout>());
        SelectionService selection = new();
        Pole pole = new(Guid.NewGuid(), "P-1");
        Terminal terminal = pole.CreateOverheadAnchorTerminal(Guid.NewGuid(), true);
        PoleLayout layout = new(pole.Id, new DocumentPoint(3, 4));
        var command = new AddPoleCommand(document, runtime, pole, terminal, layout, selection);

        command.Execute();
        Assert.Same(pole, Assert.Single(document.Devices));
        Assert.True(runtime.DrawingLayout.Poles.ContainsKey(pole.Id));
        command.Undo();
        Assert.Empty(document.Devices);
        Assert.Empty(runtime.DrawingLayout.Poles);
        command.Redo();
        Assert.Same(pole, Assert.Single(document.Devices));
        Assert.Equal(pole.Id, selection.CurrentSelection?.TargetId);
    }

    [Fact]
    public void AddFailure_DoesNotLeaveHalfCreatedObject()
    {
        DrawingDocument document = new(Guid.NewGuid(), "Test");
        Pole existing = new(Guid.NewGuid(), "P-1");
        document.AddDevice(existing);
        var runtime = new RuntimeLayoutDocument(new DrawingLayout(), new Dictionary<Guid, RingCabinetLayout>());
        runtime.DrawingLayout.Add(new PoleLayout(existing.Id, new DocumentPoint(0, 0)));
        Pole candidate = new(Guid.NewGuid(), "P-2");
        Terminal terminal = candidate.CreateOverheadAnchorTerminal(Guid.NewGuid(), true);
        var command = new AddPoleCommand(
            document,
            runtime,
            candidate,
            terminal,
            new PoleLayout(existing.Id, new DocumentPoint(2, 2)));

        Assert.Throws<InvalidOperationException>(command.Execute);
        Assert.DoesNotContain(document.Devices, device => device.Id == candidate.Id);
        Assert.Single(document.Devices);
        Assert.Single(runtime.DrawingLayout.Poles);
    }

    private static RingCabinet CreateCabinet()
    {
        RingCabinetDomainBuildOutcome outcome = new RingCabinetTemplateDomainBuilder().Build(
            new RingCabinetTemplate(
                new TemplateId("test:add-cabinet"),
                "Test cabinet",
                RingCabinetTemplateType.Conventional,
                [
                    new BayTemplate(1, new LoadSwitchConfiguration()),
                    new BayTemplate(2, new LoadSwitchConfiguration()),
                    new BayTemplate(3, new LoadSwitchConfiguration())
                ],
                RingCabinetLayoutRule.Default,
                NoSecondaryConfiguration.Instance),
            "Test cabinet");
        Assert.NotNull(outcome.Result);
        return outcome.Result!.Cabinet;
    }
}
