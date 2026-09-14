using DistributionDrawing.Application.Interaction;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.Building;
using DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;
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
        Assert.Same(cabinet, Assert.Single(document.Devices.OfType<RingCabinet>()));
        Assert.Equal(cabinet.Id, selection.CurrentSelection?.TargetId);
        command.Undo();
        Assert.Empty(document.Devices);
        Assert.Null(selection.CurrentSelection);
        command.Redo();
        Assert.Same(cabinet, Assert.Single(document.Devices.OfType<RingCabinet>()));
        Assert.Equal(cabinet.Id, selection.CurrentSelection?.TargetId);
    }

    [Theory]
    [InlineData(4, 5)]
    [InlineData(6, 7)]
    public void AddIntegratedRingCabinetWithPT_ExecuteUndoRedoPreservesCompleteAggregate(
        int businessIntervalCount,
        int totalIntervalCount)
    {
        DrawingDocument document = new(Guid.NewGuid(), "Test");
        var runtime = new RuntimeLayoutDocument(
            new DrawingLayout(),
            new Dictionary<Guid, RingCabinetLayout>());
        RingCabinetTemplate template = new RingCabinetCreationTemplateFactory().Create(
            RingCabinetTemplateType.PrimarySecondaryIntegrated,
            businessIntervalCount,
            includePTInterval: true);
        AddRingCabinetCommand command = new DeviceCommandFactory().CreateAddRingCabinet(
            document,
            runtime,
            new RingCabinetCreationConfiguration("Integrated PT cabinet", template),
            new DocumentPoint(1, 2));
        Guid cabinetId = command.Cabinet.Id;
        Guid[] intervalIds = command.Cabinet.Intervals
            .Select(interval => interval.IntervalId)
            .ToArray();
        Guid[] switchIds = command.Cabinet.Intervals
            .SelectMany(interval => interval.SwitchDevices)
            .Select(device => device.Id)
            .ToArray();

        command.Execute();
        Assert.Equal(totalIntervalCount, Assert.Single(
            document.Devices.OfType<RingCabinet>()).Intervals.Count);
        Assert.Equal(totalIntervalCount, runtime.RingCabinetLayouts[cabinetId]
            .IntervalLayouts.Count);

        command.Undo();
        Assert.Empty(document.Devices);
        Assert.False(runtime.RingCabinetLayouts.ContainsKey(cabinetId));

        command.Redo();
        RingCabinet redone = Assert.Single(document.Devices.OfType<RingCabinet>());
        Assert.Equal(intervalIds, redone.Intervals.Select(interval => interval.IntervalId));
        Assert.Equal(switchIds, redone.Intervals
            .SelectMany(interval => interval.SwitchDevices)
            .Select(device => device.Id));
        Assert.Equal(businessIntervalCount, redone.Intervals.Count(interval =>
            interval.IntervalKind == IntervalKind.IntegratedFeederInterval));
        Assert.Single(redone.Intervals, interval =>
            interval.IntervalKind == IntervalKind.PTInterval);
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
        Pole candidate = new(Guid.NewGuid(), "P-2");
        Terminal terminal = candidate.CreateOverheadAnchorTerminal(Guid.NewGuid(), true);
        PoleLayout candidateLayout = new(candidate.Id, new DocumentPoint(2, 2));
        runtime.DrawingLayout.Add(candidateLayout);
        var command = new AddPoleCommand(
            document,
            runtime,
            candidate,
            terminal,
            candidateLayout);

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
