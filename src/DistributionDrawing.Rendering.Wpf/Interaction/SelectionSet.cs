namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class SelectionSet
{
    private static readonly SelectionSet EmptyValue = new([], null);
    private readonly IReadOnlyList<SelectionReference> _selectedReferences;

    private SelectionSet(
        IReadOnlyList<SelectionReference> selectedReferences,
        SelectionReference? primarySelection)
    {
        _selectedReferences = selectedReferences;
        PrimarySelection = primarySelection;
    }

    public static SelectionSet Empty => EmptyValue;

    public IReadOnlyList<SelectionReference> SelectedReferences => _selectedReferences;

    public SelectionReference? PrimarySelection { get; }

    public int Count => _selectedReferences.Count;

    public bool Contains(SelectionReference target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return _selectedReferences.Any(item => HasSameIdentity(item, target));
    }

    public static SelectionSet Create(
        IEnumerable<SelectionReference> targets,
        SelectionReference? primarySelection = null)
    {
        ArgumentNullException.ThrowIfNull(targets);

        SelectionReference[] values = Distinct(targets);
        if (values.Length == 0)
        {
            return Empty;
        }

        SelectionReference primary = primarySelection is null
            ? values[^1]
            : values.SingleOrDefault(item => HasSameIdentity(item, primarySelection))
                ?? throw new ArgumentException(
                    "Primary selection must belong to the selected references.",
                    nameof(primarySelection));
        return new SelectionSet(Array.AsReadOnly(values), primary);
    }

    public SelectionSet AddRange(
        IEnumerable<SelectionReference> targets,
        bool makeLastAddedPrimary = true)
    {
        ArgumentNullException.ThrowIfNull(targets);

        SelectionReference[] additions = Distinct(targets)
            .Where(target => !Contains(target))
            .ToArray();
        if (additions.Length == 0)
        {
            return this;
        }

        SelectionReference[] combined = _selectedReferences.Concat(additions).ToArray();
        return new SelectionSet(
            Array.AsReadOnly(combined),
            makeLastAddedPrimary ? additions[^1] : PrimarySelection ?? additions[^1]);
    }

    public SelectionSet Remove(SelectionReference target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!Contains(target))
        {
            return this;
        }

        SelectionReference[] remaining = _selectedReferences
            .Where(item => !HasSameIdentity(item, target))
            .ToArray();
        if (remaining.Length == 0)
        {
            return Empty;
        }

        SelectionReference primary = PrimarySelection is not null &&
                                     !HasSameIdentity(PrimarySelection, target)
            ? PrimarySelection
            : remaining[^1];
        return new SelectionSet(Array.AsReadOnly(remaining), primary);
    }

    public SelectionSet Retain(Func<SelectionReference, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        SelectionReference[] remaining = _selectedReferences.Where(predicate).ToArray();
        if (remaining.Length == _selectedReferences.Count)
        {
            return this;
        }

        return Create(
            remaining,
            PrimarySelection is not null && remaining.Any(item =>
                HasSameIdentity(item, PrimarySelection))
                ? PrimarySelection
                : null);
    }

    internal bool HasSameSelections(SelectionSet other)
    {
        return PrimarySelection == other.PrimarySelection &&
               _selectedReferences.SequenceEqual(other._selectedReferences);
    }

    private static SelectionReference[] Distinct(IEnumerable<SelectionReference> targets)
    {
        var identities = new HashSet<(SelectionTargetKind Kind, Guid ObjectId)>();
        var values = new List<SelectionReference>();
        foreach (SelectionReference target in targets)
        {
            ArgumentNullException.ThrowIfNull(target);
            if (identities.Add((target.Kind, target.ObjectId)))
            {
                values.Add(target);
            }
        }

        return values.ToArray();
    }

    private static bool HasSameIdentity(
        SelectionReference first,
        SelectionReference second)
    {
        return first.Kind == second.Kind && first.ObjectId == second.ObjectId;
    }
}
