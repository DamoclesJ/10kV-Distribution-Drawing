using System.Windows.Media;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
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
    private readonly SymbolLibrary _symbolLibrary;
    private readonly GroundingPresentationAnchorResolver _groundingAnchorResolver;

    public ProfessionalSceneBuilder(SymbolLibrary symbolLibrary)
    {
        ArgumentNullException.ThrowIfNull(symbolLibrary);
        _symbolLibrary = symbolLibrary;
        _groundingAnchorResolver = new GroundingPresentationAnchorResolver();
    }

    public ProfessionalSceneResult Build(
        DrawingDocument document,
        DrawingLayout drawingLayout,
        IReadOnlyDictionary<Guid, RingCabinetLayout> ringCabinetLayouts)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(drawingLayout);
        ArgumentNullException.ThrowIfNull(ringCabinetLayouts);

        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            drawingLayout,
            ringCabinetLayouts);
        var elements = new List<SceneElement>();
        var hitTestEntries = new List<SelectionHitTestEntry>();
        var diagnostics = new List<SceneBuildDiagnostic>();

        foreach (GroundingPoint groundingPoint in document.GroundingPoints)
        {
            if (!_groundingAnchorResolver.TryResolve(
                    groundingPoint,
                    document,
                    drawingLayout,
                    anchors,
                    out GroundingPresentationAnchor anchor))
            {
                diagnostics.Add(new SceneBuildDiagnostic(
                    "GroundingPresentationAnchorMissing",
                    $"工作地线 '{groundingPoint.GroundingPointId}' 无法解析专业显示锚点。",
                    SelectionTargetKind.GroundingPoint,
                    groundingPoint.GroundingPointId));
                continue;
            }

            elements.AddRange(CreateGroundingPointElements(groundingPoint, anchor));
            hitTestEntries.Add(
                new SelectionHitTestEntry(
                    new SelectionReference(
                        SelectionTargetKind.GroundingPoint,
                        groundingPoint.GroundingPointId),
                    MarkerBounds(anchor.Position, 7),
                    60));
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
        DocumentPoint end = Move(
            anchor.Position,
            anchor.Direction,
            DrawingMetrics.Default.Routing.PortStubLength);
        string? label = groundingPoint.Number ?? groundingPoint.Location;
        var elements = new List<SceneElement>
        {
            new SceneRectangle(
                MarkerBounds(anchor.Position, 5),
                Colors.DarkGreen,
                0.9)
        };
        elements.AddRange(
            _symbolLibrary.CreateGroundingLine(anchor.Position, end, label));
        return elements;
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
}
