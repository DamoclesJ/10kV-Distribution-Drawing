namespace DistributionDrawing.Application.Interaction;

public sealed class SelectionService
{
    public SelectionTarget? CurrentSelection { get; private set; }

    public event EventHandler? SelectionChanged;

    public void Select(SelectionTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (CurrentSelection == target)
        {
            return;
        }

        CurrentSelection = target;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        if (CurrentSelection is null)
        {
            return;
        }

        CurrentSelection = null;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
