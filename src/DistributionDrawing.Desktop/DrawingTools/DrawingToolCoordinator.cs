using DistributionDrawing.Desktop.ConnectionEditing;
using DistributionDrawing.Desktop.Placement;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Desktop.DrawingTools;

public sealed class DrawingToolCoordinator
{
    private readonly PlacementController _placement;
    private readonly OverheadLineConnectionController _overheadLine;

    public DrawingToolCoordinator(
        PlacementController placement,
        OverheadLineConnectionController overheadLine)
    {
        _placement = placement ?? throw new ArgumentNullException(nameof(placement));
        _overheadLine = overheadLine ?? throw new ArgumentNullException(nameof(overheadLine));
    }

    public bool IsActive =>
        _placement.Mode != PlacementMode.Idle || _overheadLine.IsActive;

    public void BeginPole()
    {
        _overheadLine.Cancel();
        _placement.BeginPole();
    }

    public void BeginRingCabinet(RingCabinetCreationConfiguration configuration)
    {
        _overheadLine.Cancel();
        _placement.BeginRingCabinet(configuration);
    }

    public void BeginOverheadLine()
    {
        _placement.Cancel();
        _overheadLine.Begin();
    }

    public void Cancel()
    {
        _placement.Cancel();
        _overheadLine.Cancel();
    }

    public bool HandleClick(DocumentPoint point, double terminalToleranceMillimeters)
    {
        if (_overheadLine.IsActive)
        {
            _overheadLine.Pick(point, terminalToleranceMillimeters);
            return true;
        }

        return _placement.Place(point);
    }

    public void UpdatePointer(DocumentPoint point)
    {
        _overheadLine.UpdatePointer(point);
    }

    public void RemoveSelected()
    {
        if (_overheadLine.IsOverheadLineSelected)
        {
            _overheadLine.RemoveSelected();
            return;
        }

        _placement.RemoveSelected();
    }

    public IReadOnlyList<SceneElement> CreateTransientElements()
    {
        return _overheadLine.CreatePreviewElements();
    }
}
