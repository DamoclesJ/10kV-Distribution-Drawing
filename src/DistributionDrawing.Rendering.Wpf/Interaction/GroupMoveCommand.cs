using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed record GroupMoveLayoutState(
    IReadOnlyList<PoleLayout> Poles,
    IReadOnlyList<RingCabinetLayout> RingCabinets,
    IReadOnlyList<AttachmentLayout> Attachments)
{
    public bool HasSamePositions(GroupMoveLayoutState other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Poles.Select(item => (item.PoleId, item.Position))
                   .SequenceEqual(other.Poles.Select(item => (item.PoleId, item.Position))) &&
               RingCabinets.Select(item => (item.CabinetId, item.Position))
                   .SequenceEqual(other.RingCabinets.Select(item =>
                       (item.CabinetId, item.Position))) &&
               Attachments.Select(item => (item.AttachmentId, item.Offset))
                   .SequenceEqual(other.Attachments.Select(item =>
                       (item.AttachmentId, item.Offset)));
    }
}

public sealed class GroupMoveCommand : ICommand
{
    private readonly RuntimeLayoutDocument _layout;

    public GroupMoveCommand(
        RuntimeLayoutDocument layout,
        GroupMoveLayoutState before,
        GroupMoveLayoutState after)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        ValidateMatchingRoots(before, after);
    }

    public GroupMoveLayoutState Before { get; }

    public GroupMoveLayoutState After { get; }

    public void Execute() => ApplyAtomically(After);

    public void Undo() => ApplyAtomically(Before);

    public void Redo() => Execute();

    internal static void Apply(
        RuntimeLayoutDocument layout,
        GroupMoveLayoutState state)
    {
        GroupMoveLayoutState current = CaptureCurrent(layout, state);
        try
        {
            ApplyUnchecked(layout, state);
        }
        catch
        {
            ApplyUnchecked(layout, current);
            throw;
        }
    }

    private static void ApplyUnchecked(
        RuntimeLayoutDocument layout,
        GroupMoveLayoutState state)
    {
        foreach (PoleLayout pole in state.Poles)
        {
            layout.DrawingLayout.Replace(pole);
        }

        foreach (RingCabinetLayout cabinet in state.RingCabinets)
        {
            layout.ReplaceRingCabinet(cabinet);
        }

        foreach (AttachmentLayout attachment in state.Attachments)
        {
            layout.DrawingLayout.Replace(attachment);
        }
    }

    private static GroupMoveLayoutState CaptureCurrent(
        RuntimeLayoutDocument layout,
        GroupMoveLayoutState roots) => new(
        Array.AsReadOnly(roots.Poles.Select(item =>
            layout.DrawingLayout.Poles[item.PoleId]).ToArray()),
        Array.AsReadOnly(roots.RingCabinets.Select(item =>
            layout.RingCabinetLayouts[item.CabinetId]).ToArray()),
        Array.AsReadOnly(roots.Attachments.Select(item =>
            layout.DrawingLayout.Attachments[item.AttachmentId]).ToArray()));

    private void ApplyAtomically(GroupMoveLayoutState state)
    {
        Apply(_layout, state);
    }

    private static void ValidateMatchingRoots(
        GroupMoveLayoutState before,
        GroupMoveLayoutState after)
    {
        if (!before.Poles.Select(item => item.PoleId)
                .SequenceEqual(after.Poles.Select(item => item.PoleId)) ||
            !before.RingCabinets.Select(item => item.CabinetId)
                .SequenceEqual(after.RingCabinets.Select(item => item.CabinetId)) ||
            !before.Attachments.Select(item => item.AttachmentId)
                .SequenceEqual(after.Attachments.Select(item => item.AttachmentId)))
        {
            throw new ArgumentException(
                "Group move before and after states must contain the same layout roots.",
                nameof(after));
        }
    }
}
