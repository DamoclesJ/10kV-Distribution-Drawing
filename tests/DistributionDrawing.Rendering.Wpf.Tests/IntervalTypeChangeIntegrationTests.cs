using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;
using DistributionDrawing.Application.Templates.RingCabinets.Building;
using DistributionDrawing.Domain.Devices;
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
    public void ChangeIntervalTypeCommand_ReplacesDeviceOwnedTopologyWithTheInternalSwitches()
    {
        RingCabinet cabinet = CreateCabinet();
        var document = new DrawingDocument(Guid.NewGuid(), "Project");
        document.AddDevice(cabinet);
        RingCabinetInterval interval = cabinet.Intervals.Single(item => item.BayIndex == 1);
        Guid[] oldDeviceIds = interval.SwitchDevices.Select(device => device.Id).ToArray();
        RuntimeLayoutDocument runtimeLayout = CreateLayout(cabinet);
        var command = new ChangeIntervalTypeCommand(
            cabinet,
            runtimeLayout,
            interval.IntervalId,
            IntervalKind.LoadSwitchInterval,
            null,
            document: document);

        command.Execute();

        RingCabinetInterval replacement = cabinet.Intervals.Single(item =>
            item.IntervalId == interval.IntervalId);
        Guid[] replacementDeviceIds = replacement.SwitchDevices
            .Select(device => device.Id)
            .ToArray();
        Assert.DoesNotContain(document.Devices, device => oldDeviceIds.Contains(device.Id));
        Assert.DoesNotContain(document.Terminals, terminal =>
            terminal.OwnerType == TopologyOwnerType.Device &&
            oldDeviceIds.Contains(terminal.OwnerId));
        Assert.All(replacementDeviceIds, replacementId =>
            Assert.Contains(document.Devices, device => device.Id == replacementId));
        Assert.All(document.Terminals.Where(terminal =>
                terminal.OwnerType == TopologyOwnerType.Device),
            terminal => Assert.Contains(document.Devices, device =>
                device.Id == terminal.OwnerId));
    }

    [Fact]
    public void ChangeIntervalTypeCommand_ChangeAndChangeBackLeavesNoStaleDeviceOwner()
    {
        RingCabinet cabinet = CreateCabinet();
        var document = new DrawingDocument(Guid.NewGuid(), "Project");
        document.AddDevice(cabinet);
        RingCabinetInterval interval = cabinet.Intervals.Single(item => item.BayIndex == 1);
        RuntimeLayoutDocument runtimeLayout = CreateLayout(cabinet);

        new ChangeIntervalTypeCommand(
            cabinet,
            runtimeLayout,
            interval.IntervalId,
            IntervalKind.LoadSwitchInterval,
            null,
            document: document).Execute();
        new ChangeIntervalTypeCommand(
            cabinet,
            runtimeLayout,
            interval.IntervalId,
            IntervalKind.IntegratedFeederInterval,
            GroundingStructureKind.UpperIsolationGrounding,
            document: document).Execute();

        Assert.All(document.Terminals.Where(terminal =>
                terminal.OwnerType == TopologyOwnerType.Device),
            terminal => Assert.Contains(document.Devices, device =>
                device.Id == terminal.OwnerId));
        Assert.All(document.ElectricalNodes.Where(node =>
                node.OwnerType == TopologyOwnerType.Device),
            node => Assert.Contains(document.Devices, device => device.Id == node.OwnerId));
    }

    [Theory]
    [InlineData(RingCabinetPTPlacement.Left, 1)]
    [InlineData(RingCabinetPTPlacement.Right, 5)]
    public void PTMigration_FromEitherEndIsAtomicAndTopologyValidAcrossUndoRedo(
        RingCabinetPTPlacement placement,
        int originalPTBay)
    {
        RingCabinet cabinet = CreateCabinetWithPTAt(placement);
        var document = new DrawingDocument(Guid.NewGuid(), "Project");
        document.AddDevice(cabinet);
        RingCabinetInterval target = cabinet.Intervals.Single(interval => interval.BayIndex == 3);
        Guid originalPTId = cabinet.Intervals.Single(interval =>
            interval.IntervalKind == IntervalKind.PTInterval).IntervalId;
        RuntimeLayoutDocument runtimeLayout = CreateLayout(cabinet);
        var stack = new CommandStack();

        stack.ExecuteCommand(new ChangeIntervalTypeCommand(
            cabinet,
            runtimeLayout,
            target.IntervalId,
            IntervalKind.PTInterval,
            null,
            document: document));

        Assert.Single(stack.History);
        Assert.Equal(IntervalKind.PTInterval, cabinet.Intervals.Single(interval =>
            interval.IntervalId == target.IntervalId).IntervalKind);
        RingCabinetInterval restored = cabinet.Intervals.Single(interval =>
            interval.IntervalId == originalPTId);
        Assert.Equal(originalPTBay, restored.BayIndex);
        Assert.Equal(IntervalKind.IntegratedFeederInterval, restored.IntervalKind);
        Assert.Equal(
            GroundingStructureKind.UpperIsolationGrounding,
            restored.GroundingStructureKind);
        Assert.Single(cabinet.Intervals, interval =>
            interval.IntervalKind == IntervalKind.PTInterval);
        AssertDeviceTopologyOwnersExist(document);

        Assert.True(stack.Undo());
        Assert.Equal(IntervalKind.PTInterval, cabinet.Intervals.Single(interval =>
            interval.IntervalId == originalPTId).IntervalKind);
        Assert.Equal(IntervalKind.IntegratedFeederInterval, cabinet.Intervals.Single(interval =>
            interval.IntervalId == target.IntervalId).IntervalKind);
        AssertDeviceTopologyOwnersExist(document);

        Assert.True(stack.Redo());
        Assert.Equal(IntervalKind.PTInterval, cabinet.Intervals.Single(interval =>
            interval.IntervalId == target.IntervalId).IntervalKind);
        Assert.Equal(IntervalKind.IntegratedFeederInterval, cabinet.Intervals.Single(interval =>
            interval.IntervalId == originalPTId).IntervalKind);
        AssertDeviceTopologyOwnersExist(document);
    }

    [Fact]
    public void PTMigration_FromMiddleToAnotherIntervalKeepsOnePTAndValidOwners()
    {
        RingCabinet cabinet = CreateCabinetWithPTAt(RingCabinetPTPlacement.Right);
        var document = new DrawingDocument(Guid.NewGuid(), "Project");
        document.AddDevice(cabinet);
        RuntimeLayoutDocument runtimeLayout = CreateLayout(cabinet);
        RingCabinetInterval middle = cabinet.Intervals.Single(interval => interval.BayIndex == 3);
        RingCabinetInterval destination = cabinet.Intervals.Single(interval => interval.BayIndex == 2);

        new ChangeIntervalTypeCommand(
            cabinet,
            runtimeLayout,
            middle.IntervalId,
            IntervalKind.PTInterval,
            null,
            document: document).Execute();
        new ChangeIntervalTypeCommand(
            cabinet,
            runtimeLayout,
            destination.IntervalId,
            IntervalKind.PTInterval,
            null,
            document: document).Execute();

        Assert.Equal(IntervalKind.PTInterval, cabinet.Intervals.Single(interval =>
            interval.IntervalId == destination.IntervalId).IntervalKind);
        Assert.Equal(IntervalKind.IntegratedFeederInterval, cabinet.Intervals.Single(interval =>
            interval.IntervalId == middle.IntervalId).IntervalKind);
        Assert.Single(cabinet.Intervals, interval =>
            interval.IntervalKind == IntervalKind.PTInterval);
        AssertDeviceTopologyOwnersExist(document);
    }

    [Fact]
    public void PTMigration_RejectsConnectedRetiredExternalTerminalWithoutDeletingConnection()
    {
        RingCabinet cabinet = CreateCabinetWithPTAt(RingCabinetPTPlacement.Right);
        var document = new DrawingDocument(Guid.NewGuid(), "Project");
        document.AddDevice(cabinet);
        RingCabinetInterval target = cabinet.Intervals.Single(interval => interval.BayIndex == 3);
        RingCabinetInterval peer = cabinet.Intervals.Single(interval => interval.BayIndex == 2);
        var connection = new Connection(
            Guid.NewGuid(),
            ConnectionType.Cable,
            target.ExternalTerminalId,
            peer.ExternalTerminalId,
            "Protected cable",
            "10kV");
        document.AddConnection(connection);
        RuntimeLayoutDocument runtimeLayout = CreateLayout(cabinet);
        var stack = new CommandStack();

        Assert.Throws<InvalidOperationException>(() => stack.ExecuteCommand(
            new ChangeIntervalTypeCommand(
                cabinet,
                runtimeLayout,
                target.IntervalId,
                IntervalKind.PTInterval,
                null,
                document: document)));

        Assert.Empty(stack.History);
        Assert.Equal(IntervalKind.IntegratedFeederInterval, cabinet.Intervals.Single(interval =>
            interval.IntervalId == target.IntervalId).IntervalKind);
        Assert.Contains(document.Connections, item => item.Id == connection.Id);
        AssertDeviceTopologyOwnersExist(document);
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

    private static RingCabinet CreateCabinetWithPTAt(RingCabinetPTPlacement placement)
    {
        RingCabinetTemplate template = new RingCabinetCreationTemplateFactory().Create(
            RingCabinetTemplateType.PrimarySecondaryIntegrated,
            5,
            includePTInterval: true,
            ptPlacement: placement);
        RingCabinetDomainBuildOutcome outcome = new RingCabinetTemplateDomainBuilder().Build(
            template,
            "Cabinet");
        Assert.NotNull(outcome.Result);
        return outcome.Result!.Cabinet;
    }

    private static void AssertDeviceTopologyOwnersExist(DrawingDocument document)
    {
        Assert.All(document.ElectricalNodes.Where(node =>
                node.OwnerType == TopologyOwnerType.Device),
            node => Assert.Contains(document.Devices, device => device.Id == node.OwnerId));
        Assert.All(document.Terminals.Where(terminal =>
                terminal.OwnerType == TopologyOwnerType.Device),
            terminal => Assert.Contains(document.Devices, device =>
                device.Id == terminal.OwnerId));
    }
}
