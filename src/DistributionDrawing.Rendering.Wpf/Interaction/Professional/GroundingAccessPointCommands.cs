using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Professional;

public sealed record GroundingAccessPointCommandSnapshot(
    Guid GroundingAccessPointId,
    Guid ConnectionId,
    Guid PoleId,
    Guid AdjacentPoleId,
    GroundingAccessLineSide LineSide)
{
    public static GroundingAccessPointCommandSnapshot From(GroundingAccessPoint point) => new(
        point.GroundingAccessPointId,
        point.ConnectionId,
        point.PoleId,
        point.AdjacentPoleId,
        point.LineSide);
}

public sealed class AddGroundingAccessPointCommand : ICommand
{
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
        After.AdjacentPoleId,
        After.LineSide);

    public void Undo() => _document.RemoveGroundingAccessPoint(After.GroundingAccessPointId);

    public void Redo() => Execute();
}

public sealed class RemoveGroundingAccessPointCommand : ICommand
{
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
        Before.AdjacentPoleId,
        Before.LineSide));

    public void Redo() => Execute();
}

public sealed class CompositeProfessionalCommand : ICommand
{
    private readonly IReadOnlyList<ICommand> _commands;

    public CompositeProfessionalCommand(IEnumerable<ICommand> commands)
    {
        _commands = commands?.ToArray() ?? throw new ArgumentNullException(nameof(commands));
        if (_commands.Count == 0)
        {
            throw new ArgumentException("At least one command is required.", nameof(commands));
        }
    }

    public void Execute()
    {
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
