using DistributionDrawing.Application.Devices;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class ChangeSwitchStateCommandTests
{
    [Fact]
    public void Execute_ChangesOpenToClosed()
    {
        (DrawingDocument document, SwitchDevice switchDevice) = CreatePoleSwitch();
        var command = new ChangeSwitchStateCommand(
            document,
            switchDevice.Id,
            SwitchState.Closed);

        command.Execute();

        Assert.Equal(SwitchState.Closed, switchDevice.SwitchState);
        Assert.NotNull(command.InitialChange);
        Assert.Equal(SwitchState.Open, command.InitialChange.PreviousState);
        Assert.Equal(SwitchState.Closed, command.InitialChange.CurrentState);
    }

    [Fact]
    public void Execute_ChangesClosedToOpen()
    {
        (DrawingDocument document, SwitchDevice switchDevice) = CreatePoleSwitch();
        document.ChangeSwitchState(switchDevice.Id, SwitchState.Closed);
        var command = new ChangeSwitchStateCommand(
            document,
            switchDevice.Id,
            SwitchState.Open);

        command.Execute();

        Assert.Equal(SwitchState.Open, switchDevice.SwitchState);
        Assert.Equal(SwitchState.Closed, command.InitialChange!.PreviousState);
    }

    [Fact]
    public void UndoAndRedo_RestoreStatesWithoutChangingIdentity()
    {
        (DrawingDocument document, SwitchDevice switchDevice) = CreatePoleSwitch();
        Guid stableId = switchDevice.Id;
        var command = new ChangeSwitchStateCommand(
            document,
            stableId,
            SwitchState.Closed);

        command.Execute();
        command.Undo();

        Assert.Equal(SwitchState.Open, switchDevice.SwitchState);
        Assert.Same(switchDevice, document.Devices.Single(device => device.Id == stableId));

        command.Redo();

        Assert.Equal(SwitchState.Closed, switchDevice.SwitchState);
        Assert.Equal(stableId, switchDevice.Id);
        Assert.Same(switchDevice, document.Devices.Single(device => device.Id == stableId));
    }

    [Fact]
    public void RejectedTargetState_DoesNotMutateOrCaptureInitialChange()
    {
        (DrawingDocument document, SwitchDevice switchDevice) = CreatePoleSwitch();
        var command = new ChangeSwitchStateCommand(
            document,
            switchDevice.Id,
            SwitchState.Closed,
            _ => throw new InvalidOperationException("rejected"));

        Assert.Throws<InvalidOperationException>(command.Execute);

        Assert.Equal(SwitchState.Open, switchDevice.SwitchState);
        Assert.Null(command.InitialChange);
    }

    [Fact]
    public void ExecuteUndoRedo_PreflightsEachDestinationBeforeMutation()
    {
        (DrawingDocument document, SwitchDevice switchDevice) = CreatePoleSwitch();
        var destinations = new List<SwitchState>();
        var command = new ChangeSwitchStateCommand(
            document,
            switchDevice.Id,
            SwitchState.Closed,
            destinations.Add);

        command.Execute();
        command.Undo();
        command.Redo();

        Assert.Equal(
            [SwitchState.Closed, SwitchState.Open, SwitchState.Closed],
            destinations);
        Assert.Equal(SwitchState.Closed, switchDevice.SwitchState);
    }

    private static (DrawingDocument Document, SwitchDevice SwitchDevice) CreatePoleSwitch()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Switch command test");
        PoleCreationResult creation = new PoleCreationFactory().CreateWithAttachments(
            "P-010",
            PoleType.Cement,
            null,
            [SwitchKind.LoadSwitch],
            includeCableTerminal: false);
        new CreatePoleCommand(document, creation).Execute();
        return (document, Assert.IsType<SwitchDevice>(Assert.Single(creation.Devices)));
    }
}
