using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Desktop.WorkScopeCreation;

public static class WorkScopeBoundaryTerminalEligibility
{
    public static bool IsEligible(DrawingDocument document, Guid terminalId)
    {
        ArgumentNullException.ThrowIfNull(document);

        Terminal? terminal = document.Terminals.SingleOrDefault(candidate =>
            candidate.Id == terminalId);
        if (terminal is null)
        {
            return false;
        }

        return terminal.OwnerType != TopologyOwnerType.Device ||
               !document.Transformers.Any(transformer =>
                   transformer.Id == terminal.OwnerId &&
                   transformer.HvTerminalId == terminal.Id);
    }
}
