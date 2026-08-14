using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Application.Topology;

public sealed class CreateIntermediateTerminalCommand
{
    private readonly DrawingDocument _document;

    public CreateIntermediateTerminalCommand(
        DrawingDocument document,
        IntermediateTerminalCreationResult result)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public IntermediateTerminalCreationResult Result { get; }

    public void Execute()
    {
        _document.AddIntermediateTerminal(
            Result.IntermediateTerminal,
            Result.Terminal);
    }

    public void Undo()
    {
        _document.RemoveIntermediateTerminal(Result.IntermediateTerminal.Id);
    }

    public void Redo()
    {
        Execute();
    }
}
