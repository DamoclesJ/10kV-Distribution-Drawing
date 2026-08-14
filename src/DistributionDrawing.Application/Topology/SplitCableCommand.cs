using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.Topology;

public sealed class SplitCableCommand
{
    private readonly DrawingDocument _document;
    private readonly Guid _cableSegmentId;
    private readonly string _intermediateDisplayName;

    public SplitCableCommand(
        DrawingDocument document,
        Guid cableSegmentId,
        string intermediateDisplayName)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        if (cableSegmentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Cable segment ID cannot be empty.",
                nameof(cableSegmentId));
        }

        if (string.IsNullOrWhiteSpace(intermediateDisplayName))
        {
            throw new ArgumentException(
                "Intermediate terminal display name is required.",
                nameof(intermediateDisplayName));
        }

        _cableSegmentId = cableSegmentId;
        _intermediateDisplayName = intermediateDisplayName.Trim();
    }

    public CableSplitResult? Result { get; private set; }

    public void Execute()
    {
        if (Result is not null)
        {
            ApplySplit(Result);
            return;
        }

        CableSegment originalCableSegment = _document.CableSegments
            .SingleOrDefault(segment => segment.Id == _cableSegmentId)
            ?? throw new InvalidOperationException(
                $"Cable segment '{_cableSegmentId}' does not exist.");
        Connection originalConnection = _document.Connections
            .SingleOrDefault(connection => connection.Id == originalCableSegment.ConnectionId)
            ?? throw new InvalidOperationException(
                $"Cable segment '{_cableSegmentId}' connection is missing.");

        ValidateOriginalPair(originalCableSegment, originalConnection);

        IntermediateTerminalCreationResult intermediateTerminal =
            new IntermediateTerminalCreationFactory().Create(_intermediateDisplayName);
        Guid firstConnectionId = Guid.NewGuid();
        Guid secondConnectionId = Guid.NewGuid();
        Guid firstCableSegmentId = Guid.NewGuid();
        Guid secondCableSegmentId = Guid.NewGuid();
        string firstName = $"{originalCableSegment.Name}-1";
        string secondName = $"{originalCableSegment.Name}-2";

        var firstConnection = new Connection(
            firstConnectionId,
            ConnectionType.Cable,
            originalCableSegment.StartTerminalId,
            intermediateTerminal.Terminal.Id,
            firstName,
            originalCableSegment.VoltageLevel);
        var secondConnection = new Connection(
            secondConnectionId,
            ConnectionType.Cable,
            intermediateTerminal.Terminal.Id,
            originalCableSegment.EndTerminalId,
            secondName,
            originalCableSegment.VoltageLevel);
        var firstCableSegment = new CableSegment(
            firstCableSegmentId,
            firstName,
            originalCableSegment.CableType,
            originalCableSegment.Length,
            originalCableSegment.VoltageLevel,
            firstConnectionId,
            originalCableSegment.StartTerminalId,
            intermediateTerminal.Terminal.Id);
        var secondCableSegment = new CableSegment(
            secondCableSegmentId,
            secondName,
            originalCableSegment.CableType,
            originalCableSegment.Length,
            originalCableSegment.VoltageLevel,
            secondConnectionId,
            intermediateTerminal.Terminal.Id,
            originalCableSegment.EndTerminalId);

        Result = new CableSplitResult(
            originalCableSegment,
            originalConnection,
            intermediateTerminal,
            firstCableSegment,
            firstConnection,
            secondCableSegment,
            secondConnection);

        try
        {
            ApplySplit(Result);
        }
        catch
        {
            Result = null;
            throw;
        }
    }

    public void Undo()
    {
        CableSplitResult result = Result
            ?? throw new InvalidOperationException(
                "The cable split has not been executed.");

        RemoveSplitObjects(result);
        _document.AddCableSegment(
            result.OriginalCableSegment,
            result.OriginalConnection);
    }

    public void Redo()
    {
        CableSplitResult result = Result
            ?? throw new InvalidOperationException(
                "The cable split has not been executed.");
        ApplySplit(result);
    }

    private void ApplySplit(CableSplitResult result)
    {
        bool originalRemoved = false;
        try
        {
            _document.RemoveCableSegment(result.OriginalCableSegment.Id);
            originalRemoved = true;
            _document.AddIntermediateTerminal(
                result.IntermediateTerminal.IntermediateTerminal,
                result.IntermediateTerminal.Terminal);
            _document.AddCableSegment(
                result.FirstCableSegment,
                result.FirstConnection);
            _document.AddCableSegment(
                result.SecondCableSegment,
                result.SecondConnection);
        }
        catch
        {
            RollbackSplitObjects(result);
            if (originalRemoved && !_document.CableSegments.Any(segment =>
                    segment.Id == result.OriginalCableSegment.Id))
            {
                _document.AddCableSegment(
                    result.OriginalCableSegment,
                    result.OriginalConnection);
            }

            throw;
        }
    }

    private void RemoveSplitObjects(CableSplitResult result)
    {
        if (_document.CableSegments.Any(segment => segment.Id == result.SecondCableSegment.Id))
        {
            _document.RemoveCableSegment(result.SecondCableSegment.Id);
        }

        if (_document.CableSegments.Any(segment => segment.Id == result.FirstCableSegment.Id))
        {
            _document.RemoveCableSegment(result.FirstCableSegment.Id);
        }

        if (_document.FindIntermediateTerminal(
                result.IntermediateTerminal.IntermediateTerminal.Id) is not null)
        {
            _document.RemoveIntermediateTerminal(
                result.IntermediateTerminal.IntermediateTerminal.Id);
        }
    }

    private void RollbackSplitObjects(CableSplitResult result)
    {
        if (_document.CableSegments.Any(segment => segment.Id == result.SecondCableSegment.Id))
        {
            _document.RemoveCableSegment(result.SecondCableSegment.Id);
        }

        if (_document.CableSegments.Any(segment => segment.Id == result.FirstCableSegment.Id))
        {
            _document.RemoveCableSegment(result.FirstCableSegment.Id);
        }

        if (_document.FindIntermediateTerminal(
                result.IntermediateTerminal.IntermediateTerminal.Id) is not null)
        {
            _document.RemoveIntermediateTerminal(
                result.IntermediateTerminal.IntermediateTerminal.Id);
        }
    }

    private static void ValidateOriginalPair(
        CableSegment cableSegment,
        Connection connection)
    {
        if (connection.Type != ConnectionType.Cable ||
            connection.StartTerminalId != cableSegment.StartTerminalId ||
            connection.EndTerminalId != cableSegment.EndTerminalId ||
            !string.Equals(
                connection.VoltageLevel,
                cableSegment.VoltageLevel,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cable segment '{cableSegment.Id}' and its connection are inconsistent.");
        }
    }
}
