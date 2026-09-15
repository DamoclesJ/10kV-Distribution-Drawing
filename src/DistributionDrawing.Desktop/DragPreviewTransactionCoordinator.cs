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
        Action clearFeedback,
        RouteContinuityContext? routeContinuity = null)
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
            clearFeedback,
            routeContinuity);
    }

    public static void CommitAndPublishRelease(
        Func<ICommand?> commit,
        Action<ICommand> executeCommand,
        Action rebuildAndPublish,
        RouteContinuityContext routeContinuity)
    {
        ArgumentNullException.ThrowIfNull(commit);
        ArgumentNullException.ThrowIfNull(executeCommand);
        ArgumentNullException.ThrowIfNull(rebuildAndPublish);
        ArgumentNullException.ThrowIfNull(routeContinuity);

        try
        {
            ICommand? command = commit();
            if (command is null)
            {
                rebuildAndPublish();
            }
            else
            {
                executeCommand(command);
            }
        }
        finally
        {
            routeContinuity.EndGesture();
        }
    }

    public static DragPreviewOutcome ProcessPointerUpdate(
        ITransactionalDragPreview drag,
        Func<bool> updatePreview,
        Action rebuildAndPublish,
        Action<string> showFeedback,
        Action clearFeedback,
        Action cancelDrag,
        Action<Exception> showUnexpectedError,
        RouteContinuityContext? routeContinuity = null)
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
                clearFeedback,
                routeContinuity);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            routeContinuity?.EndGesture();
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
        Action clearFeedback,
        RouteContinuityContext? routeContinuity = null)
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
                routeContinuity?.DiscardProvisional();
                clearFeedback();
                return DragPreviewOutcome.Unchanged;
            }

            rebuildAndPublish();
            drag.AcceptCurrentPreview();
            routeContinuity?.AcceptProvisional();
            clearFeedback();
            return DragPreviewOutcome.Accepted;
        }
        catch (Exception exception) when (exception is
            RoutingConstraintException or DragCandidateConstraintException)
        {
            routeContinuity?.DiscardProvisional();
            drag.RollbackToLastValid();
            rebuildAndPublish();
            routeContinuity?.DiscardProvisional();
            showFeedback(InvalidCandidateFeedback);
            return DragPreviewOutcome.Rejected;
        }
    }
}
