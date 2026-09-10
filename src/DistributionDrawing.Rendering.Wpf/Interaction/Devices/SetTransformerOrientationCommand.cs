using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class SetTransformerOrientationCommand : ICommand
{
    private readonly RuntimeLayoutDocument _layout;
    private readonly Transformer _transformer;
    private readonly TransformerLayout _before;
    private readonly TransformerLayout _after;

    public SetTransformerOrientationCommand(
        RuntimeLayoutDocument layout,
        Transformer transformer,
        TransformerOrientation orientation)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));
        if (transformer.TransformerKind != TransformerKind.PublicIndoor)
        {
            throw new InvalidOperationException(
                "Only PublicIndoor transformer orientation can be edited.");
        }

        if (!layout.TransformerLayouts.TryGetValue(
                transformer.Id,
                out TransformerLayout? before))
        {
            throw new InvalidOperationException(
                $"No layout exists for transformer '{transformer.Id}'.");
        }

        _before = before;
        _after = new TransformerLayout(
            transformer.Id,
            _before.Position,
            orientation,
            transformer.TransformerKind);
    }

    public void Execute() => Apply(_after);

    public void Undo() => Apply(_before);

    public void Redo() => Execute();

    private void Apply(TransformerLayout layout) =>
        _layout.ReplaceTransformer(layout, _transformer.TransformerKind);
}
