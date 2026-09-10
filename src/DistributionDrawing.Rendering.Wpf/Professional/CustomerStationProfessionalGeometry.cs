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
    DocumentPoint CableContact,
    DocumentPoint StationContact,
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
                cableAnchor = new DocumentPoint(
                    bodyEntry.XMillimeters + (facesLeft ? -metrics.SwitchLength : metrics.SwitchLength),
                    bodyEntry.YMillimeters);
                DocumentPoint bladeEnd = feeder.IsolationSwitch.SwitchState == SwitchState.Closed
                    ? bodyEntry
                    : new DocumentPoint(
                        bodyEntry.XMillimeters,
                        bodyEntry.YMillimeters - metrics.SwitchOpenRise);
                double minX = Math.Min(cableAnchor.XMillimeters, bodyEntry.XMillimeters);
                switches.Add(new CustomerStationSwitchGeometry(
                    feeder.IncomingFeederId,
                    feeder.IsolationSwitch.Id,
                    feeder.CableTerminalId,
                    feeder.IsolationSwitch.SwitchState ?? throw new InvalidOperationException(
                        $"Incoming switch '{feeder.IsolationSwitch.Id}' has no state."),
                    cableAnchor,
                    bodyEntry,
                    bladeEnd,
                    direction,
                    new DocumentRect(
                        minX - metrics.HitPadding,
                        bodyEntry.YMillimeters - metrics.SwitchOpenRise - metrics.HitPadding,
                        metrics.SwitchLength + metrics.HitPadding * 2,
                        metrics.SwitchOpenRise + metrics.HitPadding * 2)));
            }

            anchors.Add(
                feeder.CableTerminalId,
                new TerminalAnchor(feeder.CableTerminalId, cableAnchor, direction));
        }

        IReadOnlyList<DocumentPoint> roof = station.StationKind == StationKind.BoxStation
            ?
            [
                new DocumentPoint(left, top),
                new DocumentPoint(layout.Position.XMillimeters, top - metrics.RoofHeight),
                new DocumentPoint(left + bodyWidth, top)
            ]
            : [];
        double boundsTop = roof.Count > 0 ? top - metrics.RoofHeight : top;
        double switchLeft = switches.Count == 0
            ? left
            : Math.Min(left, switches.Min(item => item.CableContact.XMillimeters));
        double switchRight = switches.Count == 0
            ? left + bodyWidth
            : Math.Max(left + bodyWidth, switches.Max(item => item.CableContact.XMillimeters));
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
