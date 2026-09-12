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
    GroundingAdjacentEndpoint AdjacentEndpoint,
    string? AdjacentPoleNumber,
    string AdjacentEndpointLabel,
    string VisualDirection,
    GroundingAccessPlacementSide PlacementSide = GroundingAccessPlacementSide.PoleSide,
    GroundingAccessLineSide? FixedLineSide = null)
{
    public string DisplayText =>
        $"{ConnectionName} · {VisualDirection} → {AdjacentEndpointLabel}";

    public Guid? AdjacentPoleId => AdjacentEndpoint.Kind == GroundingAdjacentEndpointKind.Pole
        ? AdjacentEndpoint.TargetId
        : null;
}

public sealed record GroundingAccessCandidateLineSideState(
    GroundingAccessLineSide? SelectedLineSide,
    bool IsLocked,
    string Message);

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
                        point.AdjacentEndpoint == GroundingAdjacentEndpoint.ForPole(adjacentPoleId) &&
                        point.PlacementSide == GroundingAccessPlacementSide.PoleSide))
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
                    GroundingAdjacentEndpoint.ForPole(adjacentPoleId),
                    adjacent.PoleNumber,
                    $"{adjacent.PoleNumber}杆",
                    ResolveDirection(
                        session,
                        line.ConnectionId,
                        poleId,
                        GroundingAdjacentEndpoint.ForPole(adjacentPoleId))));
            }

            AddTransformerEndpointCandidate(session, pole, line, candidates);
        }
        return candidates;
    }

    public static IReadOnlyList<GroundingAccessCandidate> GetTransformerCandidates(
        ProjectRuntimeSession session,
        Guid transformerId)
    {
        ArgumentNullException.ThrowIfNull(session);
        DrawingDocument document = session.PersistenceSession.Domain;
        Transformer transformer = document.Transformers.Single(item => item.Id == transformerId);
        if (transformer.TransformerKind == TransformerKind.PublicIndoor)
        {
            return [];
        }

        var candidates = new List<GroundingAccessCandidate>();
        foreach (OverheadLine line in document.OverheadLines.Where(item =>
                     item.SupportPoleIds.Count == 1))
        {
            Connection connection = document.Connections.Single(item => item.Id == line.ConnectionId);
            if (!connection.UsesTerminal(transformer.HvTerminalId))
            {
                continue;
            }
            Pole pole = document.Devices.OfType<Pole>().Single(item =>
                item.Id == line.SupportPoleIds[0]);
            Guid oppositeTerminalId = connection.StartTerminalId == transformer.HvTerminalId
                ? connection.EndTerminalId
                : connection.StartTerminalId;
            if (!IsPhysicallyAtPole(document, oppositeTerminalId, pole.Id))
            {
                continue;
            }
            GroundingAdjacentEndpoint endpoint =
                GroundingAdjacentEndpoint.ForTerminal(transformer.HvTerminalId);
            if (document.GroundingAccessPoints.Any(point =>
                    point.ConnectionId == line.ConnectionId &&
                    point.PoleId == pole.Id &&
                    point.AdjacentEndpoint == endpoint &&
                    point.PlacementSide == GroundingAccessPlacementSide.AdjacentEndpointSide))
            {
                continue;
            }
            candidates.Add(new GroundingAccessCandidate(
                line.ConnectionId,
                connection.DisplayName,
                pole.Id,
                pole.PoleNumber,
                endpoint,
                null,
                "变压器高压侧导线",
                ResolveDirection(
                    session,
                    line.ConnectionId,
                    pole.Id,
                    endpoint,
                    GroundingAccessPlacementSide.AdjacentEndpointSide),
                GroundingAccessPlacementSide.AdjacentEndpointSide,
                GroundingAccessLineSide.TransformerSide));
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

    public static GroundingAccessCandidateLineSideState ResolveLineSideState(
        GroundingAccessCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.FixedLineSide == GroundingAccessLineSide.TransformerSide)
        {
            return new GroundingAccessCandidateLineSideState(
                GroundingAccessLineSide.TransformerSide,
                true,
                "该方向为变压器侧；从杆塔入口创建靠杆塔端的导线验电接地环。");
        }
        GroundingAccessLineSide? recommendation =
            candidate.AdjacentPoleNumber is string adjacentPoleNumber
                ? RecommendLineSide(candidate.PoleNumber, adjacentPoleNumber)
                : null;
        return new GroundingAccessCandidateLineSideState(
            recommendation,
            false,
            recommendation is null
                ? "杆号无法可靠解析，请人工选择小号侧或大号侧。"
                : "已按简单杆号推荐；可人工覆盖，实际相邻杆方向不会改变。");
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
        GroundingAccessLineSide effectiveSide = candidate.FixedLineSide ?? side;
        return addGroundingPoint
            ? commands.CreateAddGroundingAccessPointWithGroundingPoint(
                session.PersistenceSession.Domain,
                candidate.ConnectionId,
                candidate.PoleId,
                candidate.AdjacentEndpoint,
                effectiveSide,
                candidate.PlacementSide)
            : commands.CreateAddGroundingAccessPoint(
                session.PersistenceSession.Domain,
                candidate.ConnectionId,
                candidate.PoleId,
                candidate.AdjacentEndpoint,
                effectiveSide,
                candidate.PlacementSide);
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
        GroundingAdjacentEndpoint adjacentEndpoint,
        GroundingAccessPlacementSide placementSide = GroundingAccessPlacementSide.PoleSide)
    {
        DrawingDocument document = session.PersistenceSession.Domain;
        OverheadLine line = document.OverheadLines.Single(item =>
            item.ConnectionId == connectionId);
        OrthogonalRoute route = session.Scene.Routes.Single(item =>
            item.ConnectionId == line.ConnectionId);
        Connection connection = document.Connections.Single(item =>
            item.Id == line.ConnectionId);
        if (!SupportPoleAwareRouteBuilder.TryResolveHalfEdge(
                route,
                line,
                session.Layout.DrawingLayout,
                poleId,
                adjacentEndpoint,
                connection,
                placementSide,
                out GroundingAccessHalfEdge halfEdge))
        {
            throw new InvalidOperationException("无法从正式线路解析验电接地环方向。");
        }

        double dx = halfEdge.DirectionPoint.XMillimeters - halfEdge.ConductorOrigin.XMillimeters;
        double dy = halfEdge.DirectionPoint.YMillimeters - halfEdge.ConductorOrigin.YMillimeters;
        return dx < 0 ? "左侧" : dx > 0 ? "右侧" : dy < 0 ? "上侧" : "下侧";
    }

    private static void AddTransformerEndpointCandidate(
        ProjectRuntimeSession session,
        Pole pole,
        OverheadLine line,
        ICollection<GroundingAccessCandidate> candidates)
    {
        if (line.SupportPoleIds.Count != 1 || line.SupportPoleIds[0] != pole.Id)
        {
            return;
        }

        DrawingDocument document = session.PersistenceSession.Domain;
        Connection connection = document.Connections.Single(item =>
            item.Id == line.ConnectionId);
        foreach (Guid terminalId in new[]
                 { connection.StartTerminalId, connection.EndTerminalId })
        {
            Transformer? transformer = document.Transformers.SingleOrDefault(item =>
                item.HvTerminalId == terminalId &&
                item.TransformerKind is TransformerKind.PublicPoleMounted or
                    TransformerKind.DedicatedPoleMounted);
            if (transformer is null)
            {
                continue;
            }

            Guid oppositeTerminalId = connection.StartTerminalId == terminalId
                ? connection.EndTerminalId
                : connection.StartTerminalId;
            if (!IsPhysicallyAtPole(document, oppositeTerminalId, pole.Id))
            {
                continue;
            }

            GroundingAdjacentEndpoint endpoint =
                GroundingAdjacentEndpoint.ForTerminal(terminalId);
            if (document.GroundingAccessPoints.Any(point =>
                    point.ConnectionId == line.ConnectionId &&
                    point.PoleId == pole.Id &&
                    point.AdjacentEndpoint == endpoint &&
                    point.PlacementSide == GroundingAccessPlacementSide.PoleSide))
            {
                continue;
            }

            candidates.Add(new GroundingAccessCandidate(
                line.ConnectionId,
                connection.DisplayName,
                pole.Id,
                pole.PoleNumber,
                endpoint,
                null,
                "变压器高压侧导线",
                ResolveDirection(session, line.ConnectionId, pole.Id, endpoint),
                GroundingAccessPlacementSide.PoleSide,
                GroundingAccessLineSide.TransformerSide));
        }
    }

    private static bool IsPhysicallyAtPole(
        DrawingDocument document,
        Guid terminalId,
        Guid poleId)
    {
        Terminal terminal = document.Terminals.Single(item => item.Id == terminalId);
        if (terminal.OwnerType != TopologyOwnerType.Device)
        {
            return false;
        }
        return document.Devices.Single(item => item.Id == terminal.OwnerId) switch
        {
            Pole pole => pole.Id == poleId,
            SwitchDevice switchDevice when
                switchDevice.InstallationType == SwitchInstallationType.Pole =>
                document.PoleAttachments.Any(attachment =>
                    attachment.AttachedDeviceId == switchDevice.Id &&
                    attachment.PoleId == poleId),
            CableTermination termination => document.PoleAttachments.Any(attachment =>
                attachment.AttachedDeviceId == termination.Id &&
                attachment.PoleId == poleId),
            _ => false
        };
    }
}
