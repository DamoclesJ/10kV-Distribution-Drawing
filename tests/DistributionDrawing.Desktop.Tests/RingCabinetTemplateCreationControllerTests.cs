using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Desktop;
using DistributionDrawing.Desktop.RingCabinetTemplateCreation;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Templates.RingCabinets.Building;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class RingCabinetTemplateCreationControllerTests
{
    [Fact]
    public void Create_ExecutesCommandAndRecordsSelectionTransition()
    {
        ProjectRuntimeSession session = CreateSession();
        var beforeSelection = new SelectionReference(
            SelectionTargetKind.Device,
            Guid.NewGuid());
        session.SelectionManager.Select(beforeSelection);
        object sceneBefore = session.Scene;
        var controller = new RingCabinetTemplateCreationController(() => session);
        int sceneChangedCount = 0;
        controller.SceneChanged += (_, _) => sceneChangedCount++;

        RingCabinetTemplateBuildOutcome outcome = controller.Create(
            CreateRequest(RingCabinetLayoutRule.Default));

        RingCabinetTemplateBuildResult result = AssertSuccessful(outcome);
        AddRingCabinetCommand command = Assert.IsType<AddRingCabinetCommand>(
            Assert.Single(session.CommandStack.History));
        var afterSelection = new SelectionReference(
            SelectionTargetKind.RingCabinet,
            result.Cabinet.Id);
        Assert.Same(result.Cabinet, command.Cabinet);
        Assert.Same(result.Layout, command.Layout);
        Assert.Contains(
            result.Cabinet,
            session.PersistenceSession.Domain.Devices);
        Assert.Same(
            result.Layout,
            session.Layout.RingCabinetLayouts[result.Cabinet.Id]);
        Assert.Equal(afterSelection, session.SelectionManager.Selected);
        Assert.True(session.SelectionTransitions.TryGetUndoSelection(
            command,
            out SelectionReference? undoSelection));
        Assert.Equal(beforeSelection, undoSelection);
        Assert.True(session.SelectionTransitions.TryGetRedoSelection(
            command,
            out SelectionReference? redoSelection));
        Assert.Equal(afterSelection, redoSelection);
        Assert.True(session.CommandStack.IsDirty);
        Assert.NotSame(sceneBefore, session.Scene);
        Assert.Equal(1, sceneChangedCount);
    }

    [Theory]
    [InlineData(UnsupportedTemplate.Pt)]
    [InlineData(UnsupportedTemplate.Dtu)]
    [InlineData(UnsupportedTemplate.UnknownLayoutRule)]
    public void Create_BuildFailureLeavesEditorStateUnchanged(
        UnsupportedTemplate unsupportedTemplate)
    {
        ProjectRuntimeSession session = CreateSession();
        var beforeSelection = new SelectionReference(
            SelectionTargetKind.Device,
            Guid.NewGuid());
        session.SelectionManager.Select(beforeSelection);
        object sceneBefore = session.Scene;
        var controller = new RingCabinetTemplateCreationController(() => session);
        int sceneChangedCount = 0;
        controller.SceneChanged += (_, _) => sceneChangedCount++;

        RingCabinetTemplateBuildOutcome outcome = controller.Create(
            CreateUnsupportedRequest(unsupportedTemplate));

        Assert.False(outcome.IsSuccess);
        Assert.Null(outcome.Result);
        RingCabinetTemplateBuildFailure failure =
            Assert.IsType<RingCabinetTemplateBuildFailure>(outcome.Failure);
        if (unsupportedTemplate is UnsupportedTemplate.UnknownLayoutRule)
        {
            Assert.Equal(RingCabinetTemplateBuildFailureStage.Layout, failure.Stage);
            Assert.Equal(
                RingCabinetTemplateBuildFailureKind.UnsupportedLayoutRule,
                failure.Kind);
            Assert.Equal("test:unsupported", failure.UnsupportedRuleId);
        }
        else
        {
            Assert.Equal(RingCabinetTemplateBuildFailureStage.Domain, failure.Stage);
            Assert.Equal(
                RingCabinetTemplateBuildFailureKind.UnsupportedCapability,
                failure.Kind);
        }
        Assert.Empty(session.CommandStack.History);
        Assert.False(session.CommandStack.IsDirty);
        Assert.Equal(beforeSelection, session.SelectionManager.Selected);
        Assert.Same(sceneBefore, session.Scene);
        Assert.Empty(session.PersistenceSession.Domain.Devices);
        Assert.Empty(session.Layout.RingCabinetLayouts);
        Assert.Equal(0, sceneChangedCount);
    }

    [Fact]
    public void Create_CommandSupportsUndoRedoWithStableIdsAndSelections()
    {
        ProjectRuntimeSession session = CreateSession();
        var beforeSelection = new SelectionReference(
            SelectionTargetKind.Device,
            Guid.NewGuid());
        session.SelectionManager.Select(beforeSelection);
        var controller = new RingCabinetTemplateCreationController(() => session);
        RingCabinetTemplateBuildResult result = AssertSuccessful(
            controller.Create(CreateRequest(RingCabinetLayoutRule.Default)));
        AddRingCabinetCommand command = Assert.IsType<AddRingCabinetCommand>(
            Assert.Single(session.CommandStack.History));
        Guid cabinetId = result.Cabinet.Id;
        Guid[] intervalIds = result.Cabinet.Intervals
            .Select(interval => interval.IntervalId)
            .ToArray();
        Guid[] switchIds = result.Cabinet.Intervals
            .SelectMany(interval => interval.SwitchDevices)
            .Select(device => device.Id)
            .ToArray();
        Guid[] assemblyIds = result.Cabinet.Intervals
            .Select(interval => interval.SwitchAssembly.AssemblyId)
            .ToArray();
        Guid[] terminalIds = result.Cabinet.Terminals
            .Select(terminal => terminal.Id)
            .ToArray();
        Guid[] nodeIds = result.Cabinet.ElectricalNodes
            .Select(node => node.Id)
            .ToArray();

        Assert.True(session.SelectionTransitions.TryGetUndoSelection(
            command,
            out SelectionReference? undoSelection));
        Assert.True(session.CommandStack.Undo());
        session.RebuildScene();
        session.SelectionManager.Select(undoSelection);
        Assert.Equal(beforeSelection, session.SelectionManager.Selected);
        Assert.DoesNotContain(
            result.Cabinet,
            session.PersistenceSession.Domain.Devices);
        Assert.False(session.Layout.RingCabinetLayouts.ContainsKey(cabinetId));

        Assert.True(session.SelectionTransitions.TryGetRedoSelection(
            command,
            out SelectionReference? redoSelection));
        Assert.True(session.CommandStack.Redo());
        session.RebuildScene();
        session.SelectionManager.Select(redoSelection);
        Assert.Same(
            result.Cabinet,
            session.PersistenceSession.Domain.Devices.Single(device =>
                device.Id == cabinetId));
        Assert.Same(result.Layout, session.Layout.RingCabinetLayouts[cabinetId]);
        Assert.Equal(
            new SelectionReference(SelectionTargetKind.RingCabinet, cabinetId),
            session.SelectionManager.Selected);
        Assert.Equal(intervalIds, result.Cabinet.Intervals.Select(x => x.IntervalId));
        Assert.Equal(
            switchIds,
            result.Cabinet.Intervals
                .SelectMany(interval => interval.SwitchDevices)
                .Select(device => device.Id));
        Assert.Equal(
            assemblyIds,
            result.Cabinet.Intervals.Select(interval =>
                interval.SwitchAssembly.AssemblyId));
        Assert.Equal(terminalIds, result.Cabinet.Terminals.Select(x => x.Id));
        Assert.Equal(nodeIds, result.Cabinet.ElectricalNodes.Select(x => x.Id));
    }

    [Fact]
    public void Create_PrunesTransitionForTruncatedRedoCommand()
    {
        ProjectRuntimeSession session = CreateSession();
        var controller = new RingCabinetTemplateCreationController(() => session);
        _ = AssertSuccessful(
            controller.Create(CreateRequest(RingCabinetLayoutRule.Default)));
        ICommand firstCommand = Assert.Single(session.CommandStack.History);
        Assert.True(session.CommandStack.Undo());
        session.RebuildScene();
        session.SelectionManager.Clear();

        _ = AssertSuccessful(
            controller.Create(CreateRequest(RingCabinetLayoutRule.Default)));

        ICommand secondCommand = Assert.Single(session.CommandStack.History);
        Assert.NotSame(firstCommand, secondCommand);
        Assert.False(session.SelectionTransitions.TryGetUndoSelection(
            firstCommand,
            out _));
        Assert.True(session.SelectionTransitions.TryGetUndoSelection(
            secondCommand,
            out SelectionReference? selection));
        Assert.Null(selection);
    }

    [Fact]
    public void RedoFailure_DoesNotAdvanceHistoryOrChangeSelectionOrScene()
    {
        ProjectRuntimeSession session = CreateSession();
        var beforeSelection = new SelectionReference(
            SelectionTargetKind.Device,
            Guid.NewGuid());
        session.SelectionManager.Select(beforeSelection);
        var controller = new RingCabinetTemplateCreationController(() => session);
        RingCabinetTemplateBuildResult result = AssertSuccessful(
            controller.Create(CreateRequest(RingCabinetLayoutRule.Default)));
        Assert.True(session.CommandStack.Undo());
        session.RebuildScene();
        session.SelectionManager.Select(beforeSelection);
        object sceneBeforeFailedRedo = session.Scene;
        session.Layout.AddRingCabinet(result.Layout);

        Assert.Throws<InvalidOperationException>(() =>
            session.CommandStack.Redo());

        Assert.Equal(0, session.CommandStack.CurrentIndex);
        Assert.True(session.CommandStack.CanRedo);
        Assert.False(session.CommandStack.IsDirty);
        Assert.Equal(beforeSelection, session.SelectionManager.Selected);
        Assert.Same(sceneBeforeFailedRedo, session.Scene);
        Assert.DoesNotContain(
            result.Cabinet,
            session.PersistenceSession.Domain.Devices);
    }

    [Fact]
    public void ExecuteFailure_DoesNotAdvanceEditorStateOrRecordTransition()
    {
        ProjectRuntimeSession session = CreateSession();
        var beforeSelection = new SelectionReference(
            SelectionTargetKind.Device,
            Guid.NewGuid());
        session.SelectionManager.Select(beforeSelection);
        object sceneBeforeFailure = session.Scene;
        RingCabinetTemplateBuildResult result = BuildSuccessfully(
            CreateRequest(RingCabinetLayoutRule.Default));
        var factory = new DeviceCommandFactory();
        AddRingCabinetCommand command = factory.CreateAddRingCabinet(
            session.PersistenceSession.Domain,
            session.Layout,
            result.Cabinet,
            result.Layout);
        session.Layout.AddRingCabinet(result.Layout);

        Assert.Throws<InvalidOperationException>(() =>
            session.CommandStack.ExecuteCommand(command));

        Assert.Empty(session.CommandStack.History);
        Assert.False(session.CommandStack.IsDirty);
        Assert.Equal(beforeSelection, session.SelectionManager.Selected);
        Assert.Same(sceneBeforeFailure, session.Scene);
        Assert.DoesNotContain(
            result.Cabinet,
            session.PersistenceSession.Domain.Devices);
        Assert.False(session.SelectionTransitions.TryGetUndoSelection(
            command,
            out _));
        Assert.False(session.SelectionTransitions.TryGetRedoSelection(
            command,
            out _));
    }

    private static RingCabinetTemplateBuildResult AssertSuccessful(
        RingCabinetTemplateBuildOutcome outcome)
    {
        Assert.True(outcome.IsSuccess);
        Assert.Null(outcome.Failure);
        return Assert.IsType<RingCabinetTemplateBuildResult>(outcome.Result);
    }

    private static RingCabinetTemplateBuildResult BuildSuccessfully(
        RingCabinetTemplateBuildRequest request)
    {
        return AssertSuccessful(
            new RingCabinetTemplateBuildCoordinator().Build(request));
    }

    private static RingCabinetTemplateBuildRequest CreateRequest(
        RingCabinetLayoutRule layoutRule)
    {
        return new RingCabinetTemplateBuildRequest(
            CreateTemplate(
                layoutRule,
                NoSecondaryConfiguration.Instance,
                new BayTemplate(10, BayFunction.Incoming, new LoadSwitchConfiguration()),
                new BayTemplate(3, BayFunction.Outgoing, new LoadSwitchConfiguration()),
                new BayTemplate(8, BayFunction.Tie, new LoadSwitchConfiguration())),
            "模板创建测试柜",
            new DocumentPoint(25, 40));
    }

    private static RingCabinetTemplateBuildRequest CreateUnsupportedRequest(
        UnsupportedTemplate unsupportedTemplate)
    {
        return unsupportedTemplate switch
        {
            UnsupportedTemplate.Pt => new RingCabinetTemplateBuildRequest(
                CreateTemplate(
                    RingCabinetLayoutRule.Default,
                    NoSecondaryConfiguration.Instance,
                    new BayTemplate(1, BayFunction.PT, new LoadSwitchConfiguration()),
                    new BayTemplate(2, BayFunction.Outgoing, new LoadSwitchConfiguration()),
                    new BayTemplate(3, BayFunction.Tie, new LoadSwitchConfiguration())),
                "PT模板",
                new DocumentPoint(0, 0)),
            UnsupportedTemplate.Dtu => new RingCabinetTemplateBuildRequest(
                CreateTemplate(
                    RingCabinetLayoutRule.Default,
                    new DtuSecondaryConfiguration(),
                    new BayTemplate(1, BayFunction.Incoming, new LoadSwitchConfiguration()),
                    new BayTemplate(2, BayFunction.Outgoing, new LoadSwitchConfiguration()),
                    new BayTemplate(3, BayFunction.Tie, new LoadSwitchConfiguration())),
                "DTU模板",
                new DocumentPoint(0, 0)),
            UnsupportedTemplate.UnknownLayoutRule => CreateRequest(
                new RingCabinetLayoutRule("test:unsupported")),
            _ => throw new ArgumentOutOfRangeException(nameof(unsupportedTemplate))
        };
    }

    private static RingCabinetTemplate CreateTemplate(
        RingCabinetLayoutRule layoutRule,
        SecondaryConfiguration secondaryConfiguration,
        params BayTemplate[] bays)
    {
        return new RingCabinetTemplate(
            new TemplateId("test:desktop-command-integration"),
            "Desktop Command Integration Test",
            RingCabinetTemplateType.Conventional,
            bays,
            layoutRule,
            secondaryConfiguration);
    }

    private static ProjectRuntimeSession CreateSession()
    {
        Guid documentId = Guid.NewGuid();
        var metadata = new ProjectFileMetadata("Template Command Test");
        ProjectFileDocument fileDocument = ProjectFileDocument.CreateEmpty(
            documentId,
            metadata);
        var domain = new DrawingDocument(documentId, metadata.Title);
        var persistenceSession = new ProjectSession(
            Path.Combine(Path.GetTempPath(), $"{documentId:N}.ddproj"),
            fileDocument,
            domain,
            isDirty: false);
        return ProjectRuntimeSession.CreateEmpty(persistenceSession);
    }

    public enum UnsupportedTemplate
    {
        Pt,
        Dtu,
        UnknownLayoutRule
    }
}
