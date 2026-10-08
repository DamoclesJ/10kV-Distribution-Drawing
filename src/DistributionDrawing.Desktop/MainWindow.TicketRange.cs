using System.Diagnostics;
using System.Windows;
using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.WorkScopes;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Desktop.ViewModels;
using DistributionDrawing.Desktop.WorkTickets;
using DistributionDrawing.Domain.Energization;

namespace DistributionDrawing.Desktop;

public partial class MainWindow
{
    private readonly WorkScopeHandoffPlanner _workScopeHandoffPlanner = new();
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
        RefreshWorkRange(session);
    }

    private void RefreshWorkRange(ProjectRuntimeSession session)
    {
        EnergizationAnalysisState state = session.Energization;
        var service = new EnergizationUiService();
        TicketRangeSelectedDevices.ItemsSource = session.PersistenceSession.EnergizationScenario.Seeds
            .Select(seed => $"{service.DescribeSeed(session.PersistenceSession.Domain, seed).DeviceName}（EA {SeedSideText(seed.Side)}）")
            .ToArray();
        bool available = state.CurrentResult is not null &&
            session.PersistenceSession.EnergizationScenario.Seeds.Count > 0;
        ConfirmWorkScopeButton.IsEnabled = available;
        TicketRangeAnalysisStatus.Text = available ? "带电分析：已完成" : state.Freshness switch
        {
            EnergizationFreshness.Stale => "带电分析：已过期，请重新分析。",
            _ when state.LatestResult?.Validity == EnergizationValidity.Failed => "带电分析：未成功，请处理分析诊断。",
            _ => "请先选择设备和侧别并完成带电分析。"
        };
        TicketRangeStatus.Text = available ? "代入将使用当前分析结果的不带电部分，无需再次选择设备。" : "";
    }

    private static string SeedSideText(EnergizationSide side) => side switch
    {
        EnergizationSide.Bus => "母线侧",
        EnergizationSide.Line => "线路侧",
        EnergizationSide.SmallerNumber => "小号侧",
        EnergizationSide.LargerNumber => "大号侧",
        _ => "未确定侧别"
    };

    private void OnConfirmTicketRange(object sender, RoutedEventArgs e)
    {
        if (_workspace.CurrentSession is not { } session) return;
        WorkTask task = new(TicketRangeTaskContent.Text.Trim(), TicketRangeTaskObject.Text.Trim());
        WorkScopeHandoffPlanningResult prepared = _workScopeHandoffPlanner.PrepareFromAnalysis(
            session.PersistenceSession.Domain,
            session.Energization,
            session.PersistenceSession.EnergizationScenario,
            session.PersistenceSession.WorkTickets,
            TicketWorkspace.SelectedTicket?.Id,
            task);
        if (!prepared.CanExecute || prepared.Plan is not { } plan)
        {
            Trace.TraceWarning("WorkRange apply rejected: status={0}, confirmation={1}, candidate={2}, projection={3}, detail={4}",
                prepared.Status, prepared.ConfirmationDiagnostic?.Code,
                string.Join(",", prepared.ConfirmationDiagnostic?.CandidateDiagnostics.Select(item => item.Code) ?? []),
                string.Join(",", prepared.Projection?.Diagnostics.Select(item => item.Code) ?? []),
                prepared.ConfirmationDiagnostic?.Message ?? prepared.Diagnostic?.Message);
            TicketRangeStatus.Text = ConfirmationFailureText(prepared);
            return;
        }

        try
        {
            session.CommandStack.ExecuteCommand(new ConfirmWorkScopeCommand(
                session.PersistenceSession.Domain, session.PersistenceSession.WorkTickets, plan));
        }
        catch (InvalidOperationException error)
        {
            Trace.TraceError("WorkRange apply command failed: {0}", error);
            TicketRangeStatus.Text = "工作范围代入未完成，工程状态在准备后发生变化，请重新代入。";
            return;
        }

        TicketWorkspace.SelectTicket(plan.TicketId);
        _ticketRangeDraftOwner = null;
        ShowTicketWorkspace(commitPendingEdits: false);
        string notice = prepared.Status switch
        {
            WorkScopeHandoffStatus.ConfirmedButUnrepresentable => FormatHandoffDiagnostic(prepared),
            WorkScopeHandoffStatus.ConfirmedHandoffNeedsInput =>
                "工作范围已代入，工作票分析已生成；请补充或复核待输入信息。",
            _ => "工作范围已代入，工作票分析已完成。"
        };
        TicketWorkspace.SetHandoffNotice(plan.TicketId, plan.AfterTicket, notice);
        RenderCurrentScene();
    }

    private static string ConfirmationFailureText(WorkScopeHandoffPlanningResult result)
    {
        if (result.Status == WorkScopeHandoffStatus.PreparationFailed)
            return "工作范围代入未完成，请检查当前工程状态后重试。";
        return result.ConfirmationDiagnostic?.Code switch
        {
            WorkScopeConfirmationFailureCode.EmptyCandidate => "当前分析结果中没有可代入的停电工作范围。",
            WorkScopeConfirmationFailureCode.CurrentCandidateUnavailable => "带电分析已变化或不可用，请重新分析后代入。",
            WorkScopeConfirmationFailureCode.ReviewedCandidateMismatch => "带电分析或已选设备已变化，请重新代入。",
            WorkScopeConfirmationFailureCode.MultipleWorkScopes => "当前工作票关联多个工作范围，无法确定要替换的范围。",
            WorkScopeConfirmationFailureCode.MissingLinkedWorkScope => "当前工作票引用的工作范围不存在，请检查工程关联。",
            WorkScopeConfirmationFailureCode.MissingTargetTicket => "当前工作票与图纸不匹配，请重新打开工作票。",
            WorkScopeConfirmationFailureCode.AmbiguousLegacyElectricalRange => "当前工作票的历史范围数据不完整，无法安全保留。",
            WorkScopeConfirmationFailureCode.IdentityUnavailable => "无法生成唯一的工作范围标识，请检查工程后重试。",
            _ => "工作范围数据与当前工程不一致，请检查分析诊断后重试。"
        };
    }

    private void OnCancelWorkRange(object sender, RoutedEventArgs e) =>
        SetDrawingRightPanelMode(DrawingRightPanelMode.Inspector);

    private void DiscardTicketRangeBuffer()
    {
        TicketRangeTaskContent.Clear();
        TicketRangeTaskObject.Clear();
        _ticketRangeDraftOwner = null;
    }

    private static string FormatHandoffDiagnostic(WorkScopeHandoffPlanningResult result)
    {
        WorkScopeBoundaryProjectionDiagnosticCode? code = result.Projection?.Diagnostics
            .Select(item => (WorkScopeBoundaryProjectionDiagnosticCode?)item.Code).FirstOrDefault();
        return code switch
        {
            WorkScopeBoundaryProjectionDiagnosticCode.UnsupportedCustomerStationBoundary =>
                "工作范围已经代入，但当前客户站进线隔离边界暂不能由现有工作票分析模型完整表示。",
            WorkScopeBoundaryProjectionDiagnosticCode.UnresolvedPoleDirection =>
                "工作范围已经代入，但当前杆上开关边界方向无法由现有工作票模型确定，请检查拓扑或边界条件。",
            WorkScopeBoundaryProjectionDiagnosticCode.UnresolvedSelectedWorkSide =>
                "工作范围已经代入，但部分 EA 已选设备无法确定工作区侧，当前工作票分析受限。",
            WorkScopeBoundaryProjectionDiagnosticCode.WtaBoundarySetRejected =>
                "工作范围已经代入，但已选设备的隔离信息组合不满足现有工作票分析模型的约束。",
            _ => "工作范围已经代入，但当前工作票分析模型无法完整表示已选设备的隔离信息。"
        };
    }
}
