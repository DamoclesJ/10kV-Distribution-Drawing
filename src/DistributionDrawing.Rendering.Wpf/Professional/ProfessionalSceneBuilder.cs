using System.Windows.Media;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Professional;

public sealed record ProfessionalSceneResult(
    IReadOnlyList<SceneElement> Elements,
    IReadOnlyList<SelectionHitTestEntry> HitTestEntries,
    IReadOnlyList<SceneBuildDiagnostic> Diagnostics);

/// <summary>
/// Projects existing Professional facts into transient scene elements and
/// hit-test entries. It never infers or changes Professional data.
/// </summary>
public sealed class ProfessionalSceneBuilder
{
    private readonly GroundingPresentationAnchorResolver _groundingAnchorResolver;
    private readonly GroundingAccessPointAnchorResolver _accessPointAnchorResolver;
    private readonly GroundingPointLayoutResolver _groundingLayoutResolver;
    private readonly GroundingLeaderCrossingDecorator _groundingCrossingDecorator;

    public ProfessionalSceneBuilder(SymbolLibrary symbolLibrary)
    {
        ArgumentNullException.ThrowIfNull(symbolLibrary);
        _groundingAnchorResolver = new GroundingPresentationAnchorResolver();
        _accessPointAnchorResolver = new GroundingAccessPointAnchorResolver();
        _groundingLayoutResolver = new GroundingPointLayoutResolver();
        _groundingCrossingDecorator = new GroundingLeaderCrossingDecorator();
    }

    public ProfessionalSceneResult Build(
        DrawingDocument document,
        DrawingLayout drawingLayout,
        IReadOnlyDictionary<Guid, RingCabinetLayout> ringCabinetLayouts,
        IReadOnlyDictionary<Guid, GroundingPointLayout> groundingPointLayouts,
        IEnumerable<OrthogonalRoute> routes)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(drawingLayout);
        ArgumentNullException.ThrowIfNull(ringCabinetLayouts);
        ArgumentNullException.ThrowIfNull(groundingPointLayouts);
        ArgumentNullException.ThrowIfNull(routes);
        OrthogonalRoute[] routeArray = routes.ToArray();
        IReadOnlyDictionary<Guid, OrthogonalRoute> routeByConnectionId = routeArray
            .ToDictionary(route => route.ConnectionId);

        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            drawingLayout,
            ringCabinetLayouts);
        var elements = new List<SceneElement>();
        var hitTestEntries = new List<SelectionHitTestEntry>();
        var diagnostics = new List<SceneBuildDiagnostic>();
        var gapMarkerBounds = new Dictionary<Guid, DocumentRect>();

        foreach (GroundingAccessPoint point in document.GroundingAccessPoints)
        {
            if (!_accessPointAnchorResolver.TryResolve(
                    point,
                    document,
                    drawingLayout,
                    routeByConnectionId,
                    out GroundingPresentationAnchor anchor))
            {
                diagnostics.Add(new SceneBuildDiagnostic(
                    "GroundingAccessPointAnchorMissing",
                    $"验电接地环 '{point.GroundingAccessPointId}' 无法解析导线半边。",
                    SelectionTargetKind.GroundingAccessPoint,
                    point.GroundingAccessPointId));
                continue;
            }

            double diameter = DrawingMetrics.Default.Line.GroundingAccessMarkerDiameter;
            DocumentRect bounds = MarkerBounds(anchor.Position, diameter);
            gapMarkerBounds[point.GroundingAccessPointId] = bounds;
            elements.Add(new SceneEllipse(
                bounds,
                Colors.Black,
                DrawingMetrics.Default.Line.ConnectionThickness,
                Colors.Black) { TargetId = point.GroundingAccessPointId });
            hitTestEntries.Add(new SelectionHitTestEntry(
                new SelectionReference(
                    SelectionTargetKind.GroundingAccessPoint,
                    point.GroundingAccessPointId),
                Expand(bounds, DrawingMetrics.Default.Line.GroundingAccessHitPadding),
                70));
        }

        foreach (GroundingPoint groundingPoint in document.GroundingPoints)
        {
            if (!_groundingAnchorResolver.TryResolve(
                    groundingPoint,
                    document,
                    drawingLayout,
                    anchors,
                    routeByConnectionId,
                    out GroundingPresentationAnchor anchor))
            {
                diagnostics.Add(new SceneBuildDiagnostic(
                    "GroundingPresentationAnchorMissing",
                    $"工作地线 '{groundingPoint.GroundingPointId}' 无法解析专业显示锚点。",
                    SelectionTargetKind.GroundingPoint,
                    groundingPoint.GroundingPointId));
                continue;
            }

            groundingPointLayouts.TryGetValue(
                groundingPoint.GroundingPointId,
                out GroundingPointLayout? manualLayout);
            GroundingPointResolvedLayout resolved = _groundingLayoutResolver.Resolve(
                groundingPoint,
                anchor,
                manualLayout);
            IReadOnlyList<SceneElement> groundingElements = CreateGroundingPointElements(
                groundingPoint,
                resolved,
                routeArray);
            elements.AddRange(groundingElements);
            SelectionReference reference = new(
                SelectionTargetKind.GroundingPoint,
                groundingPoint.GroundingPointId);
            foreach (OrthogonalRouteSegment segment in resolved.LeaderSegments)
            {
                DocumentPoint hitStart = TrimGapMarkerStart(
                    groundingPoint,
                    resolved.TargetAnchor.Position,
                    segment);
                if (hitStart == segment.End) continue;
                DocumentRect bounds = SceneGeometryBounds.Expand(
                    SceneGeometryBounds.FromPoints([hitStart, segment.End]),
                    DrawingMetrics.Default.Grounding.HitPadding);
                hitTestEntries.Add(new SelectionHitTestEntry(
                    reference,
                    bounds,
                    80,
                    hitStart,
                    segment.End,
                    CanStartDrag: false));
            }
            DocumentRect? markerExclusion =
                groundingPoint.Target.Kind == GroundingTargetKind.GroundingAccessPoint &&
                gapMarkerBounds.TryGetValue(
                    groundingPoint.Target.TargetId,
                    out DocumentRect targetMarkerBounds)
                    ? targetMarkerBounds
                    : null;
            AddGroundingBodyHitEntries(
                hitTestEntries,
                reference,
                Expand(resolved.BodyBounds, DrawingMetrics.Default.Grounding.HitPadding),
                markerExclusion,
                resolved.TargetAnchor);
            if (resolved.NumberBounds is DocumentRect numberBounds)
            {
                AddGroundingBodyHitEntries(
                    hitTestEntries,
                    reference,
                    Expand(numberBounds, DrawingMetrics.Default.Grounding.HitPadding),
                    markerExclusion,
                    resolved.TargetAnchor);
            }
        }

        foreach (WorkScope workScope in document.WorkScopes)
        {
            if (!anchors.TryGet(
                    workScope.StartBoundary.TerminalId,
                    out TerminalAnchor startAnchor) ||
                !anchors.TryGet(
                    workScope.EndBoundary.TerminalId,
                    out TerminalAnchor endAnchor))
            {
                continue;
            }

            elements.AddRange(
                CreateBoundaryElements(
                    startAnchor.Position,
                    workScope.StartBoundary.Side,
                    "起",
                    Colors.OrangeRed));
            elements.AddRange(
                CreateBoundaryElements(
                    endAnchor.Position,
                    workScope.EndBoundary.Side,
                    "止",
                    Colors.OrangeRed));

            SelectionReference target = new(
                SelectionTargetKind.WorkScope,
                workScope.WorkScopeId);
            hitTestEntries.Add(
                new SelectionHitTestEntry(
                    target,
                    MarkerBounds(startAnchor.Position, 7),
                    65));
            hitTestEntries.Add(
                new SelectionHitTestEntry(
                    target,
                    MarkerBounds(endAnchor.Position, 7),
                    65));
        }

        return new ProfessionalSceneResult(elements, hitTestEntries, diagnostics);
    }

    private IReadOnlyList<SceneElement> CreateGroundingPointElements(
        GroundingPoint groundingPoint,
        GroundingPointResolvedLayout layout,
        IEnumerable<OrthogonalRoute> electricalRoutes)
    {
        DrawingMetrics metrics = DrawingMetrics.Default;
        var elements = _groundingCrossingDecorator.Project(
                layout.LeaderSegments,
                electricalRoutes,
                Colors.DarkGreen,
                metrics.General.StandardStrokeThickness)
            .ToList();
        elements.Add(new SceneLine(
            layout.Stem.Start,
            layout.Stem.End,
            Colors.DarkGreen,
            metrics.General.StandardStrokeThickness));
        foreach (OrthogonalRouteSegment bar in layout.Bars)
        {
            elements.Add(new SceneLine(
                bar.Start,
                bar.End,
                Colors.DarkGreen, metrics.General.StandardStrokeThickness));
        }
        if (layout.NumberOrigin is DocumentPoint numberOrigin)
        {
            double fontSize = metrics.Typography.GroundingPointNumberFontSize;
            elements.Add(new SceneText(
                numberOrigin,
                groundingPoint.Number,
                Colors.DarkGreen,
                fontSize,
                SceneTextHorizontalAlignment.Center));
        }
        return elements.Select(element => element with { TargetId = groundingPoint.GroundingPointId }).ToArray();
    }

    private static DocumentPoint TrimGapMarkerStart(
        GroundingPoint groundingPoint,
        DocumentPoint anchor,
        OrthogonalRouteSegment segment)
    {
        if (groundingPoint.Target.Kind != GroundingTargetKind.GroundingAccessPoint ||
            segment.Start != anchor)
        {
            return segment.Start;
        }
        double exclusion =
            (DrawingMetrics.Default.Line.GroundingAccessMarkerDiameter +
             DrawingMetrics.Default.Line.ConnectionThickness) / 2 +
            DrawingMetrics.Default.Line.GroundingAccessHitPadding;
        double length = segment.Length;
        if (length <= exclusion) return segment.End;
        double ratio = exclusion / length;
        return new DocumentPoint(
            segment.Start.XMillimeters +
            (segment.End.XMillimeters - segment.Start.XMillimeters) * ratio,
            segment.Start.YMillimeters +
            (segment.End.YMillimeters - segment.Start.YMillimeters) * ratio);
    }

    private static void AddGroundingBodyHitEntries(
        ICollection<SelectionHitTestEntry> entries,
        SelectionReference reference,
        DocumentRect bounds,
        DocumentRect? markerExclusion,
        GroundingPresentationAnchor anchor)
    {
        foreach (DocumentRect hitBounds in Exclude(bounds, markerExclusion))
        {
            entries.Add(new SelectionHitTestEntry(
                reference,
                hitBounds,
                80,
                GroundingAnchor: anchor));
        }
    }

    private static IEnumerable<DocumentRect> Exclude(
        DocumentRect bounds,
        DocumentRect? exclusion)
    {
        if (exclusion is not DocumentRect value)
        {
            yield return bounds;
            yield break;
        }

        double left = Math.Max(bounds.XMillimeters, value.XMillimeters);
        double top = Math.Max(bounds.YMillimeters, value.YMillimeters);
        double right = Math.Min(
            bounds.XMillimeters + bounds.WidthMillimeters,
            value.XMillimeters + value.WidthMillimeters);
        double bottom = Math.Min(
            bounds.YMillimeters + bounds.HeightMillimeters,
            value.YMillimeters + value.HeightMillimeters);
        if (left >= right || top >= bottom)
        {
            yield return bounds;
            yield break;
        }

        foreach (DocumentRect remainder in new[]
                 {
                     new DocumentRect(
                         bounds.XMillimeters,
                         bounds.YMillimeters,
                         left - bounds.XMillimeters,
                         bounds.HeightMillimeters),
                     new DocumentRect(
                         right,
                         bounds.YMillimeters,
                         bounds.XMillimeters + bounds.WidthMillimeters - right,
                         bounds.HeightMillimeters),
                     new DocumentRect(left, bounds.YMillimeters, right - left,
                         top - bounds.YMillimeters),
                     new DocumentRect(left, bottom, right - left,
                         bounds.YMillimeters + bounds.HeightMillimeters - bottom)
                 }.Where(rectangle =>
                     rectangle.WidthMillimeters > 0 && rectangle.HeightMillimeters > 0))
        {
            yield return remainder;
        }
    }

    private static IReadOnlyList<SceneElement> CreateBoundaryElements(
        DocumentPoint position,
        string side,
        string role,
        Color color)
    {
        DocumentRect bounds = MarkerBounds(position, 7);
        var elements = new List<SceneElement>
        {
            new SceneRectangle(bounds, color, 1.2),
            new SceneLine(
                new DocumentPoint(position.XMillimeters, position.YMillimeters - 3),
                new DocumentPoint(position.XMillimeters, position.YMillimeters + 3),
                color,
                1.2),
            new SceneText(
                new DocumentPoint(
                    position.XMillimeters + 5,
                    position.YMillimeters - 4),
                $"{role}:{side}",
                color,
                3.2)
        };
        return elements;
    }

    private static DocumentRect MarkerBounds(DocumentPoint position, double size)
    {
        return new DocumentRect(
            position.XMillimeters - size / 2,
            position.YMillimeters - size / 2,
            size,
            size);
    }

    private static DocumentRect Expand(DocumentRect bounds, double padding)
    {
        return new DocumentRect(
            bounds.XMillimeters - padding,
            bounds.YMillimeters - padding,
            bounds.WidthMillimeters + padding * 2,
            bounds.HeightMillimeters + padding * 2);
    }
}
