using System.Windows.Media;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Rendering.Wpf.Interaction.Professional;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Desktop;

public sealed record GroundingTargetCandidate(
    GroundingTarget Target,
    DocumentPoint Position,
    bool IsOccupied);

/// <summary>
/// Resolves only frozen grounding targets and produces a transient creation
/// affordance. It never maps device bodies or arbitrary line segments.
/// </summary>
public sealed class GroundingTargetPicker
{
    public GroundingTargetCandidate? Resolve(
        DrawingDocument document,
        RuntimeLayoutDocument layout,
        DrawingScene scene,
        DocumentPoint pointer,
        double toleranceMillimeters)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(scene);
        if (!double.IsFinite(toleranceMillimeters) || toleranceMillimeters < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(toleranceMillimeters));
        }

        var candidates = new List<(GroundingTarget Target, DocumentPoint Position, double Distance)>();
        foreach (GroundingAccessPoint gap in document.GroundingAccessPoints)
        {
            SceneEllipse? marker = scene.Elements.OfType<SceneEllipse>().SingleOrDefault(element =>
                element.TargetId == gap.GroundingAccessPointId);
            if (marker is null) continue;
            DocumentPoint position = Center(marker.Bounds);
            AddIfNear(
                candidates,
                GroundingTarget.ForGroundingAccessPoint(gap.GroundingAccessPointId),
                position,
                pointer,
                toleranceMillimeters);
        }

        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            layout.DrawingLayout,
            layout.RingCabinetLayouts,
            document.Connections,
            document.CableSegments,
            layout.TransformerLayouts);
        foreach (TerminalAnchor anchor in anchors.Anchors.Where(anchor =>
                     ProfessionalCommandFactory.IsEligibleNewTerminalTarget(
                         document,
                         anchor.TerminalId)))
        {
            AddIfNear(
                candidates,
                GroundingTarget.ForTerminal(anchor.TerminalId),
                anchor.Position,
                pointer,
                toleranceMillimeters);
        }

        if (candidates.Count == 0) return null;
        (GroundingTarget Target, DocumentPoint Position, double Distance) value = candidates
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Target.Kind)
            .ThenBy(candidate => candidate.Target.TargetId)
            .First();
        return new GroundingTargetCandidate(
            value.Target,
            value.Position,
            document.GroundingPoints.Any(point => point.Target == value.Target));
    }

    public IReadOnlyList<SceneElement> CreateAffordance(GroundingTargetCandidate? candidate)
    {
        if (candidate is null) return [];
        const double radius = 5;
        Color color = candidate.IsOccupied ? Colors.OrangeRed : Colors.DeepSkyBlue;
        return
        [
            new SceneEllipse(
                new DocumentRect(
                    candidate.Position.XMillimeters - radius,
                    candidate.Position.YMillimeters - radius,
                    radius * 2,
                    radius * 2),
                color,
                1.2,
                null,
                SceneStrokeStyle.Dashed)
        ];
    }

    private static void AddIfNear(
        ICollection<(GroundingTarget Target, DocumentPoint Position, double Distance)> output,
        GroundingTarget target,
        DocumentPoint position,
        DocumentPoint pointer,
        double tolerance)
    {
        double distance = Distance(position, pointer);
        if (distance <= tolerance)
        {
            output.Add((target, position, distance));
        }
    }

    private static DocumentPoint Center(DocumentRect bounds) => new(
        bounds.XMillimeters + bounds.WidthMillimeters / 2,
        bounds.YMillimeters + bounds.HeightMillimeters / 2);

    private static double Distance(DocumentPoint first, DocumentPoint second)
    {
        double dx = first.XMillimeters - second.XMillimeters;
        double dy = first.YMillimeters - second.YMillimeters;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
