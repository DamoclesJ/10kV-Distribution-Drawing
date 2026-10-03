using System.IO;
using DistributionDrawing.Application.Energization;
using DistributionDrawing.Desktop.DrawingTools;
using DistributionDrawing.Desktop.GroundingSafety;
using DistributionDrawing.Desktop.SwitchOperation;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Energization;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Connections;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Interaction.Professional;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class GroundingEnergizationLifecycleTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"gs-af-05-{Guid.NewGuid():N}.kvdrawing");
    private readonly ProfessionalCommandFactory _factory = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GroundingPointAddRemoveAndHistoryPreserveSameValidResult(bool onGap)
    {
        Fixture fixture = Create();
        ProjectRuntimeSession runtime = fixture.Runtime;
        EnergizationResult result = runtime.Energization.CurrentResult!;
        GroundingTarget target = onGap ? AddGap(fixture) : GroundingTarget.ForTerminal(fixture.GroundingTerminalId);
        var add = (AddGroundingPointCommand)_factory.CreateAddGroundingPoint(
            runtime.PersistenceSession.Domain, target,
            beforeExecute: snapshot => GroundingPointCommandPreflight.EnsureAllowed(runtime, snapshot));
        runtime.CommandStack.ExecuteCommand(add);
        AssertPreserved(runtime, result);
        Assert.True(runtime.CommandStack.Undo());
        Assert.Empty(runtime.PersistenceSession.Domain.GroundingPoints);
        AssertPreserved(runtime, result);
        Assert.True(runtime.CommandStack.Redo());
        Assert.Single(runtime.PersistenceSession.Domain.GroundingPoints);
        AssertPreserved(runtime, result);

        // Exercise the actual selection-delete wrapper as well as its restore guard.
        runtime.CommandStack.ExecuteCommand(new SelectionDeletePlanner().Create(runtime,
            SelectionSet.Create([new SelectionReference(SelectionTargetKind.GroundingPoint, add.After.GroundingPointId)])));
        Assert.Empty(runtime.PersistenceSession.Domain.GroundingPoints);
        AssertPreserved(runtime, result);
        Assert.True(runtime.CommandStack.Undo());
        Assert.Single(runtime.PersistenceSession.Domain.GroundingPoints);
        AssertPreserved(runtime, result);
        Assert.True(runtime.CommandStack.Redo());
        Assert.Empty(runtime.PersistenceSession.Domain.GroundingPoints);
        AssertPreserved(runtime, result);
    }

    [Fact]
    public void GapAddRemoveAndHistoryPreserveSameValidResult()
    {
        Fixture fixture = Create();
        ProjectRuntimeSession runtime = fixture.Runtime;
        EnergizationResult result = runtime.Energization.CurrentResult!;
        GroundingTarget target = AddGap(fixture);
        AssertPreserved(runtime, result);
        Assert.True(runtime.CommandStack.Undo());
        Assert.Empty(runtime.PersistenceSession.Domain.GroundingAccessPoints);
        AssertPreserved(runtime, result);
        Assert.True(runtime.CommandStack.Redo());
        AssertPreserved(runtime, result);
        runtime.CommandStack.ExecuteCommand(new SelectionDeletePlanner().Create(runtime,
            SelectionSet.Create([new SelectionReference(SelectionTargetKind.GroundingAccessPoint, target.TargetId)])));
        AssertPreserved(runtime, result);
        Assert.True(runtime.CommandStack.Undo());
        Assert.Single(runtime.PersistenceSession.Domain.GroundingAccessPoints);
        AssertPreserved(runtime, result);
        Assert.True(runtime.CommandStack.Redo());
        Assert.Empty(runtime.PersistenceSession.Domain.GroundingAccessPoints);
        AssertPreserved(runtime, result);
    }

    [Fact]
    public void CombinedGapAndGroundingPointCreationAndHistoryPreserveResult()
    {
        Fixture fixture = Create();
        ProjectRuntimeSession runtime = fixture.Runtime;
        EnergizationResult result = runtime.Energization.CurrentResult!;
        runtime.CommandStack.ExecuteCommand(_factory.CreateAddGroundingAccessPointWithGroundingPoint(
            runtime.PersistenceSession.Domain, fixture.RightConnectionId, fixture.CenterPoleId,
            fixture.EndPoleId, GroundingAccessLineSide.LargerNumberSide,
            beforeGroundingPointExecute: (snapshot, gap) => GroundingPointCommandPreflight.EnsureAllowed(runtime, snapshot, gap)));
        Assert.Single(runtime.PersistenceSession.Domain.GroundingPoints);
        AssertPreserved(runtime, result);
        Assert.True(runtime.CommandStack.Undo());
        Assert.Empty(runtime.PersistenceSession.Domain.GroundingAccessPoints);
        AssertPreserved(runtime, result);
        Assert.True(runtime.CommandStack.Redo());
        Assert.Single(runtime.PersistenceSession.Domain.GroundingPoints);
        AssertPreserved(runtime, result);
    }

    [Fact]
    public void NonElectricalGroundingPropertiesAndLayoutPreserveResultThroughHistory()
    {
        Fixture fixture = Create();
        ProjectRuntimeSession runtime = fixture.Runtime;
        GroundingTarget target = AddGap(fixture);
        runtime.CommandStack.ExecuteCommand(_factory.CreateAddGroundingPoint(runtime.PersistenceSession.Domain, target));
        EnergizationResult result = runtime.Energization.CurrentResult!;
        GroundingPointCommandSnapshot before = GroundingPointCommandSnapshot.From(Assert.Single(runtime.PersistenceSession.Domain.GroundingPoints));
        runtime.CommandStack.ExecuteCommand(new ChangeGroundingPointCommand(runtime.PersistenceSession.Domain,
            before, before with { Number = "L99", Note = "工作地线", Location = "现场接地点" }));
        AssertPreserved(runtime, result);
        Assert.True(runtime.CommandStack.Undo());
        AssertPreserved(runtime, result);
        Assert.True(runtime.CommandStack.Redo());
        AssertPreserved(runtime, result);
        runtime.CommandStack.ExecuteCommand(new MoveGroundingPointLayoutCommand(runtime.PersistenceSession.Domain,
            runtime.Layout, before.GroundingPointId, null,
            new GroundingPointLayout(before.GroundingPointId, new DocumentPoint(10, 20))));
        AssertPreserved(runtime, result);
        Assert.True(runtime.CommandStack.Undo());
        AssertPreserved(runtime, result);
        Assert.True(runtime.CommandStack.Redo());
        AssertPreserved(runtime, result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreservedEaStillRejectsUpstreamEnergizationAndAllowsRelease(bool onGap)
    {
        Fixture fixture = Create();
        ProjectRuntimeSession runtime = fixture.Runtime;
        EnergizationResult result = runtime.Energization.CurrentResult!;
        GroundingTarget target = onGap ? AddGap(fixture) : GroundingTarget.ForTerminal(fixture.GroundingTerminalId);
        var add = (AddGroundingPointCommand)_factory.CreateAddGroundingPoint(runtime.PersistenceSession.Domain, target,
            beforeExecute: snapshot => GroundingPointCommandPreflight.EnsureAllowed(runtime, snapshot));
        runtime.CommandStack.ExecuteCommand(add);
        AssertPreserved(runtime, result);
        runtime.RebuildScene();
        runtime.SelectionManager.Select(new SelectionReference(SelectionTargetKind.Device, fixture.Switch.Id));
        var controller = new SwitchOperationController(() => runtime);
        long stateId = runtime.CommandStack.CurrentStateId;
        SwitchOperationResult rejected = controller.SetSelectedState(SwitchState.Closed);
        Assert.False(rejected.IsSuccess);
        Assert.Equal(SwitchState.Open, fixture.Switch.SwitchState);
        Assert.Equal(stateId, runtime.CommandStack.CurrentStateId);
        AssertPreserved(runtime, result);

        runtime.CommandStack.ExecuteCommand(_factory.CreateRemoveGroundingPoint(runtime.PersistenceSession.Domain,
            add.After.GroundingPointId, beforeRestore: snapshot => GroundingPointCommandPreflight.EnsureAllowed(runtime, snapshot)));
        AssertPreserved(runtime, result);
        Assert.True(controller.SetSelectedState(SwitchState.Closed).IsSuccess);
        Assert.NotSame(result, runtime.Energization.CurrentResult);
        Assert.Equal(EnergizationState.Energized, runtime.Energization.CurrentResult!.Terminals[fixture.Switch.SecondTerminalId].State);
        // The shared history returns to a safe state before restoring grounding.
        Assert.True(runtime.CommandStack.Undo()); // Switch opens, EA refreshes.
        Assert.True(runtime.CommandStack.Undo()); // Grounding point can now be restored.
        Assert.Single(runtime.PersistenceSession.Domain.GroundingPoints);
        Assert.Throws<InvalidOperationException>(() => SwitchStateCommandPreflight.EnsureAllowed(runtime, fixture.Switch.Id, SwitchState.Closed));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MixedCompositeWithElectricalTopologyChangeStillInvalidates(bool delete)
    {
        Fixture fixture = Create();
        ProjectRuntimeSession runtime = fixture.Runtime;
        GroundingTarget target = AddGap(fixture);
        ICommand command = delete
            ? new SelectionDeletePlanner().Create(runtime, SelectionSet.Create([
                new SelectionReference(SelectionTargetKind.GroundingAccessPoint, target.TargetId),
                new SelectionReference(SelectionTargetKind.Connection, fixture.LeftConnectionId)]))
            : new CompositeProfessionalCommand([
                _factory.CreateRemoveGroundingAccessPoint(runtime.PersistenceSession.Domain, target.TargetId),
                new DeviceCommandFactory().CreateAddPole(runtime.PersistenceSession.Domain, runtime.Layout, new DocumentPoint(300, 40))]);
        runtime.CommandStack.ExecuteCommand(command);
        Assert.Equal(EnergizationFreshness.Stale, runtime.Energization.Freshness);
        Assert.Null(runtime.Energization.CurrentResult);
        Assert.False(runtime.Energization.CanShowOverlay);
        Assert.True(runtime.CommandStack.Undo());
        Assert.Null(runtime.Energization.CurrentResult);
        Assert.True(runtime.CommandStack.Redo());
        Assert.Null(runtime.Energization.CurrentResult);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NeutralCommandsDoNotRestoreUnavailableEaOrEnableHiddenOverlay(bool unavailable)
    {
        Fixture fixture = Create();
        ProjectRuntimeSession runtime = fixture.Runtime;
        if (unavailable) runtime.Energization.Invalidate();
        else runtime.Energization.SetOverlayRequested(false);
        EnergizationResult? result = runtime.Energization.CurrentResult;
        AddGap(fixture);
        Assert.Same(result, runtime.Energization.CurrentResult);
        Assert.False(runtime.Energization.CanShowOverlay);
    }

    private GroundingTarget AddGap(Fixture fixture)
    {
        AddGroundingAccessPointCommand command = _factory.CreateAddGroundingAccessPoint(
            fixture.Runtime.PersistenceSession.Domain, fixture.RightConnectionId, fixture.CenterPoleId,
            fixture.EndPoleId, GroundingAccessLineSide.LargerNumberSide);
        fixture.Runtime.CommandStack.ExecuteCommand(command);
        return GroundingTarget.ForGroundingAccessPoint(command.After.GroundingAccessPointId);
    }

    private Fixture Create()
    {
        ProjectRuntimeSession runtime = ProjectRuntimeSession.CreateEmpty(new ProjectService().CreateProject(_path, "GS-AF-05"));
        var devices = new DeviceCommandFactory();
        AddPoleCommand start = devices.CreateAddPole(runtime.PersistenceSession.Domain, runtime.Layout, new DocumentPoint(10, 40));
        start.Execute();
        AddPoleCommand center = devices.CreateAddPole(runtime.PersistenceSession.Domain, runtime.Layout, new DocumentPoint(100, 40));
        center.Execute();
        AddPoleCommand end = devices.CreateAddPole(runtime.PersistenceSession.Domain, runtime.Layout, new DocumentPoint(200, 40));
        end.Execute();
        AddPoleSwitchAttachmentCommand addSwitch = devices.CreateAddPoleSwitchAttachment(runtime.PersistenceSession.Domain,
            runtime.Layout, center.Pole.Id, SwitchKind.LoadSwitch, new DocumentPoint(0, 0));
        addSwitch.Execute();
        SwitchDevice device = runtime.PersistenceSession.Domain.Devices.OfType<SwitchDevice>().Single();
        AddCableTerminationAttachmentCommand addTermination = devices.CreateAddCableTerminationAttachment(
            runtime.PersistenceSession.Domain, runtime.Layout, end.Pole.Id, "工作电缆端", new DocumentPoint(0, 0));
        addTermination.Execute();
        CableTermination termination = runtime.PersistenceSession.Domain.Devices.OfType<CableTermination>().Single();
        var lines = new OverheadLineCommandFactory();
        AddOverheadLineCommand left = lines.CreateAdd(runtime.PersistenceSession.Domain, runtime.Layout,
            start.Terminal.Id, device.FirstTerminalId, start.Layout.Position, center.Layout.Position);
        left.Execute();
        AddOverheadLineCommand right = lines.CreateAdd(runtime.PersistenceSession.Domain, runtime.Layout,
            device.SecondTerminalId, termination.OverheadSideTerminalId, center.Layout.Position, end.Layout.Position);
        right.Execute();
        runtime.PersistenceSession.EnergizationScenario.AddSeed(new EnergizedSeed(Guid.NewGuid(), device.Id, EnergizationSide.SmallerNumber));
        Assert.Null(runtime.ExecuteEnergizationAnalysis());
        Assert.NotNull(runtime.Energization.CurrentResult);
        Assert.Equal(EnergizationState.Energized, runtime.Energization.CurrentResult.Terminals[device.FirstTerminalId].State);
        Assert.Equal(EnergizationState.Deenergized, runtime.Energization.CurrentResult.Terminals[device.SecondTerminalId].State);
        runtime.RebuildScene();
        return new Fixture(runtime, device, center.Pole.Id, end.Pole.Id, right.Connection.Id,
            left.Connection.Id, termination.CableSideTerminalId);
    }

    private static void AssertPreserved(ProjectRuntimeSession runtime, EnergizationResult result)
    {
        Assert.Equal(EnergizationFreshness.Current, runtime.Energization.Freshness);
        Assert.Same(result, runtime.Energization.CurrentResult);
        Assert.Same(result, runtime.Energization.LatestResult);
        Assert.True(runtime.Energization.CanShowOverlay);
        runtime.RebuildScene();
        Assert.Same(result, runtime.Energization.CurrentResult);
        Assert.True(runtime.Energization.CanShowOverlay);
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private sealed record Fixture(ProjectRuntimeSession Runtime, SwitchDevice Switch,
        Guid CenterPoleId, Guid EndPoleId, Guid RightConnectionId, Guid LeftConnectionId, Guid GroundingTerminalId);
}
