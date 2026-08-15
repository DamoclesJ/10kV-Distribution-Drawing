using ApplicationSelectionTargetKind = DistributionDrawing.Application.Interaction.SelectionTargetKind;
using DistributionDrawing.Application.Interaction;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.Building;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class DeleteRuntimeTests
{
    [Fact]
    public void DeleteRingCabinet_UndoRedoRestoresSameStableId()
    {
        RingCabinetDomainBuildOutcome build = new RingCabinetTemplateDomainBuilder().Build(
            new RingCabinetTemplate(
                new TemplateId("test:ring-cabinet"),
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
        Assert.NotNull(build.Result);
        var cabinet = build.Result!.Cabinet;
        DrawingDocument document = new(Guid.NewGuid(), "Test");
        document.AddDevice(cabinet);
        RingCabinetLayout layout = new(cabinet.Id, new DocumentPoint(0, 0), 100, 50, 10, []);
        var runtime = new RuntimeLayoutDocument(
            new DrawingLayout(),
            new Dictionary<Guid, RingCabinetLayout> { [cabinet.Id] = layout });
        var selection = new SelectionService();
        selection.Select(new SelectionTarget(ApplicationSelectionTargetKind.RingCabinet, cabinet.Id));
        var command = new DeleteLayoutCommand(
            document,
            runtime,
            selection.CurrentSelection!,
            selection);

        command.Execute();
        Assert.DoesNotContain(document.Devices, device => device.Id == cabinet.Id);
        Assert.False(runtime.RingCabinetLayouts.ContainsKey(cabinet.Id));
        Assert.Null(selection.CurrentSelection);

        command.Undo();
        Assert.Same(cabinet, document.Devices.Single(device => device.Id == cabinet.Id));
        Assert.True(runtime.RingCabinetLayouts.ContainsKey(cabinet.Id));
        command.Redo();
        Assert.DoesNotContain(document.Devices, device => device.Id == cabinet.Id);
    }

    [Fact]
    public void DeletePole_UndoRestoresSameStableId()
    {
        Pole pole = new(Guid.NewGuid(), "P-1");
        DrawingDocument document = new(Guid.NewGuid(), "Test");
        document.AddDevice(pole);
        var runtime = new RuntimeLayoutDocument(
            new DrawingLayout(),
            new Dictionary<Guid, RingCabinetLayout>());
        runtime.DrawingLayout.Add(new PoleLayout(pole.Id, new DocumentPoint(0, 0)));
        var selection = new SelectionService();
        var command = new DeleteLayoutCommand(
            document,
            runtime,
            new SelectionTarget(ApplicationSelectionTargetKind.Pole, pole.Id),
            selection);

        command.Execute();
        Assert.Empty(document.Devices);
        Assert.Empty(runtime.DrawingLayout.Poles);
        command.Undo();
        Assert.Same(pole, Assert.Single(document.Devices));
        Assert.True(runtime.DrawingLayout.Poles.ContainsKey(pole.Id));
    }

    [Fact]
    public void DeleteFailure_PreservesSelectionAndState()
    {
        Pole pole = new(Guid.NewGuid(), "P-1");
        SwitchDevice switchDevice = SwitchDevice.CreateForPole(
            Guid.NewGuid(),
            SwitchKind.LoadSwitch,
            Guid.NewGuid(),
            Guid.NewGuid());
        DrawingDocument document = new(Guid.NewGuid(), "Test");
        document.AddDevice(pole);
        document.AddDevice(switchDevice);
        document.AddPoleAttachment(new PoleAttachment(Guid.NewGuid(), pole.Id, switchDevice.Id));
        var runtime = new RuntimeLayoutDocument(new DrawingLayout(), new Dictionary<Guid, RingCabinetLayout>());
        runtime.DrawingLayout.Add(new PoleLayout(pole.Id, new DocumentPoint(0, 0)));
        var selection = new SelectionService();
        SelectionTarget target = new(ApplicationSelectionTargetKind.Pole, pole.Id);
        selection.Select(target);
        var command = new DeleteLayoutCommand(document, runtime, target, selection);

        Assert.Throws<InvalidOperationException>(command.Execute);
        Assert.Equal(target, selection.CurrentSelection);
        Assert.Contains(document.Devices, device => device.Id == pole.Id);
        Assert.True(runtime.DrawingLayout.Poles.ContainsKey(pole.Id));
    }
}
