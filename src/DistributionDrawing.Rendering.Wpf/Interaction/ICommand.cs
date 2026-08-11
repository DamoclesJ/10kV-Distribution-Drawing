namespace DistributionDrawing.Rendering.Wpf.Interaction;

public interface ICommand
{
    void Execute();

    void Undo();

    void Redo();
}
