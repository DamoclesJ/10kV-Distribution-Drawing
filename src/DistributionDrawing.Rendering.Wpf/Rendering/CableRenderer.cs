using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Application.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Labels;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;
using DistributionDrawing.Rendering.Wpf.Routing;
using System.Windows.Media;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

/// <summary>
/// Renders an existing cable segment without creating or modifying Domain objects.
/// </summary>
public sealed class CableRenderer
{
    private readonly CableSymbol _cableSymbol;
    private readonly CableLabel _cableLabel;
    private readonly LabelLayoutEngine _labelLayoutEngine;
    private readonly LineJumpDecorator _lineJumpDecorator;

    public CableRenderer(
        SymbolLibrary? symbolLibrary = null,
        LabelLayoutEngine? labelLayoutEngine = null)
    {
        var library = symbolLibrary ?? new SymbolLibrary();
        _cableSymbol = new CableSymbol(library);
        _cableLabel = new CableLabel();
        _labelLayoutEngine = labelLayoutEngine ?? new LabelLayoutEngine();
        _lineJumpDecorator = new LineJumpDecorator();
    }

    public IReadOnlyList<SceneElement> Render(
        CableSegment cableSegment,
        CableLayout layout)
    {
        return Render([(cableSegment, layout)]);
    }

    public IReadOnlyList<SceneElement> Render(
        IEnumerable<(CableSegment CableSegment, CableLayout Layout)> cables,
        IReadOnlyList<RouteIntersection>? intersections = null)
    {
        ArgumentNullException.ThrowIfNull(cables);

        (CableSegment CableSegment, CableLayout Layout)[] inputs = cables.ToArray();
        foreach ((CableSegment cableSegment, CableLayout layout) in inputs)
        {
            ArgumentNullException.ThrowIfNull(cableSegment);
            ArgumentNullException.ThrowIfNull(layout);
        }

        Dictionary<Guid, SceneText> labelsByCableId = RenderLabels(inputs)
            .ToDictionary(label => label.TargetId!.Value);

        var elements = new List<SceneElement>();
        foreach ((CableSegment cableSegment, CableLayout layout) in inputs)
        {
            DocumentRect hitTestBounds = CreateBounds(layout.Path, 2);
            IReadOnlyList<SceneElement> routeElements = intersections is null
                ? _cableSymbol.CreateElements(layout)
                : _lineJumpDecorator.Project(
                    new OrthogonalRoute(
                        cableSegment.ConnectionId,
                        ConnectionType.Cable,
                        cableSegment.StartTerminalId,
                        cableSegment.EndTerminalId,
                        layout.Path),
                    intersections,
                    Colors.Black,
                    SceneStrokeStyle.Dashed);
            elements.AddRange(routeElements.Select(element => element with
            {
                TargetKind = SelectionTargetKind.CableSegment,
                TargetId = cableSegment.Id,
                HitTestBounds = hitTestBounds
            }));
            elements.Add(labelsByCableId[cableSegment.Id]);
        }

        return elements;
    }

    public IReadOnlyList<SceneText> RenderLabels(
        IEnumerable<(CableSegment CableSegment, CableLayout Layout)> cables)
    {
        ArgumentNullException.ThrowIfNull(cables);
        (CableSegment CableSegment, CableLayout Layout)[] inputs = cables.ToArray();
        return _labelLayoutEngine
            .Layout(inputs.Select(input =>
                _cableLabel.CreateRequest(input.CableSegment, input.Layout)))
            .Select(result => _cableLabel.CreateElement(result) with
            {
                TargetKind = SelectionTargetKind.CableSegment,
                TargetId = result.TargetId
            })
            .ToArray();
    }

    private static DocumentRect CreateBounds(
        IReadOnlyList<DocumentPoint> path,
        double paddingMillimeters)
    {
        double minX = path.Min(point => point.XMillimeters) - paddingMillimeters;
        double minY = path.Min(point => point.YMillimeters) - paddingMillimeters;
        double maxX = path.Max(point => point.XMillimeters) + paddingMillimeters;
        double maxY = path.Max(point => point.YMillimeters) + paddingMillimeters;
        return new DocumentRect(minX, minY, maxX - minX, maxY - minY);
    }
}
