using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.WorkScopes;

public enum WorkScopeConfirmationFailureCode
{
    CurrentCandidateUnavailable,
    CurrentCandidateInvalid,
    EmptyCandidate,
    ReviewedCandidateMismatch,
    BlockingCandidateDiagnostic,
    MissingTargetTicket,
    MultipleWorkScopes,
    MissingLinkedWorkScope,
    AmbiguousLegacyElectricalRange,
    BoundaryMaterializationAmbiguous,
    InvalidMaterializedWorkScope,
    IdentityUnavailable
}

public sealed record WorkScopeConfirmationDiagnostic(
    WorkScopeConfirmationFailureCode Code,
    string Message,
    IReadOnlyList<WorkScopeCandidateDiagnostic> CandidateDiagnostics);

public sealed class WorkScopeConfirmationPlan
{
    internal WorkScopeConfirmationPlan(
        Guid ticketId,
        WorkTicketSession? beforeTicket,
        WorkTicketSession afterTicket,
        WorkScope? beforeWorkScope,
        WorkScope afterWorkScope)
    {
        TicketId = ticketId;
        BeforeTicket = beforeTicket;
        AfterTicket = afterTicket;
        BeforeWorkScope = beforeWorkScope;
        AfterWorkScope = afterWorkScope;
    }

    public Guid TicketId { get; }
    public WorkTicketSession? BeforeTicket { get; }
    public WorkTicketSession AfterTicket { get; }
    public WorkScope? BeforeWorkScope { get; }
    public WorkScope AfterWorkScope { get; }
}

public sealed record WorkScopeConfirmationPlanningResult(
    WorkScopeConfirmationPlan? Plan,
    WorkScopeConfirmationDiagnostic? Diagnostic)
{
    public bool CanConfirm => Plan is not null;
}

/// <summary>Validates the reviewed EA candidate and captures immutable Confirm snapshots.</summary>
public sealed class WorkScopeConfirmationPlanner
{
    private readonly WorkScopeCandidateProjector _projector;
    private readonly Func<Guid> _newId;

    public WorkScopeConfirmationPlanner()
        : this(new WorkScopeCandidateProjector(), Guid.NewGuid)
    {
    }

    public WorkScopeConfirmationPlanner(Func<Guid> newId)
        : this(new WorkScopeCandidateProjector(), newId)
    {
    }

    internal WorkScopeConfirmationPlanner(WorkScopeCandidateProjector projector, Func<Guid> newId)
    {
        _projector = projector ?? throw new ArgumentNullException(nameof(projector));
        _newId = newId ?? throw new ArgumentNullException(nameof(newId));
    }

    public WorkScopeConfirmationPlanningResult Prepare(
        DrawingDocument drawing,
        EnergizationAnalysisState analysisState,
        WorkScopeCandidate reviewedCandidate,
        WorkTicketDataRoot tickets,
        Guid? targetTicketId = null)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        ArgumentNullException.ThrowIfNull(analysisState);
        ArgumentNullException.ThrowIfNull(reviewedCandidate);
        ArgumentNullException.ThrowIfNull(tickets);
        if (tickets.DocumentId != drawing.Id)
            return Reject(WorkScopeConfirmationFailureCode.MissingTargetTicket,
                "工作票数据与当前图纸不匹配。");

        WorkScopeCandidateProjection currentProjection = _projector.Project(drawing, analysisState);
        if (!currentProjection.IsValid || currentProjection.Candidate is null)
            return Reject(WorkScopeConfirmationFailureCode.CurrentCandidateUnavailable,
                "当前 EA 结果不可用于确认工作范围。", currentProjection.Diagnostics);

        WorkScopeCandidate currentCandidate = currentProjection.Candidate;
        if (currentCandidate.IsEmpty)
            return Reject(WorkScopeConfirmationFailureCode.EmptyCandidate,
                "Candidate 不包含可确认的业务 Region。", currentCandidate.Diagnostics);
        if (currentCandidate.Diagnostics.Count > 0 || reviewedCandidate.Diagnostics.Count > 0)
            return Reject(WorkScopeConfirmationFailureCode.BlockingCandidateDiagnostic,
                "Candidate 含有阻止确认的诊断。",
                [.. currentCandidate.Diagnostics, .. reviewedCandidate.Diagnostics]);
        if (!Equivalent(reviewedCandidate, currentCandidate))
            return Reject(WorkScopeConfirmationFailureCode.ReviewedCandidateMismatch,
                "当前工作范围已变化，请重新检查后再确认。");

        WorkTicketSession? beforeTicket = targetTicketId is Guid id
            ? tickets.Selected(id)
            : null;
        if (targetTicketId is not null && beforeTicket is null)
            return Reject(WorkScopeConfirmationFailureCode.MissingTargetTicket,
                "目标工作票不存在。");
        if (beforeTicket is not null &&
            (beforeTicket.WorkScopeIds is null || beforeTicket.WorkScopeItems is null))
            return Reject(WorkScopeConfirmationFailureCode.AmbiguousLegacyElectricalRange,
                "工作票的工作范围数据不完整，无法安全确认。");
        if (beforeTicket is not null && beforeTicket.WorkScopeItems.Any(item => item is null ||
                item.Kind == WorkScopeItemKind.ElectricalRange))
            return Reject(WorkScopeConfirmationFailureCode.AmbiguousLegacyElectricalRange,
                "工作票含有旧 ElectricalRange 范围项，无法判断唯一的 EA 工作范围。");
        if (beforeTicket is not null && beforeTicket.WorkScopeIds.Count > 1)
            return Reject(WorkScopeConfirmationFailureCode.MultipleWorkScopes,
                "目标工作票已引用多个 WorkScope，无法确定要替换的范围。");

        WorkScope? linkedScope = null;
        bool replaceExclusive = false;
        if (beforeTicket?.WorkScopeIds.Count == 1)
        {
            Guid linkedId = beforeTicket.WorkScopeIds[0];
            linkedScope = drawing.WorkScopes.SingleOrDefault(scope => scope.WorkScopeId == linkedId);
            if (linkedScope is null)
                return Reject(WorkScopeConfirmationFailureCode.MissingLinkedWorkScope,
                    "目标工作票引用的 WorkScope 不存在。");
            bool shared = tickets.Tickets.Any(ticket => ticket.Id != beforeTicket.Id &&
                (ticket.WorkScopeIds?.Contains(linkedId) == true ||
                 ticket.WorkScopeItems?.Any(item => item?.Kind == WorkScopeItemKind.ElectricalRange &&
                     item.TargetId == linkedId) == true));
            replaceExclusive = !shared;
        }

        var reservedIds = DrawingObjectIds(drawing)
            .Concat(tickets.Tickets.Select(ticket => ticket.Id))
            .ToHashSet();
        Guid ticketId = beforeTicket?.Id ?? Guid.Empty;
        if (beforeTicket is null)
        {
            if (!TryCreateId(reservedIds, out ticketId) || ticketId == drawing.Id)
                return Reject(WorkScopeConfirmationFailureCode.IdentityUnavailable,
                    "无法生成唯一的工作票标识。");
            reservedIds.Add(ticketId);
        }

        Guid workScopeId = replaceExclusive ? linkedScope!.WorkScopeId : Guid.Empty;
        if (workScopeId == Guid.Empty && !TryCreateId(reservedIds, out workScopeId))
            return Reject(WorkScopeConfirmationFailureCode.IdentityUnavailable,
                "无法生成唯一的 WorkScope 标识。");

        var regions = reviewedCandidate.Regions.Select(region =>
            new WorkScopeRegion(region.TerminalIds, region.ElectricalNodeIds)).ToArray();
        var boundaries = new List<WorkScopeBoundary>(reviewedCandidate.Boundaries.Count);
        foreach (WorkScopeCandidateBoundary candidateBoundary in reviewedCandidate.Boundaries)
        {
            if (!TryMaterializeBoundary(drawing, candidateBoundary,
                    out WorkScopeBoundary? boundary, out string? issue))
                return Reject(WorkScopeConfirmationFailureCode.BoundaryMaterializationAmbiguous,
                    issue ?? "Candidate Boundary 无法确定性转换。");
            boundaries.Add(boundary!);
        }

        WorkScope workScope;
        try
        {
            workScope = WorkScope.Create(workScopeId, regions, boundaries,
                linkedScope?.Description);
            drawing.ValidateWorkScopeReferences(workScope);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            return Reject(WorkScopeConfirmationFailureCode.InvalidMaterializedWorkScope,
                error.Message);
        }

        WorkTicketSession afterTicket = beforeTicket is null
            ? new WorkTicketSession(ticketId, new WorkTask("", ""), [], [workScopeId], [], [], null, null, null)
            : beforeTicket with { WorkScopeIds = [workScopeId] };
        WorkScope? beforeWorkScope = replaceExclusive ? Clone(linkedScope!) : null;
        return new WorkScopeConfirmationPlanningResult(
            new WorkScopeConfirmationPlan(ticketId, beforeTicket, afterTicket,
                beforeWorkScope, workScope), null);
    }

    private bool TryMaterializeBoundary(
        DrawingDocument drawing,
        WorkScopeCandidateBoundary candidate,
        out WorkScopeBoundary? boundary,
        out string? issue)
    {
        boundary = null;
        issue = null;
        SwitchDevice? device = drawing.Devices.OfType<SwitchDevice>()
            .SingleOrDefault(item => item.Id == candidate.SwitchDeviceId);
        Terminal? deenergized = drawing.Terminals.SingleOrDefault(item =>
            item.Id == candidate.DeenergizedTerminalId);
        if (device is null || deenergized is null ||
            !device.TerminalIds.Contains(deenergized.Id) ||
            candidate.EnergizedTerminalId == deenergized.Id ||
            !device.TerminalIds.Contains(candidate.EnergizedTerminalId))
        {
            issue = "Boundary 的 Switch 或 Energized / Deenergized Terminal identity 不匹配。";
            return false;
        }

        BoundarySide side;
        switch (device.InstallationType)
        {
            case SwitchInstallationType.CabinetInterval:
                side = deenergized.Role switch
                {
                    "BusSide" => BoundarySide.Bus,
                    "CircuitSide" => BoundarySide.Line,
                    _ => BoundarySide.Unknown
                };
                break;
            case SwitchInstallationType.CustomerStationIncomingFeeder:
                IncomingFeeder? feeder = drawing.CustomerStations.SelectMany(station => station.IncomingFeeders)
                    .SingleOrDefault(item => item.IsolationSwitch.Id == device.Id);
                if (feeder is null || feeder.IncomingFeederId != candidate.TopologyParentId)
                {
                    issue = "CustomerStation Boundary 的 Feeder parent identity 不匹配。";
                    return false;
                }
                side = deenergized.Id == feeder.CableTerminalId ? BoundarySide.Source :
                    deenergized.Id == feeder.StationTerminalId ? BoundarySide.Load : BoundarySide.Unknown;
                break;
            case SwitchInstallationType.Pole:
                if (!TryPoleSide(drawing, candidate, device, out side, out issue)) return false;
                break;
            default:
                side = BoundarySide.Unknown;
                break;
        }

        if (side == BoundarySide.Unknown)
        {
            issue ??= "Boundary 的 Deenergized side 无法由已保存的 Terminal identity 确定。";
            return false;
        }

        Connection[] sideConnections = candidate.RelatedConnectionIds
            .Select(id => drawing.Connections.SingleOrDefault(item => item.Id == id))
            .Where(item => item?.UsesTerminal(deenergized.Id) == true)
            .Cast<Connection>()
            .ToArray();
        if (sideConnections.Length > 1)
        {
            issue = "Boundary 的 Deenergized side 对应多个 Connection，无法无损 materialize。";
            return false;
        }
        if (device.InstallationType == SwitchInstallationType.Pole && sideConnections.Length != 1)
        {
            issue = "Pole Boundary 缺少唯一的 Deenergized-side Connection。";
            return false;
        }

        boundary = new WorkScopeBoundary(device.Id, side, deenergized.Id,
            sideConnections.SingleOrDefault()?.Id);
        return true;
    }

    private static bool TryPoleSide(
        DrawingDocument drawing,
        WorkScopeCandidateBoundary candidate,
        SwitchDevice device,
        out BoundarySide side,
        out string? issue)
    {
        side = BoundarySide.Unknown;
        issue = null;
        PoleAttachment? attachment = drawing.PoleAttachments.SingleOrDefault(item =>
            item.AttachedDeviceId == device.Id);
        Pole? pole = attachment is null ? null : drawing.Devices.OfType<Pole>()
            .SingleOrDefault(item => item.Id == attachment.PoleId);
        if (attachment is null || pole is null || candidate.AttachedPoleId != pole.Id)
        {
            issue = "Pole Boundary 的 attached-pole identity 无法确定。";
            return false;
        }

        Connection[] connections = candidate.RelatedConnectionIds
            .Select(id => drawing.Connections.SingleOrDefault(item => item.Id == id))
            .Where(item => item?.UsesTerminal(candidate.DeenergizedTerminalId) == true)
            .Cast<Connection>()
            .ToArray();
        if (connections.Length != 1)
        {
            issue = "Pole Boundary 的 Deenergized side 没有唯一 Connection。";
            return false;
        }
        OverheadLine? line = drawing.OverheadLines.SingleOrDefault(item =>
            item.ConnectionId == connections[0].Id);
        int index = line?.SupportPoleIds.ToList().IndexOf(pole.Id) ?? -1;
        Guid? adjacentId = line is not null && index == 0 && line.SupportPoleIds.Count > 1
            ? line.SupportPoleIds[1]
            : line is not null && index == line.SupportPoleIds.Count - 1 && index > 0
                ? line.SupportPoleIds[index - 1]
                : null;
        Pole? adjacent = adjacentId is Guid id
            ? drawing.Devices.OfType<Pole>().SingleOrDefault(item => item.Id == id)
            : null;
        if (adjacent is null)
        {
            issue = "Pole Boundary 的 Deenergized-side 相邻 Pole 无法唯一确定。";
            return false;
        }

        side = PoleNumberComparer.Compare(adjacent.PoleNumber, pole.PoleNumber) switch
        {
            PoleNumberOrder.Less => BoundarySide.SmallerNumber,
            PoleNumberOrder.Greater => BoundarySide.LargerNumber,
            _ => BoundarySide.Unknown
        };
        if (side == BoundarySide.Unknown)
            issue = "Pole Boundary 的 Pole number 无法确定 Deenergized side。";
        return side != BoundarySide.Unknown;
    }

    private bool TryCreateId(HashSet<Guid> reserved, out Guid id)
    {
        for (int attempt = 0; attempt < 16; attempt++)
        {
            id = _newId();
            if (id != Guid.Empty && reserved.Add(id)) return true;
        }
        id = Guid.Empty;
        return false;
    }

    private static IEnumerable<Guid> DrawingObjectIds(DrawingDocument drawing) =>
        [drawing.Id,
         .. drawing.Devices.Select(item => item.Id),
         .. drawing.Devices.OfType<RingCabinet>().SelectMany(item => item.Intervals.SelectMany(interval =>
             Enumerable.Repeat(interval.IntervalId, 1).Concat(interval.SwitchDevices.Select(device => device.Id)))),
         .. drawing.CustomerStations.SelectMany(item => item.IncomingFeeders.Select(feeder => feeder.IncomingFeederId)),
         .. drawing.CustomerStations.SelectMany(item => item.IncomingFeeders.Select(feeder => feeder.IsolationSwitch.Id)),
         .. drawing.Terminals.Select(item => item.Id),
         .. drawing.ElectricalNodes.Select(item => item.Id),
         .. drawing.SwitchAssemblies.Select(item => item.AssemblyId),
         .. drawing.Connections.Select(item => item.Id),
         .. drawing.CableSegments.Select(item => item.Id),
         .. drawing.IntermediateTerminals.Select(item => item.Id),
         .. drawing.PoleAttachments.Select(item => item.AttachmentId),
         .. drawing.OverheadLines.Select(item => item.ConnectionId),
         .. drawing.GroundingPoints.Select(item => item.GroundingPointId),
         .. drawing.GroundingAccessPoints.Select(item => item.GroundingAccessPointId),
         .. drawing.WorkScopes.Select(item => item.WorkScopeId)];

    private static bool Equivalent(WorkScopeCandidate left, WorkScopeCandidate right) =>
        RegionSignature(left).SequenceEqual(RegionSignature(right)) &&
        BoundarySignature(left).SequenceEqual(BoundarySignature(right)) &&
        left.Diagnostics.Select(DiagnosticSignature).Order(StringComparer.Ordinal)
            .SequenceEqual(right.Diagnostics.Select(DiagnosticSignature).Order(StringComparer.Ordinal));

    private static IEnumerable<string> RegionSignature(WorkScopeCandidate candidate) =>
        candidate.Regions.Select(region =>
            string.Join(',', region.TerminalIds.Order()) + "/" + string.Join(',', region.ElectricalNodeIds.Order()))
            .Order(StringComparer.Ordinal);

    private static IEnumerable<string> BoundarySignature(WorkScopeCandidate candidate) =>
        candidate.Boundaries.Select(item =>
            $"{item.SwitchDeviceId:N}:{item.SwitchKind}:{item.InstallationType}:{item.DeenergizedTerminalId:N}:" +
            $"{item.EnergizedTerminalId:N}:{item.TopologyParentId:N}:{item.AttachedPoleId:N}:" +
            string.Join(',', item.RelatedConnectionIds.Order()))
            .Order(StringComparer.Ordinal);

    private static string DiagnosticSignature(WorkScopeCandidateDiagnostic item) =>
        $"{item.Code}:{item.Identity:N}:{item.Detail}";

    private static WorkScope Clone(WorkScope scope) => WorkScope.Create(scope.WorkScopeId,
        scope.Regions.Select(region => new WorkScopeRegion(region.TerminalIds, region.ElectricalNodeIds)),
        scope.Boundaries, scope.Description);

    private static WorkScopeConfirmationPlanningResult Reject(
        WorkScopeConfirmationFailureCode code,
        string message,
        IReadOnlyList<WorkScopeCandidateDiagnostic>? candidateDiagnostics = null) =>
        new(null, new WorkScopeConfirmationDiagnostic(code, message, candidateDiagnostics ?? []));
}
