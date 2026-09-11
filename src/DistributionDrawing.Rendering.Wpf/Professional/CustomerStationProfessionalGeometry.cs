using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Professional;

public sealed record CustomerStationUnitGeometry(
    Guid IncomingFeederId,
    int Sequence,
    string DisplayName,
    DocumentRect Body,
    IReadOnlyList<DocumentPoint> Triangle,
    DocumentPoint LabelOrigin);

public sealed record CustomerStationSwitchGeometry(
    Guid IncomingFeederId,
    Guid SwitchDeviceId,
    Guid CableTerminalId,
    SwitchState SwitchState,
    DocumentPoint CableLeadOuterEnd,
    DocumentPoint CableContact,
    DocumentPoint StationContact,
    DocumentPoint StationEntry,
    DocumentPoint BladeEnd,
    TerminalAnchorDirection CableDirection,
    DocumentRect Bounds);

public sealed record CustomerStationProfessionalGeometry(
    IReadOnlyList<CustomerStationUnitGeometry> Units,
    IReadOnlyList<CustomerStationSwitchGeometry> Switches,
    IReadOnlyList<DocumentPoint> Roof,
    IReadOnlyDictionary<Guid, TerminalAnchor> CableTerminalAnchors,
    DocumentRect Bounds)
{
    public static CustomerStationProfessionalGeometry Create(
        CustomerStation station,
        CustomerStationLayout layout,
        CustomerStationDrawingMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(station);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(metrics);
        layout.ValidateFor(station);

        IncomingFeeder[] feeders = station.IncomingFeeders
            .OrderBy(feeder => feeder.Sequence)
            .ToArray();
        double bodyWidth = feeders.Length * metrics.UnitWidth +
            (feeders.Length - 1) * metrics.UnitSpacing;
        double left = layout.Position.XMillimeters - bodyWidth / 2;
        double top = layout.Position.YMillimeters - metrics.UnitHeight / 2;
        var units = new List<CustomerStationUnitGeometry>();
        var switches = new List<CustomerStationSwitchGeometry>();
        var anchors = new Dictionary<Guid, TerminalAnchor>();

        foreach (IncomingFeeder feeder in feeders)
        {
            int index = feeder.Sequence - 1;
            var body = new DocumentRect(
                left + index * (metrics.UnitWidth + metrics.UnitSpacing),
                top,
                metrics.UnitWidth,
                metrics.UnitHeight);
            DocumentPoint center = new(
                body.XMillimeters + body.WidthMillimeters / 2,
                body.YMillimeters + body.HeightMillimeters / 2);
            DocumentPoint[] triangle =
            [
                new DocumentPoint(center.XMillimeters, center.YMillimeters - metrics.TriangleHalfHeight),
                new DocumentPoint(center.XMillimeters - metrics.TriangleHalfWidth, center.YMillimeters + metrics.TriangleHalfHeight),
                new DocumentPoint(center.XMillimeters + metrics.TriangleHalfWidth, center.YMillimeters + metrics.TriangleHalfHeight)
            ];
            units.Add(new CustomerStationUnitGeometry(
                feeder.IncomingFeederId,
                feeder.Sequence,
                feeder.DisplayName,
                body,
                triangle,
                new DocumentPoint(
                    center.XMillimeters,
                    body.YMillimeters + body.HeightMillimeters + metrics.LabelOffset)));

            bool facesLeft = feeders.Length == 1 || feeder.Sequence == 1;
            TerminalAnchorDirection direction = facesLeft
                ? TerminalAnchorDirection.Left
                : TerminalAnchorDirection.Right;
            DocumentPoint bodyEntry = new(
                facesLeft ? body.XMillimeters : body.XMillimeters + body.WidthMillimeters,
                center.YMillimeters);
            CustomerStationIncomingFeederLayout feederLayout =
                layout.IncomingFeeders[feeder.IncomingFeederId];
            DocumentPoint cableAnchor = bodyEntry;
            if (feederLayout.ShowIncomingSwitch)
            {
                double outward = facesLeft ? -1 : 1;
                DocumentPoint stationContact = new(
                    bodyEntry.XMillimeters + outward * metrics.IncomingSwitchLeadLength,
                    bodyEntry.YMillimeters);
                DocumentPoint cableContact = new(
                    stationContact.XMillimeters + outward * metrics.SwitchLength,
                    bodyEntry.YMillimeters);
                cableAnchor = new DocumentPoint(
                    cableContact.XMillimeters + outward * metrics.IncomingSwitchLeadLength,
                    bodyEntry.YMillimeters);
                DocumentPoint bladeEnd = feeder.IsolationSwitch.SwitchState == SwitchState.Closed
                    ? stationContact
                    : new DocumentPoint(
                        stationContact.XMillimeters,
                        bodyEntry.YMillimeters - metrics.SwitchOpenRise);
                double minX = Math.Min(cableAnchor.XMillimeters, bodyEntry.XMillimeters);
                double maxX = Math.Max(cableAnchor.XMillimeters, bodyEntry.XMillimeters);
                switches.Add(new CustomerStationSwitchGeometry(
                    feeder.IncomingFeederId,
                    feeder.IsolationSwitch.Id,
                    feeder.CableTerminalId,
                    feeder.IsolationSwitch.SwitchState ?? throw new InvalidOperationException(
                        $"Incoming switch '{feeder.IsolationSwitch.Id}' has no state."),
                    cableAnchor,
                    cableContact,
                    stationContact,
                    bodyEntry,
                    bladeEnd,
                    direction,
                    new DocumentRect(
                        minX - metrics.HitPadding,
                        bodyEntry.YMillimeters - metrics.SwitchOpenRise - metrics.HitPadding,
                        maxX - minX + metrics.HitPadding * 2,
                        metrics.SwitchOpenRise + metrics.HitPadding * 2)));
            }

            anchors.Add(
                feeder.CableTerminalId,
                new TerminalAnchor(feeder.CableTerminalId, cableAnchor, direction));
        }

        double roofSlope = metrics.RoofHeight / (bodyWidth / 2);
        double eaveDrop = metrics.RoofOverhang * roofSlope;
        IReadOnlyList<DocumentPoint> roof = station.StationKind == StationKind.BoxStation
            ?
            [
                new DocumentPoint(left - metrics.RoofOverhang, top + eaveDrop),
                new DocumentPoint(layout.Position.XMillimeters, top - metrics.RoofHeight),
                new DocumentPoint(
                    left + bodyWidth + metrics.RoofOverhang,
                    top + eaveDrop)
            ]
            : [];
        double boundsTop = roof.Count > 0 ? top - metrics.RoofHeight : top;
        double roofLeft = roof.Count == 0 ? left : roof.Min(item => item.XMillimeters);
        double roofRight = roof.Count == 0
            ? left + bodyWidth
            : roof.Max(item => item.XMillimeters);
        double switchLeft = switches.Count == 0
            ? roofLeft
            : Math.Min(roofLeft, switches.Min(item => item.CableLeadOuterEnd.XMillimeters));
        double switchRight = switches.Count == 0
            ? roofRight
            : Math.Max(roofRight, switches.Max(item => item.CableLeadOuterEnd.XMillimeters));
        return new CustomerStationProfessionalGeometry(
            units,
            switches,
            roof,
            anchors,
            new DocumentRect(
                switchLeft,
                boundsTop,
                switchRight - switchLeft,
                metrics.UnitHeight + (top - boundsTop) + metrics.LabelOffset));
    }
}
