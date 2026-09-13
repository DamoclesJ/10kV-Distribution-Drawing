using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class MoveTransformerCommand : ICommand
{
    private readonly RuntimeLayoutDocument _layout;
    private readonly TransformerKind _transformerKind;

    public MoveTransformerCommand(
        RuntimeLayoutDocument layout,
        Guid transformerId,
        TransformerKind transformerKind,
        DocumentPoint before,
        DocumentPoint after)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        if (transformerId == Guid.Empty)
        {
            throw new ArgumentException(
                "Transformer ID cannot be empty.",
                nameof(transformerId));
        }

        if (!Enum.IsDefined(transformerKind))
        {
            throw new ArgumentOutOfRangeException(nameof(transformerKind));
        }

        TransformerId = transformerId;
        _transformerKind = transformerKind;
        Before = before;
        After = after;
    }

    public Guid TransformerId { get; }

    public DocumentPoint Before { get; }

    public DocumentPoint After { get; }

    public void Execute() => Apply(After);

    public void Undo() => Apply(Before);

    public void Redo() => Execute();

    private void Apply(DocumentPoint position)
    {
        if (!_layout.TransformerLayouts.TryGetValue(
                TransformerId,
                out TransformerLayout? current))
        {
            throw new InvalidOperationException(
                $"No layout exists for transformer '{TransformerId}'.");
        }

        _layout.ReplaceTransformer(
            current.MoveTo(position, _transformerKind),
            _transformerKind);
    }
}
