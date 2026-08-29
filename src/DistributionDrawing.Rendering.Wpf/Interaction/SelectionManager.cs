namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class SelectionManager
{
    public SelectionSet SelectionSet { get; private set; } = SelectionSet.Empty;

    public SelectionReference? Selected => SelectionSet.PrimarySelection;

    public int SelectionCount => SelectionSet.Count;

    public bool HasSingleSelection => SelectionCount == 1;

    public event EventHandler? SelectionChanged;

    public event EventHandler? SelectionSetChanged;

    public event EventHandler? SelectionCountChanged;

    public void Select(SelectionReference? target)
    {
        Replace(target is null ? [] : [target]);
    }

    public void Replace(IEnumerable<SelectionReference> targets)
    {
        SetSelectionSet(SelectionSet.Create(targets));
    }

    public void AddRange(IEnumerable<SelectionReference> targets)
    {
        SetSelectionSet(SelectionSet.AddRange(targets));
    }

    public void Toggle(SelectionReference target)
    {
        ArgumentNullException.ThrowIfNull(target);
        SetSelectionSet(
            SelectionSet.Contains(target)
                ? SelectionSet.Remove(target)
                : SelectionSet.AddRange([target]));
    }

    public void Retain(Func<SelectionReference, bool> predicate)
    {
        SetSelectionSet(SelectionSet.Retain(predicate));
    }

    public void Clear()
    {
        SetSelectionSet(SelectionSet.Empty);
    }

    private void SetSelectionSet(SelectionSet value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (SelectionSet.HasSameSelections(value))
        {
            return;
        }

        int previousCount = SelectionSet.Count;
        SelectionSet = value;
        SelectionSetChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        if (previousCount != value.Count)
        {
            SelectionCountChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
