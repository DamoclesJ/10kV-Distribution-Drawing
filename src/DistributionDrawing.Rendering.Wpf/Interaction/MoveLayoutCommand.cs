using ApplicationSelectionTargetKind = DistributionDrawing.Application.Interaction.SelectionTargetKind;
using DistributionDrawing.Application.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class MoveLayoutCommand : ICommand
{
    private readonly RuntimeLayoutDocument _layout;

    public MoveLayoutCommand(
        RuntimeLayoutDocument layout,
        SelectionTarget target,
        object beforeLayout,
        object afterLayout)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(beforeLayout);
        ArgumentNullException.ThrowIfNull(afterLayout);

        if (target.TargetKind is not (ApplicationSelectionTargetKind.RingCabinet or
            ApplicationSelectionTargetKind.Pole))
        {
            throw new ArgumentException(
                "Only ring cabinets and poles can be moved.",
                nameof(target));
        }

        Target = target;
        BeforeLayout = beforeLayout;
        AfterLayout = afterLayout;
        ValidateLayoutType(beforeLayout, target.TargetKind);
        ValidateLayoutType(afterLayout, target.TargetKind);
    }

    public SelectionTarget Target { get; }

    public object BeforeLayout { get; }

    public object AfterLayout { get; }

    public void Execute() => Apply(AfterLayout);

    public void Undo() => Apply(BeforeLayout);

    public void Redo() => Execute();

    private void Apply(object value)
    {
        switch (Target.TargetKind)
        {
            case ApplicationSelectionTargetKind.RingCabinet:
                _layout.ReplaceRingCabinet((RingCabinetLayout)value);
                break;
            case ApplicationSelectionTargetKind.Pole:
                _layout.DrawingLayout.Replace((PoleLayout)value);
                break;
            default:
                throw new InvalidOperationException("Unsupported move target.");
        }
    }

    private static void ValidateLayoutType(
        object layout,
        ApplicationSelectionTargetKind targetKind)
    {
        bool valid = targetKind switch
        {
            ApplicationSelectionTargetKind.RingCabinet => layout is RingCabinetLayout,
            ApplicationSelectionTargetKind.Pole => layout is PoleLayout,
            _ => false
        };
        if (!valid)
        {
            throw new ArgumentException(
                "The layout type does not match the selection target.",
                nameof(layout));
        }
    }
}
