using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Application.Devices;

public sealed class CreatePoleCommand
{
    private readonly DrawingDocument _document;

    public CreatePoleCommand(
        DrawingDocument document,
        PoleCreationResult result)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public PoleCreationResult Result { get; }

    public Pole Pole => Result.Pole;

    public void Execute()
    {
        if (_document.Devices.Any(device => device.Id == Pole.Id))
        {
            throw new InvalidOperationException(
                $"Pole '{Pole.Id}' already exists in the document.");
        }

        _document.AddDevice(Pole);
    }

    public void Undo()
    {
        _document.RemoveDevice(Pole.Id);
    }

    public void Redo()
    {
        Execute();
    }
}
