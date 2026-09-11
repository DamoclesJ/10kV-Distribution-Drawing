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
    string VisualDirection)
{
    public string DisplayText =>
        $"{ConnectionName} · {VisualDirection} → {AdjacentEndpointLabel}";

    public Guid AdjacentPoleId => AdjacentEndpoint.Kind == GroundingAdjacentEndpointKind.Pole
        ? AdjacentEndpoint.TargetId
        : throw new InvalidOperationException("This candidate has a terminal adjacent endpoint.");
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
                        point.AdjacentEndpoint == GroundingAdjacentEndpoint.ForPole(adjacentPoleId)))
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
        return addGroundingPoint
            ? commands.CreateAddGroundingAccessPointWithGroundingPoint(
                session.PersistenceSession.Domain,
                candidate.ConnectionId,
                candidate.PoleId,
                candidate.AdjacentEndpoint,
                side)
            : commands.CreateAddGroundingAccessPoint(
                session.PersistenceSession.Domain,
                candidate.ConnectionId,
                candidate.PoleId,
                candidate.AdjacentEndpoint,
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
        GroundingAdjacentEndpoint adjacentEndpoint)
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
                    TransformerKind.DedicatedPoleMounted or TransformerKind.PublicIndoor);
            if (transformer is null)
            {
                continue;
            }

            Guid oppositeTerminalId = connection.StartTerminalId == terminalId
                ? connection.EndTerminalId
                : connection.StartTerminalId;
            Terminal oppositeTerminal = document.Terminals.Single(item =>
                item.Id == oppositeTerminalId);
            bool physicallyAtPole = oppositeTerminal.OwnerType == TopologyOwnerType.Device &&
                document.Devices.Single(item => item.Id == oppositeTerminal.OwnerId) switch
                {
                    Pole ownerPole => ownerPole.Id == pole.Id,
                    SwitchDevice switchDevice when
                        switchDevice.InstallationType == SwitchInstallationType.Pole =>
                        document.PoleAttachments.Any(attachment =>
                            attachment.AttachedDeviceId == switchDevice.Id &&
                            attachment.PoleId == pole.Id),
                    CableTermination termination => document.PoleAttachments.Any(attachment =>
                        attachment.AttachedDeviceId == termination.Id &&
                        attachment.PoleId == pole.Id),
                    _ => false
                };
            if (!physicallyAtPole)
            {
                continue;
            }

            GroundingAdjacentEndpoint endpoint =
                GroundingAdjacentEndpoint.ForTerminal(terminalId);
            if (document.GroundingAccessPoints.Any(point =>
                    point.ConnectionId == line.ConnectionId &&
                    point.PoleId == pole.Id &&
                    point.AdjacentEndpoint == endpoint))
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
                ResolveDirection(session, line.ConnectionId, pole.Id, endpoint)));
        }
    }
}
