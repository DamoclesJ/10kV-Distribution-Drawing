using DistributionDrawing.Desktop.Selection;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Templates.RingCabinets.Building;

namespace DistributionDrawing.Desktop.RingCabinetTemplateCreation;

public sealed class RingCabinetTemplateCreationController
{
    private readonly Func<ProjectRuntimeSession?> _getSession;
    private readonly RingCabinetTemplateBuildCoordinator _buildCoordinator;
    private readonly DeviceCommandFactory _commandFactory;

    public RingCabinetTemplateCreationController(
        Func<ProjectRuntimeSession?> getSession,
        RingCabinetTemplateBuildCoordinator? buildCoordinator = null,
        DeviceCommandFactory? commandFactory = null)
    {
        _getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
        _buildCoordinator = buildCoordinator ?? new RingCabinetTemplateBuildCoordinator();
        _commandFactory = commandFactory ?? new DeviceCommandFactory();
    }

    public event EventHandler? SceneChanged;

    public RingCabinetTemplateBuildOutcome Create(
        RingCabinetTemplateBuildRequest? request)
    {
        ProjectRuntimeSession session = RequireSession();
        SelectionReference? beforeSelection = session.SelectionManager.Selected;

        RingCabinetTemplateBuildOutcome outcome = _buildCoordinator.Build(request);
        if (!outcome.IsSuccess)
        {
            return outcome;
        }

        RingCabinetTemplateBuildResult result = outcome.Result ??
            throw new InvalidOperationException(
                "A successful template build outcome must contain a result.");
        AddRingCabinetCommand command = _commandFactory.CreateAddRingCabinet(
            session.PersistenceSession.Domain,
            session.Layout,
            result.Cabinet,
            result.Layout);

        session.CommandStack.ExecuteCommand(command);
        SelectionReference afterSelection = new(
            SelectionTargetKind.RingCabinet,
            result.Cabinet.Id);
        session.SelectionTransitions.RecordExecuted(
            command,
            SelectionTransition.ForAdd(beforeSelection, afterSelection));
        session.SelectionTransitions.Prune(session.CommandStack.History);
        session.RebuildScene();
        session.SelectionManager.Select(afterSelection);
        SceneChanged?.Invoke(this, EventArgs.Empty);
        return outcome;
    }

    private ProjectRuntimeSession RequireSession()
    {
        return _getSession()
            ?? throw new InvalidOperationException("No project is currently open.");
    }
}
