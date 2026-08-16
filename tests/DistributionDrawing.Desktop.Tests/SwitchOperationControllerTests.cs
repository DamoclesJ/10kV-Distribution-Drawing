using System.IO;
using DistributionDrawing.Desktop.SwitchOperation;
using DistributionDrawing.Desktop.Workspace;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class SwitchOperationControllerTests
{
    [Fact]
    public void ToggleSelected_ChangesStateAndPreservesSelection()
    {
        using TestProject project = CreateProject(CreateLoadSwitchConfiguration());
        SwitchDevice loadSwitch = project.GetSwitch(SwitchKind.LoadSwitch);
        var selection = new SelectionReference(
            SelectionTargetKind.Device,
            loadSwitch.Id,
            project.Interval.IntervalId);
        project.Session.SelectionManager.Select(selection);
        var controller = new SwitchOperationController(() => project.Session);
        int sceneChanges = 0;
        controller.SceneChanged += (_, _) => sceneChanges++;

        SwitchOperationResult result = controller.ToggleSelected();

        Assert.True(result.IsSuccess);
        Assert.Equal(SwitchState.Closed, loadSwitch.SwitchState);
        Assert.Equal(selection, project.Session.SelectionManager.Selected);
        Assert.Equal(1, sceneChanges);
        Assert.True(project.Session.CommandStack.CanUndo);

        Assert.True(project.Session.CommandStack.Undo());
        Assert.Equal(SwitchState.Open, loadSwitch.SwitchState);
        Assert.Equal(selection, project.Session.SelectionManager.Selected);

        Assert.True(project.Session.CommandStack.Redo());
        Assert.Equal(SwitchState.Closed, loadSwitch.SwitchState);
        Assert.Equal(selection, project.Session.SelectionManager.Selected);
    }

    [Fact]
    public void InterlockFailure_DoesNotChangeStateOrHistoryAndReturnsChineseMessage()
    {
        using TestProject project = CreateProject(CreateLoadSwitchConfiguration());
        SwitchDevice groundSwitch = project.GetSwitch(SwitchKind.GroundSwitch);
        var controller = new SwitchOperationController(() => project.Session);
        project.Session.SelectionManager.Select(
            new SelectionReference(
                SelectionTargetKind.Device,
                groundSwitch.Id,
                project.Interval.IntervalId));

        Assert.True(controller.ToggleSelected().IsSuccess);
        SwitchDevice loadSwitch = project.GetSwitch(SwitchKind.LoadSwitch);
        var loadSelection = new SelectionReference(
            SelectionTargetKind.Device,
            loadSwitch.Id,
            project.Interval.IntervalId);
        project.Session.SelectionManager.Select(loadSelection);
        int historyCount = project.Session.CommandStack.History.Count;

        SwitchOperationResult result = controller.ToggleSelected();

        Assert.False(result.IsSuccess);
        Assert.Contains("不能同时合闸", result.ErrorMessage);
        Assert.Equal(SwitchState.Open, loadSwitch.SwitchState);
        Assert.Equal(SwitchState.Closed, groundSwitch.SwitchState);
        Assert.Equal(loadSelection, project.Session.SelectionManager.Selected);
        Assert.Equal(historyCount, project.Session.CommandStack.History.Count);
    }

    [Fact]
    public void IntegratedFeederSwitch_UsesSameOperationPath()
    {
        using TestProject project = CreateProject(CreateIntegratedFeederConfiguration());
        SwitchDevice circuitBreaker = project.GetSwitch(SwitchKind.CircuitBreaker);
        project.Session.SelectionManager.Select(
            new SelectionReference(
                SelectionTargetKind.Device,
                circuitBreaker.Id,
                project.Interval.IntervalId));
        var controller = new SwitchOperationController(() => project.Session);

        SwitchOperationResult result = controller.ToggleSelected();

        Assert.True(result.IsSuccess);
        Assert.Equal(SwitchState.Closed, circuitBreaker.SwitchState);
    }

    private static TestProject CreateProject(RingCabinetCreationConfiguration configuration)
    {
        var dialogs = new TestDialogs
        {
            NewRequest = new NewProjectRequest(
                Path.Combine(Path.GetTempPath(), $"switch-operation-{Guid.NewGuid():N}.kvdrawing"),
                "开关操作测试",
                null)
        };
        var workspace = new ProjectWorkspaceController(dialogs, new DrawingSceneBuilder());
        Assert.True(workspace.NewProject());
        ProjectRuntimeSession session = workspace.CurrentSession!;
        var command = new DeviceCommandFactory().CreateAddRingCabinet(
            session.PersistenceSession.Domain,
            session.Layout,
            configuration,
            new DocumentPoint(40, 40));
        session.CommandStack.ExecuteCommand(command);
        session.RebuildScene();
        return new TestProject(workspace, session);
    }

    private static RingCabinetCreationConfiguration CreateLoadSwitchConfiguration()
    {
        return new RingCabinetCreationConfiguration(
            "负荷开关柜",
            Enumerable.Range(1, 3).Select(index =>
                new RingCabinetIntervalCreationConfiguration(
                    index,
                    $"间隔 {index}",
                    IntervalKind.LoadSwitchInterval,
                    null)));
    }

    private static RingCabinetCreationConfiguration CreateIntegratedFeederConfiguration()
    {
        return new RingCabinetCreationConfiguration(
            "融合柜",
            Enumerable.Range(1, 4).Select(index =>
                new RingCabinetIntervalCreationConfiguration(
                    index,
                    $"间隔 {index}",
                    IntervalKind.IntegratedFeederInterval,
                    GroundingStructureKind.UpperIsolationGrounding)));
    }

    private sealed class TestProject : IDisposable
    {
        private readonly ProjectWorkspaceController _workspace;

        public TestProject(ProjectWorkspaceController workspace, ProjectRuntimeSession session)
        {
            _workspace = workspace;
            Session = session;
            FilePath = session.PersistenceSession.FilePath;
        }

        public ProjectRuntimeSession Session { get; }

        private string FilePath { get; }

        public RingCabinetInterval Interval =>
            Session.PersistenceSession.Domain.Devices
                .OfType<RingCabinet>()
                .Single()
                .Intervals
                .Single();

        public SwitchDevice GetSwitch(SwitchKind kind) =>
            Interval.SwitchDevices.Single(device => device.SwitchKind == kind);

        public void Dispose()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
    }

    private sealed class TestDialogs : IProjectWorkspaceDialogs
    {
        public NewProjectRequest? NewRequest { get; init; }

        public NewProjectRequest? RequestNewProject() => NewRequest;

        public string? ChooseOpenProject() => null;

        public string? ChooseSaveAs(string? currentFilePath) => currentFilePath;

        public DirtyDecision ConfirmDirty(string operation) => DirtyDecision.Cancel;

        public void ShowError(string title, string message)
        {
        }
    }
}
