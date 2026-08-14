namespace DistributionDrawing.Application.Interaction;

public enum SelectionTargetKind
{
    RingCabinet,
    Interval,
    SwitchDevice,
    Pole,
    PoleAttachment,
    CableSegment,
    IntermediateTerminal
}

public sealed record SelectionTarget
{
    public SelectionTarget(SelectionTargetKind targetKind, Guid targetId)
    {
        if (targetId == Guid.Empty)
        {
            throw new ArgumentException(
                "Selection target ID cannot be empty.",
                nameof(targetId));
        }

        TargetKind = targetKind;
        TargetId = targetId;
    }

    public SelectionTargetKind TargetKind { get; }

    public Guid TargetId { get; }
}
