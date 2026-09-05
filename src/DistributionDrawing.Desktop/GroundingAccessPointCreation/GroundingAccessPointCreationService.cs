using System.Globalization;
using System.Text.RegularExpressions;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Professional;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Desktop.GroundingAccessPointCreation;

public sealed record GroundingAccessCandidate(
    Guid ConnectionId,
    string ConnectionName,
    Guid PoleId,
    string PoleNumber,
    Guid AdjacentPoleId,
    string AdjacentPoleNumber,
    string VisualDirection)
{
    public string DisplayText =>
        $"{ConnectionName} · {VisualDirection} → {AdjacentPoleNumber}杆";
}

public static class GroundingAccessPointCreationService
{
    private static readonly Regex SimplePoleNumber = new(
        "^(?:P-)?(?<number>[0-9]+)#$|^(?:P-)?(?<plain>[0-9]+)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static IReadOnlyList<GroundingAccessCandidate> GetCandidates(
        ProjectRuntimeSession session,
        Guid poleId)
    {
        ArgumentNullException.ThrowIfNull(session);
        DrawingDocument document = session.PersistenceSession.Domain;
        Pole pole = document.Devices.OfType<Pole>().Single(item => item.Id == poleId);
        var candidates = new List<GroundingAccessCandidate>();
        foreach (OverheadLine line in document.OverheadLines.Where(item =>
                     item.SupportPoleIds.Contains(poleId)))
        {
            int index = line.SupportPoleIds.ToList().IndexOf(poleId);
            foreach (int adjacentIndex in new[] { index - 1, index + 1 }
                         .Where(value => value >= 0 && value < line.SupportPoleIds.Count))
            {
                Guid adjacentPoleId = line.SupportPoleIds[adjacentIndex];
                if (document.GroundingAccessPoints.Any(point =>
                        point.ConnectionId == line.ConnectionId &&
                        point.PoleId == poleId &&
                        point.AdjacentPoleId == adjacentPoleId))
                {
                    continue;
                }
                Pole adjacent = document.Devices.OfType<Pole>()
                    .Single(item => item.Id == adjacentPoleId);
                Connection connection = document.Connections.Single(item =>
                    item.Id == line.ConnectionId);
                candidates.Add(new GroundingAccessCandidate(
                    line.ConnectionId,
                    connection.DisplayName,
                    poleId,
                    pole.PoleNumber,
                    adjacentPoleId,
                    adjacent.PoleNumber,
                    ResolveDirection(session, line.ConnectionId, poleId, adjacentPoleId)));
            }
        }
        return candidates;
    }

    public static GroundingAccessLineSide? RecommendLineSide(
        string poleNumber,
        string adjacentPoleNumber)
    {
        if (!TryParseSimpleNumber(poleNumber, out int pole) ||
            !TryParseSimpleNumber(adjacentPoleNumber, out int adjacent) ||
            pole == adjacent)
        {
            return null;
        }
        return adjacent < pole
            ? GroundingAccessLineSide.SmallerNumberSide
            : GroundingAccessLineSide.LargerNumberSide;
    }

    public static ICommand CreateCommand(
        ProjectRuntimeSession session,
        GroundingAccessCandidate candidate,
        GroundingAccessLineSide side,
        bool addGroundingPoint,
        ProfessionalCommandFactory? factory = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(candidate);
        ProfessionalCommandFactory commands = factory ?? new ProfessionalCommandFactory();
        string location = side == GroundingAccessLineSide.SmallerNumberSide
            ? "小号侧"
            : "大号侧";
        return addGroundingPoint
            ? commands.CreateAddGroundingAccessPointWithGroundingPoint(
                session.PersistenceSession.Domain,
                candidate.ConnectionId,
                candidate.PoleId,
                candidate.AdjacentPoleId,
                side,
                location)
            : commands.CreateAddGroundingAccessPoint(
                session.PersistenceSession.Domain,
                candidate.ConnectionId,
                candidate.PoleId,
                candidate.AdjacentPoleId,
                side);
    }

    private static bool TryParseSimpleNumber(string value, out int number)
    {
        number = 0;
        Match match = SimplePoleNumber.Match(value.Trim());
        string digits = match.Groups["number"].Success
            ? match.Groups["number"].Value
            : match.Groups["plain"].Value;
        return match.Success && int.TryParse(
            digits,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out number);
    }

    private static string ResolveDirection(
        ProjectRuntimeSession session,
        Guid connectionId,
        Guid poleId,
        Guid adjacentPoleId)
    {
        DrawingDocument document = session.PersistenceSession.Domain;
        OverheadLine line = document.OverheadLines.Single(item =>
            item.ConnectionId == connectionId);
        OrthogonalRoute route = session.Scene.Routes.Single(item =>
            item.ConnectionId == line.ConnectionId);
        if (!SupportPoleAwareRouteBuilder.TryResolveHalfEdge(
                route,
                line,
                session.Layout.DrawingLayout,
                poleId,
                adjacentPoleId,
                out GroundingAccessHalfEdge halfEdge))
        {
            throw new InvalidOperationException("无法从正式线路解析验电接地点方向。");
        }

        double dx = halfEdge.DirectionPoint.XMillimeters - halfEdge.PoleCenter.XMillimeters;
        double dy = halfEdge.DirectionPoint.YMillimeters - halfEdge.PoleCenter.YMillimeters;
        return dx < 0 ? "左侧" : dx > 0 ? "右侧" : dy < 0 ? "上侧" : "下侧";
    }
}
