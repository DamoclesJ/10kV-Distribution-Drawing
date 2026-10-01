using DistributionDrawing.Application.Energization;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.WorkScopes;

public sealed record WorkScopeBoundaryAnchor(
    Guid SeedId,
    Guid BoundaryDeviceId,
    EnergizationSide SourceSide,
    EnergizationSide WorkSide,
    Guid SourceTerminalId,
    Guid WorkTerminalId,
    Guid? SourceNodeId,
    Guid? WorkNodeId);

public sealed class WorkScopeBoundaryResolver
{
    private readonly EnergizationBoundaryPolicy _policy = new();

    public bool TryResolve(DrawingDocument drawing, EnergizedSeed seed,
        out WorkScopeBoundaryAnchor? anchor)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        ArgumentNullException.ThrowIfNull(seed);
        anchor = null;
        EnergizationSide workSide = seed.Side switch
        {
            EnergizationSide.Bus => EnergizationSide.Line,
            EnergizationSide.Line => EnergizationSide.Bus,
            EnergizationSide.SmallerNumber => EnergizationSide.LargerNumber,
            EnergizationSide.LargerNumber => EnergizationSide.SmallerNumber,
            _ => throw new ArgumentOutOfRangeException(nameof(seed))
        };
        if (!_policy.TryResolve(drawing, seed, out Guid sourceId, out _) ||
            !_policy.TryResolve(drawing,
                new EnergizedSeed(seed.Id, seed.BoundaryDeviceId, workSide),
                out Guid workId, out _) || sourceId == workId)
            return false;

        SwitchDevice? device = drawing.Devices.OfType<SwitchDevice>()
            .SingleOrDefault(item => item.Id == seed.BoundaryDeviceId);
        Terminal? source = drawing.Terminals.SingleOrDefault(item => item.Id == sourceId);
        Terminal? work = drawing.Terminals.SingleOrDefault(item => item.Id == workId);
        if (device is null || source is null || work is null ||
            !device.OwnsTerminal(sourceId) || !device.OwnsTerminal(workId))
            return false;

        anchor = new WorkScopeBoundaryAnchor(seed.Id, device.Id, seed.Side, workSide,
            sourceId, workId, source.ElectricalNodeId, work.ElectricalNodeId);
        return true;
    }
}
