using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;
using DistributionDrawing.Desktop.Energization;
using DistributionDrawing.Desktop.SwitchOperation;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Energization;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class EnergizationPanelTests
{
    [Fact]
    public void PanelExecutesExplicitlyAndWithdrawsLegendWhenScenarioChanges()
    {
        RunOnSta(() =>
        {
            string path = Path.Combine(Path.GetTempPath(),
                $"wp-ea-panel-{Guid.NewGuid():N}.kvdrawing");
            try
            {
                ProjectRuntimeSession runtime = ProjectRuntimeSession.CreateEmpty(
                    new ProjectService().CreateProject(path, "EA panel"));
                var panel = new EnergizationPanel();
                panel.Bind(runtime);
                TextBlock status = (TextBlock)panel.FindName("AnalysisText")!;
                Button execute = (Button)panel.FindName("AnalyzeButton")!;
                Button confirm = (Button)panel.FindName("ConfirmButton")!;
                Border legend = (Border)panel.FindName("Legend")!;

                Assert.Equal("尚未执行带电分析", status.Text);
                Assert.False(confirm.IsEnabled);
                execute.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Contains("未设置电源点", status.Text);
                Assert.Equal(Visibility.Visible, legend.Visibility);
                runtime.ExecuteScenarioCommand(EnergizationScenarioCommand.Add(
                    runtime.PersistenceSession.EnergizationScenario,
                    new EnergizedSeed(Guid.NewGuid(), Guid.NewGuid(), EnergizationSide.Bus)));
                Assert.Contains("请重新执行分析", status.Text);
                Assert.Equal(Visibility.Collapsed, legend.Visibility);
                Assert.False(confirm.IsEnabled);
                execute.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Contains("分析信息不完整", status.Text);
                Assert.NotEmpty(((ItemsControl)panel.FindName("DiagnosticList")!).Items);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        });
    }

    [Fact]
    public void CabinetFallbackSupportsAddConfirmReplaceUndoAndRemove()
    {
        RunOnSta(() =>
        {
            string path = Path.Combine(Path.GetTempPath(),
                $"wp-ea-panel-{Guid.NewGuid():N}.kvdrawing");
            try
            {
                ProjectRuntimeSession runtime = ProjectRuntimeSession.CreateEmpty(
                    new ProjectService().CreateProject(path, "EA cabinet panel"));
                var addCabinet = new DeviceCommandFactory().CreateAddRingCabinet(
                    runtime.PersistenceSession.Domain, runtime.Layout,
                    new RingCabinetCreationConfiguration("EA cabinet",
                        new RingCabinetCreationTemplateFactory().Create(
                            RingCabinetTemplateType.Conventional, 4), "10kV"),
                    new DocumentPoint(20, 20));
                runtime.CommandStack.ExecuteCommand(addCabinet);
                runtime.RebuildScene();
                RingCabinet cabinet = addCabinet.Cabinet;
                var panel = new EnergizationPanel();
                panel.Bind(runtime);
                panel.SetSelection(cabinet.Id);
                ListBox candidates = (ListBox)panel.FindName("CandidateList")!;
                ListBox seeds = (ListBox)panel.FindName("SeedList")!;
                Button add = (Button)panel.FindName("AddButton")!;
                Button replace = (Button)panel.FindName("ReplaceButton")!;
                Button remove = (Button)panel.FindName("RemoveButton")!;
                Button confirm = (Button)panel.FindName("ConfirmButton")!;
                Button analyze = (Button)panel.FindName("AnalyzeButton")!;
                TextBlock status = (TextBlock)panel.FindName("AnalysisText")!;
                TextBlock confirmation = (TextBlock)panel.FindName("CompletionText")!;

                Assert.Equal(8, candidates.Items.Count);
                candidates.SelectedIndex = 0;
                Assert.True(add.IsEnabled);
                add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                EnergizationScenario scenario = runtime.PersistenceSession.EnergizationScenario;
                EnergizedSeed original = Assert.Single(scenario.Seeds);
                panel.SetSelection(original.BoundaryDeviceId);
                Assert.Equal(2, candidates.Items.Count);
                panel.SetSelection(cabinet.Id);
                Assert.True(confirm.IsEnabled);
                analyze.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Contains("电源点尚未确认完整", status.Text);
                confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.True(scenario.IsSourceSetComplete);
                Assert.Equal(EnergizationFreshness.Stale, runtime.Energization.Freshness);
                analyze.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Contains("分析完成", status.Text);
                Assert.Contains(cabinet.Intervals.SelectMany(interval => interval.SwitchDevices),
                    device => device.Id == original.BoundaryDeviceId);
                RingCabinetInterval sourceInterval = Assert.Single(cabinet.Intervals,
                    interval => interval.SwitchDevices.Any(device =>
                        device.Id == original.BoundaryDeviceId));
                runtime.SelectionManager.Select(new SelectionReference(
                    SelectionTargetKind.Device, original.BoundaryDeviceId,
                    sourceInterval.IntervalId));
                SwitchOperationResult switchResult =
                    new SwitchOperationController(() => runtime).ToggleSelected();
                Assert.True(switchResult.IsSuccess, switchResult.ErrorMessage);
                SwitchState changedState = runtime.PersistenceSession.Domain.Devices
                    .OfType<SwitchDevice>().Single(device => device.Id == original.BoundaryDeviceId)
                    .SwitchState!.Value;
                Assert.True(runtime.CommandStack.CanUndo);
                Assert.True(scenario.IsSourceSetComplete);
                Assert.Equal(EnergizationFreshness.Current, runtime.Energization.Freshness);
                Assert.True(runtime.CommandStack.Undo());
                Assert.Equal(EnergizationFreshness.Current, runtime.Energization.Freshness);
                Assert.NotEqual(changedState, runtime.PersistenceSession.Domain.Devices
                    .OfType<SwitchDevice>().Single(device => device.Id == original.BoundaryDeviceId)
                    .SwitchState);
                Assert.True(runtime.CommandStack.Redo());
                Assert.Equal(EnergizationFreshness.Current, runtime.Energization.Freshness);
                Assert.True(new SwitchOperationController(() => runtime).ToggleSelected().IsSuccess);
                Assert.Equal(EnergizationFreshness.Current, runtime.Energization.Freshness);
                Assert.True(scenario.IsSourceSetComplete);

                seeds.SelectedIndex = 0;
                candidates.SelectedIndex = 1;
                Assert.True(replace.IsEnabled);
                replace.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(original.Id, Assert.Single(scenario.Seeds).Id);
                Assert.NotEqual(original.Side, scenario.Seeds[0].Side);
                Assert.False(scenario.IsSourceSetComplete);
                Assert.Contains("确认已失效", confirmation.Text);
                Assert.Equal(EnergizationFreshness.Stale, runtime.Energization.Freshness);
                runtime.CommandStack.Undo();
                Assert.Equal(original, Assert.Single(scenario.Seeds));
                Assert.True(scenario.IsSourceSetComplete);

                panel.SetSelection(cabinet.Intervals[0].SwitchDevices.Single(device =>
                    device.SwitchKind == SwitchKind.GroundSwitch).Id);
                Assert.Empty(candidates.Items);
                Assert.False(add.IsEnabled);
                seeds.SelectedIndex = 0;
                remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Empty(scenario.Seeds);
                Assert.False(confirm.IsEnabled);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        });
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
