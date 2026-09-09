using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Professional;

public sealed class RemoveGroundingPointCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly RuntimeLayoutDocument? _layout;

    public RemoveGroundingPointCommand(
        DrawingDocument document,
        GroundingPointCommandSnapshot before,
        RuntimeLayoutDocument? layout = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(before);

        _document = document;
        _layout = layout;
        Before = before;
        if (layout is not null)
        {
            layout.GroundingPointLayouts.TryGetValue(
                before.GroundingPointId,
                out GroundingPointLayout? groundingLayout);
            BeforeLayout = groundingLayout;
        }
    }

    public GroundingPointCommandSnapshot Before { get; }

    public GroundingPointLayout? BeforeLayout { get; }

    public void Execute()
    {
        _document.RemoveGroundingPoint(Before.GroundingPointId);
        _layout?.RemoveGroundingPointLayout(Before.GroundingPointId);
    }

    public void Undo()
    {
        _document.CreateGroundingPoint(
            Before.GroundingPointId,
            Before.Target,
            Before.Location,
            Before.Number,
            Before.Note);
        if (BeforeLayout is not null)
        {
            _layout!.SetGroundingPointLayout(BeforeLayout);
        }
    }

    public void Redo()
    {
        Execute();
    }
}
