using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Desktop.Placement;

public sealed class PlacementController
{
    private readonly Func<ProjectRuntimeSession?> _getSession;
    private readonly DeviceCommandFactory _commandFactory;

    public PlacementController(
        Func<ProjectRuntimeSession?> getSession,
        DeviceCommandFactory? commandFactory = null)
    {
        _getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
        _commandFactory = commandFactory ?? new DeviceCommandFactory();
    }

    public PlacementMode Mode { get; private set; }

    public event EventHandler? SceneChanged;

    public void BeginPole() => Mode = PlacementMode.PlacingPole;

    public void BeginRingCabinet() => Mode = PlacementMode.PlacingRingCabinet;

    public void Cancel() => Mode = PlacementMode.Idle;

    public bool Place(DocumentPoint position)
    {
        ProjectRuntimeSession session = RequireSession();
        ICommand command;
        SelectionReference selection;
        switch (Mode)
        {
            case PlacementMode.PlacingPole:
                AddPoleCommand pole = _commandFactory.CreateAddPole(
                    session.PersistenceSession.Domain,
                    session.Layout,
                    position);
                command = pole;
                selection = new SelectionReference(SelectionTargetKind.Device, pole.Pole.Id);
                break;
            case PlacementMode.PlacingRingCabinet:
                AddRingCabinetCommand cabinet = _commandFactory.CreateAddRingCabinet(
                    session.PersistenceSession.Domain,
                    session.Layout,
                    position);
                command = cabinet;
                selection = new SelectionReference(
                    SelectionTargetKind.RingCabinet,
                    cabinet.Cabinet.Id);
                break;
            default:
                return false;
        }

        session.CommandStack.ExecuteCommand(command);
        Mode = PlacementMode.Idle;
        session.RebuildScene();
        session.SelectionManager.Select(selection);
        SceneChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void RemoveSelected()
    {
        ProjectRuntimeSession session = RequireSession();
        SelectionReference selected = session.SelectionManager.Selected
            ?? throw new InvalidOperationException("No device is selected.");
        if (selected.Kind is not (SelectionTargetKind.Device or SelectionTargetKind.RingCabinet))
        {
            throw new InvalidOperationException("The selected object is not a removable device.");
        }

        ICommand command = _commandFactory.CreateRemove(
            session.PersistenceSession.Domain,
            session.Layout,
            selected.ObjectId);
        session.CommandStack.ExecuteCommand(command);
        session.SelectionManager.Clear();
        session.RebuildScene();
        SceneChanged?.Invoke(this, EventArgs.Empty);
    }

    private ProjectRuntimeSession RequireSession()
    {
        return _getSession()
            ?? throw new InvalidOperationException("No project is currently open.");
    }
}
