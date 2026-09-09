using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class RemoveTransformerCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly RuntimeLayoutDocument _runtimeLayout;

    public RemoveTransformerCommand(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        Guid transformerId)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _runtimeLayout = runtimeLayout ?? throw new ArgumentNullException(nameof(runtimeLayout));

        Transformer transformer = document.Transformers.SingleOrDefault(candidate =>
                candidate.Id == transformerId)
            ?? throw new InvalidOperationException(
                $"Transformer '{transformerId}' does not exist.");
        Terminal hvTerminal = document.Terminals.SingleOrDefault(candidate =>
                candidate.Id == transformer.HvTerminalId)
            ?? throw new InvalidOperationException(
                $"Transformer '{transformerId}' HV terminal is missing.");
        TransformerLayout layout = runtimeLayout.TransformerLayouts.TryGetValue(
                transformerId,
                out TransformerLayout? existingLayout)
            ? existingLayout
            : throw new InvalidOperationException(
                $"Transformer layout '{transformerId}' does not exist.");

        Creation = new TransformerCreation(transformer, hvTerminal, layout);
    }

    public TransformerCreation Creation { get; }

    public void Execute()
    {
        _ = _runtimeLayout.TransformerLayouts[Creation.Transformer.Id];
        _document.RemoveDevice(Creation.Transformer.Id);
        try
        {
            _runtimeLayout.RemoveTransformer(Creation.Transformer.Id);
        }
        catch
        {
            AddDomainAggregate();
            throw;
        }
    }

    public void Undo()
    {
        if (_runtimeLayout.TransformerLayouts.ContainsKey(Creation.Transformer.Id))
        {
            throw new InvalidOperationException(
                $"Transformer layout '{Creation.Transformer.Id}' already exists.");
        }

        AddDomainAggregate();
        try
        {
            _runtimeLayout.AddTransformer(
                Creation.Layout,
                Creation.Transformer.TransformerKind);
        }
        catch
        {
            _document.RemoveDevice(Creation.Transformer.Id);
            throw;
        }
    }

    public void Redo() => Execute();

    private void AddDomainAggregate()
    {
        _document.AddTransformer(Creation.Transformer, Creation.HvTerminal);
    }
}
