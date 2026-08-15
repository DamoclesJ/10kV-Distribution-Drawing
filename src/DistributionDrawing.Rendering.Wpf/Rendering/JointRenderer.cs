using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Application.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Labels;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

/// <summary>
/// Renders an intermediate topology terminal as a joint symbol without
/// creating or modifying Domain objects.
/// </summary>
public sealed class JointRenderer
{
    private readonly JointSymbol _jointSymbol;
    private readonly LabelLayoutEngine _labelLayoutEngine;

    public JointRenderer(
        SymbolLibrary? symbolLibrary = null,
        LabelLayoutEngine? labelLayoutEngine = null)
    {
        SymbolLibrary library = symbolLibrary ?? new SymbolLibrary();
        _jointSymbol = new JointSymbol(library);
        _labelLayoutEngine = labelLayoutEngine ?? new LabelLayoutEngine();
    }

    public IReadOnlyList<SceneElement> Render(
        IntermediateTerminal intermediateTerminal,
        JointLayout layout)
    {
        return Render([(intermediateTerminal, layout)]);
    }

    public IReadOnlyList<SceneElement> Render(
        IEnumerable<(IntermediateTerminal IntermediateTerminal, JointLayout Layout)> joints)
    {
        ArgumentNullException.ThrowIfNull(joints);

        (IntermediateTerminal IntermediateTerminal, JointLayout Layout)[] inputs = joints.ToArray();
        foreach ((IntermediateTerminal intermediateTerminal, JointLayout layout) in inputs)
        {
            ArgumentNullException.ThrowIfNull(intermediateTerminal);
            ArgumentNullException.ThrowIfNull(layout);
        }

        LabelLayoutResult[] labels = _labelLayoutEngine
            .Layout(inputs.Select(input => new LabelRequest(
                LabelTargetKind.IntermediateTerminal,
                input.IntermediateTerminal.Id,
                input.IntermediateTerminal.DisplayName,
                input.Layout.Position,
                new DocumentPoint(0, 0),
                fontSizeMillimeters: 3.5)))
            .ToArray();
        Dictionary<Guid, LabelLayoutResult> labelsById = labels
            .ToDictionary(label => label.TargetId);

        var elements = new List<SceneElement>();
        foreach ((IntermediateTerminal intermediateTerminal, JointLayout layout) in inputs)
        {
            DocumentRect hitTestBounds = new(
                layout.Position.XMillimeters - layout.SizeMillimeters / 2,
                layout.Position.YMillimeters - layout.SizeMillimeters / 2,
                layout.SizeMillimeters,
                layout.SizeMillimeters);
            elements.AddRange(_jointSymbol
                .CreateElements(intermediateTerminal, layout)
                .Select(element => element with
                {
                    TargetKind = SelectionTargetKind.IntermediateTerminal,
                    TargetId = intermediateTerminal.Id,
                    HitTestBounds = hitTestBounds
                }));
            LabelLayoutResult label = labelsById[intermediateTerminal.Id];
            elements.Add(new SceneText(
                label.Position,
                label.Text,
                System.Windows.Media.Colors.Black,
                label.Request.FontSizeMillimeters)
            {
                TargetKind = SelectionTargetKind.IntermediateTerminal,
                TargetId = intermediateTerminal.Id,
                HitTestBounds = hitTestBounds
            });
        }

        return elements;
    }
}
