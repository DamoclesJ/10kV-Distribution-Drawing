using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.Topology;

public sealed class CableSegmentCreationFactory
{
    public CableSegmentCreationResult Create(
        DrawingDocument document,
        Guid startTerminalId,
        Guid endTerminalId,
        string name,
        string cableType,
        double length,
        string voltageLevel = "10kV")
    {
        ArgumentNullException.ThrowIfNull(document);

        if (startTerminalId == Guid.Empty || endTerminalId == Guid.Empty)
        {
            throw new ArgumentException("Cable segment terminal IDs are required.");
        }

        if (startTerminalId == endTerminalId)
        {
            throw new ArgumentException(
                "A cable segment requires two different terminals.");
        }

        if (!document.Terminals.Any(terminal => terminal.Id == startTerminalId) ||
            !document.Terminals.Any(terminal => terminal.Id == endTerminalId))
        {
            throw new InvalidOperationException(
                "Cable segment terminals must already exist in the document.");
        }

        Guid connectionId = Guid.NewGuid();
        var connection = new Connection(
            connectionId,
            ConnectionType.Cable,
            startTerminalId,
            endTerminalId,
            name,
            voltageLevel);
        var cableSegment = new CableSegment(
            Guid.NewGuid(),
            name,
            cableType,
            length,
            voltageLevel,
            connectionId,
            startTerminalId,
            endTerminalId);

        return new CableSegmentCreationResult(cableSegment, connection);
    }
}
