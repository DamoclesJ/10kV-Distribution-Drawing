using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Connections;

public sealed class OverheadLineCommandFactory
{
    private const string VoltageLevel = "10kV";
    private const string DefaultLineModel = "JKLYJ-10kV";

    public AddOverheadLineCommand CreateAdd(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        Guid startTerminalId,
        Guid endTerminalId,
        DocumentPoint start,
        DocumentPoint end)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(runtimeLayout);

        Guid connectionId = Guid.NewGuid();
        var connection = new Connection(
            connectionId,
            ConnectionType.OverheadLine,
            startTerminalId,
            endTerminalId,
            "架空线路",
            VoltageLevel);
        var overheadLine = new OverheadLine(
            connectionId,
            DefaultLineModel,
            ResolveSupportPoleIds(document, startTerminalId, endTerminalId));
        var layout = new OverheadLineLayout(connectionId, start, end);
        return new AddOverheadLineCommand(
            document,
            runtimeLayout,
            connection,
            overheadLine,
            layout);
    }

    public RemoveOverheadLineCommand CreateRemove(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        Guid connectionId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(runtimeLayout);

        Connection connection = document.Connections.SingleOrDefault(item => item.Id == connectionId)
            ?? throw new InvalidOperationException(
                $"Connection '{connectionId}' does not exist.");
        OverheadLine overheadLine = document.OverheadLines.SingleOrDefault(
                item => item.ConnectionId == connectionId)
            ?? throw new InvalidOperationException(
                $"Overhead line '{connectionId}' does not exist.");
        overheadLine.ValidateAgainst(connection);
        OverheadLineLayout layout = runtimeLayout.DrawingLayout.OverheadLines.TryGetValue(
                connectionId,
                out OverheadLineLayout? found)
            ? found
            : throw new InvalidOperationException(
                $"Overhead-line layout '{connectionId}' does not exist.");
        return new RemoveOverheadLineCommand(
            document,
            runtimeLayout,
            connection,
            overheadLine,
            layout,
            document.GroundingAccessPoints.Where(point =>
                point.ConnectionId == connectionId));
    }

    private static IReadOnlyList<Guid> ResolveSupportPoleIds(
        DrawingDocument document,
        Guid startTerminalId,
        Guid endTerminalId)
    {
        Guid? startPoleId = ResolvePhysicalPoleId(document, startTerminalId);
        Guid? endPoleId = ResolvePhysicalPoleId(document, endTerminalId);
        Guid[] poleIds = new[] { startPoleId, endPoleId }
            .Where(id => id.HasValue)
            .Select(id => id.Value)
            .Distinct()
            .ToArray();
        if (poleIds.Length == 0)
        {
            throw new InvalidOperationException(
                "An overhead line requires at least one endpoint located at a pole.");
        }

        return poleIds;
    }

    private static Guid? ResolvePhysicalPoleId(DrawingDocument document, Guid terminalId)
    {
        Terminal terminal = document.Terminals.SingleOrDefault(item => item.Id == terminalId)
            ?? throw new InvalidOperationException($"Terminal '{terminalId}' does not exist.");
        if (terminal.OwnerType != TopologyOwnerType.Device)
        {
            return null;
        }

        Device owner = document.Devices.Single(item => item.Id == terminal.OwnerId);
        if (owner is Pole pole)
        {
            return pole.Id;
        }

        if (owner is CableTermination or SwitchDevice)
        {
            return document.PoleAttachments.SingleOrDefault(
                    attachment => attachment.AttachedDeviceId == owner.Id)?.PoleId
                ?? throw new InvalidOperationException(
                    $"Device '{owner.Id}' must be attached to a pole before overhead connection.");
        }

        return null;
    }
}
