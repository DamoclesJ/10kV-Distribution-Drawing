using DistributionDrawing.Desktop.ConnectionEditing;
using DistributionDrawing.Desktop.CableConnection;
using DistributionDrawing.Desktop.CableTerminationCreation;
using DistributionDrawing.Desktop.Placement;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Desktop.DrawingTools;

public sealed class DrawingToolCoordinator
{
    private readonly PlacementController _placement;
    private readonly OverheadLineConnectionController _overheadLine;
    private readonly CableTerminationAttachmentController _cableTerminationAttachment;
    private readonly CableConnectionController _cableConnection;
    private readonly CableReconnectController _cableReconnect;

    public DrawingToolCoordinator(
        PlacementController placement,
        OverheadLineConnectionController overheadLine,
        CableTerminationAttachmentController cableTerminationAttachment,
        CableConnectionController cableConnection,
        CableReconnectController cableReconnect)
    {
        _placement = placement ?? throw new ArgumentNullException(nameof(placement));
        _overheadLine = overheadLine ?? throw new ArgumentNullException(nameof(overheadLine));
        _cableTerminationAttachment = cableTerminationAttachment ??
            throw new ArgumentNullException(nameof(cableTerminationAttachment));
        _cableConnection = cableConnection ??
            throw new ArgumentNullException(nameof(cableConnection));
        _cableReconnect = cableReconnect ??
            throw new ArgumentNullException(nameof(cableReconnect));
    }

    public bool IsActive =>
        _placement.Mode != PlacementMode.Idle ||
        _overheadLine.IsActive ||
        _cableConnection.IsActive ||
        _cableReconnect.IsActive;

    public void BeginPole()
    {
        _overheadLine.Cancel();
        _cableReconnect.Cancel();
        _placement.BeginPole();
    }

    public void BeginRingCabinet(RingCabinetCreationConfiguration configuration)
    {
        _overheadLine.Cancel();
        _cableReconnect.Cancel();
        _placement.BeginRingCabinet(configuration);
    }

    public void BeginOverheadLine()
    {
        _placement.Cancel();
        _cableConnection.Cancel();
        _cableReconnect.Cancel();
        _overheadLine.Begin();
    }

    public void BeginCable()
    {
        _placement.Cancel();
        _overheadLine.Cancel();
        _cableReconnect.Cancel();
        _cableConnection.Begin();
    }

    public void Cancel()
    {
        _placement.Cancel();
        _overheadLine.Cancel();
        _cableConnection.Cancel();
        _cableReconnect.Cancel();
    }

    public bool HandleClick(DocumentPoint point, double terminalToleranceMillimeters)
    {
        if (_overheadLine.IsActive)
        {
            _overheadLine.Pick(point, terminalToleranceMillimeters);
            return true;
        }

        if (_cableConnection.IsActive)
        {
            _cableConnection.Pick(point, terminalToleranceMillimeters);
            return true;
        }

        if (_cableReconnect.IsActive)
        {
            _cableReconnect.Pick(point, terminalToleranceMillimeters);
            return true;
        }

        return _placement.Place(point);
    }

    public void UpdatePointer(DocumentPoint point)
    {
        _overheadLine.UpdatePointer(point);
        _cableConnection.UpdatePointer(point);
    }

    public void RemoveSelected()
    {
        if (_overheadLine.IsOverheadLineSelected)
        {
            _overheadLine.RemoveSelected();
            return;
        }

        if (_cableConnection.IsCableSegmentSelected)
        {
            _cableConnection.RemoveSelected();
            return;
        }

        if (_cableTerminationAttachment.IsCableTerminationAttachmentSelected)
        {
            _cableTerminationAttachment.RemoveSelected();
            return;
        }

        _placement.RemoveSelected();
    }

    public IReadOnlyList<SceneElement> CreateTransientElements()
    {
        return _overheadLine.CreatePreviewElements()
            .Concat(_cableConnection.CreatePreviewElements())
            .ToArray();
    }
}
