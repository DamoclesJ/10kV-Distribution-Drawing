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

    public ProfessionalSceneBuilder(SymbolLibrary symbolLibrary)
    {
        ArgumentNullException.ThrowIfNull(symbolLibrary);
        _groundingAnchorResolver = new GroundingPresentationAnchorResolver();
        _accessPointAnchorResolver = new GroundingAccessPointAnchorResolver();
    }

    public ProfessionalSceneResult Build(
        DrawingDocument document,
        DrawingLayout drawingLayout,
        IReadOnlyDictionary<Guid, RingCabinetLayout> ringCabinetLayouts,
        IEnumerable<OrthogonalRoute> routes)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(drawingLayout);
        ArgumentNullException.ThrowIfNull(ringCabinetLayouts);
        ArgumentNullException.ThrowIfNull(routes);
        IReadOnlyDictionary<Guid, OrthogonalRoute> routeByConnectionId = routes
            .ToDictionary(route => route.ConnectionId);

        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            drawingLayout,
            ringCabinetLayouts);
        var elements = new List<SceneElement>();
        var hitTestEntries = new List<SelectionHitTestEntry>();
        var diagnostics = new List<SceneBuildDiagnostic>();

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

            IReadOnlyList<SceneElement> groundingElements = CreateGroundingPointElements(groundingPoint, anchor);
            elements.AddRange(groundingElements);
            foreach (SceneLine line in groundingElements.OfType<SceneLine>())
            {
                // The leader's target end remains available for direct GAP selection.
                DocumentPoint hitStart = line.Start;
                if (line.Start == anchor.Position)
                {
                    if (groundingPoint.Target.Kind != GroundingTargetKind.GroundingAccessPoint ||
                        line.Start.XMillimeters != line.End.XMillimeters ||
                        line.End.YMillimeters <= line.Start.YMillimeters)
                    {
                        continue;
                    }
                    double markerExclusion =
                        (DrawingMetrics.Default.Line.GroundingAccessMarkerDiameter +
                         DrawingMetrics.Default.Line.ConnectionThickness) / 2 +
                        DrawingMetrics.Default.Line.GroundingAccessHitPadding;
                    hitStart = new DocumentPoint(
                        line.Start.XMillimeters,
                        Math.Min(line.End.YMillimeters, line.Start.YMillimeters + markerExclusion));
                    if (hitStart == line.End)
                    {
                        continue;
                    }
                }
                DocumentRect bounds = SceneGeometryBounds.Expand(
                    SceneGeometryBounds.FromPoints([hitStart, line.End]),
                    DrawingMetrics.Default.Grounding.HitPadding);
                hitTestEntries.Add(new SelectionHitTestEntry(
                    new SelectionReference(SelectionTargetKind.GroundingPoint, groundingPoint.GroundingPointId),
                    bounds, 80));
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
        GroundingPresentationAnchor anchor)
    {
        DrawingMetrics metrics = DrawingMetrics.Default;
        GroundingDrawingMetrics grounding = metrics.Grounding;
        bool gapTarget = groundingPoint.Target.Kind == GroundingTargetKind.GroundingAccessPoint;
        bool verticalGap = gapTarget &&
            anchor.Direction is TerminalAnchorDirection.Up or TerminalAnchorDirection.Down;
        double leaderLength = verticalGap
            ? grounding.TopBarWidth / 2 + grounding.HitPadding
            : grounding.LeaderLength;
        DocumentPoint stemTop = gapTarget && !verticalGap
            ? anchor.Position
            : Move(
                anchor.Position,
                anchor.Direction == TerminalAnchorDirection.Left
                    ? TerminalAnchorDirection.Left : TerminalAnchorDirection.Right,
                leaderLength);
        DocumentPoint stemBottom = new(stemTop.XMillimeters,
            stemTop.YMillimeters + grounding.StemLength);
        var elements = new List<SceneElement>();
        if (stemTop != anchor.Position)
        {
            elements.Add(new SceneLine(
                anchor.Position,
                stemTop,
                Colors.DarkGreen,
                metrics.General.StandardStrokeThickness));
        }
        elements.Add(new SceneLine(
            stemTop,
            stemBottom,
            Colors.DarkGreen,
            metrics.General.StandardStrokeThickness));
        double[] widths = [grounding.TopBarWidth, grounding.MiddleBarWidth, grounding.BottomBarWidth];
        double bottomBarY = stemBottom.YMillimeters;
        for (int index = 0; index < widths.Length; index++)
        {
            double y = stemBottom.YMillimeters + index * grounding.BarSpacing;
            bottomBarY = y;
            elements.Add(new SceneLine(
                new DocumentPoint(stemBottom.XMillimeters - widths[index] / 2, y),
                new DocumentPoint(stemBottom.XMillimeters + widths[index] / 2, y),
                Colors.DarkGreen, metrics.General.StandardStrokeThickness));
        }
        if (!string.IsNullOrWhiteSpace(groundingPoint.Number))
        {
            double fontSize = metrics.Typography.GroundingPointNumberFontSize;
            elements.Add(new SceneText(new DocumentPoint(
                stemTop.XMillimeters,
                bottomBarY + grounding.BarSpacing),
                groundingPoint.Number,
                Colors.DarkGreen,
                fontSize,
                SceneTextHorizontalAlignment.Center));
        }
        return elements.Select(element => element with { TargetId = groundingPoint.GroundingPointId }).ToArray();
    }

    private static DocumentPoint Move(
        DocumentPoint start,
        TerminalAnchorDirection direction,
        double distance) => direction switch
        {
            TerminalAnchorDirection.Left => new DocumentPoint(
                start.XMillimeters - distance,
                start.YMillimeters),
            TerminalAnchorDirection.Up => new DocumentPoint(
                start.XMillimeters,
                start.YMillimeters - distance),
            TerminalAnchorDirection.Down => new DocumentPoint(
                start.XMillimeters,
                start.YMillimeters + distance),
            _ => new DocumentPoint(
                start.XMillimeters + distance,
                start.YMillimeters)
        };

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
