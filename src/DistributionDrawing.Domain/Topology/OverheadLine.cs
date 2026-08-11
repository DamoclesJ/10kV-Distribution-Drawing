namespace DistributionDrawing.Domain.Topology;

public sealed class OverheadLine
{
    private readonly IReadOnlyList<Guid> _supportPoleIds;

    public OverheadLine(
        Guid connectionId,
        string lineModel,
        double? lengthMeters,
        IEnumerable<Guid> supportPoleIds,
        bool isContinued = false,
        Guid? continuationTerminalId = null,
        ContinuationState? continuationState = null,
        string? continuationDescription = null)
        : this(
            connectionId,
            lineModel,
            supportPoleIds,
            isContinued,
            continuationTerminalId,
            continuationState,
            continuationDescription,
            lengthMeters)
    {
    }

    public OverheadLine(
        Guid connectionId,
        string lineModel,
        IEnumerable<Guid> supportPoleIds,
        bool isContinued = false,
        Guid? continuationTerminalId = null,
        ContinuationState? continuationState = null,
        string? continuationDescription = null,
        double? lengthMeters = null)
    {
        if (connectionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Connection ID cannot be empty.",
                nameof(connectionId));
        }

        if (string.IsNullOrWhiteSpace(lineModel))
        {
            throw new ArgumentException(
                "Overhead line model is required.",
                nameof(lineModel));
        }

        Guid[] supportPoles = supportPoleIds?.ToArray()
            ?? throw new ArgumentNullException(nameof(supportPoleIds));

        if (supportPoles.Length == 0)
        {
            throw new ArgumentException(
                "An overhead line requires at least one support pole.",
                nameof(supportPoleIds));
        }

        if (supportPoles.Any(poleId => poleId == Guid.Empty))
        {
            throw new ArgumentException(
                "Support pole IDs cannot contain empty IDs.",
                nameof(supportPoleIds));
        }

        if (supportPoles.Distinct().Count() != supportPoles.Length)
        {
            throw new ArgumentException(
                "Support pole IDs cannot contain duplicates.",
                nameof(supportPoleIds));
        }

        if (lengthMeters is double length &&
            (double.IsNaN(length) || double.IsInfinity(length) || length <= 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(lengthMeters),
                "Overhead line length must be greater than zero when specified.");
        }

        if (continuationTerminalId == Guid.Empty)
        {
            throw new ArgumentException(
                "Continuation terminal ID cannot be empty when specified.",
                nameof(continuationTerminalId));
        }

        if (continuationState is ContinuationState state && !Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(continuationState));
        }

        string? description = NormalizeOptionalText(continuationDescription);

        if (isContinued && (continuationTerminalId is null || continuationState is null))
        {
            throw new ArgumentException(
                "A continued overhead line requires a continuation terminal and state.");
        }

        if (!isContinued &&
            (continuationTerminalId is not null || continuationState is not null || description is not null))
        {
            throw new ArgumentException(
                "A non-continued overhead line cannot have continuation data.");
        }

        ConnectionId = connectionId;
        LineModel = lineModel.Trim();
        LengthMeters = lengthMeters;
        _supportPoleIds = Array.AsReadOnly(supportPoles);
        IsContinued = isContinued;
        ContinuationTerminalId = continuationTerminalId;
        ContinuationState = continuationState;
        ContinuationDescription = description;
    }

    public Guid ConnectionId { get; }

    public string LineModel { get; }

    public double? LengthMeters { get; }

    public IReadOnlyList<Guid> SupportPoleIds => _supportPoleIds;

    public bool IsContinued { get; }

    public Guid? ContinuationTerminalId { get; }

    public ContinuationState? ContinuationState { get; }

    public string? ContinuationDescription { get; }

    public void ValidateAgainst(Connection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.Id != ConnectionId)
        {
            throw new InvalidOperationException(
                "Overhead line and connection IDs must match.");
        }

        if (connection.Type != ConnectionType.OverheadLine)
        {
            throw new InvalidOperationException(
                "An overhead line detail requires an OverheadLine connection.");
        }

        if (IsContinued &&
            (ContinuationTerminalId is null || !connection.UsesTerminal(ContinuationTerminalId.Value)))
        {
            throw new InvalidOperationException(
                "The continuation terminal must be one of the connection endpoints.");
        }
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
