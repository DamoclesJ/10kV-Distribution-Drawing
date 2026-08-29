using System.Windows.Media;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class SelectionRectangleController
{
    private readonly SelectionManager _selectionManager;
    private readonly SceneSelectionQuery _query;
    private DocumentPoint? _start;
    private DocumentPoint? _current;
    private bool _addToSelection;

    public SelectionRectangleController(
        SelectionManager selectionManager,
        SceneSelectionQuery? query = null)
    {
        _selectionManager = selectionManager ??
            throw new ArgumentNullException(nameof(selectionManager));
        _query = query ?? new SceneSelectionQuery();
    }

    public bool IsActive => _start is not null;

    public DocumentRect? Rectangle => _start is DocumentPoint start &&
                                     _current is DocumentPoint current
        ? CreateRectangle(start, current)
        : null;

    public void Begin(DocumentPoint start, bool addToSelection = false)
    {
        if (IsActive)
        {
            throw new InvalidOperationException("A selection rectangle is already active.");
        }

        _start = start;
        _current = start;
        _addToSelection = addToSelection;
    }

    public void Update(DocumentPoint current)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("No selection rectangle is active.");
        }

        _current = current;
    }

    public IReadOnlyList<SelectionReference> Complete(
        SelectionHitTestIndex hitTestIndex)
    {
        ArgumentNullException.ThrowIfNull(hitTestIndex);
        DocumentRect rectangle = Rectangle ??
            throw new InvalidOperationException("No selection rectangle is active.");
        bool addToSelection = _addToSelection;
        _start = null;
        _current = null;
        _addToSelection = false;

        IReadOnlyList<SelectionReference> targets = _query.QueryRectangle(
            hitTestIndex,
            rectangle);
        if (addToSelection)
        {
            _selectionManager.AddRange(targets);
        }
        else
        {
            _selectionManager.Replace(targets);
        }

        return targets;
    }

    public bool Cancel()
    {
        if (!IsActive)
        {
            return false;
        }

        _start = null;
        _current = null;
        _addToSelection = false;
        return true;
    }

    public IReadOnlyList<SceneElement> CreateOverlayElements()
    {
        if (Rectangle is not { } rectangle ||
            rectangle.WidthMillimeters < 0.1 ||
            rectangle.HeightMillimeters < 0.1)
        {
            return [];
        }

        return
        [
            new SceneRectangle(
                rectangle,
                Colors.DeepSkyBlue,
                0.8,
                Color.FromArgb(32, 0, 191, 255),
                SceneStrokeStyle.Dashed)
        ];
    }

    private static DocumentRect CreateRectangle(
        DocumentPoint first,
        DocumentPoint second)
    {
        double left = Math.Min(first.XMillimeters, second.XMillimeters);
        double top = Math.Min(first.YMillimeters, second.YMillimeters);
        return new DocumentRect(
            left,
            top,
            Math.Abs(second.XMillimeters - first.XMillimeters),
            Math.Abs(second.YMillimeters - first.YMillimeters));
    }
}
