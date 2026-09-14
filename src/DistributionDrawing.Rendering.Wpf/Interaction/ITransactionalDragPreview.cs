namespace DistributionDrawing.Rendering.Wpf.Interaction;

public interface ITransactionalDragPreview
{
    void AcceptCurrentPreview();

    bool RollbackToLastValid();
}
