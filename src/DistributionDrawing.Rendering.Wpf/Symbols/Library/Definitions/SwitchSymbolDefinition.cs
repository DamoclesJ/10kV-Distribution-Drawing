using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Symbols.Library.Definitions;

public sealed class SwitchSymbolDefinition : ISymbolDefinition
{
    private readonly DrawingMetrics _metrics;

    public SwitchSymbolDefinition(SymbolKind kind, DrawingMetrics? metrics = null)
    {
        if (kind is not SymbolKind.CircuitBreaker and
            not SymbolKind.LoadSwitch and
            not SymbolKind.IsolationSwitch and
            not SymbolKind.GroundSwitch and
            not SymbolKind.DropoutFuse)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        Kind = kind;
        _metrics = metrics ?? DrawingMetrics.Default;
    }

    public SymbolKind Kind { get; }

    public IReadOnlyList<SceneElement> Create(SymbolRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var elements = new List<SceneElement>
        {
            new SceneLogicalBounds(new DocumentRect(
                context.Origin.XMillimeters,
                context.Origin.YMillimeters,
                context.WidthMillimeters,
                context.HeightMillimeters))
        };

        switch (Kind)
        {
            case SymbolKind.CircuitBreaker:
                CreateCircuitBreaker(context, elements);
                break;
            case SymbolKind.LoadSwitch:
                CreateLoadSwitch(context, elements);
                break;
            case SymbolKind.IsolationSwitch:
            case SymbolKind.GroundSwitch:
                CreateIsolationSwitch(context, elements);
                break;
            case SymbolKind.DropoutFuse:
                CreateDropoutFuse(context, elements);
                break;
        }

        AddText(context, elements);
        return elements;
    }

    private void CreateCircuitBreaker(SymbolRenderContext context, ICollection<SceneElement> elements)
    {
        double x = context.Origin.XMillimeters;
        double y = context.Origin.YMillimeters;
        double width = context.WidthMillimeters;
        double height = context.HeightMillimeters;
        double centerY = y + height / 2;
        double inset = Math.Min(_metrics.PoleAttachment.InternalInset, width / 5);
        elements.Add(new SceneRectangle(new DocumentRect(x, y, width, height), context.Stroke, context.ThicknessMillimeters, context.Fill));
        elements.Add(Line(context, new DocumentPoint(x, centerY), new DocumentPoint(x + inset, centerY)));
        elements.Add(Line(context, new DocumentPoint(x + width - inset, centerY), new DocumentPoint(x + width, centerY)));
        DocumentPoint bladeEnd = context.State == SymbolVisualState.Open
            ? new DocumentPoint(x + width - inset, y + inset)
            : new DocumentPoint(x + width - inset, centerY);
        elements.Add(Line(context, new DocumentPoint(x + inset, centerY), bladeEnd));
        double contactX = x + width - inset;
        double crossHalfSize = _metrics.PoleAttachment.ContactCrossSize / 2;
        elements.Add(Line(context, new DocumentPoint(contactX - crossHalfSize, centerY - crossHalfSize), new DocumentPoint(contactX + crossHalfSize, centerY + crossHalfSize)));
        elements.Add(Line(context, new DocumentPoint(contactX - crossHalfSize, centerY + crossHalfSize), new DocumentPoint(contactX + crossHalfSize, centerY - crossHalfSize)));
    }

    private void CreateLoadSwitch(SymbolRenderContext context, ICollection<SceneElement> elements)
    {
        double x = context.Origin.XMillimeters;
        double y = context.Origin.YMillimeters;
        double width = context.WidthMillimeters;
        double height = context.HeightMillimeters;
        double centerY = y + height / 2;
        double inset = Math.Min(_metrics.PoleAttachment.InternalInset, width / 5);
        double contactX = x + width - inset;
        elements.Add(new SceneRectangle(new DocumentRect(x, y, width, height), context.Stroke, context.ThicknessMillimeters, context.Fill));
        elements.Add(Line(context, new DocumentPoint(x, centerY), new DocumentPoint(x + inset, centerY)));
        elements.Add(Line(context, new DocumentPoint(contactX, centerY), new DocumentPoint(x + width, centerY)));
        elements.Add(new SceneEllipse(
            new DocumentRect(contactX - _metrics.Switch.ContactRadius, centerY - _metrics.Switch.ContactRadius, _metrics.Switch.ContactRadius * 2, _metrics.Switch.ContactRadius * 2),
            context.Stroke,
            context.ThicknessMillimeters));
        DocumentPoint bladeEnd = context.State == SymbolVisualState.Open
            ? new DocumentPoint(contactX - 1, y + inset)
            : new DocumentPoint(contactX, centerY);
        elements.Add(Line(context, new DocumentPoint(x + inset, centerY), bladeEnd));
        double markerHalfLength = _metrics.PoleAttachment.ContactMarkerLength / 2;
        elements.Add(Line(context, new DocumentPoint(contactX + _metrics.Switch.ContactRadius + 1, centerY - markerHalfLength), new DocumentPoint(contactX + _metrics.Switch.ContactRadius + 1, centerY + markerHalfLength)));
    }

    private void CreateIsolationSwitch(SymbolRenderContext context, ICollection<SceneElement> elements)
    {
        double x = context.Origin.XMillimeters;
        double y = context.Origin.YMillimeters;
        double width = context.WidthMillimeters;
        double height = context.HeightMillimeters;
        double centerY = y + height / 2;
        double contactX = x + width * _metrics.PoleAttachment.IsolationContactRatio;
        DocumentPoint bladeStart = new(x + width * _metrics.PoleAttachment.IsolationBladeStartRatio, centerY);
        elements.Add(new SceneRectangle(
            new DocumentRect(x, y, width, height),
            context.Stroke,
            context.ThicknessMillimeters,
            context.Fill));
        elements.Add(Line(context, new DocumentPoint(x, centerY), bladeStart));
        elements.Add(Line(context, new DocumentPoint(contactX, centerY), new DocumentPoint(x + width, centerY)));
        elements.Add(Line(context, bladeStart, context.State == SymbolVisualState.Open
            ? new DocumentPoint(contactX, y + height * _metrics.PoleAttachment.OpenBladeTopRatio)
            : new DocumentPoint(contactX, centerY)));
        double markerHalfLength = _metrics.PoleAttachment.ContactMarkerLength / 2;
        elements.Add(Line(context, new DocumentPoint(contactX, centerY - markerHalfLength), new DocumentPoint(contactX, centerY + markerHalfLength)));
    }

    private void CreateDropoutFuse(SymbolRenderContext context, ICollection<SceneElement> elements)
    {
        double x = context.Origin.XMillimeters;
        double y = context.Origin.YMillimeters;
        double width = context.WidthMillimeters;
        double height = context.HeightMillimeters;
        double centerX = x + width / 2;
        double inset = Math.Min(_metrics.PoleAttachment.FuseTubeInset, height / 5);
        DocumentPoint tubeTop = context.State == SymbolVisualState.Open
            ? new DocumentPoint(centerX - _metrics.PoleAttachment.FuseOpenOffset, y + inset)
            : new DocumentPoint(centerX, y + inset);
        DocumentPoint tubeBottom = new(centerX, y + height - inset);
        elements.Add(Line(context, new DocumentPoint(centerX, y), new DocumentPoint(centerX, y + inset)));
        elements.Add(Line(context, tubeBottom, new DocumentPoint(centerX, y + height)));
        double halfTubeWidth = _metrics.PoleAttachment.FuseTubeWidth / 2;
        elements.Add(new ScenePolyline(
            [
                new DocumentPoint(tubeTop.XMillimeters - halfTubeWidth, tubeTop.YMillimeters),
                new DocumentPoint(tubeTop.XMillimeters + halfTubeWidth, tubeTop.YMillimeters),
                new DocumentPoint(tubeBottom.XMillimeters + halfTubeWidth, tubeBottom.YMillimeters),
                new DocumentPoint(tubeBottom.XMillimeters - halfTubeWidth, tubeBottom.YMillimeters)
            ],
            isClosed: true,
            context.Stroke,
            context.ThicknessMillimeters,
            context.Fill));
        double arrowLength = _metrics.PoleAttachment.OperationArrowLength;
        DocumentPoint arrowTip = new(x + width * 0.15, y + height * 0.62);
        elements.Add(Line(context, new DocumentPoint(arrowTip.XMillimeters + arrowLength, arrowTip.YMillimeters - arrowLength * 0.6), arrowTip));
        elements.Add(Line(context, arrowTip, new DocumentPoint(arrowTip.XMillimeters + arrowLength * 0.4, arrowTip.YMillimeters - arrowLength * 0.1)));
        elements.Add(Line(context, arrowTip, new DocumentPoint(arrowTip.XMillimeters + arrowLength * 0.1, arrowTip.YMillimeters - arrowLength * 0.4)));
    }

    private void AddText(SymbolRenderContext context, ICollection<SceneElement> elements)
    {
        if (context.IncludeLabel && context.Label is not null)
        {
            elements.Add(new SceneText(context.LabelOrigin, context.Label, context.Stroke, _metrics.General.SmallFontSize));
        }

        if (context.State is SymbolVisualState.Open or SymbolVisualState.Closed)
        {
            elements.Add(new SceneText(
                new DocumentPoint(context.Origin.XMillimeters + context.WidthMillimeters + 2, context.Origin.YMillimeters + context.HeightMillimeters / 2),
                context.State == SymbolVisualState.Closed ? "合" : "分",
                context.Stroke,
                _metrics.General.SmallFontSize));
        }
    }

    private static SceneLine Line(SymbolRenderContext context, DocumentPoint start, DocumentPoint end) =>
        new(start, end, context.Stroke, context.ThicknessMillimeters);
}
