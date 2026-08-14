using ApplicationSelectionTargetKind = DistributionDrawing.Application.Interaction.SelectionTargetKind;
using DistributionDrawing.Application.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class MoveController
{
    private readonly SelectionService _selectionService;
    private DragState? _drag;

    public MoveController(SelectionService selectionService)
    {
        _selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));
    }

    public bool IsActive => _drag is not null;

    public bool MouseDown(
        SelectionTarget target,
        DocumentPoint pointer,
        RuntimeLayoutDocument layout)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(layout);
        if (_drag is not null)
        {
            throw new InvalidOperationException("A move operation is already active.");
        }

        _drag = target.TargetKind switch
        {
            ApplicationSelectionTargetKind.RingCabinet =>
                CreateCabinetDrag(target, pointer, layout),
            ApplicationSelectionTargetKind.Pole =>
                CreatePoleDrag(target, pointer, layout),
            _ => null
        };
        if (_drag is null)
        {
            return false;
        }

        _selectionService.Select(target);
        return true;
    }

    public bool MouseMove(DocumentPoint pointer)
    {
        if (_drag is not { } drag)
        {
            throw new InvalidOperationException("No move operation is active.");
        }

        DocumentPoint position = Add(
            drag.StartPosition,
            Subtract(pointer, drag.StartPointer));
        if (position == drag.CurrentPosition)
        {
            return false;
        }

        _drag = drag.MoveTo(position);
        return true;
    }

    public bool MouseUp(CommandStack commandStack)
    {
        ArgumentNullException.ThrowIfNull(commandStack);
        if (_drag is not { } drag)
        {
            return false;
        }

        _drag = null;
        if (drag.StartPosition == drag.CurrentPosition)
        {
            return false;
        }

        commandStack.ExecuteCommand(drag.CreateCommand());
        return true;
    }

    public bool Cancel()
    {
        if (_drag is not { } drag)
        {
            return false;
        }

        _drag = null;
        drag.RestoreBefore();
        return true;
    }

    private static DragState CreateCabinetDrag(
        SelectionTarget target,
        DocumentPoint pointer,
        RuntimeLayoutDocument layout)
    {
        if (!layout.RingCabinetLayouts.TryGetValue(
                target.TargetId,
                out RingCabinetLayout? cabinet))
        {
            throw new InvalidOperationException(
                $"No layout exists for ring cabinet '{target.TargetId}'.");
        }

        return new CabinetDrag(target, pointer, layout, cabinet, cabinet);
    }

    private static DragState CreatePoleDrag(
        SelectionTarget target,
        DocumentPoint pointer,
        RuntimeLayoutDocument layout)
    {
        if (!layout.DrawingLayout.Poles.TryGetValue(target.TargetId, out PoleLayout? pole))
        {
            throw new InvalidOperationException(
                $"No layout exists for pole '{target.TargetId}'.");
        }

        return new PoleDrag(target, pointer, layout, pole, pole);
    }

    private static DocumentPoint Add(DocumentPoint left, DocumentPoint right) =>
        new(left.XMillimeters + right.XMillimeters, left.YMillimeters + right.YMillimeters);

    private static DocumentPoint Subtract(DocumentPoint left, DocumentPoint right) =>
        new(left.XMillimeters - right.XMillimeters, left.YMillimeters - right.YMillimeters);

    private abstract record DragState(
        SelectionTarget Target,
        DocumentPoint StartPointer,
        RuntimeLayoutDocument Layout)
    {
        public abstract DocumentPoint StartPosition { get; }

        public abstract DocumentPoint CurrentPosition { get; }

        public abstract DragState MoveTo(DocumentPoint position);

        public abstract MoveLayoutCommand CreateCommand();

        public abstract void RestoreBefore();
    }

    private sealed record CabinetDrag(
        SelectionTarget Target,
        DocumentPoint StartPointer,
        RuntimeLayoutDocument Layout,
        RingCabinetLayout Before,
        RingCabinetLayout Current)
        : DragState(Target, StartPointer, Layout)
    {
        public override DocumentPoint StartPosition => Before.Position;

        public override DocumentPoint CurrentPosition => Current.Position;

        public override DragState MoveTo(DocumentPoint position)
        {
            RingCabinetLayout preview = Before.MoveTo(position);
            Layout.ReplaceRingCabinet(preview);
            return this with { Current = preview };
        }

        public override MoveLayoutCommand CreateCommand() =>
            new(Layout, Target, Before, Current);

        public override void RestoreBefore() => Layout.ReplaceRingCabinet(Before);
    }

    private sealed record PoleDrag(
        SelectionTarget Target,
        DocumentPoint StartPointer,
        RuntimeLayoutDocument Layout,
        PoleLayout Before,
        PoleLayout Current)
        : DragState(Target, StartPointer, Layout)
    {
        public override DocumentPoint StartPosition => Before.Position;

        public override DocumentPoint CurrentPosition => Current.Position;

        public override DragState MoveTo(DocumentPoint position)
        {
            PoleLayout preview = Before.MoveTo(position);
            Layout.DrawingLayout.Replace(preview);
            return this with { Current = preview };
        }

        public override MoveLayoutCommand CreateCommand() =>
            new(Layout, Target, Before, Current);

        public override void RestoreBefore() => Layout.DrawingLayout.Replace(Before);
    }
}
