using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Routing;

namespace DistributionDrawing.Desktop;

internal enum DragPreviewOutcome
{
    Accepted,
    Rejected,
    Unchanged,
    UnexpectedFailure
}

internal static class DragPreviewTransactionCoordinator
{
    internal const string InvalidCandidateFeedback =
        "当前位置无法形成有效连接，已保持最近一次有效位置。";

    public static DragPreviewOutcome ValidateAndPublish(
        ITransactionalDragPreview drag,
        Action rebuildAndPublish,
        Action<string> showFeedback,
        Action clearFeedback)
    {
        ArgumentNullException.ThrowIfNull(drag);
        ArgumentNullException.ThrowIfNull(rebuildAndPublish);
        ArgumentNullException.ThrowIfNull(showFeedback);
        ArgumentNullException.ThrowIfNull(clearFeedback);

        return UpdateValidateAndPublish(
            drag,
            () => true,
            rebuildAndPublish,
            showFeedback,
            clearFeedback);
    }

    public static DragPreviewOutcome ProcessPointerUpdate(
        ITransactionalDragPreview drag,
        Func<bool> updatePreview,
        Action rebuildAndPublish,
        Action<string> showFeedback,
        Action clearFeedback,
        Action cancelDrag,
        Action<Exception> showUnexpectedError)
    {
        ArgumentNullException.ThrowIfNull(cancelDrag);
        ArgumentNullException.ThrowIfNull(showUnexpectedError);
        try
        {
            return UpdateValidateAndPublish(
                drag,
                updatePreview,
                rebuildAndPublish,
                showFeedback,
                clearFeedback);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            cancelDrag();
            showUnexpectedError(exception);
            return DragPreviewOutcome.UnexpectedFailure;
        }
    }

    public static DragPreviewOutcome UpdateValidateAndPublish(
        ITransactionalDragPreview drag,
        Func<bool> updatePreview,
        Action rebuildAndPublish,
        Action<string> showFeedback,
        Action clearFeedback)
    {
        ArgumentNullException.ThrowIfNull(drag);
        ArgumentNullException.ThrowIfNull(updatePreview);
        ArgumentNullException.ThrowIfNull(rebuildAndPublish);
        ArgumentNullException.ThrowIfNull(showFeedback);
        ArgumentNullException.ThrowIfNull(clearFeedback);

        try
        {
            if (!updatePreview())
            {
                clearFeedback();
                return DragPreviewOutcome.Unchanged;
            }

            rebuildAndPublish();
            drag.AcceptCurrentPreview();
            clearFeedback();
            return DragPreviewOutcome.Accepted;
        }
        catch (Exception exception) when (exception is
            RoutingConstraintException or DragCandidateConstraintException)
        {
            drag.RollbackToLastValid();
            rebuildAndPublish();
            showFeedback(InvalidCandidateFeedback);
            return DragPreviewOutcome.Rejected;
        }
    }
}
