using System.IO;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Desktop;
using DistributionDrawing.Desktop.RingCabinetTemplateCreation;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.PropertyInspector;
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

    [Fact]
    public void CreateUndoRedo_WithNullBeforeSelection_KeepsSceneResolverAndInspectorConsistent()
    {
        ProjectRuntimeSession session = CreateSession();
        var controller = new RingCabinetTemplateCreationController(() => session);

        RingCabinetTemplateBuildResult result = AssertSuccessful(
            controller.Create(CreateRequest(
                RingCabinetLayoutRule.Default,
                "空选择模板柜",
                new DocumentPoint(25, 40))));
        AddRingCabinetCommand command = Assert.IsType<AddRingCabinetCommand>(
            Assert.Single(session.CommandStack.History));
        var afterSelection = new SelectionReference(
            SelectionTargetKind.RingCabinet,
            result.Cabinet.Id);
        StableIds ids = CaptureStableIds(result);

        Assert.True(session.SelectionTransitions.TryGetUndoSelection(
            command,
            out SelectionReference? undoSelection));
        Assert.Null(undoSelection);
        Assert.True(session.SelectionTransitions.TryGetRedoSelection(
            command,
            out SelectionReference? redoSelection));
        Assert.Equal(afterSelection, redoSelection);
        AssertSelectionIsResolvableAndProjected(session, afterSelection, "空选择模板柜");
        Assert.NotNull(session.Scene.HitTestIndex.Find(afterSelection));
        Assert.True(session.CommandStack.IsDirty);

        Assert.True(session.CommandStack.Undo());
        session.RebuildScene();
        session.SelectionManager.Clear();

        Assert.Null(session.SelectionManager.Selected);
        Assert.Null(session.SelectionResolver.Resolve(afterSelection));
        Assert.Null(session.Scene.HitTestIndex.Find(afterSelection));
        PropertyInspectorSnapshot emptySnapshot = session.PropertyProjector.Project(
            session.SelectionResolver.Resolve(session.SelectionManager.Selected));
        Assert.Null(emptySnapshot.Selection);
        Assert.Equal("未选择对象", emptySnapshot.ObjectType);
        Assert.False(session.CommandStack.IsDirty);

        Assert.True(session.CommandStack.Redo());
        session.RebuildScene();
        session.SelectionManager.Select(redoSelection);

        Assert.Equal(afterSelection, session.SelectionManager.Selected);
        AssertSelectionIsResolvableAndProjected(session, afterSelection, "空选择模板柜");
        Assert.NotNull(session.Scene.HitTestIndex.Find(afterSelection));
        AssertStableIds(ids, result);
        Assert.True(session.CommandStack.IsDirty);
    }

    [Fact]
    public void CreateUndoRedo_WithResolvableBeforeSelection_RestoresBothSelections()
    {
        ProjectRuntimeSession session = CreateSession();
        var controller = new RingCabinetTemplateCreationController(() => session);
        RingCabinetTemplateBuildResult existing = AssertSuccessful(
            controller.Create(CreateRequest(
                RingCabinetLayoutRule.Default,
                "已有环网柜",
                new DocumentPoint(0, 0))));
        var beforeSelection = new SelectionReference(
            SelectionTargetKind.RingCabinet,
            existing.Cabinet.Id);
        Assert.Equal(beforeSelection, session.SelectionManager.Selected);
        AssertSelectionIsResolvableAndProjected(session, beforeSelection, "已有环网柜");

        RingCabinetTemplateBuildResult created = AssertSuccessful(
            controller.Create(CreateRequest(
                RingCabinetLayoutRule.Default,
                "新增模板柜",
                new DocumentPoint(150, 0))));
        AddRingCabinetCommand command = Assert.IsType<AddRingCabinetCommand>(
            session.CommandStack.History[^1]);
        var afterSelection = new SelectionReference(
            SelectionTargetKind.RingCabinet,
            created.Cabinet.Id);
        StableIds ids = CaptureStableIds(created);

        Assert.True(session.SelectionTransitions.TryGetUndoSelection(
            command,
            out SelectionReference? undoSelection));
        Assert.Equal(beforeSelection, undoSelection);
        Assert.True(session.SelectionTransitions.TryGetRedoSelection(
            command,
            out SelectionReference? redoSelection));
        Assert.Equal(afterSelection, redoSelection);
        Assert.Equal(afterSelection, session.SelectionManager.Selected);
        AssertSelectionIsResolvableAndProjected(session, afterSelection, "新增模板柜");
        Assert.NotNull(session.Scene.HitTestIndex.Find(beforeSelection));
        Assert.NotNull(session.Scene.HitTestIndex.Find(afterSelection));

        Assert.True(session.CommandStack.Undo());
        session.RebuildScene();
        Assert.NotNull(session.SelectionResolver.Resolve(undoSelection));
        session.SelectionManager.Select(undoSelection);

        Assert.Equal(beforeSelection, session.SelectionManager.Selected);
        AssertSelectionIsResolvableAndProjected(session, beforeSelection, "已有环网柜");
        Assert.NotNull(session.Scene.HitTestIndex.Find(beforeSelection));
        Assert.Null(session.Scene.HitTestIndex.Find(afterSelection));
        Assert.Null(session.SelectionResolver.Resolve(afterSelection));

        Assert.True(session.CommandStack.Redo());
        session.RebuildScene();
        Assert.NotNull(session.SelectionResolver.Resolve(redoSelection));
        session.SelectionManager.Select(redoSelection);

        Assert.Equal(afterSelection, session.SelectionManager.Selected);
        AssertSelectionIsResolvableAndProjected(session, afterSelection, "新增模板柜");
        Assert.NotNull(session.Scene.HitTestIndex.Find(beforeSelection));
        Assert.NotNull(session.Scene.HitTestIndex.Find(afterSelection));
        AssertStableIds(ids, created);
        Assert.Equal(2, session.PersistenceSession.Domain.Devices
            .OfType<RingCabinet>()
            .Count());
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
        return CreateRequest(
            layoutRule,
            "模板创建测试柜",
            new DocumentPoint(25, 40));
    }

    private static RingCabinetTemplateBuildRequest CreateRequest(
        RingCabinetLayoutRule layoutRule,
        string displayName,
        DocumentPoint position)
    {
        return new RingCabinetTemplateBuildRequest(
            CreateTemplate(
                layoutRule,
                NoSecondaryConfiguration.Instance,
                new BayTemplate(10, new LoadSwitchConfiguration()),
                new BayTemplate(3, new LoadSwitchConfiguration()),
                new BayTemplate(8, new LoadSwitchConfiguration())),
            displayName,
            position);
    }

    private static RingCabinetTemplateBuildRequest CreateUnsupportedRequest(
        UnsupportedTemplate unsupportedTemplate)
    {
        return unsupportedTemplate switch
        {
            UnsupportedTemplate.Dtu => new RingCabinetTemplateBuildRequest(
                CreateTemplate(
                    RingCabinetLayoutRule.Default,
                    new DtuSecondaryConfiguration(),
                    new BayTemplate(1, new LoadSwitchConfiguration()),
                    new BayTemplate(2, new LoadSwitchConfiguration()),
                    new BayTemplate(3, new LoadSwitchConfiguration())),
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

    private static void AssertSelectionIsResolvableAndProjected(
        ProjectRuntimeSession session,
        SelectionReference selection,
        string expectedTitle)
    {
        ResolvedSelection resolved = Assert.IsType<ResolvedSelection>(
            session.SelectionResolver.Resolve(selection));
        Assert.Equal(selection, resolved.Reference);
        Assert.NotNull(resolved.RingCabinet);
        Assert.NotNull(resolved.RingCabinetLayout);
        PropertyInspectorSnapshot snapshot = session.PropertyProjector.Project(resolved);
        Assert.Equal(selection, snapshot.Selection);
        Assert.Equal("环网柜", snapshot.ObjectType);
        Assert.Equal(expectedTitle, snapshot.ObjectTitle);
    }

    private static StableIds CaptureStableIds(
        RingCabinetTemplateBuildResult result)
    {
        return new StableIds(
            result.Cabinet.Id,
            result.Cabinet.Intervals.Select(x => x.IntervalId).ToArray(),
            result.Cabinet.Intervals
                .SelectMany(x => x.SwitchDevices)
                .Select(x => x.Id)
                .ToArray(),
            result.Cabinet.Terminals.Select(x => x.Id).ToArray(),
            result.Cabinet.ElectricalNodes.Select(x => x.Id).ToArray(),
            result.Cabinet.Intervals
                .Select(x => x.SwitchAssembly.AssemblyId)
                .ToArray());
    }

    private static void AssertStableIds(
        StableIds expected,
        RingCabinetTemplateBuildResult result)
    {
        Assert.Equal(expected.CabinetId, result.Cabinet.Id);
        Assert.Equal(
            expected.IntervalIds,
            result.Cabinet.Intervals.Select(x => x.IntervalId));
        Assert.Equal(
            expected.SwitchIds,
            result.Cabinet.Intervals
                .SelectMany(x => x.SwitchDevices)
                .Select(x => x.Id));
        Assert.Equal(expected.TerminalIds, result.Cabinet.Terminals.Select(x => x.Id));
        Assert.Equal(
            expected.ElectricalNodeIds,
            result.Cabinet.ElectricalNodes.Select(x => x.Id));
        Assert.Equal(
            expected.SwitchAssemblyIds,
            result.Cabinet.Intervals.Select(x => x.SwitchAssembly.AssemblyId));
        Assert.Equal(expected.CabinetId, result.Layout.CabinetId);
    }

    private sealed record StableIds(
        Guid CabinetId,
        Guid[] IntervalIds,
        Guid[] SwitchIds,
        Guid[] TerminalIds,
        Guid[] ElectricalNodeIds,
        Guid[] SwitchAssemblyIds);

    public enum UnsupportedTemplate
    {
        Dtu,
        UnknownLayoutRule
    }
}
