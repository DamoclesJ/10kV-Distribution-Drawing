using System.IO;
using DistributionDrawing.Application.Devices;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;
using DistributionDrawing.Desktop.SwitchOperation;
using DistributionDrawing.Desktop.Workspace;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
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
        RingCabinetInterval interval = project.Interval;
        SwitchDevice loadSwitch = project.GetSwitch(SwitchKind.LoadSwitch);
        var selection = new SelectionReference(
            SelectionTargetKind.Device,
            loadSwitch.Id,
            interval.IntervalId);
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
        RingCabinetInterval interval = project.Interval;
        SwitchDevice groundSwitch = project.GetSwitch(SwitchKind.GroundSwitch);
        var controller = new SwitchOperationController(() => project.Session);
        project.Session.SelectionManager.Select(
            new SelectionReference(
                SelectionTargetKind.Device,
                groundSwitch.Id,
                interval.IntervalId));

        Assert.True(controller.ToggleSelected().IsSuccess);
        SwitchDevice loadSwitch = project.GetSwitch(SwitchKind.LoadSwitch);
        var loadSelection = new SelectionReference(
            SelectionTargetKind.Device,
            loadSwitch.Id,
            interval.IntervalId);
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
        RingCabinetInterval interval = project.Interval;
        SwitchDevice circuitBreaker = project.GetSwitch(SwitchKind.CircuitBreaker);
        project.Session.SelectionManager.Select(
            new SelectionReference(
                SelectionTargetKind.Device,
                circuitBreaker.Id,
                interval.IntervalId));
        var controller = new SwitchOperationController(() => project.Session);

        SwitchOperationResult result = controller.ToggleSelected();

        Assert.True(result.IsSuccess);
        Assert.Equal(SwitchState.Closed, circuitBreaker.SwitchState);
    }

    [Fact]
    public void SwitchCreatedByIntervalTypeChange_RemainsOperable()
    {
        using TestProject project = CreateProject(CreateLoadSwitchConfiguration());
        RingCabinet cabinet = Assert.Single(
            project.Session.PersistenceSession.Domain.Devices.OfType<RingCabinet>());
        Guid intervalId = cabinet.Intervals[0].IntervalId;
        var changeType = new ChangeIntervalTypeCommand(
            cabinet,
            project.Session.Layout,
            intervalId,
            IntervalKind.IntegratedFeederInterval,
            GroundingStructureKind.UpperLowerGrounding);
        project.Session.CommandStack.ExecuteCommand(changeType);
        project.Session.RebuildScene();
        RingCabinetInterval changedInterval = cabinet.Intervals.Single(interval =>
            interval.IntervalId == intervalId);
        SwitchDevice circuitBreaker = changedInterval.SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.CircuitBreaker);
        var selection = new SelectionReference(
            SelectionTargetKind.Device,
            circuitBreaker.Id,
            intervalId);
        project.Session.SelectionManager.Select(selection);
        var controller = new SwitchOperationController(() => project.Session);

        SwitchOperationResult result = controller.ToggleSelected();

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(SwitchState.Closed, circuitBreaker.SwitchState);
        Assert.Equal(selection, project.Session.SelectionManager.Selected);
        Assert.True(project.Session.CommandStack.CanUndo);
    }

    [Theory]
    [InlineData(SwitchKind.LoadSwitch)]
    [InlineData(SwitchKind.IsolationSwitch)]
    [InlineData(SwitchKind.CircuitBreaker)]
    [InlineData(SwitchKind.DropoutFuse)]
    public void PoleSwitch_UsesSameOperationPathAndPreservesSelection(
        SwitchKind switchKind)
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"pole-switch-operation-{Guid.NewGuid():N}.kvdrawing");
        var dialogs = new TestDialogs
        {
            NewRequest = new NewProjectRequest(filePath, "柱上开关操作测试", null)
        };
        var workspace = new ProjectWorkspaceController(dialogs, new DrawingSceneBuilder());
        Assert.True(workspace.NewProject());
        ProjectRuntimeSession session = workspace.CurrentSession!;
        PoleCreationResult creation = new PoleCreationFactory().CreateWithAttachments(
            "P-1",
            PoleType.Cement,
            null,
            [switchKind],
            includeCableTerminal: false);
        SwitchDevice switchDevice = Assert.Single(creation.Devices.OfType<SwitchDevice>());
        PoleAttachment attachment = Assert.Single(creation.Attachments);
        session.PersistenceSession.Domain.AddDevice(creation.Pole);
        session.PersistenceSession.Domain.AddPoleSwitchAttachment(
            switchDevice,
            creation.Terminals[0],
            creation.Terminals[1],
            attachment);
        session.Layout.DrawingLayout.Add(new PoleLayout(
            creation.Pole.Id,
            new DocumentPoint(20, 20)));
        session.Layout.DrawingLayout.Add(new AttachmentLayout(
            attachment.AttachmentId,
            new DocumentPoint(21, 3)));
        session.RebuildScene();
        var selection = new SelectionReference(
            SelectionTargetKind.Device,
            switchDevice.Id,
            attachment.AttachmentId);
        session.SelectionManager.Select(selection);
        var controller = new SwitchOperationController(() => session);

        SwitchOperationResult result = controller.ToggleSelected();

        Assert.True(result.IsSuccess);
        Assert.Equal(SwitchState.Closed, switchDevice.SwitchState);
        Assert.Equal(selection, session.SelectionManager.Selected);
        Assert.True(session.CommandStack.Undo());
        Assert.Equal(SwitchState.Open, switchDevice.SwitchState);
        Assert.True(session.CommandStack.Redo());
        Assert.Equal(SwitchState.Closed, switchDevice.SwitchState);
        Assert.Equal(selection, session.SelectionManager.Selected);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
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
        RingCabinet cabinet = Assert.Single(
            session.PersistenceSession.Domain.Devices.OfType<RingCabinet>());
        return new TestProject(workspace, session, cabinet.Intervals[0]);
    }

    private static RingCabinetCreationConfiguration CreateLoadSwitchConfiguration()
    {
        return new RingCabinetCreationConfiguration(
            "负荷开关柜",
            new RingCabinetCreationTemplateFactory().Create(
                RingCabinetTemplateType.Conventional,
                3));
    }

    private static RingCabinetCreationConfiguration CreateIntegratedFeederConfiguration()
    {
        return new RingCabinetCreationConfiguration(
            "融合柜",
            new RingCabinetCreationTemplateFactory().Create(
                RingCabinetTemplateType.PrimarySecondaryIntegrated,
                4));
    }

    private sealed class TestProject : IDisposable
    {
        private readonly ProjectWorkspaceController _workspace;

        public TestProject(
            ProjectWorkspaceController workspace,
            ProjectRuntimeSession session,
            RingCabinetInterval interval)
        {
            _workspace = workspace;
            Session = session;
            Interval = interval;
            FilePath = session.PersistenceSession.FilePath;
        }

        public ProjectRuntimeSession Session { get; }

        private string FilePath { get; }

        public RingCabinetInterval Interval { get; }

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
