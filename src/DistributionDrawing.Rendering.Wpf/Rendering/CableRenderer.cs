using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Application.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Labels;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

/// <summary>
/// Renders an existing cable segment without creating or modifying Domain objects.
/// </summary>
public sealed class CableRenderer
{
    private readonly CableSymbol _cableSymbol;
    private readonly CableLabel _cableLabel;
    private readonly LabelLayoutEngine _labelLayoutEngine;

    public CableRenderer(
        SymbolLibrary? symbolLibrary = null,
        LabelLayoutEngine? labelLayoutEngine = null)
    {
        var library = symbolLibrary ?? new SymbolLibrary();
        _cableSymbol = new CableSymbol(library);
        _cableLabel = new CableLabel();
        _labelLayoutEngine = labelLayoutEngine ?? new LabelLayoutEngine();
    }

    public IReadOnlyList<SceneElement> Render(
        CableSegment cableSegment,
        CableLayout layout)
    {
        return Render([(cableSegment, layout)]);
    }

    public IReadOnlyList<SceneElement> Render(
        IEnumerable<(CableSegment CableSegment, CableLayout Layout)> cables)
    {
        ArgumentNullException.ThrowIfNull(cables);

        (CableSegment CableSegment, CableLayout Layout)[] inputs = cables.ToArray();
        foreach ((CableSegment cableSegment, CableLayout layout) in inputs)
        {
            ArgumentNullException.ThrowIfNull(cableSegment);
            ArgumentNullException.ThrowIfNull(layout);
        }

        LabelLayoutResult[] labelResults = _labelLayoutEngine
            .Layout(inputs.Select(input => _cableLabel.CreateRequest(input.CableSegment, input.Layout)))
            .ToArray();
        Dictionary<Guid, LabelLayoutResult> labelsByCableId = labelResults
            .ToDictionary(result => result.TargetId);

        var elements = new List<SceneElement>();
        foreach ((CableSegment cableSegment, CableLayout layout) in inputs)
        {
            DocumentRect hitTestBounds = CreateBounds(layout.Start, layout.End, 2);
            elements.AddRange(_cableSymbol.CreateElements(layout).Select(element => element with
            {
                TargetKind = SelectionTargetKind.CableSegment,
                TargetId = cableSegment.Id,
                HitTestBounds = hitTestBounds
            }));
            elements.Add(_cableLabel.CreateElement(labelsByCableId[cableSegment.Id]));
        }

        return elements;
    }

    private static DocumentRect CreateBounds(
        DocumentPoint first,
        DocumentPoint second,
        double paddingMillimeters)
    {
        double minX = Math.Min(first.XMillimeters, second.XMillimeters) - paddingMillimeters;
        double minY = Math.Min(first.YMillimeters, second.YMillimeters) - paddingMillimeters;
        double maxX = Math.Max(first.XMillimeters, second.XMillimeters) + paddingMillimeters;
        double maxY = Math.Max(first.YMillimeters, second.YMillimeters) + paddingMillimeters;
        return new DocumentRect(minX, minY, maxX - minX, maxY - minY);
    }
}
