using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Layout;

public sealed record OverheadLineLayout
{
    public OverheadLineLayout(
        Guid connectionId,
        DocumentPoint start,
        DocumentPoint end,
        bool isContinued = false,
        DocumentPoint? continuationOffset = null)
    {
        if (connectionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Connection ID cannot be empty.",
                nameof(connectionId));
        }

        ConnectionId = connectionId;
        Start = start;
        End = end;
        IsContinued = isContinued;
        ContinuationOffset = continuationOffset ?? new DocumentPoint(4, 0);
    }

    public Guid ConnectionId { get; }

    public DocumentPoint Start { get; }

    public DocumentPoint End { get; }

    public bool IsContinued { get; }

    public DocumentPoint ContinuationOffset { get; }
}
