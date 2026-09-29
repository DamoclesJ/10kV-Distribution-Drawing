using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.Energization;

public sealed class EnergizationBoundaryPolicy
{
    public bool TryResolve(
        DrawingDocument drawing,
        EnergizedSeed seed,
        out Guid terminalId,
        out EnergizationDiagnosticCode issue)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        ArgumentNullException.ThrowIfNull(seed);
        terminalId = Guid.Empty;

        SwitchDevice? device = drawing.Devices.OfType<SwitchDevice>()
            .SingleOrDefault(candidate => candidate.Id == seed.BoundaryDeviceId);
        if (device is null)
        {
            issue = EnergizationDiagnosticCode.MissingBoundaryDevice;
            return false;
        }

        if (device.InstallationType == SwitchInstallationType.CabinetInterval)
        {
            RingCabinetInterval? interval = drawing.Devices.OfType<RingCabinet>()
                .SelectMany(cabinet => cabinet.Intervals)
                .SingleOrDefault(candidate => candidate.SwitchDevices.Any(member => member.Id == device.Id));
            if (interval is null || !IsEligibleCabinetSwitch(interval, device))
            {
                issue = EnergizationDiagnosticCode.UnsupportedBoundary;
                return false;
            }

            if (seed.Side is not (EnergizationSide.Bus or EnergizationSide.Line))
            {
                issue = EnergizationDiagnosticCode.InvalidSide;
                return false;
            }

            terminalId = seed.Side == EnergizationSide.Bus
                ? device.FirstTerminalId : device.SecondTerminalId;
        }
        else if (device.InstallationType == SwitchInstallationType.Pole)
        {
            if (device.SwitchKind is not (SwitchKind.LoadSwitch or SwitchKind.CircuitBreaker or
                SwitchKind.IsolationSwitch or SwitchKind.DropoutFuse))
            {
                issue = EnergizationDiagnosticCode.UnsupportedBoundary;
                return false;
            }

            if (seed.Side is not (EnergizationSide.SmallerNumber or EnergizationSide.LargerNumber))
            {
                issue = EnergizationDiagnosticCode.InvalidSide;
                return false;
            }

            if (!TryResolvePoleSide(drawing, device, seed.Side, out terminalId))
            {
                issue = EnergizationDiagnosticCode.UnresolvedSide;
                return false;
            }
        }
        else
        {
            issue = EnergizationDiagnosticCode.UnsupportedBoundary;
            return false;
        }

        Guid resolvedTerminalId = terminalId;
        if (!drawing.Terminals.Any(terminal => terminal.Id == resolvedTerminalId))
        {
            terminalId = Guid.Empty;
            issue = EnergizationDiagnosticCode.MissingTerminal;
            return false;
        }

        issue = default;
        return true;
    }

    private static bool IsEligibleCabinetSwitch(RingCabinetInterval interval, SwitchDevice device) =>
        interval.IntervalKind switch
        {
            IntervalKind.LoadSwitchInterval => device.SwitchKind == SwitchKind.LoadSwitch,
            IntervalKind.IntegratedFeederInterval =>
                device.SwitchKind == (interval.GroundingStructureKind ==
                    GroundingStructureKind.LowerLowerGrounding
                        ? SwitchKind.CircuitBreaker : SwitchKind.IsolationSwitch),
            _ => false
        };

    private static bool TryResolvePoleSide(
        DrawingDocument drawing,
        SwitchDevice device,
        EnergizationSide side,
        out Guid terminalId)
    {
        terminalId = Guid.Empty;
        PoleAttachment? attachment = drawing.PoleAttachments.SingleOrDefault(candidate =>
            candidate.AttachedDeviceId == device.Id);
        Pole? pole = attachment is null ? null : drawing.Devices.OfType<Pole>()
            .SingleOrDefault(candidate => candidate.Id == attachment.PoleId);
        if (pole is null) return false;

        var directions = new Dictionary<EnergizationSide, Guid>();
        foreach (Guid switchTerminalId in device.TerminalIds)
        {
            Terminal? switchTerminal = drawing.Terminals.SingleOrDefault(candidate =>
                candidate.Id == switchTerminalId);
            if (switchTerminal is null) return false;

            IEnumerable<Guid> connectedTerminalIds = switchTerminal.ElectricalNodeId is Guid nodeId
                ? (IEnumerable<Guid>?)drawing.ElectricalNodes
                    .SingleOrDefault(node => node.Id == nodeId)?.TerminalIds ?? []
                : [switchTerminalId];

            foreach (Connection connection in drawing.Connections.Where(connection =>
                connectedTerminalIds.Any(connection.UsesTerminal)))
            {
                OverheadLine? line = drawing.OverheadLines.SingleOrDefault(candidate =>
                    candidate.ConnectionId == connection.Id);
                if (line is null) continue;
                int index = line.SupportPoleIds.ToList().IndexOf(pole.Id);
                Guid? adjacentId = index == 0 && line.SupportPoleIds.Count > 1
                    ? line.SupportPoleIds[1]
                    : index == line.SupportPoleIds.Count - 1 && index > 0
                        ? line.SupportPoleIds[index - 1] : null;
                Pole? adjacent = adjacentId is Guid id ? drawing.Devices.OfType<Pole>()
                    .SingleOrDefault(candidate => candidate.Id == id) : null;
                if (adjacent is null) continue;
                PoleNumberOrder order = PoleNumberComparer.Compare(adjacent.PoleNumber, pole.PoleNumber);
                if (order is PoleNumberOrder.Unresolved or PoleNumberOrder.Equal) continue;
                EnergizationSide direction = order == PoleNumberOrder.Less
                    ? EnergizationSide.SmallerNumber : EnergizationSide.LargerNumber;
                if (directions.TryGetValue(direction, out Guid previous) &&
                    previous != switchTerminalId) return false;
                directions[direction] = switchTerminalId;
            }
        }

        if (directions.Values.Distinct().Count() != directions.Count) return false;
        return directions.TryGetValue(side, out terminalId);
    }
}
