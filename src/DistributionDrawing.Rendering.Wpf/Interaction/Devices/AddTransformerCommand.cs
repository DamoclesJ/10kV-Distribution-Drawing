using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class AddTransformerCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly RuntimeLayoutDocument _runtimeLayout;

    public AddTransformerCommand(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        TransformerCreation creation)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _runtimeLayout = runtimeLayout ?? throw new ArgumentNullException(nameof(runtimeLayout));
        Creation = creation ?? throw new ArgumentNullException(nameof(creation));
    }

    public TransformerCreation Creation { get; }

    public void Execute()
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

    public void Undo()
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

    public void Redo() => Execute();

    private void AddDomainAggregate()
    {
        _document.AddTransformer(Creation.Transformer, Creation.HvTerminal);
    }
}
