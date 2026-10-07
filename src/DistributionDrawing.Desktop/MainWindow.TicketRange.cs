using DistributionDrawing.Application.WorkScopes;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Desktop.ViewModels;
using DistributionDrawing.Desktop.WorkTickets;
using System.Windows;

namespace DistributionDrawing.Desktop;

public partial class MainWindow
{
    private sealed record WorkScopeReviewLine(string Display);

    private readonly WorkScopeCandidateProjector _workScopeCandidateProjector = new();
    private readonly WorkScopeHandoffPlanner _workScopeHandoffPlanner = new();
    private WorkScopeCandidate? _reviewedWorkScopeCandidate;
    private WorkTicketRangeOwner? _ticketRangeDraftOwner;

    private void OnOpenTicketRange(object sender, RoutedEventArgs e)
    {
        if (_workspace.CurrentSession is not { } session) return;
        TicketWorkspace.CommitPendingEdits();
        DrawingWorkspace.Visibility = Visibility.Visible;
        TicketWorkspace.Visibility = Visibility.Collapsed;
        _drawingTools.Cancel();
        CancelProfessionalPicking();
        _shellViewModel.Toolbox.SetSelectedMode(DesktopToolMode.Select);
        WorkTicketSession? ticket = TicketWorkspace.SelectedTicket;
        var owner = new WorkTicketRangeOwner(session.PersistenceSession.Domain.Id, ticket?.Id);
        if (_ticketRangeDraftOwner != owner)
        {
            TicketRangeTaskContent.Text = ticket?.Task.Content ?? "";
            TicketRangeTaskObject.Text = ticket?.Task.WorkObject ?? "";
            _ticketRangeDraftOwner = owner;
        }
        SetDrawingRightPanelMode(DrawingRightPanelMode.WorkRange);
        RefreshWorkScopeCandidateReview(session);
    }

    private void RefreshWorkScopeCandidateReview(ProjectRuntimeSession session)
    {
        WorkScopeCandidateProjection projection = _workScopeCandidateProjector.Project(
            session.PersistenceSession.Domain, session.Energization);
        _reviewedWorkScopeCandidate = projection.Candidate;
        TicketRangeRegions.ItemsSource = null;
        TicketRangeBoundaries.ItemsSource = null;
        ConfirmWorkScopeButton.IsEnabled = false;

        if (!projection.IsValid || projection.Candidate is null)
        {
            TicketRangeCounts.Text = "";
            TicketRangeCandidateStatus.Text = CandidateUnavailableText(
                projection.Diagnostics.FirstOrDefault()?.Code);
            TicketRangeStatus.Text = "请先完成带电范围分析后再确认。";
            return;
        }

        WorkScopeCandidate candidate = projection.Candidate;
        TicketRangeCounts.Text = $"停电区域：{candidate.Regions.Count}    隔离边界：{candidate.Boundaries.Count}";
        TicketRangeRegions.ItemsSource = candidate.Regions.Select((region, index) =>
            new WorkScopeReviewLine($"区域 {index + 1}：{region.TerminalIds.Count} 个端子，" +
                $"{region.ElectricalNodeIds.Count} 个电气节点（确认时全部纳入）")).ToArray();
        DrawingDocument drawing = session.PersistenceSession.Domain;
        TicketRangeBoundaries.ItemsSource = candidate.Boundaries.Select((boundary, index) =>
        {
            string deviceName = drawing.Devices.FirstOrDefault(item =>
                item.Id == boundary.SwitchDeviceId)?.DisplayName ?? "开关设备";
            return new WorkScopeReviewLine(
                $"边界 {index + 1}：{deviceName}（带电侧 / 停电侧已由分析识别）");
        }).ToArray();

        bool hasBlockingDiagnostic = candidate.Diagnostics.Count > 0;
        if (candidate.IsEmpty)
        {
            TicketRangeCandidateStatus.Text = "当前分析结果中没有可确认的停电工作范围。";
            TicketRangeStatus.Text = "不会回退到手工选择边界。";
        }
        else if (hasBlockingDiagnostic)
        {
            TicketRangeCandidateStatus.Text = "候选工作范围需要先处理分析诊断。";
            TicketRangeStatus.Text = CandidateUnavailableText(candidate.Diagnostics[0].Code);
        }
        else
        {
            TicketRangeCandidateStatus.Text = "候选有效：当前分析推导出一个或多个停电区域。";
            TicketRangeStatus.Text = "确认将创建或更新一个工作票及其已确认工作范围。";
            ConfirmWorkScopeButton.IsEnabled = true;
        }
    }

    private static string CandidateUnavailableText(WorkScopeCandidateDiagnosticCode? code) => code switch
    {
        WorkScopeCandidateDiagnosticCode.StaleAnalysis => "带电范围已变化，请重新执行带电分析。",
        WorkScopeCandidateDiagnosticCode.NoSeeds => "当前没有配置电源点，请先完成带电范围分析。",
        WorkScopeCandidateDiagnosticCode.FailedAnalysis => "带电分析未成功，请检查诊断并重新分析。",
        WorkScopeCandidateDiagnosticCode.IdentityMismatch => "分析结果与当前图纸不一致，请重新执行带电分析。",
        WorkScopeCandidateDiagnosticCode.EmptyCandidate => "当前分析结果中没有可确认的停电工作范围。",
        _ => "请先完成带电范围分析。"
    };

    private void OnConfirmTicketRange(object sender, RoutedEventArgs e)
    {
        if (_workspace.CurrentSession is not { } session || _reviewedWorkScopeCandidate is not { } candidate)
        {
            TicketRangeStatus.Text = "请先完成带电范围分析并检查候选工作范围。";
            return;
        }

        WorkTask task = new(TicketRangeTaskContent.Text.Trim(), TicketRangeTaskObject.Text.Trim());
        WorkScopeHandoffPlanningResult prepared = _workScopeHandoffPlanner.Prepare(
            session.PersistenceSession.Domain,
            session.Energization,
            candidate,
            session.PersistenceSession.WorkTickets,
            TicketWorkspace.SelectedTicket?.Id,
            task);
        if (!prepared.CanExecute || prepared.Plan is not { } plan)
        {
            TicketRangeStatus.Text = ConfirmationFailureText(prepared);
            if (prepared.ConfirmationDiagnostic?.Code is
                WorkScopeConfirmationFailureCode.ReviewedCandidateMismatch or
                WorkScopeConfirmationFailureCode.CurrentCandidateUnavailable)
            {
                _reviewedWorkScopeCandidate = null;
                ConfirmWorkScopeButton.IsEnabled = false;
            }
            return;
        }

        try
        {
            session.CommandStack.ExecuteCommand(new ConfirmWorkScopeCommand(
                session.PersistenceSession.Domain,
                session.PersistenceSession.WorkTickets,
                plan));
        }
        catch (Exception)
        {
            TicketRangeStatus.Text = "工作范围确认未完成，请检查当前工程状态后重试。";
            return;
        }

        TicketWorkspace.SelectTicket(plan.TicketId);
        _ticketRangeDraftOwner = null;
        OnShowTicketWorkspace(this, new RoutedEventArgs());
        string notice = prepared.Status switch
        {
            WorkScopeHandoffStatus.ConfirmedButUnrepresentable => FormatHandoffDiagnostic(prepared),
            WorkScopeHandoffStatus.ConfirmedHandoffNeedsInput =>
                "工作范围已确认，工作票分析已生成；请补充或复核待输入信息。",
            _ => "工作范围已确认，工作票分析已完成。"
        };
        TicketWorkspace.SetHandoffNotice(plan.TicketId, plan.AfterTicket, notice);
        TicketWorkspace.Refresh();
        RenderCurrentScene();
    }

    private static string ConfirmationFailureText(WorkScopeHandoffPlanningResult result)
    {
        if (result.Status == WorkScopeHandoffStatus.PreparationFailed)
            return "工作范围确认未完成，请检查当前工程状态后重试。";
        return result.ConfirmationDiagnostic?.Code switch
        {
            WorkScopeConfirmationFailureCode.EmptyCandidate => "当前分析结果中没有可确认的停电工作范围。",
            WorkScopeConfirmationFailureCode.ReviewedCandidateMismatch => "带电范围已发生变化，请重新检查工作范围。",
            WorkScopeConfirmationFailureCode.CurrentCandidateUnavailable => "请先完成或重新运行带电范围分析。",
            _ => "当前工作范围无法确认，请检查工作票关联和分析诊断。"
        };
    }

    private void OnCancelWorkRange(object sender, RoutedEventArgs e)
    {
        _reviewedWorkScopeCandidate = null;
        SetDrawingRightPanelMode(DrawingRightPanelMode.Inspector);
    }

    private void DiscardTicketRangeBuffer()
    {
        _reviewedWorkScopeCandidate = null;
        TicketRangeTaskContent.Clear();
        TicketRangeTaskObject.Clear();
        _ticketRangeDraftOwner = null;
    }

    private static string FormatHandoffDiagnostic(WorkScopeHandoffPlanningResult result)
    {
        WorkScopeBoundaryProjectionDiagnosticCode? code = result.Projection?.Diagnostics
            .Select(item => (WorkScopeBoundaryProjectionDiagnosticCode?)item.Code)
            .FirstOrDefault();
        return code switch
        {
            WorkScopeBoundaryProjectionDiagnosticCode.UnsupportedCustomerStationBoundary =>
                "工作范围已经确认，但当前客户站进线隔离边界暂不能由现有工作票分析模型完整表示。",
            WorkScopeBoundaryProjectionDiagnosticCode.UnresolvedPoleDirection =>
                "工作范围已经确认，但当前杆上开关边界方向无法由现有工作票模型确定，请检查拓扑或边界条件。",
            WorkScopeBoundaryProjectionDiagnosticCode.WtaBoundarySetRejected =>
                "工作范围已经确认，但其中多个隔离边界组合不满足现有工作票分析模型的约束。",
            _ => "工作范围已经确认，但当前工作票分析模型无法完整表示部分隔离边界。"
        };
    }
}
