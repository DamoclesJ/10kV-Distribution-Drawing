using DistributionDrawing.Domain.Professional;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using DistributionDrawing.Desktop.WorkTickets;
using DistributionDrawing.Desktop.Energization;
using DistributionDrawing.Desktop;
using DistributionDrawing.Application.Devices;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Rendering.Wpf.Interaction;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class WorkRangeCanvasActivationTests
{
    [Fact]
    public void ReclickingSelectedObjectRestoresInspectorWithoutChangingSelectionOrCommittingRange()
    {
        var selections = new SelectionManager();
        SelectionReference target = new(SelectionTargetKind.Device, Guid.NewGuid());
        selections.Select(target);
        int selectionChanges = 0;
        selections.SelectionChanged += (_, _) => selectionChanges++;

        bool rangePanelVisible = true;
        bool inspectorVisible = false;
        var pending = new TicketBoundarySlotCollection();
        IsolationBoundary boundary = new(Guid.NewGuid(), BoundarySide.Line, Guid.NewGuid());
        pending.Load([boundary], item => item);
        TicketBoundarySlot[] pendingBeforeClick = pending.Slots.ToArray();
        var commandStack = new CommandStack();
        bool activated = WorkRangeCanvasActivation.ActivateOrdinaryObject(
            TicketRangePickMode.Idle,
            DrawingRightPanelMode.WorkRange,
            target,
            () =>
            {
                rangePanelVisible = false;
                inspectorVisible = true;
            });
        selections.Select(target);

        Assert.True(activated);
        Assert.False(rangePanelVisible);
        Assert.True(inspectorVisible);
        Assert.Equal(target, selections.Selected);
        Assert.Equal(0, selectionChanges);
        Assert.Equal(pendingBeforeClick, pending.Slots);
        Assert.False(commandStack.CanUndo);
    }

    [Fact]
    public void BoundaryPickerActivationDoesNotRestoreInspector()
    {
        foreach (TicketRangePickMode pickerMode in new[]
                 {
                     TicketRangePickMode.PickingBoundaryDevice,
                     TicketRangePickMode.ChoosingBoundarySide
                 })
        {
            bool rangePanelVisible = true;
            bool inspectorVisible = false;

            bool activated = WorkRangeCanvasActivation.ActivateOrdinaryObject(
                pickerMode,
                DrawingRightPanelMode.WorkRange,
                new SelectionReference(SelectionTargetKind.Device, Guid.NewGuid()),
                () =>
                {
                    rangePanelVisible = false;
                    inspectorVisible = true;
                });

            Assert.False(activated);
            Assert.True(rangePanelVisible);
            Assert.False(inspectorVisible);
        }
    }

    [Fact]
    public void EaFirstSwitchSelectionKeepsPanelAndRefreshesCandidate()
    {
        RunOnSta(() => WithEaCabinet((runtime, panel, mode, switchDevices) =>
        {
            SelectionReference target = new(SelectionTargetKind.Device,
                switchDevices[0].Id, switchDevices[0].ParentId);
            bool inspectorOpened = WorkRangeCanvasActivation.ActivateOrdinaryObject(
                TicketRangePickMode.Idle, mode, target, () => mode = DrawingRightPanelMode.Inspector);

            runtime.SelectionManager.Select(target);

            Assert.False(inspectorOpened);
            Assert.Equal(DrawingRightPanelMode.Energization, mode);
            Assert.Equal(System.Windows.Visibility.Visible, panel.Visibility);
            AssertCandidateVisible(panel, switchDevices[0].Id);
        }));
    }

    [Fact]
    public void RepeatedEaSelectionsNeverOpenInspectorAndRefreshEveryCandidate()
    {
        RunOnSta(() => WithEaCabinet((runtime, panel, mode, switchDevices) =>
        {
            foreach (SwitchDevice device in switchDevices)
            {
                SelectionReference target = new(SelectionTargetKind.Device,
                    device.Id, device.ParentId);
                bool inspectorOpened = WorkRangeCanvasActivation.ActivateOrdinaryObject(
                    TicketRangePickMode.Idle, mode, target,
                    () => mode = DrawingRightPanelMode.Inspector);

                runtime.SelectionManager.Select(target);

                Assert.False(inspectorOpened);
                Assert.Equal(DrawingRightPanelMode.Energization, mode);
                Assert.Equal(System.Windows.Visibility.Visible, panel.Visibility);
                AssertCandidateVisible(panel, device.Id);
            }
        }));
    }

    private static void WithEaCabinet(
        Action<ProjectRuntimeSession, EnergizationPanel, DrawingRightPanelMode,
            IReadOnlyList<SwitchDevice>> verify)
    {
        string path = Path.Combine(Path.GetTempPath(), $"ea-selection-{Guid.NewGuid():N}.kvdrawing");
        try
        {
            ProjectRuntimeSession runtime = ProjectRuntimeSession.CreateEmpty(
                new ProjectService().CreateProject(path, "EA selection"));
            var addCabinet = new DeviceCommandFactory().CreateAddRingCabinet(
                runtime.PersistenceSession.Domain, runtime.Layout,
                new RingCabinetCreationConfiguration("EA selection cabinet",
                    new RingCabinetCreationTemplateFactory().Create(
                        RingCabinetTemplateType.Conventional, 4), "10kV"),
                new DocumentPoint(20, 20));
            runtime.CommandStack.ExecuteCommand(addCabinet);
            runtime.RebuildScene();
            SwitchDevice[] switches = addCabinet.Cabinet.Intervals
                .SelectMany(interval => interval.SwitchDevices)
                .Where(device => device.SwitchKind != SwitchKind.GroundSwitch)
                .Take(3).ToArray();
            Assert.Equal(3, switches.Length);
            var panel = new EnergizationPanel();
            panel.Bind(runtime);
            DrawingRightPanelMode mode = DrawingRightPanelMode.Energization;
            panel.Visibility = System.Windows.Visibility.Visible;
            runtime.SelectionManager.SelectionChanged += (_, _) =>
                panel.SetSelection(runtime.SelectionManager.Selected?.ObjectId);

            verify(runtime, panel, mode, switches);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void AssertCandidateVisible(EnergizationPanel panel, Guid deviceId)
    {
        var candidates = (System.Windows.Controls.ListBox)panel.FindName("CandidateList")!;
        Assert.Contains(candidates.Items.Cast<object>(), item =>
            (Guid)item.GetType().GetProperty("DeviceId")!.GetValue(item)! == deviceId);
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception caught) { exception = caught; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception is not null) ExceptionDispatchInfo.Capture(exception).Throw();
    }
}
