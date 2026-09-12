using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;

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
            return ResolveGroundingAccessPoint(
                document,
                point.PoleId,
                point.AdjacentEndpoint,
                point.LineSide,
                point.PlacementSide);
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

        IncomingFeeder? feeder = document.CustomerStations
            .SelectMany(station => station.IncomingFeeders)
            .SingleOrDefault(item => item.CableTerminalId == target.TargetId);
        if (feeder is not null)
        {
            return $"{feeder.DisplayName}进线";
        }

        if (document.Transformers.Any(transformer =>
                transformer.HvTerminalId == target.TargetId))
        {
            return "变压器高压侧";
        }

        CableTermination? termination = document.Devices.OfType<CableTermination>()
            .SingleOrDefault(item => item.CableSideTerminalId == target.TargetId);
        if (termination is not null)
        {
            Guid? poleId = document.PoleAttachments.SingleOrDefault(attachment =>
                attachment.AttachedDeviceId == termination.Id)?.PoleId;
            Pole? pole = poleId is Guid ownerPoleId
                ? document.Devices.OfType<Pole>().SingleOrDefault(item => item.Id == ownerPoleId)
                : null;
            return pole is not null && !string.IsNullOrWhiteSpace(pole.PoleNumber)
                ? $"{pole.PoleNumber.Trim()}杆电缆终端"
                : "电缆终端";
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
            GroundingAccessLineSide.TransformerSide => "变压器侧",
            _ => throw new ArgumentOutOfRangeException(nameof(lineSide))
        };
        return $"{pole.PoleNumber}杆{side}";
    }

    public static string ResolveGroundingAccessPoint(
        DrawingDocument document,
        Guid poleId,
        GroundingAdjacentEndpoint adjacentEndpoint,
        GroundingAccessLineSide lineSide,
        GroundingAccessPlacementSide placementSide)
    {
        ArgumentNullException.ThrowIfNull(document);
        Pole pole = document.Devices.OfType<Pole>().Single(item => item.Id == poleId);
        if (adjacentEndpoint.Kind == GroundingAdjacentEndpointKind.Terminal)
        {
            return placementSide == GroundingAccessPlacementSide.AdjacentEndpointSide
                ? "变压器高压侧导线"
                : $"{pole.PoleNumber}杆变压器侧";
        }
        return ResolveGroundingAccessPoint(document, poleId, lineSide);
    }
}
