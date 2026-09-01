using DistributionDrawing.Desktop.ConnectionEditing;
using DistributionDrawing.Desktop.CableConnection;
using DistributionDrawing.Desktop.CableTerminationCreation;
using DistributionDrawing.Desktop.PoleSwitchCreation;
using DistributionDrawing.Desktop.Selection;
using DistributionDrawing.Desktop.Placement;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Desktop.DrawingTools;

public sealed class DrawingToolCoordinator
{
    private readonly PlacementController _placement;
    private readonly OverheadLineConnectionController _overheadLine;
    private readonly CableTerminationAttachmentController _cableTerminationAttachment;
    private readonly CableConnectionController _cableConnection;
    private readonly CableReconnectController _cableReconnect;
    private readonly PoleSwitchAttachmentController _poleSwitchAttachment;
    private readonly Func<ProjectRuntimeSession?> _getSession;
    private readonly SelectionDeletePlanner _selectionDeletePlanner = new();

    public DrawingToolCoordinator(
        PlacementController placement,
        OverheadLineConnectionController overheadLine,
        CableTerminationAttachmentController cableTerminationAttachment,
        CableConnectionController cableConnection,
        CableReconnectController cableReconnect,
        PoleSwitchAttachmentController poleSwitchAttachment,
        Func<ProjectRuntimeSession?>? getSession = null)
    {
        _placement = placement ?? throw new ArgumentNullException(nameof(placement));
        _overheadLine = overheadLine ?? throw new ArgumentNullException(nameof(overheadLine));
        _cableTerminationAttachment = cableTerminationAttachment ??
            throw new ArgumentNullException(nameof(cableTerminationAttachment));
        _cableConnection = cableConnection ??
            throw new ArgumentNullException(nameof(cableConnection));
        _cableReconnect = cableReconnect ??
            throw new ArgumentNullException(nameof(cableReconnect));
        _poleSwitchAttachment = poleSwitchAttachment ??
            throw new ArgumentNullException(nameof(poleSwitchAttachment));
        _getSession = getSession ?? (() => null);
    }

    public void AddSwitchAttachment(SwitchKind switchKind)
    {
        _poleSwitchAttachment.AddToSelectedPole(switchKind);
    }

    public bool IsActive =>
        _placement.Mode != PlacementMode.Idle ||
        _overheadLine.IsActive ||
        _cableConnection.IsActive ||
        _cableReconnect.IsActive ||
        _poleSwitchAttachment.IsSelectingControlledConnection;

    public void BeginPole()
    {
        _overheadLine.Cancel();
        _cableConnection.Cancel();
        _cableReconnect.Cancel();
        _poleSwitchAttachment.Cancel();
        _placement.BeginPole();
    }

    public void BeginRingCabinet(RingCabinetCreationConfiguration configuration)
    {
        _overheadLine.Cancel();
        _cableConnection.Cancel();
        _cableReconnect.Cancel();
        _poleSwitchAttachment.Cancel();
        _placement.BeginRingCabinet(configuration);
    }

    public void BeginOverheadLine()
    {
        _placement.Cancel();
        _cableConnection.Cancel();
        _cableReconnect.Cancel();
        _poleSwitchAttachment.Cancel();
        _overheadLine.Begin();
    }

    public void BeginCable()
    {
        _placement.Cancel();
        _overheadLine.Cancel();
        _cableReconnect.Cancel();
        _poleSwitchAttachment.Cancel();
        _cableConnection.Begin();
    }

    public void Cancel()
    {
        _placement.Cancel();
        _overheadLine.Cancel();
        _cableConnection.Cancel();
        _cableReconnect.Cancel();
        _poleSwitchAttachment.Cancel();
    }

    public bool HandleClick(
        DocumentPoint point,
        double terminalToleranceMillimeters,
        SelectionReference? hitTarget = null,
        bool snapPlacement = false)
    {
        if (_poleSwitchAttachment.IsSelectingControlledConnection)
        {
            if (hitTarget?.Kind != SelectionTargetKind.Connection)
            {
                throw new InvalidOperationException("请选择一条与当前杆塔相连的架空线路。");
            }

            _poleSwitchAttachment.PickControlledConnection(hitTarget.ObjectId);
            return true;
        }

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

        return _placement.Place(point, snapPlacement);
    }

    public void UpdatePointer(DocumentPoint point, bool snapPlacement = false)
    {
        _placement.UpdatePointer(point, snapPlacement);
        _overheadLine.UpdatePointer(point);
        _cableConnection.UpdatePointer(point);
    }

    public void RemoveSelected()
    {
        ProjectRuntimeSession session = _getSession()
            ?? throw new InvalidOperationException("No project is currently open.");
        ICommand command = _selectionDeletePlanner.Create(
            session,
            session.SelectionManager.SelectionSet);
        session.CommandStack.ExecuteCommand(command, session.RebuildScene);
        session.SelectionManager.Clear();
    }

    public IReadOnlyList<SceneElement> CreateTransientElements()
    {
        return _placement.CreatePreviewElements()
            .Concat(_overheadLine.CreatePreviewElements())
            .Concat(_cableConnection.CreatePreviewElements())
            .ToArray();
    }
}
