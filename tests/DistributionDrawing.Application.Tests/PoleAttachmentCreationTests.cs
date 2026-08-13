using DistributionDrawing.Application.Devices;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class PoleAttachmentCreationTests
{
    [Theory]
    [InlineData(SwitchKind.CircuitBreaker)]
    [InlineData(SwitchKind.LoadSwitch)]
    [InlineData(SwitchKind.IsolationSwitch)]
    [InlineData(SwitchKind.DropoutFuse)]
    public void CreateWithSwitchAttachment_CreatesRequestedSwitchKind(
        SwitchKind switchKind)
    {
        var document = CreateDocument();
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P-002",
            PoleType.Cement,
            null,
            [switchKind],
            includeCableTerminal: false);
        var command = new CreatePoleCommand(document, result);

        command.Execute();

        SwitchDevice switchDevice = Assert.IsType<SwitchDevice>(
            Assert.Single(result.Devices));
        Assert.Equal(switchKind, switchDevice.SwitchKind);
        Assert.Single(document.PoleAttachments);
        Assert.Same(switchDevice, document.Devices.Single(device =>
            device.Id == switchDevice.Id));
        Assert.Equal(2, document.Terminals.Count);
    }

    [Fact]
    public void CreateWithCableTerminalAttachment_CreatesCableTerminationAggregate()
    {
        var document = CreateDocument();
        PoleCreationResult result = CreateResult(
            switchKinds: null,
            includeCableTerminal: true);
        var command = new CreatePoleCommand(document, result);

        command.Execute();

        CableTermination cableTermination = Assert.IsType<CableTermination>(
            Assert.Single(result.Devices));
        Assert.Single(document.PoleAttachments);
        Assert.Single(document.ElectricalNodes);
        Assert.Equal(2, document.Terminals.Count);
        Assert.Same(cableTermination, document.Devices.Single(device =>
            device.Id == cableTermination.Id));
    }

    [Fact]
    public void CreateWithBothAttachments_UndoAndRedoPreserveAllStableIds()
    {
        var document = CreateDocument();
        PoleCreationResult result = CreateResult(
            [SwitchKind.LoadSwitch],
            includeCableTerminal: true);
        var command = new CreatePoleCommand(document, result);
        Guid[] deviceIds = result.Devices.Select(device => device.Id).ToArray();
        Guid[] attachmentIds = result.Attachments
            .Select(attachment => attachment.AttachmentId)
            .ToArray();
        Guid[] terminalIds = result.Terminals.Select(terminal => terminal.Id).ToArray();
        Guid[] nodeIds = result.ElectricalNodes.Select(node => node.Id).ToArray();

        command.Execute();
        Assert.Equal(2, document.PoleAttachments.Count);

        command.Undo();

        Assert.Empty(document.Devices);
        Assert.Empty(document.PoleAttachments);
        Assert.Empty(document.Terminals);
        Assert.Empty(document.ElectricalNodes);

        command.Redo();

        Assert.Equal(result.Pole.Id, document.Devices.Single(device =>
            device.Id == result.Pole.Id).Id);
        Assert.Equal(deviceIds.Order(), document.Devices
            .Where(device => device.Id != result.Pole.Id)
            .Select(device => device.Id)
            .Order());
        Assert.Equal(attachmentIds.Order(), document.PoleAttachments
            .Select(attachment => attachment.AttachmentId)
            .Order());
        Assert.Equal(terminalIds.Order(), document.Terminals.Select(terminal => terminal.Id).Order());
        Assert.Equal(nodeIds.Order(), document.ElectricalNodes.Select(node => node.Id).Order());
        Assert.All(result.Devices, device => Assert.Same(
            device,
            document.Devices.Single(candidate => candidate.Id == device.Id)));
    }

    private static PoleCreationResult CreateResult(
        IEnumerable<SwitchKind>? switchKinds,
        bool includeCableTerminal)
    {
        return new PoleCreationFactory().CreateWithAttachments(
            "P-003",
            PoleType.Cement,
            null,
            switchKinds,
            includeCableTerminal);
    }

    private static DrawingDocument CreateDocument()
    {
        return new DrawingDocument(Guid.NewGuid(), "Pole attachment test");
    }
}
