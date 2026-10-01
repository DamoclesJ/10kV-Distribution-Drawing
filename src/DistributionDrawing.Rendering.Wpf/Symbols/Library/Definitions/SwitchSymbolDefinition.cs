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
        elements.Add(Line(context, new DocumentPoint(x, centerY), new DocumentPoint(x + inset, centerY), context.FirstSide));
        elements.Add(Line(context, new DocumentPoint(x + width - inset, centerY), new DocumentPoint(x + width, centerY), context.SecondSide));
        DocumentPoint bladeEnd = context.State == SymbolVisualState.Open
            ? new DocumentPoint(x + width - inset, y + inset)
            : new DocumentPoint(x + width - inset, centerY);
        elements.Add(Line(context, new DocumentPoint(x + inset, centerY), bladeEnd,
            context.State == SymbolVisualState.Closed ? context.ConductingPath : context.FirstSide));
        double contactX = x + width - inset;
        double crossHalfSize = _metrics.PoleAttachment.ContactCrossSize / 2;
        elements.Add(Line(context, new DocumentPoint(contactX - crossHalfSize, centerY - crossHalfSize), new DocumentPoint(contactX + crossHalfSize, centerY + crossHalfSize), context.SecondSide));
        elements.Add(Line(context, new DocumentPoint(contactX - crossHalfSize, centerY + crossHalfSize), new DocumentPoint(contactX + crossHalfSize, centerY - crossHalfSize), context.SecondSide));
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
        elements.Add(Line(context, new DocumentPoint(x, centerY), new DocumentPoint(x + inset, centerY), context.FirstSide));
        elements.Add(Line(context, new DocumentPoint(contactX, centerY), new DocumentPoint(x + width, centerY), context.SecondSide));
        elements.Add(new SceneEllipse(
            new DocumentRect(contactX - _metrics.Switch.ContactRadius, centerY - _metrics.Switch.ContactRadius, _metrics.Switch.ContactRadius * 2, _metrics.Switch.ContactRadius * 2),
            context.Stroke,
            context.ThicknessMillimeters)
        {
            ElectricalIdentity = context.SecondSide
        });
        DocumentPoint bladeEnd = context.State == SymbolVisualState.Open
            ? new DocumentPoint(contactX - 1, y + inset)
            : new DocumentPoint(contactX, centerY);
        elements.Add(Line(context, new DocumentPoint(x + inset, centerY), bladeEnd,
            context.State == SymbolVisualState.Closed ? context.ConductingPath : context.FirstSide));
        double markerHalfLength = _metrics.PoleAttachment.ContactMarkerLength / 2;
        elements.Add(Line(context, new DocumentPoint(contactX + _metrics.Switch.ContactRadius + 1, centerY - markerHalfLength), new DocumentPoint(contactX + _metrics.Switch.ContactRadius + 1, centerY + markerHalfLength), context.SecondSide));
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
        elements.Add(Line(context, new DocumentPoint(x, centerY), bladeStart, context.FirstSide));
        elements.Add(Line(context, new DocumentPoint(contactX, centerY), new DocumentPoint(x + width, centerY), context.SecondSide));
        elements.Add(Line(context, bladeStart, context.State == SymbolVisualState.Open
            ? new DocumentPoint(contactX, y + height * _metrics.PoleAttachment.OpenBladeTopRatio)
            : new DocumentPoint(contactX, centerY),
            context.State == SymbolVisualState.Closed ? context.ConductingPath : context.FirstSide));
        double markerHalfLength = _metrics.PoleAttachment.ContactMarkerLength / 2;
        elements.Add(Line(context, new DocumentPoint(contactX, centerY - markerHalfLength), new DocumentPoint(contactX, centerY + markerHalfLength), context.SecondSide));
    }

    private void CreateDropoutFuse(SymbolRenderContext context, ICollection<SceneElement> elements)
    {
        double x = context.Origin.XMillimeters;
        double y = context.Origin.YMillimeters;
        double width = context.WidthMillimeters;
        double height = context.HeightMillimeters;
        double centerX = x + width / 2;
        double inset = Math.Min(_metrics.PoleAttachment.FuseTubeInset / 2, height / 8);
        double tubeStartY = y + inset;
        DocumentPoint tubeTop = context.State == SymbolVisualState.Open
            ? new DocumentPoint(
                centerX - _metrics.PoleAttachment.FuseOpenOffset,
                tubeStartY)
            : new DocumentPoint(centerX, tubeStartY);
        DocumentPoint tubeBottom = new(centerX, y + height - inset);
        elements.Add(Line(context, new DocumentPoint(centerX, y), new DocumentPoint(centerX, y + inset), context.FirstSide));
        elements.Add(Line(
            context,
            new DocumentPoint(
                centerX - _metrics.PoleAttachment.ContactMarkerLength / 2,
                y + inset),
            new DocumentPoint(
                centerX + _metrics.PoleAttachment.ContactMarkerLength / 2,
                y + inset), context.FirstSide));
        elements.Add(Line(context, tubeBottom, new DocumentPoint(centerX, y + height), context.SecondSide));
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
            context.Fill)
        {
            ElectricalIdentity = context.State == SymbolVisualState.Closed
                ? context.ConductingPath : context.SecondSide
        });
        elements.Add(Line(context, tubeTop, tubeBottom,
            context.State == SymbolVisualState.Closed ? context.ConductingPath : context.SecondSide));
        AddDropoutFuseOperationArrow(context, elements, tubeTop, tubeBottom);
    }

    private void AddDropoutFuseOperationArrow(
        SymbolRenderContext context,
        ICollection<SceneElement> elements,
        DocumentPoint tubeTop,
        DocumentPoint tubeBottom)
    {
        DocumentPoint tubeCenter = new(
            (tubeTop.XMillimeters + tubeBottom.XMillimeters) / 2,
            (tubeTop.YMillimeters + tubeBottom.YMillimeters) / 2);
        double directionX = context.State == SymbolVisualState.Open
            ? -1 / Math.Sqrt(2)
            : -1;
        double directionY = context.State == SymbolVisualState.Open
            ? 1 / Math.Sqrt(2)
            : 0;
        double arrowLength = _metrics.PoleAttachment.OperationArrowLength;
        DocumentPoint arrowTip = new(
            tubeCenter.XMillimeters + directionX * arrowLength,
            tubeCenter.YMillimeters + directionY * arrowLength);
        elements.Add(Line(context, tubeCenter, arrowTip));

        double headLength = _metrics.PoleAttachment.FuseTubeWidth;
        double perpendicularX = -directionY;
        double perpendicularY = directionX;
        DocumentPoint headBase = new(
            arrowTip.XMillimeters - directionX * headLength,
            arrowTip.YMillimeters - directionY * headLength);
        elements.Add(new ScenePolyline(
            [
                arrowTip,
                new DocumentPoint(
                    headBase.XMillimeters + perpendicularX * headLength / 2,
                    headBase.YMillimeters + perpendicularY * headLength / 2),
                new DocumentPoint(
                    headBase.XMillimeters - perpendicularX * headLength / 2,
                    headBase.YMillimeters - perpendicularY * headLength / 2)
            ],
            isClosed: true,
            context.Stroke,
            context.ThicknessMillimeters,
            context.Stroke));
    }

    private void AddText(SymbolRenderContext context, ICollection<SceneElement> elements)
    {
        if (context.IncludeLabel && context.Label is not null)
        {
            elements.Add(new SceneText(context.LabelOrigin, context.Label, context.Stroke, _metrics.General.SmallFontSize));
        }

    }

    private static SceneLine Line(SymbolRenderContext context, DocumentPoint start, DocumentPoint end,
        ElectricalVisualIdentity? identity = null) =>
        new(start, end, context.Stroke, context.ThicknessMillimeters)
        {
            ElectricalIdentity = identity
        };
}
