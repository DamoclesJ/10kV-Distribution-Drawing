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
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void EaPanelSwitchOpenCloseUndoRedoReanalyzesLatestDomainAndPreservesCompleteness(
        bool complete, bool includeUnresolvedSeed)
    {
        RunOnSta(() =>
        {
            string path = Path.Combine(Path.GetTempPath(), $"ea-realtime-{Guid.NewGuid():N}.kvdrawing");
            try
            {
                var runtime = ProjectRuntimeSession.CreateEmpty(new ProjectService().CreateProject(path, "EA realtime"));
                var addCabinet = new DeviceCommandFactory().CreateAddRingCabinet(runtime.PersistenceSession.Domain, runtime.Layout,
                    new RingCabinetCreationConfiguration("Realtime cabinet", new RingCabinetCreationTemplateFactory().Create(
                        RingCabinetTemplateType.Conventional, 4), "10kV"), new DocumentPoint(20, 20));
                runtime.CommandStack.ExecuteCommand(addCabinet);
                runtime.RebuildScene();
                var interval = addCabinet.Cabinet.Intervals[0];
                var load = interval.SwitchDevices.Single(device => device.SwitchKind == SwitchKind.LoadSwitch);
                var selected = new SelectionReference(SelectionTargetKind.Device, load.Id, interval.IntervalId);
                runtime.SelectionManager.Select(selected);
                var scenario = runtime.PersistenceSession.EnergizationScenario;
                runtime.ExecuteScenarioCommand(EnergizationScenarioCommand.Add(scenario, new EnergizedSeed(Guid.NewGuid(), load.Id, EnergizationSide.Bus)));
                if (complete) runtime.ExecuteScenarioCommand(EnergizationScenarioCommand.SetComplete(scenario, true));
                if (includeUnresolvedSeed)
                    runtime.ExecuteScenarioCommand(EnergizationScenarioCommand.Add(scenario,
                        new EnergizedSeed(Guid.NewGuid(), Guid.NewGuid(), EnergizationSide.Bus)));
                runtime.ExecuteEnergizationAnalysis();
                EnergizationValidity validity = complete ? EnergizationValidity.Complete :
                    includeUnresolvedSeed ? EnergizationValidity.Incomplete : EnergizationValidity.ForwardOnly;
                Assert.Equal(validity, runtime.Energization.CurrentResult!.Validity);
                var panel = new EnergizationPanel();
                panel.Bind(runtime);
                panel.SetSelection(load.Id);
                var open = (Button)panel.FindName("OpenSwitchButton")!;
                var close = (Button)panel.FindName("CloseSwitchButton")!;
                int applied = 0;
                panel.SwitchOperationApplied += (_, _) => applied++;
                runtime.Energization.SetOverlayRequested(false);
                var controller = new SwitchOperationController(() => runtime);
                EnergizationResult? previous = runtime.Energization.CurrentResult;
                void AssertLatest(bool closed)
                {
                    Assert.Equal(EnergizationFreshness.Current, runtime.Energization.Freshness);
                    Assert.NotSame(previous, runtime.Energization.CurrentResult);
                    previous = runtime.Energization.CurrentResult;
                    Assert.NotNull(previous);
                    Assert.Equal(validity, previous.Validity);
                    Assert.False(runtime.Energization.CanShowOverlay);
                    Assert.Equal(EnergizationState.Energized, previous.Terminals[load.FirstTerminalId].State);
                    Assert.Equal(closed ? EnergizationState.Energized : complete ? EnergizationState.Deenergized : EnergizationState.Unknown,
                        previous.Terminals[load.SecondTerminalId].State);
                    Assert.Equal(complete, scenario.IsSourceSetComplete);
                    Assert.Equal(selected, runtime.SelectionManager.Selected);
                }
                Assert.True(close.IsEnabled);
                close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                AssertLatest(true);
                Assert.True(open.IsEnabled);
                Assert.False(close.IsEnabled);
                open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                AssertLatest(false);
                Assert.Equal(2, applied);
                Assert.True(runtime.CommandStack.Undo());
                AssertLatest(true);
                Assert.True(runtime.CommandStack.Redo());
                AssertLatest(false);
                Assert.True(controller.SetSelectedState(SwitchState.Closed).IsSuccess);
                AssertLatest(true);
                // A no-op command or save checkpoint must not publish another result.
                Assert.True(controller.SetSelectedState(SwitchState.Closed).IsSuccess);
                runtime.CommandStack.MarkSaved();
                Assert.Same(previous, runtime.Energization.CurrentResult);
                var ground = interval.SwitchDevices.Single(device => device.SwitchKind == SwitchKind.GroundSwitch);
                runtime.SelectionManager.Select(new(SelectionTargetKind.Device, ground.Id, interval.IntervalId));
                Assert.False(controller.SetSelectedState(SwitchState.Closed).IsSuccess);
                Assert.Same(previous, runtime.Energization.CurrentResult);
                runtime.SelectionManager.Select(selected);
                // Seed-side edits revoke completeness and require explicit analysis; later switches cannot revive stale facts.
                var seed = scenario.Seeds[0];
                runtime.ExecuteScenarioCommand(EnergizationScenarioCommand.Replace(scenario,
                    new EnergizedSeed(seed.Id, seed.BoundaryDeviceId, EnergizationSide.Line)));
                Assert.False(scenario.IsSourceSetComplete);
                Assert.Equal(EnergizationFreshness.Stale, runtime.Energization.Freshness);
                Assert.True(controller.SetSelectedState(SwitchState.Open).IsSuccess);
                Assert.Equal(EnergizationFreshness.Stale, runtime.Energization.Freshness);
                Assert.Null(runtime.Energization.CurrentResult);
                runtime.ExecuteEnergizationAnalysis();
                Assert.True(runtime.Energization.CanShowOverlay);
                // A layout/structural command still invalidates; it is not marked as a switch operation.
                runtime.CommandStack.ExecuteCommand(new DeviceCommandFactory().CreateAddRingCabinet(runtime.PersistenceSession.Domain, runtime.Layout,
                    new RingCabinetCreationConfiguration("Other cabinet", new RingCabinetCreationTemplateFactory().Create(
                        RingCabinetTemplateType.Conventional, 4), "10kV"), new DocumentPoint(200, 20)));
                Assert.Equal(EnergizationFreshness.Stale, runtime.Energization.Freshness);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        });
    }

    [Fact]
    public void SwitchChangeWithoutSeedsDoesNotCreateAnAnalysisResult()
    {
        RunOnSta(() =>
        {
            string path = Path.Combine(Path.GetTempPath(), $"ea-no-seed-{Guid.NewGuid():N}.kvdrawing");
            try
            {
                var runtime = ProjectRuntimeSession.CreateEmpty(new ProjectService().CreateProject(path, "EA no seed"));
                var addCabinet = new DeviceCommandFactory().CreateAddRingCabinet(runtime.PersistenceSession.Domain, runtime.Layout,
                    new RingCabinetCreationConfiguration("No-seed cabinet", new RingCabinetCreationTemplateFactory().Create(
                        RingCabinetTemplateType.Conventional, 4), "10kV"), new DocumentPoint(20, 20));
                runtime.CommandStack.ExecuteCommand(addCabinet);
                runtime.RebuildScene();
                var interval = addCabinet.Cabinet.Intervals[0];
                var load = interval.SwitchDevices.Single(device => device.SwitchKind == SwitchKind.LoadSwitch);
                runtime.SelectionManager.Select(new SelectionReference(SelectionTargetKind.Device, load.Id, interval.IntervalId));
                runtime.ExecuteEnergizationAnalysis();
                Assert.Equal(EnergizationValidity.NoSeeds, runtime.Energization.CurrentResult!.Validity);
                Assert.Equal(EnergizationFreshness.Current, runtime.Energization.Freshness);

                SwitchOperationResult operation = new SwitchOperationController(() => runtime).SetSelectedState(SwitchState.Closed);

                Assert.True(operation.IsSuccess);
                Assert.Equal(EnergizationFreshness.Stale, runtime.Energization.Freshness);
                Assert.Null(runtime.Energization.CurrentResult);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        });
    }

    [Fact]
    public void PanelExecutesExplicitlyAndWithdrawsStaleStatusWhenScenarioChanges()
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
                Assert.Null(panel.FindName("Legend"));

                Assert.Equal("尚未执行带电分析", status.Text);
                Assert.False(confirm.IsEnabled);
                execute.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Contains("未设置电源点", status.Text);
                runtime.ExecuteScenarioCommand(EnergizationScenarioCommand.Add(
                    runtime.PersistenceSession.EnergizationScenario,
                    new EnergizedSeed(Guid.NewGuid(), Guid.NewGuid(), EnergizationSide.Bus)));
                Assert.Contains("请重新执行带电分析", status.Text);
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
                ComboBox sides = (ComboBox)panel.FindName("SourceSideComboBox")!;
                ListBox seeds = (ListBox)panel.FindName("SeedList")!;
                Button add = (Button)panel.FindName("AddButton")!;
                Button replace = (Button)panel.FindName("ReplaceButton")!;
                Button remove = (Button)panel.FindName("RemoveButton")!;
                Button confirm = (Button)panel.FindName("ConfirmButton")!;
                Button analyze = (Button)panel.FindName("AnalyzeButton")!;
                TextBlock status = (TextBlock)panel.FindName("AnalysisText")!;
                TextBlock confirmation = (TextBlock)panel.FindName("CompletionText")!;

                Assert.Equal(4, candidates.Items.Count);
                candidates.SelectedIndex = 0;
                Assert.Null(sides.SelectedItem);
                Assert.False(add.IsEnabled);
                Assert.Equal(new[] { "母线侧", "线路侧" }, sides.Items.Cast<object>()
                    .Select(item => item.GetType().GetProperty("DisplayText")!.GetValue(item)!.ToString()));
                sides.SelectedIndex = 0;
                Assert.True(add.IsEnabled);
                add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                EnergizationScenario scenario = runtime.PersistenceSession.EnergizationScenario;
                EnergizedSeed original = Assert.Single(scenario.Seeds);
                panel.SetSelection(original.BoundaryDeviceId);
                Assert.Single(candidates.Items);
                Assert.Equal(2, sides.Items.Count);
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
                Assert.True(runtime.CommandStack.CanUndo);
                Assert.True(scenario.IsSourceSetComplete);
                Assert.Equal(EnergizationFreshness.Current, runtime.Energization.Freshness);

                seeds.SelectedIndex = 0;
                panel.SetSelection(original.BoundaryDeviceId);
                candidates.SelectedIndex = 0;
                sides.SelectedIndex = original.Side == EnergizationSide.Bus ? 1 : 0;
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

    [Fact]
    public void ExitKeepsScenarioAndResultWhileStartAnalysisReenablesDisplayAndRequestsExit()
    {
        RunOnSta(() =>
        {
            string path = Path.Combine(Path.GetTempPath(),
                $"wp-ea-actions-{Guid.NewGuid():N}.kvdrawing");
            try
            {
                ProjectRuntimeSession runtime = ProjectRuntimeSession.CreateEmpty(
                    new ProjectService().CreateProject(path, "EA actions"));
                var addCabinet = new DeviceCommandFactory().CreateAddRingCabinet(
                    runtime.PersistenceSession.Domain, runtime.Layout,
                    new RingCabinetCreationConfiguration("EA actions cabinet",
                        new RingCabinetCreationTemplateFactory().Create(
                            RingCabinetTemplateType.Conventional, 4), "10kV"),
                    new DocumentPoint(20, 20));
                runtime.CommandStack.ExecuteCommand(addCabinet);
                runtime.RebuildScene();
                SwitchDevice source = addCabinet.Cabinet.Intervals[0].SwitchDevices
                    .Single(device => device.SwitchKind == SwitchKind.LoadSwitch);
                runtime.ExecuteScenarioCommand(EnergizationScenarioCommand.Add(
                    runtime.PersistenceSession.EnergizationScenario,
                    new EnergizedSeed(Guid.NewGuid(), source.Id, EnergizationSide.Bus)));
                runtime.ExecuteEnergizationAnalysis();
                EnergizationScenario scenario = runtime.PersistenceSession.EnergizationScenario;
                EnergizationResult originalResult = runtime.Energization.CurrentResult!;
                var panel = new EnergizationPanel();
                panel.Bind(runtime);
                int exitRequests = 0;
                panel.ExitRequested += (_, _) => exitRequests++;
                Button exit = (Button)panel.FindName("ExitButton")!;
                Button start = (Button)panel.FindName("AnalyzeButton")!;

                exit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(1, exitRequests);
                Assert.Same(scenario, runtime.PersistenceSession.EnergizationScenario);
                Assert.Same(originalResult, runtime.Energization.CurrentResult);
                Assert.Equal(EnergizationFreshness.Current, runtime.Energization.Freshness);

                runtime.Energization.SetOverlayRequested(false);
                Assert.False(runtime.Energization.CanShowOverlay);
                start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(2, exitRequests);
                Assert.Same(scenario, runtime.PersistenceSession.EnergizationScenario);
                Assert.Equal(EnergizationFreshness.Current, runtime.Energization.Freshness);
                Assert.NotSame(originalResult, runtime.Energization.CurrentResult);
                Assert.True(runtime.Energization.CanShowOverlay);
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
