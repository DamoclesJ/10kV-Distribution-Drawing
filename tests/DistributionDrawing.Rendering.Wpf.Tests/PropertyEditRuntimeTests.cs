using DistributionDrawing.Application.Interaction;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.Building;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using Xunit;
using ApplicationSelectionTargetKind = DistributionDrawing.Application.Interaction.SelectionTargetKind;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class PropertyEditRuntimeTests
{
    [Fact]
    public void EditRingCabinetName_UndoRedo_PreservesStableIdAndSelection()
    {
        RingCabinet cabinet = CreateCabinet("Before");
        SelectionService selection = new();
        SelectionTarget target = new(ApplicationSelectionTargetKind.RingCabinet, cabinet.Id);
        selection.Select(target);
        var command = new EditPropertyCommand(
            cabinet,
            EditPropertyCommand.RingCabinetNameProperty,
            "Before",
            "After");

        command.Execute();
        Assert.Equal("After", cabinet.DisplayName);
        Assert.Equal(target, selection.CurrentSelection);
        command.Undo();
        Assert.Equal("Before", cabinet.DisplayName);
        command.Redo();
        Assert.Equal("After", cabinet.DisplayName);
        Assert.Equal(cabinet.Id, target.TargetId);
    }

    [Fact]
    public void EditCableLength_UndoRedo_PreservesStableAndTopologyIds()
    {
        CableSegment cable = CreateCable(out Guid connectionId, out Guid startTerminalId, out Guid endTerminalId);
        var command = new EditPropertyCommand(
            cable,
            EditPropertyCommand.CableLengthProperty,
            10d,
            15d);

        command.Execute();
        Assert.Equal(15d, cable.Length);
        Assert.Equal(connectionId, cable.ConnectionId);
        Assert.Equal(startTerminalId, cable.StartTerminalId);
        Assert.Equal(endTerminalId, cable.EndTerminalId);
        command.Undo();
        Assert.Equal(10d, cable.Length);
        command.Redo();
        Assert.Equal(15d, cable.Length);
    }

    [Fact]
    public void EditSwitchDisplayName_DoesNotChangeSwitchState()
    {
        SwitchDevice switchDevice = SwitchDevice.CreateForPole(
            Guid.NewGuid(),
            SwitchKind.LoadSwitch,
            Guid.NewGuid(),
            Guid.NewGuid(),
            SwitchState.Closed,
            "Before");
        var command = new EditPropertyCommand(
            switchDevice,
            EditPropertyCommand.SwitchDisplayNameProperty,
            "Before",
            "After");

        command.Execute();

        Assert.Equal("After", switchDevice.DisplayName);
        Assert.Equal(SwitchState.Closed, switchDevice.SwitchState);
    }

    [Fact]
    public void InvalidEdit_DoesNotModifyObject()
    {
        Pole pole = new(Guid.NewGuid(), "P-1", "Description");
        var command = new EditPropertyCommand(
            pole,
            EditPropertyCommand.PoleNumberProperty,
            "P-1",
            "P-2");

        command.Execute();
        Assert.Equal("P-2", pole.PoleNumber);
        Assert.Throws<ArgumentException>(() => new EditPropertyCommand(
            pole,
            EditPropertyCommand.CableLengthProperty,
            1d,
            2d));
    }

    private static RingCabinet CreateCabinet(string name)
    {
        RingCabinetDomainBuildOutcome outcome = new RingCabinetTemplateDomainBuilder().Build(
            new RingCabinetTemplate(
                new TemplateId("test:property-edit"),
                name,
                RingCabinetTemplateType.Conventional,
                [
                    new BayTemplate(1, new LoadSwitchConfiguration()),
                    new BayTemplate(2, new LoadSwitchConfiguration()),
                    new BayTemplate(3, new LoadSwitchConfiguration())
                ],
                RingCabinetLayoutRule.Default,
                NoSecondaryConfiguration.Instance),
            name);
        Assert.NotNull(outcome.Result);
        return outcome.Result!.Cabinet;
    }

    private static CableSegment CreateCable(
        out Guid connectionId,
        out Guid startTerminalId,
        out Guid endTerminalId)
    {
        connectionId = Guid.NewGuid();
        startTerminalId = Guid.NewGuid();
        endTerminalId = Guid.NewGuid();
        return new CableSegment(
            Guid.NewGuid(),
            "Cable",
            "XLPE",
            10,
            "10kV",
            connectionId,
            startTerminalId,
            endTerminalId);
    }
}
