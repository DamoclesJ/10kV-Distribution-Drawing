using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed record TransformerGroupMoveLayout(
    TransformerLayout Layout,
    TransformerKind TransformerKind);

public sealed record CustomerStationGroupMoveLayout(
    CustomerStationLayout Layout,
    CustomerStation Station);

public sealed record GroupMoveLayoutState(
    IReadOnlyList<PoleLayout> Poles,
    IReadOnlyList<RingCabinetLayout> RingCabinets,
    IReadOnlyList<AttachmentLayout> Attachments,
    IReadOnlyList<TransformerGroupMoveLayout> Transformers,
    IReadOnlyList<CustomerStationGroupMoveLayout> CustomerStations)
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
                       (item.AttachmentId, item.Offset))) &&
               Transformers.Select(item => (item.Layout.TransformerId, item.Layout.Position))
                   .SequenceEqual(other.Transformers.Select(item =>
                       (item.Layout.TransformerId, item.Layout.Position))) &&
               CustomerStations.Select(item =>
                       (item.Layout.CustomerStationId, item.Layout.Position))
                   .SequenceEqual(other.CustomerStations.Select(item =>
                       (item.Layout.CustomerStationId, item.Layout.Position)));
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

        foreach (TransformerGroupMoveLayout transformer in state.Transformers)
        {
            layout.ReplaceTransformer(
                transformer.Layout,
                transformer.TransformerKind);
        }

        foreach (CustomerStationGroupMoveLayout station in state.CustomerStations)
        {
            layout.ReplaceCustomerStation(station.Layout, station.Station);
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
            layout.DrawingLayout.Attachments[item.AttachmentId]).ToArray()),
        Array.AsReadOnly(roots.Transformers.Select(item =>
            new TransformerGroupMoveLayout(
                layout.TransformerLayouts[item.Layout.TransformerId],
                item.TransformerKind)).ToArray()),
        Array.AsReadOnly(roots.CustomerStations.Select(item =>
            new CustomerStationGroupMoveLayout(
                layout.CustomerStationLayouts[item.Layout.CustomerStationId],
                item.Station)).ToArray()));

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
                .SequenceEqual(after.Attachments.Select(item => item.AttachmentId)) ||
            !before.Transformers.Select(item => item.Layout.TransformerId)
                .SequenceEqual(after.Transformers.Select(item => item.Layout.TransformerId)) ||
            !before.CustomerStations.Select(item => item.Layout.CustomerStationId)
                .SequenceEqual(after.CustomerStations.Select(item => item.Layout.CustomerStationId)))
        {
            throw new ArgumentException(
                "Group move before and after states must contain the same layout roots.",
                nameof(after));
        }
    }
}
