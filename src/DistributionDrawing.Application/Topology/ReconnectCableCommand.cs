using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.Topology;

public sealed class ReconnectCableCommand
{
    private readonly DrawingDocument _document;
    private readonly Guid _cableSegmentId;
    private readonly Guid _newStartTerminalId;
    private readonly Guid _newEndTerminalId;

    public ReconnectCableCommand(
        DrawingDocument document,
        Guid cableSegmentId,
        Guid newStartTerminalId,
        Guid newEndTerminalId)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        if (cableSegmentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Cable segment ID cannot be empty.",
                nameof(cableSegmentId));
        }

        if (newStartTerminalId == Guid.Empty)
        {
            throw new ArgumentException(
                "New start terminal ID cannot be empty.",
                nameof(newStartTerminalId));
        }

        if (newEndTerminalId == Guid.Empty)
        {
            throw new ArgumentException(
                "New end terminal ID cannot be empty.",
                nameof(newEndTerminalId));
        }

        if (newStartTerminalId == newEndTerminalId)
        {
            throw new ArgumentException(
                "Reconnect requires two different terminals.");
        }

        _cableSegmentId = cableSegmentId;
        _newStartTerminalId = newStartTerminalId;
        _newEndTerminalId = newEndTerminalId;
    }

    public CableReconnectResult? Result { get; private set; }

    public void Execute()
    {
        if (Result is not null)
        {
            Apply(Result);
            return;
        }

        CableSegment beforeCableSegment = _document.CableSegments
            .SingleOrDefault(segment => segment.Id == _cableSegmentId)
            ?? throw new InvalidOperationException(
                $"Cable segment '{_cableSegmentId}' does not exist.");
        Connection beforeConnection = _document.Connections
            .SingleOrDefault(connection => connection.Id == beforeCableSegment.ConnectionId)
            ?? throw new InvalidOperationException(
                $"Cable segment '{_cableSegmentId}' connection is missing.");

        if (!_document.Terminals.Any(terminal => terminal.Id == _newStartTerminalId) ||
            !_document.Terminals.Any(terminal => terminal.Id == _newEndTerminalId))
        {
            throw new InvalidOperationException(
                "Reconnect terminals must already exist in the document.");
        }

        if (_newStartTerminalId == _newEndTerminalId)
        {
            throw new InvalidOperationException(
                "Reconnect requires two different terminals.");
        }

        var afterConnection = new Connection(
            beforeConnection.Id,
            ConnectionType.Cable,
            _newStartTerminalId,
            _newEndTerminalId,
            beforeConnection.DisplayName,
            beforeConnection.VoltageLevel);
        var afterCableSegment = new CableSegment(
            beforeCableSegment.Id,
            beforeCableSegment.Name,
            beforeCableSegment.CableType,
            beforeCableSegment.Length,
            beforeCableSegment.VoltageLevel,
            beforeConnection.Id,
            _newStartTerminalId,
            _newEndTerminalId);

        Result = new CableReconnectResult(
            beforeCableSegment,
            beforeConnection,
            afterCableSegment,
            afterConnection);

        try
        {
            Apply(Result);
        }
        catch
        {
            Result = null;
            throw;
        }
    }

    public void Undo()
    {
        CableReconnectResult result = Result
            ?? throw new InvalidOperationException(
                "The cable reconnect has not been executed.");
        _document.ReplaceCableSegmentConnection(
            result.AfterCableSegment,
            result.AfterConnection,
            result.BeforeCableSegment,
            result.BeforeConnection);
    }

    public void Redo()
    {
        CableReconnectResult result = Result
            ?? throw new InvalidOperationException(
                "The cable reconnect has not been executed.");
        Apply(result);
    }

    private void Apply(CableReconnectResult result)
    {
        _document.ReplaceCableSegmentConnection(
            result.BeforeCableSegment,
            result.BeforeConnection,
            result.AfterCableSegment,
            result.AfterConnection);
    }
}
