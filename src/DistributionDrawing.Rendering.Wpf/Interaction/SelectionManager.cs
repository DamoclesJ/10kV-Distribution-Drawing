namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class SelectionManager
{
    public SelectionReference? Selected { get; private set; }

    public event EventHandler? SelectionChanged;

    public void Select(SelectionReference? target)
    {
        if (Selected == target)
        {
            return;
        }

        Selected = target;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        Select(null);
    }
}
