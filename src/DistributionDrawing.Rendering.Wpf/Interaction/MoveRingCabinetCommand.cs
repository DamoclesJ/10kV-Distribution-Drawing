using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class MoveRingCabinetCommand : ICommand
{
    private readonly RuntimeLayoutDocument _layout;

    public MoveRingCabinetCommand(
        RuntimeLayoutDocument layout,
        Guid cabinetId,
        DocumentPoint before,
        DocumentPoint after)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        if (cabinetId == Guid.Empty)
        {
            throw new ArgumentException("Cabinet ID cannot be empty.", nameof(cabinetId));
        }

        CabinetId = cabinetId;
        Before = before;
        After = after;
    }

    public Guid CabinetId { get; }

    public DocumentPoint Before { get; }

    public DocumentPoint After { get; }

    public void Execute() => Apply(After);

    public void Undo() => Apply(Before);

    public void Redo() => Execute();

    private void Apply(DocumentPoint position)
    {
        if (!_layout.RingCabinetLayouts.TryGetValue(
                CabinetId,
                out RingCabinetLayout? current))
        {
            throw new InvalidOperationException(
                $"No layout exists for ring cabinet '{CabinetId}'.");
        }

        _layout.ReplaceRingCabinet(current.MoveTo(position));
    }
}
