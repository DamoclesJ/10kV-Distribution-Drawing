using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Professional;

public static class GroundingPointLocationResolver
{
    public static string ResolveDefault(
        DrawingDocument document,
        GroundingTarget target)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(target);

        if (target.Kind == GroundingTargetKind.GroundingAccessPoint)
        {
            GroundingAccessPoint point = document.GetGroundingAccessPoint(target.TargetId);
            return ResolveGroundingAccessPoint(document, point.PoleId, point.LineSide);
        }

        RingCabinet? cabinet = document.Devices.OfType<RingCabinet>()
            .SingleOrDefault(item => item.Intervals.Any(interval =>
                interval.CableTerminalId == target.TargetId));
        if (cabinet is not null)
        {
            RingCabinetInterval interval = cabinet.Intervals.Single(item =>
                item.CableTerminalId == target.TargetId);
            return $"{cabinet.DisplayName ?? "环网柜"}{interval.DisplayName}间隔";
        }

        CableTermination? termination = document.Devices.OfType<CableTermination>()
            .SingleOrDefault(item => item.CableSideTerminalId == target.TargetId);
        if (termination is not null)
        {
            (RingCabinet Cabinet, RingCabinetInterval Interval)[] connectedIntervals =
                document.Connections
                    .Where(connection => connection.Type == ConnectionType.Cable &&
                        connection.UsesTerminal(target.TargetId))
                    .Select(connection => connection.StartTerminalId == target.TargetId
                        ? connection.EndTerminalId
                        : connection.StartTerminalId)
                    .SelectMany(terminalId => document.Devices.OfType<RingCabinet>()
                        .SelectMany(owner => owner.Intervals
                            .Where(interval => interval.CableTerminalId == terminalId)
                            .Select(interval => (owner, interval))))
                    .ToArray();
            if (connectedIntervals.Length == 1)
            {
                (RingCabinet owner, RingCabinetInterval interval) = connectedIntervals[0];
                return $"{owner.DisplayName ?? "环网柜"}{interval.DisplayName}间隔";
            }

            if (!string.IsNullOrWhiteSpace(termination.DisplayName))
            {
                return termination.DisplayName;
            }

            Guid? poleId = document.PoleAttachments.SingleOrDefault(attachment =>
                attachment.AttachedDeviceId == termination.Id)?.PoleId;
            Pole? pole = poleId is Guid ownerPoleId
                ? document.Devices.OfType<Pole>().SingleOrDefault(item => item.Id == ownerPoleId)
                : null;
            return pole is null ? "电缆终端" : $"{pole.PoleNumber}杆电缆终端";
        }

        throw new InvalidOperationException(
            "The selected target has no supported grounding-point location.");
    }

    public static string ResolveGroundingAccessPoint(
        DrawingDocument document,
        Guid poleId,
        GroundingAccessLineSide lineSide)
    {
        ArgumentNullException.ThrowIfNull(document);
        Pole pole = document.Devices.OfType<Pole>().Single(item => item.Id == poleId);
        string side = lineSide switch
        {
            GroundingAccessLineSide.SmallerNumberSide => "小号侧",
            GroundingAccessLineSide.LargerNumberSide => "大号侧",
            _ => throw new ArgumentOutOfRangeException(nameof(lineSide))
        };
        return $"{pole.PoleNumber}杆{side}";
    }
}
