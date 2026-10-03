using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Professional;

public sealed record GroundingAccessPointCommandSnapshot(
    Guid GroundingAccessPointId,
    Guid ConnectionId,
    Guid PoleId,
    GroundingAdjacentEndpoint AdjacentEndpoint,
    GroundingAccessLineSide LineSide,
    GroundingAccessPlacementSide PlacementSide = GroundingAccessPlacementSide.PoleSide)
{
    public GroundingAccessPointCommandSnapshot(
        Guid groundingAccessPointId,
        Guid connectionId,
        Guid poleId,
        Guid adjacentPoleId,
        GroundingAccessLineSide lineSide)
        : this(
            groundingAccessPointId,
            connectionId,
            poleId,
            GroundingAdjacentEndpoint.ForPole(adjacentPoleId),
            lineSide,
            GroundingAccessPlacementSide.PoleSide)
    {
    }

    public Guid AdjacentPoleId => AdjacentEndpoint.Kind == GroundingAdjacentEndpointKind.Pole
        ? AdjacentEndpoint.TargetId
        : throw new InvalidOperationException("This snapshot has a terminal adjacent endpoint.");

    public static GroundingAccessPointCommandSnapshot From(GroundingAccessPoint point) => new(
        point.GroundingAccessPointId,
        point.ConnectionId,
        point.PoleId,
        point.AdjacentEndpoint,
        point.LineSide,
        point.PlacementSide);
}

public sealed class AddGroundingAccessPointCommand : IEnergizationImpactCommand
{
    public bool AffectsEnergization => false;

    private readonly DrawingDocument _document;

    public AddGroundingAccessPointCommand(
        DrawingDocument document,
        GroundingAccessPointCommandSnapshot after)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        After = after ?? throw new ArgumentNullException(nameof(after));
    }

    public GroundingAccessPointCommandSnapshot After { get; }

    public void Execute() => _document.CreateGroundingAccessPoint(
        After.GroundingAccessPointId,
        After.ConnectionId,
        After.PoleId,
        After.AdjacentEndpoint,
        After.LineSide,
        After.PlacementSide);

    public void Undo() => _document.RemoveGroundingAccessPoint(After.GroundingAccessPointId);

    public void Redo() => Execute();
}

public sealed class RemoveGroundingAccessPointCommand : IEnergizationImpactCommand
{
    public bool AffectsEnergization => false;

    private readonly DrawingDocument _document;

    public RemoveGroundingAccessPointCommand(
        DrawingDocument document,
        GroundingAccessPointCommandSnapshot before)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        Before = before ?? throw new ArgumentNullException(nameof(before));
    }

    public GroundingAccessPointCommandSnapshot Before { get; }

    public void Execute() => _document.RemoveGroundingAccessPoint(Before.GroundingAccessPointId);

    public void Undo() => _document.AddGroundingAccessPoint(new GroundingAccessPoint(
        Before.GroundingAccessPointId,
        Before.ConnectionId,
        Before.PoleId,
        Before.AdjacentEndpoint,
        Before.LineSide,
        Before.PlacementSide));

    public void Redo() => Execute();
}

public sealed class CompositeProfessionalCommand : IEnergizationImpactCommand
{
    public bool AffectsEnergization => _commands.Any(command =>
        command is not IEnergizationImpactCommand { AffectsEnergization: false });

    private readonly IReadOnlyList<ICommand> _commands;
    private readonly Action? _beforeExecute;

    public CompositeProfessionalCommand(IEnumerable<ICommand> commands, Action? beforeExecute = null)
    {
        _commands = commands?.ToArray() ?? throw new ArgumentNullException(nameof(commands));
        if (_commands.Count == 0)
        {
            throw new ArgumentException("At least one command is required.", nameof(commands));
        }
        _beforeExecute = beforeExecute;
    }

    public void Execute()
    {
        _beforeExecute?.Invoke();
        int executed = 0;
        try
        {
            foreach (ICommand command in _commands)
            {
                command.Execute();
                executed++;
            }
        }
        catch
        {
            foreach (ICommand command in _commands.Take(executed).Reverse())
            {
                command.Undo();
            }
            throw;
        }
    }

    public void Undo()
    {
        foreach (ICommand command in _commands.Reverse())
        {
            command.Undo();
        }
    }

    public void Redo() => Execute();
}
