using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Professional;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Professional;

public sealed class ProfessionalCommandFactory
{
    private static readonly Regex StandardGroundingNumber = new(
        "^L(?<number>[0-9]{2,})$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public ICommand CreateAddGroundingPoint(
        DrawingDocument document,
        Guid terminalId,
        string location,
        string? number = null,
        string? note = null,
        Guid? groundingPointId = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!IsEligibleNewTerminalTarget(document, terminalId))
        {
            throw new InvalidOperationException(
                "Only a cable-side cable-termination or ring-cabinet cable terminal can receive a new terminal-target grounding point.");
        }

        return CreateAddGroundingPoint(
            document,
            GroundingTarget.ForTerminal(terminalId),
            location,
            number,
            note,
            groundingPointId);
    }

    public ICommand CreateAddGroundingPoint(
        DrawingDocument document,
        GroundingTarget target,
        string location,
        string? number = null,
        string? note = null,
        Guid? groundingPointId = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(target);
        if (target.Kind == GroundingTargetKind.GroundingAccessPoint)
        {
            _ = document.GetGroundingAccessPoint(target.TargetId);
        }
        else if (!IsEligibleNewTerminalTarget(document, target.TargetId))
        {
            throw new InvalidOperationException(
                "The selected terminal is not eligible for a new grounding point.");
        }

        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException(
                "Grounding point location cannot be empty.",
                nameof(location));
        }

        return new AddGroundingPointCommand(
            document,
            new GroundingPointCommandSnapshot(
                groundingPointId ?? Guid.NewGuid(),
                target,
                location.Trim(),
                NormalizeNewNumber(document, number),
                NormalizeOptional(note)));
    }

    public ICommand CreateRemoveGroundingPoint(
        DrawingDocument document,
        Guid groundingPointId)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new RemoveGroundingPointCommand(
            document,
            GroundingPointCommandSnapshot.From(
                document.GetGroundingPoint(groundingPointId)));
    }

    public AddGroundingAccessPointCommand CreateAddGroundingAccessPoint(
        DrawingDocument document,
        Guid connectionId,
        Guid poleId,
        Guid adjacentPoleId,
        GroundingAccessLineSide lineSide,
        Guid? groundingAccessPointId = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var snapshot = new GroundingAccessPointCommandSnapshot(
            groundingAccessPointId ?? Guid.NewGuid(),
            connectionId,
            poleId,
            adjacentPoleId,
            lineSide);
        return new AddGroundingAccessPointCommand(document, snapshot);
    }

    public RemoveGroundingAccessPointCommand CreateRemoveGroundingAccessPoint(
        DrawingDocument document,
        Guid groundingAccessPointId)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.GroundingPoints.Any(point =>
                point.Target == GroundingTarget.ForGroundingAccessPoint(groundingAccessPointId)))
        {
            throw new InvalidOperationException(
                "The grounding access point is occupied by a grounding point and cannot be deleted.");
        }
        return new RemoveGroundingAccessPointCommand(
            document,
            GroundingAccessPointCommandSnapshot.From(
                document.GetGroundingAccessPoint(groundingAccessPointId)));
    }

    public CompositeProfessionalCommand CreateAddGroundingAccessPointWithGroundingPoint(
        DrawingDocument document,
        Guid connectionId,
        Guid poleId,
        Guid adjacentPoleId,
        GroundingAccessLineSide lineSide,
        string location,
        string? note = null)
    {
        AddGroundingAccessPointCommand addAccessPoint = CreateAddGroundingAccessPoint(
            document,
            connectionId,
            poleId,
            adjacentPoleId,
            lineSide);
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException(
                "Grounding point location cannot be empty.",
                nameof(location));
        }
        ICommand addGroundingPoint = new AddGroundingPointCommand(
            document,
            new GroundingPointCommandSnapshot(
                Guid.NewGuid(),
                GroundingTarget.ForGroundingAccessPoint(
                    addAccessPoint.After.GroundingAccessPointId),
                location.Trim(),
                AllocateGroundingPointNumber(document),
                NormalizeOptional(note)));
        return new CompositeProfessionalCommand([addAccessPoint, addGroundingPoint]);
    }

    public ICommand CreateAddWorkScope(
        DrawingDocument document,
        BoundaryPointCommandValue startBoundary,
        BoundaryPointCommandValue endBoundary,
        string description,
        IEnumerable<Guid>? groundingPointIds = null,
        Guid? workScopeId = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(startBoundary);
        ArgumentNullException.ThrowIfNull(endBoundary);

        return new AddWorkScopeCommand(
            document,
            new WorkScopeCommandSnapshot(
                workScopeId ?? Guid.NewGuid(),
                startBoundary,
                endBoundary,
                NormalizeRequired(description, "Work scope description cannot be empty."),
                NormalizeIds(groundingPointIds)));
    }

    public ICommand CreateRemoveWorkScope(
        DrawingDocument document,
        Guid workScopeId)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new RemoveWorkScopeCommand(
            document,
            WorkScopeCommandSnapshot.From(document.GetWorkScope(workScopeId)));
    }

    /// <summary>
    /// The first WorkScope editor only changes Description and existing
    /// GroundingPointId references. Boundary values are retained verbatim;
    /// rebinding them belongs to a later explicit Pick workflow.
    /// </summary>
    public ICommand CreateChangeWorkScope(
        DrawingDocument document,
        Guid workScopeId,
        string description,
        IEnumerable<Guid>? groundingPointIds)
    {
        ArgumentNullException.ThrowIfNull(document);

        WorkScopeCommandSnapshot before =
            WorkScopeCommandSnapshot.From(document.GetWorkScope(workScopeId));
        WorkScopeCommandSnapshot after = before with
        {
            Description = NormalizeRequired(
                description,
                "Work scope description cannot be empty."),
            GroundingPointIds = NormalizeIds(groundingPointIds)
        };

        if (before.Description == after.Description &&
            before.StartBoundary == after.StartBoundary &&
            before.EndBoundary == after.EndBoundary &&
            before.GroundingPointIds.SequenceEqual(after.GroundingPointIds))
        {
            throw new InvalidOperationException("No WorkScope property has changed.");
        }

        return new ChangeWorkScopeCommand(document, before, after);
    }

    private static IReadOnlyList<Guid> NormalizeIds(IEnumerable<Guid>? ids)
    {
        return (ids ?? Array.Empty<Guid>()).ToArray();
    }

    private static string NormalizeRequired(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, nameof(value));
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeNewNumber(DrawingDocument document, string? value)
    {
        string number = string.IsNullOrWhiteSpace(value)
            ? AllocateGroundingPointNumber(document)
            : value.Trim();
        if (document.GroundingPoints.Any(point =>
                string.Equals(point.Number, number, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Grounding point number '{number}' is already in use.");
        }

        return number;
    }

    public static string AllocateGroundingPointNumber(DrawingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        HashSet<int> occupied = document.GroundingPoints
            .Select(point => point.Number)
            .OfType<string>()
            .Select(number => StandardGroundingNumber.Match(number.Trim()))
            .Where(match => match.Success)
            .Select(match => int.TryParse(
                match.Groups["number"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int value) && value > 0 ? value : 0)
            .Where(value => value > 0)
            .ToHashSet();
        int available = 1;
        while (occupied.Contains(available))
        {
            available = checked(available + 1);
        }
        return $"L{available:D2}";
    }

    public static bool IsEligibleNewTerminalTarget(
        DrawingDocument document,
        Guid terminalId)
    {
        if (terminalId == Guid.Empty)
        {
            return false;
        }

        if (document.Devices.OfType<CableTermination>().Any(device =>
                device.CableSideTerminalId == terminalId))
        {
            return true;
        }

        return document.Devices.OfType<RingCabinet>()
            .SelectMany(cabinet => cabinet.Intervals)
            .Any(interval => interval.CableTerminalId == terminalId);
    }
}
