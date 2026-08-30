using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.Building;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.PropertyInspector;
using DistributionDrawing.Rendering.Wpf.Scene;
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
        RuntimeLayoutDocument runtimeLayout = CreateLayout(cabinet);
        RingCabinetIntervalLayout beforeIntervalLayout = runtimeLayout
            .RingCabinetLayouts[cabinet.Id]
            .IntervalLayouts[intervalId];
        var commandStack = new CommandStack();
        var command = new ChangeIntervalTypeCommand(
            cabinet,
            runtimeLayout,
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
        RingCabinetIntervalLayout changedLayout = runtimeLayout
            .RingCabinetLayouts[cabinet.Id]
            .IntervalLayouts[intervalId];

        Assert.Equal(intervalId, changed.IntervalId);
        Assert.Equal(IntervalKind.PTInterval, changed.IntervalKind);
        Assert.NotEqual(beforeAssemblyId, afterAssemblyId);
        Assert.NotEqual(beforeSwitchIds[0], afterSwitchIds[0]);
        Assert.NotEqual(beforeTerminalIds[0], afterTerminalIds[0]);
        Assert.NotEqual(beforeNodeIds[1], afterNodeIds[1]);
        Assert.Equal(RingCabinetLayoutFactory.DefaultPTSymbolPosition, changedLayout.PTSymbolPosition);
        Assert.True(changed.SwitchDevices.Select(device => device.Id).ToHashSet()
            .SetEquals(changedLayout.SwitchLayouts.Keys));

        Assert.True(commandStack.Undo());
        RingCabinetInterval undone = cabinet.Intervals.Single(item => item.BayIndex == 1);
        Assert.Equal(IntervalKind.IntegratedFeederInterval, undone.IntervalKind);
        Assert.Equal(beforeSwitchIds, undone.SwitchDevices.Select(device => device.Id));
        Assert.Equal(beforeTerminalIds, undone.SwitchDevices.SelectMany(device => device.TerminalIds));
        Assert.Equal(beforeNodeIds, cabinet.ElectricalNodes.Select(node => node.Id));
        Assert.Equal(beforeAssemblyId, undone.SwitchAssembly.AssemblyId);
        Assert.Same(
            beforeIntervalLayout,
            runtimeLayout.RingCabinetLayouts[cabinet.Id].IntervalLayouts[intervalId]);

        Assert.True(commandStack.Redo());
        RingCabinetInterval redone = cabinet.Intervals.Single(item => item.BayIndex == 1);
        Assert.Equal(afterSwitchIds, redone.SwitchDevices.Select(device => device.Id));
        Assert.Equal(afterTerminalIds, redone.SwitchDevices.SelectMany(device => device.TerminalIds));
        Assert.Equal(afterNodeIds, cabinet.ElectricalNodes.Select(node => node.Id));
        Assert.Equal(afterAssemblyId, redone.SwitchAssembly.AssemblyId);
        Assert.Same(
            changedLayout,
            runtimeLayout.RingCabinetLayouts[cabinet.Id].IntervalLayouts[intervalId]);
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
        Assert.Contains(rows, row => row is { PropertyKey: "BusinessNumber", DisplayValue: "负1" });
        Assert.Contains(rows, row => row is { PropertyKey: "Sequence", IsReadOnly: true });
        Assert.DoesNotContain(rows, row => row.PropertyKey is
            "IntervalId" or "ParentCabinetId" or "ExternalTerminalId" or
            "SymbolKind" or "SymbolVisualState" or "HitBounds" or "HitPriority");
    }

    [Fact]
    public void InspectorTypeChange_RegistersTheReplacementCableTerminalForPickingAndSelection()
    {
        RingCabinet cabinet = CreateCabinet();
        var document = new DrawingDocument(Guid.NewGuid(), "Project");
        document.AddDevice(cabinet);
        RingCabinetInterval interval = cabinet.Intervals.Single(item => item.BayIndex == 1);
        Guid previousExternalTerminalId = interval.ExternalTerminalId;
        RuntimeLayoutDocument runtimeLayout = CreateLayout(cabinet);
        var selection = new ResolvedSelection
        {
            Reference = new SelectionReference(
                SelectionTargetKind.RingCabinetInterval,
                interval.IntervalId,
                cabinet.Id),
            Document = document,
            RingCabinet = cabinet,
            RingCabinetInterval = interval
        };
        var factory = new PropertyCommandFactory();
        var stack = new CommandStack();

        Assert.True(factory.TryCreateIntervalTypeChange(
            selection,
            runtimeLayout,
            IntervalKind.PTInterval,
            null,
            out ICommand? command,
            out _));
        stack.ExecuteCommand(command!);

        RingCabinetInterval changed = cabinet.Intervals.Single(item => item.BayIndex == 1);
        Assert.NotEqual(previousExternalTerminalId, changed.ExternalTerminalId);
        Assert.DoesNotContain(document.Terminals, terminal =>
            terminal.Id == previousExternalTerminalId);
        Terminal replacement = Assert.Single(document.Terminals, terminal =>
            terminal.Id == changed.ExternalTerminalId);
        Assert.True(replacement.IsExternal);

        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            runtimeLayout.DrawingLayout,
            runtimeLayout.RingCabinetLayouts);
        Assert.True(anchors.TryGet(changed.ExternalTerminalId, out _));

        var resolver = new SelectionObjectResolver();
        resolver.SetSource(new PropertyInspectionSource
        {
            Document = document,
            Terminals = document.Terminals,
            RingCabinetLayouts = runtimeLayout.RingCabinetLayouts
        });
        ResolvedSelection? resolved = resolver.Resolve(new SelectionReference(
            SelectionTargetKind.Terminal,
            changed.ExternalTerminalId));
        Assert.Same(replacement, resolved?.Terminal);

        Assert.True(stack.Undo());
        Assert.Contains(document.Terminals, terminal =>
            terminal.Id == previousExternalTerminalId);
        Assert.DoesNotContain(document.Terminals, terminal =>
            terminal.Id == changed.ExternalTerminalId);

        Assert.True(stack.Redo());
        RingCabinetInterval redone = cabinet.Intervals.Single(item => item.BayIndex == 1);
        Assert.Equal(changed.ExternalTerminalId, redone.ExternalTerminalId);
        Assert.Contains(document.Terminals, terminal =>
            terminal.Id == redone.ExternalTerminalId);
    }

    [Fact]
    public void ChangeIntervalTypeCommand_PTToLoadSwitch_ReplacesPTAndSwitchLayoutOnUndoRedo()
    {
        RingCabinet cabinet = CreateCabinetWithPTAndFeeder();
        RingCabinetInterval pt = cabinet.Intervals.Single(interval =>
            interval.IntervalKind == IntervalKind.PTInterval);
        RuntimeLayoutDocument runtimeLayout = CreateLayout(cabinet);
        var stack = new CommandStack();
        var command = new ChangeIntervalTypeCommand(
            cabinet,
            runtimeLayout,
            pt.IntervalId,
            IntervalKind.LoadSwitchInterval,
            null);

        stack.ExecuteCommand(command);
        RingCabinetInterval changed = cabinet.Intervals.Single(interval =>
            interval.IntervalId == pt.IntervalId);
        RingCabinetIntervalLayout changedLayout = runtimeLayout
            .RingCabinetLayouts[cabinet.Id]
            .IntervalLayouts[pt.IntervalId];
        Guid[] changedSwitchIds = changed.SwitchDevices.Select(device => device.Id).ToArray();

        Assert.Equal(IntervalKind.LoadSwitchInterval, changed.IntervalKind);
        Assert.Null(changedLayout.PTSymbolPosition);
        Assert.True(changedSwitchIds.ToHashSet().SetEquals(changedLayout.SwitchLayouts.Keys));
        Assert.True(stack.Undo());
        Assert.Equal(
            IntervalKind.PTInterval,
            cabinet.Intervals.Single(interval => interval.IntervalId == pt.IntervalId).IntervalKind);
        Assert.Equal(
            RingCabinetLayoutFactory.DefaultPTSymbolPosition,
            runtimeLayout.RingCabinetLayouts[cabinet.Id]
                .IntervalLayouts[pt.IntervalId].PTSymbolPosition);
        Assert.True(stack.Redo());
        Assert.Equal(
            changedSwitchIds,
            cabinet.Intervals.Single(interval => interval.IntervalId == pt.IntervalId)
                .SwitchDevices.Select(device => device.Id));
        Assert.Null(runtimeLayout.RingCabinetLayouts[cabinet.Id]
            .IntervalLayouts[pt.IntervalId].PTSymbolPosition);
    }

    [Fact]
    public void IntervalTypeFactory_RejectsSecondPTWithoutCreatingACommand()
    {
        RingCabinet cabinet = CreateCabinetWithPTAndFeeder();
        RingCabinetInterval candidate = cabinet.Intervals.Single(
            interval => interval.BayIndex == 2 &&
                        interval.IntervalKind == IntervalKind.IntegratedFeederInterval);
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
        RuntimeLayoutDocument runtimeLayout = CreateLayout(cabinet);

        Assert.True(factory.TryCreateIntervalTypeChange(
            selection,
            runtimeLayout,
            IntervalKind.PTInterval,
            null,
            out ICommand? command,
            out _));
        Assert.NotNull(command);
        Assert.Throws<InvalidOperationException>(() => command!.Execute());
        Assert.Equal(IntervalKind.IntegratedFeederInterval, candidate.IntervalKind);
        Assert.Null(runtimeLayout.RingCabinetLayouts[cabinet.Id]
            .IntervalLayouts[candidate.IntervalId].PTSymbolPosition);
    }

    [Fact]
    public void IntervalNameProperty_ChangesGeneratedNameThroughCommandStack()
    {
        RingCabinet cabinet = CreateCabinet();
        RingCabinetInterval interval = cabinet.Intervals[0];
        Guid[] stableIds = interval.SwitchDevices.Select(device => device.Id).ToArray();
        var selection = new ResolvedSelection
        {
            Reference = new SelectionReference(
                SelectionTargetKind.RingCabinetInterval,
                interval.IntervalId,
                cabinet.Id),
            RingCabinet = cabinet,
            RingCabinetInterval = interval
        };
        var factory = new PropertyCommandFactory();
        var stack = new CommandStack();

        Assert.True(factory.TryCreate(
            selection,
            PropertyCommandFactory.IntervalDisplayNamePropertyKey,
            "用户修改名称",
            out ICommand? command,
            out _));
        stack.ExecuteCommand(command!);
        Assert.Equal(
            "用户修改名称",
            cabinet.Intervals.Single(item => item.IntervalId == interval.IntervalId).DisplayName);
        Assert.True(stack.Undo());
        Assert.Equal(
            interval.DisplayName,
            cabinet.Intervals.Single(item => item.IntervalId == interval.IntervalId).DisplayName);
        Assert.True(stack.Redo());
        RingCabinetInterval redone = cabinet.Intervals.Single(item =>
            item.IntervalId == interval.IntervalId);
        Assert.Equal("用户修改名称", redone.DisplayName);
        Assert.Equal(stableIds, redone.SwitchDevices.Select(device => device.Id));
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

    private static RuntimeLayoutDocument CreateLayout(RingCabinet cabinet)
    {
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(20, 30));
        return new RuntimeLayoutDocument(
            new DrawingLayout(),
            new Dictionary<Guid, RingCabinetLayout> { [cabinet.Id] = layout });
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
