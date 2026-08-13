using DistributionDrawing.Application.Devices;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class PoleCreationCommandTests
{
    [Fact]
    public void Execute_AddsPoleWithStableIdentity()
    {
        var document = CreateDocument();
        PoleCreationResult result = new PoleCreationFactory().Create("P-001");
        var command = new CreatePoleCommand(document, result);

        command.Execute();

        Assert.Contains(document.Devices, device => device.Id == result.Pole.Id);
        Assert.Same(result.Pole, document.Devices.Single(device => device.Id == result.Pole.Id));
        Assert.NotEqual(Guid.Empty, result.Pole.Id);
        Assert.Equal("P-001", result.Pole.PoleNumber);
        Assert.Equal(PoleType.Cement, result.Pole.PoleType);
    }

    [Fact]
    public void Undo_RemovesPole()
    {
        var document = CreateDocument();
        var command = CreateCommand(document);

        command.Execute();
        command.Undo();

        Assert.DoesNotContain(document.Devices, device => device.Id == command.Pole.Id);
    }

    [Fact]
    public void Redo_RestoresSamePoleAndStableIdentity()
    {
        var document = CreateDocument();
        var command = CreateCommand(document);
        Guid poleId = command.Pole.Id;

        command.Execute();
        command.Undo();
        command.Redo();

        Device restored = Assert.Single(
            document.Devices,
            device => device.Id == poleId);
        Assert.Same(command.Pole, restored);
        Assert.Equal(poleId, restored.Id);
    }

    private static CreatePoleCommand CreateCommand(DrawingDocument document)
    {
        PoleCreationResult result = new PoleCreationFactory().Create("P-001");
        return new CreatePoleCommand(document, result);
    }

    private static DrawingDocument CreateDocument()
    {
        return new DrawingDocument(Guid.NewGuid(), "Pole creation test");
    }
}
