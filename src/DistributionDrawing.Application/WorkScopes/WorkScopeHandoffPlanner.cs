using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Application.WorkScopes;

public enum WorkScopeHandoffStatus
{
    ConfirmedAndAnalyzed,
    ConfirmedHandoffNeedsInput,
    ConfirmedButUnrepresentable,
    ConfirmationRejected,
    PreparationFailed
}

public enum WorkScopeHandoffDiagnosticCode
{
    ProjectionInvalid,
    ProjectionUnrepresentable,
    AnalyzerNeedsInput,
    AnalyzerPreparationFailed
}

public sealed record WorkScopeHandoffDiagnostic(
    WorkScopeHandoffDiagnosticCode Code,
    string Message,
    IReadOnlyList<WorkScopeBoundaryProjectionDiagnostic> ProjectionDiagnostics);

public sealed record WorkScopeHandoffPlanningResult(
    WorkScopeHandoffStatus Status,
    WorkScopeConfirmationPlan? Plan,
    WorkScopeConfirmationDiagnostic? ConfirmationDiagnostic,
    WorkScopeIsolationBoundaryProjection? Projection,
    WorkScopeHandoffDiagnostic? Diagnostic)
{
    public bool CanExecute => Plan is not null && Status is
        WorkScopeHandoffStatus.ConfirmedAndAnalyzed or
        WorkScopeHandoffStatus.ConfirmedHandoffNeedsInput or
        WorkScopeHandoffStatus.ConfirmedButUnrepresentable;
}
/// <summary>Prepares confirmation, complete WTA handoff, and Analyzer snapshots before one atomic command.</summary>
public sealed class WorkScopeHandoffPlanner
{
    private readonly WorkScopeConfirmationPlanner _confirmationPlanner;
    private readonly IWorkScopeIsolationBoundaryProjectionService _projectionService;
    private readonly IWorkTicketHandoffAnalyzer _analyzer;

    public WorkScopeHandoffPlanner()
        : this(new WorkScopeConfirmationPlanner(), new WorkScopeIsolationBoundaryProjector(),
            new WorkTicketAnalyzer())
    {
    }

    public WorkScopeHandoffPlanner(
        WorkScopeConfirmationPlanner confirmationPlanner,
        IWorkScopeIsolationBoundaryProjectionService projectionService,
        IWorkTicketHandoffAnalyzer analyzer)
    {
        _confirmationPlanner = confirmationPlanner ?? throw new ArgumentNullException(nameof(confirmationPlanner));
        _projectionService = projectionService ?? throw new ArgumentNullException(nameof(projectionService));
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
    }

    public WorkScopeHandoffPlanningResult Prepare(
        DrawingDocument drawing,
        EnergizationAnalysisState analysisState,
        WorkScopeCandidate reviewedCandidate,
        WorkTicketDataRoot tickets,
        Guid? targetTicketId = null)
    {
        WorkScopeConfirmationPlanningResult confirmation = _confirmationPlanner.Prepare(
            drawing, analysisState, reviewedCandidate, tickets, targetTicketId);
        if (confirmation.Plan is not WorkScopeConfirmationPlan confirmationPlan)
            return new WorkScopeHandoffPlanningResult(WorkScopeHandoffStatus.ConfirmationRejected,
                null, confirmation.Diagnostic, null, null);

        WorkScopeIsolationBoundaryProjection projection;
        try
        {
            projection = _projectionService.Project(drawing, confirmationPlan.AfterWorkScope);
        }
        catch (Exception error)
        {
            return Failed(WorkScopeHandoffDiagnosticCode.ProjectionInvalid,
                $"WorkScope 边界投影准备失败：{error.Message}");
        }

        if (projection.Status == WorkScopeIsolationBoundaryProjectionStatus.Invalid)
            return Failed(WorkScopeHandoffDiagnosticCode.ProjectionInvalid,
                "刚生成的 Confirmed WorkScope 无法通过 Phase 4 引用与拓扑验证。");

        if (projection.Status == WorkScopeIsolationBoundaryProjectionStatus.Unrepresentable ||
            !projection.IsComplete)
        {
            WorkTicketSession staleDerived = confirmationPlan.AfterTicket.Invalidate() with
            {
                IsolationBoundaries = [],
                Analysis = null,
                AnalyzedFingerprint = null,
                RulePackVersion = null,
                PhraseLibraryVersion = null
            };
            WorkScopeConfirmationPlan safePlan = confirmationPlan.WithAfterTicket(staleDerived);
            return new WorkScopeHandoffPlanningResult(
                WorkScopeHandoffStatus.ConfirmedButUnrepresentable,
                safePlan,
                null,
                projection,
                new WorkScopeHandoffDiagnostic(
                    WorkScopeHandoffDiagnosticCode.ProjectionUnrepresentable,
                    "工作范围已确认，但当前工作票 Analyzer 无法完整表示其隔离边界。",
                    projection.Diagnostics));
        }

        WorkTicketSession analyzerInput = confirmationPlan.AfterTicket with
        {
            IsolationBoundaries = projection.IsolationBoundaries.ToArray()
        };
        WorkTicketSession analyzed;
        try
        {
            analyzed = _analyzer.AnalyzeConfirmedWorkScopeHandoff(drawing, analyzerInput);
        }
        catch (Exception error)
        {
            return Failed(WorkScopeHandoffDiagnosticCode.AnalyzerPreparationFailed,
                $"工作范围已准备，但 Analyzer snapshot 生成失败：{error.Message}", projection);
        }

        if (analyzed.Id != confirmationPlan.TicketId || analyzed.IsolationBoundaries is null ||
            !analyzed.IsolationBoundaries.SequenceEqual(projection.IsolationBoundaries))
            return Failed(WorkScopeHandoffDiagnosticCode.AnalyzerPreparationFailed,
                "Analyzer 返回的工作票身份或边界与已准备的 handoff 不一致。", projection);

        WorkScopeConfirmationPlan preparedPlan = confirmationPlan.WithAfterTicket(analyzed);
        bool needsInput = analyzed.Analysis?.Issues.Count > 0 ||
            analyzed.Draft?.Sections.Any(item => item.Completion == SectionCompletion.NeedsInput) == true;
        WorkScopeHandoffDiagnostic? analyzerDiagnostic = needsInput
            ? new WorkScopeHandoffDiagnostic(WorkScopeHandoffDiagnosticCode.AnalyzerNeedsInput,
                "工作范围已确认并完成分析；工作票仍包含需要补充或复核的输入。", [])
            : null;
        return new WorkScopeHandoffPlanningResult(
            needsInput ? WorkScopeHandoffStatus.ConfirmedHandoffNeedsInput :
                WorkScopeHandoffStatus.ConfirmedAndAnalyzed,
            preparedPlan,
            null,
            projection,
            analyzerDiagnostic);

        WorkScopeHandoffPlanningResult Failed(
            WorkScopeHandoffDiagnosticCode code,
            string message,
            WorkScopeIsolationBoundaryProjection? failedProjection = null) =>
            new(WorkScopeHandoffStatus.PreparationFailed, null, null, failedProjection,
                new WorkScopeHandoffDiagnostic(code, message,
                    failedProjection?.Diagnostics ?? Array.Empty<WorkScopeBoundaryProjectionDiagnostic>()));
    }
}
