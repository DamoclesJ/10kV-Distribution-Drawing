namespace DistributionDrawing.Rendering.Wpf.Interaction;

public enum SelectionTargetKind
{
    Device,
    RingCabinet,
    RingCabinetInterval,
    PoleAttachment,
    Connection
}

public sealed record SelectionReference(
    SelectionTargetKind Kind,
    Guid ObjectId,
    Guid? ParentId = null);
