using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.Building;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.PropertyInspector;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class IntervalTypeChangeIntegrationTests
{
    [Fact]
    public void ChangeIntervalTypeCommand_UndoRedoRestoresTheSameStableIds()
    {
        RingCabinet cabinet = CreateCabinet();
        RingCabinetInterval interval = cabinet.Intervals.Single(item => item.BayIndex == 1);
        Guid intervalId = interval.IntervalId;
        Guid[] beforeSwitchIds = interval.SwitchDevices.Select(device => device.Id).ToArray();
        Guid[] beforeTerminalIds = interval.SwitchDevices
            .SelectMany(device => device.TerminalIds)
            .ToArray();
        Guid[] beforeNodeIds = cabinet.ElectricalNodes.Select(node => node.Id).ToArray();
        Guid beforeAssemblyId = interval.SwitchAssembly.AssemblyId;
        var commandStack = new CommandStack();
        var command = new ChangeIntervalTypeCommand(
            cabinet,
            intervalId,
            IntervalKind.PTInterval,
            null);

        commandStack.ExecuteCommand(command);
        RingCabinetInterval changed = cabinet.Intervals.Single(item => item.BayIndex == 1);
        Guid[] afterSwitchIds = changed.SwitchDevices.Select(device => device.Id).ToArray();
        Guid[] afterTerminalIds = changed.SwitchDevices
            .SelectMany(device => device.TerminalIds)
            .ToArray();
        Guid[] afterNodeIds = cabinet.ElectricalNodes.Select(node => node.Id).ToArray();
        Guid afterAssemblyId = changed.SwitchAssembly.AssemblyId;

        Assert.Equal(intervalId, changed.IntervalId);
        Assert.Equal(IntervalKind.PTInterval, changed.IntervalKind);
        Assert.NotEqual(beforeAssemblyId, afterAssemblyId);
        Assert.NotEqual(beforeSwitchIds[0], afterSwitchIds[0]);
        Assert.NotEqual(beforeTerminalIds[0], afterTerminalIds[0]);
        Assert.NotEqual(beforeNodeIds[1], afterNodeIds[1]);

        Assert.True(commandStack.Undo());
        RingCabinetInterval undone = cabinet.Intervals.Single(item => item.BayIndex == 1);
        Assert.Equal(IntervalKind.IntegratedFeederInterval, undone.IntervalKind);
        Assert.Equal(beforeSwitchIds, undone.SwitchDevices.Select(device => device.Id));
        Assert.Equal(beforeTerminalIds, undone.SwitchDevices.SelectMany(device => device.TerminalIds));
        Assert.Equal(beforeNodeIds, cabinet.ElectricalNodes.Select(node => node.Id));
        Assert.Equal(beforeAssemblyId, undone.SwitchAssembly.AssemblyId);

        Assert.True(commandStack.Redo());
        RingCabinetInterval redone = cabinet.Intervals.Single(item => item.BayIndex == 1);
        Assert.Equal(afterSwitchIds, redone.SwitchDevices.Select(device => device.Id));
        Assert.Equal(afterTerminalIds, redone.SwitchDevices.SelectMany(device => device.TerminalIds));
        Assert.Equal(afterNodeIds, cabinet.ElectricalNodes.Select(node => node.Id));
        Assert.Equal(afterAssemblyId, redone.SwitchAssembly.AssemblyId);
    }

    [Fact]
    public void InspectorProjector_UsesDomainNumberingAndKeepsIntervalSelection()
    {
        RingCabinet cabinet = CreateCabinet();
        RingCabinetInterval interval = cabinet.Intervals.Single(item => item.BayIndex == 1);
        var selection = new ResolvedSelection
        {
            Reference = new SelectionReference(
                SelectionTargetKind.RingCabinetInterval,
                interval.IntervalId,
                cabinet.Id),
            RingCabinet = cabinet,
            RingCabinetInterval = interval
        };

        PropertyInspectorSnapshot snapshot = new PropertyProjector().Project(selection);
        PropertyRowViewModel[] rows = snapshot.Sections
            .SelectMany(section => section.Properties)
            .ToArray();

        Assert.Equal(interval.IntervalId, snapshot.Selection!.ObjectId);
        Assert.Contains(rows, row => row is { PropertyKey: "BayIndex", DisplayValue: "1" });
        Assert.Contains(rows, row => row is { PropertyKey: "BusinessNumber", DisplayValue: "-1" });
        Assert.Contains(rows, row => row is { PropertyKey: "Sequence", IsReadOnly: true });
    }

    [Fact]
    public void IntervalTypeFactory_RejectsSecondPTWithoutCreatingACommand()
    {
        RingCabinet cabinet = CreateCabinetWithPTAndFeeder();
        RingCabinetInterval candidate = cabinet.Intervals.Single(
            interval => interval.IntervalKind == IntervalKind.IntegratedFeederInterval);
        var selection = new ResolvedSelection
        {
            Reference = new SelectionReference(
                SelectionTargetKind.RingCabinetInterval,
                candidate.IntervalId,
                cabinet.Id),
            RingCabinet = cabinet,
            RingCabinetInterval = candidate
        };
        var factory = new PropertyCommandFactory();

        Assert.True(factory.TryCreateIntervalTypeChange(
            selection,
            IntervalKind.PTInterval,
            null,
            out ICommand? command,
            out _));
        Assert.NotNull(command);
        Assert.Throws<InvalidOperationException>(() => command!.Execute());
        Assert.Equal(IntervalKind.IntegratedFeederInterval, candidate.IntervalKind);
    }

    private static RingCabinet CreateCabinet()
    {
        RingCabinetDomainBuildOutcome outcome = new RingCabinetTemplateDomainBuilder().Build(
            new RingCabinetTemplate(
                new TemplateId("test:interval-inspector"),
                "Cabinet",
                RingCabinetTemplateType.Conventional,
                [
                    new BayTemplate(
                        1,
                        new IntegratedFeederConfiguration(
                            GroundingStructureKind.UpperIsolationGrounding)),
                    new BayTemplate(
                        2,
                        new IntegratedFeederConfiguration(
                            GroundingStructureKind.UpperIsolationGrounding)),
                    new BayTemplate(
                        3,
                        new IntegratedFeederConfiguration(
                            GroundingStructureKind.UpperIsolationGrounding)),
                    new BayTemplate(
                        4,
                        new IntegratedFeederConfiguration(
                            GroundingStructureKind.UpperIsolationGrounding))
                ],
                RingCabinetLayoutRule.Default,
                NoSecondaryConfiguration.Instance),
            "Cabinet");
        Assert.NotNull(outcome.Result);
        return outcome.Result!.Cabinet;
    }

    private static RingCabinet CreateCabinetWithPTAndFeeder()
    {
        RingCabinetDomainBuildOutcome outcome = new RingCabinetTemplateDomainBuilder().Build(
            new RingCabinetTemplate(
                new TemplateId("test:interval-pt-uniqueness"),
                "Cabinet",
                RingCabinetTemplateType.Conventional,
                [
                    new BayTemplate(
                        1,
                        new IntegratedFeederConfiguration(
                            GroundingStructureKind.UpperIsolationGrounding)),
                    new BayTemplate(
                        2,
                        new IntegratedFeederConfiguration(
                            GroundingStructureKind.UpperIsolationGrounding)),
                    new BayTemplate(
                        3,
                        new IntegratedFeederConfiguration(
                            GroundingStructureKind.UpperIsolationGrounding)),
                    new BayTemplate(
                        4,
                        new IntegratedFeederConfiguration(
                            GroundingStructureKind.UpperIsolationGrounding))
                ],
                RingCabinetLayoutRule.Default,
                NoSecondaryConfiguration.Instance),
            "Cabinet");
        Assert.NotNull(outcome.Result);
        RingCabinet cabinet = outcome.Result!.Cabinet;
        cabinet.ChangeIntervalType(
            cabinet.Intervals[0].IntervalId,
            IntervalKind.PTInterval,
            null);
        return cabinet;
    }
}
