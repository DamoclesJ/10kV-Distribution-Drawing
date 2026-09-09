using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Professional;

public sealed class MoveGroundingPointLayoutCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly RuntimeLayoutDocument _layout;

    public MoveGroundingPointLayoutCommand(
        DrawingDocument document,
        RuntimeLayoutDocument layout,
        Guid groundingPointId,
        GroundingPointLayout? before,
        GroundingPointLayout after)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        After = after ?? throw new ArgumentNullException(nameof(after));
        if (groundingPointId == Guid.Empty ||
            after.GroundingPointId != groundingPointId ||
            before is not null && before.GroundingPointId != groundingPointId)
        {
            throw new ArgumentException("Grounding layout states must use the same grounding point ID.");
        }
        GroundingPointId = groundingPointId;
        Before = before;
    }

    public Guid GroundingPointId { get; }

    public GroundingPointLayout? Before { get; }

    public GroundingPointLayout After { get; }

    public void Execute() => Apply(After);

    public void Undo() => Apply(Before);

    public void Redo() => Execute();

    private void Apply(GroundingPointLayout? value)
    {
        _ = _document.GetGroundingPoint(GroundingPointId);
        if (value is null)
        {
            _layout.RemoveGroundingPointLayout(GroundingPointId);
        }
        else
        {
            _layout.SetGroundingPointLayout(value);
        }
    }
}
