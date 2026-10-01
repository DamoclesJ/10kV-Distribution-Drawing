using System.Windows;
using System.Windows.Controls;
using DistributionDrawing.Application.Energization;
using DistributionDrawing.Domain.Energization;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Desktop.SwitchOperation;

namespace DistributionDrawing.Desktop.Energization;

public partial class EnergizationPanel : UserControl
{
    private sealed record CandidateItem(EnergizationBoundaryCandidate Candidate,
        string DisplayText);
    private sealed record SeedItem(EnergizedSeed Seed, string DisplayText);

    private readonly EnergizationUiService _service = new();
    private ProjectRuntimeSession? _session;
    private Guid? _selectedObjectId;
    private bool _everConfirmed;
    private bool _updating;

    public EnergizationPanel()
    {
        InitializeComponent();
        Refresh();
    }

    public event EventHandler? VisualStateChanged;
    public event EventHandler? SwitchOperationApplied;
    private void OnOpenSwitch(object sender, RoutedEventArgs e) => OperateSwitch(SwitchState.Open);
    private void OnCloseSwitch(object sender, RoutedEventArgs e) => OperateSwitch(SwitchState.Closed);

    private void OperateSwitch(SwitchState state)
    {
        SwitchOperationResult result = new SwitchOperationController(() => _session).SetSelectedState(state);
        if (!result.IsSuccess) SwitchStateText.Text = result.ErrorMessage;
        else
        {
            Refresh();
            SwitchOperationApplied?.Invoke(this, EventArgs.Empty);
            VisualStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Bind(ProjectRuntimeSession? session)
    {
        if (_session is not null)
        {
            _session.CommandStack.StateChanged -= OnSessionChanged;
            _session.Energization.Changed -= OnSessionChanged;
        }
        _session = session;
        _selectedObjectId = null;
        _everConfirmed = session?.PersistenceSession.EnergizationScenario.IsSourceSetComplete == true;
        if (session is not null)
        {
            session.CommandStack.StateChanged += OnSessionChanged;
            session.Energization.Changed += OnSessionChanged;
        }
        Refresh();
    }

    public void SetSelection(Guid? objectId)
    {
        _selectedObjectId = objectId;
        RefreshCandidates();
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        Refresh();
        VisualStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Refresh()
    {
        _updating = true;
        try
        {
            RefreshCandidates();
            Guid? selectedSeedId = (SeedList.SelectedItem as SeedItem)?.Seed.Id;
            if (_session is null)
            {
                SeedList.ItemsSource = null;
                CompletionText.Text = "没有打开的图纸";
                AnalysisText.Text = "尚未执行带电分析";
                DiagnosticList.ItemsSource = null;
                Legend.Visibility = Visibility.Collapsed;
                UpdateButtons();
                return;
            }

            EnergizationScenario scenario = _session.PersistenceSession.EnergizationScenario;
            SeedItem[] seeds = scenario.Seeds.Select(seed =>
            {
                EnergizationSeedDisplay display = _service.DescribeSeed(
                    _session.PersistenceSession.Domain, seed);
                string side = SideText(seed.Side);
                string status = display.IsResolvable ? "已解析" :
                    $"无法解析：{EnergizationUiService.DiagnosticText(display.Diagnostic)}";
                return new SeedItem(seed,
                    $"{display.OwnerName} → {display.DeviceName} ({display.DeviceType}) / {side}\n{status}");
            }).ToArray();
            SeedList.ItemsSource = seeds;
            SeedList.SelectedItem = seeds.FirstOrDefault(item => item.Seed.Id == selectedSeedId);
            if (scenario.IsSourceSetComplete) _everConfirmed = true;
            CompletionText.Text = scenario.IsSourceSetComplete
                ? "已确认：以上为本图纸全部电源点"
                : _everConfirmed
                    ? "电源全集确认已失效，请重新确认"
                    : "电源全集尚未确认；未确认时只能证明已带电区域";

            EnergizationAnalysisState state = _session.Energization;
            AnalysisText.Text = state.Freshness switch
            {
                EnergizationFreshness.NotAnalyzed => "尚未执行带电分析",
                EnergizationFreshness.Stale => "图纸或电源点已变化，请重新执行分析",
                _ => state.CurrentResult?.Validity switch
                {
                    EnergizationValidity.NoSeeds => "未设置电源点；无法判定无电",
                    EnergizationValidity.ForwardOnly =>
                        "电源点尚未确认完整；仅显示已证明带电区域，其他区域为未知",
                    EnergizationValidity.Complete => "分析完成；可判定带电、无电及异常未知区域",
                    EnergizationValidity.Incomplete => "分析信息不完整；请处理电源点或拓扑异常",
                    _ => "尚未执行带电分析"
                }
            };
            DiagnosticList.ItemsSource = state.Freshness == EnergizationFreshness.Current
                ? state.LatestDiagnostics.Select(item =>
                    item.SeedId is Guid id
                        ? $"电源点 {Array.FindIndex(seeds, seed => seed.Seed.Id == id) + 1}：{item.Message}"
                        : $"图纸：{item.Message}").ToArray()
                : null;
            Legend.Visibility = state.CanShowOverlay ? Visibility.Visible : Visibility.Collapsed;
            UpdateButtons();
        }
        finally
        {
            _updating = false;
        }
    }

    private void RefreshCandidates()
    {
        Guid? selectedDeviceId = (CandidateList.SelectedItem as CandidateItem)?.Candidate.DeviceId;
        EnergizationSide? selectedSide = (CandidateList.SelectedItem as CandidateItem)?.Candidate.Side;
        CandidateItem[] candidates = _session is not null && _selectedObjectId is Guid id
            ? _service.Candidates(_session.PersistenceSession.Domain, id)
                .Select(item => new CandidateItem(item,
                    $"{item.OwnerName} → {item.DeviceName} ({item.DeviceType}) / {SideText(item.Side)}"))
                .ToArray()
            : [];
        CandidateList.ItemsSource = candidates;
        CandidateList.SelectedItem = candidates.FirstOrDefault(item =>
            item.Candidate.DeviceId == selectedDeviceId &&
            item.Candidate.Side == selectedSide);
        CandidateDiagnostic.Text = candidates.Length == 0
            ? "选择可用的开关设备、柜体、间隔或杆塔"
            : "请选择设备及电气侧";
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        SwitchDevice? selectedSwitch = _session?.SelectionManager.HasSingleSelection == true
            ? _session.SelectionResolver.Resolve(_session.SelectionManager.Selected)?.SwitchDevice : null;
        OpenSwitchButton.IsEnabled = selectedSwitch?.SwitchState == SwitchState.Closed;
        CloseSwitchButton.IsEnabled = selectedSwitch?.SwitchState == SwitchState.Open;
        SwitchStateText.Text = selectedSwitch is null ? "选择开关后可直接分闸 / 合闸" :
            $"当前开关：{selectedSwitch.DisplayName ?? "开关"}；{(selectedSwitch.SwitchState == SwitchState.Closed ? "合" : "分")}";
        CandidateItem? candidate = CandidateList.SelectedItem as CandidateItem;
        bool validCandidate = candidate?.Candidate.IsResolvable == true;
        AddButton.IsEnabled = _session is not null && validCandidate;
        ReplaceButton.IsEnabled = AddButton.IsEnabled && SeedList.SelectedItem is SeedItem;
        RemoveButton.IsEnabled = _session is not null && SeedList.SelectedItem is SeedItem;
        AnalyzeButton.IsEnabled = _session is not null;
        ConfirmButton.IsEnabled = _session is not null &&
            !_session.PersistenceSession.EnergizationScenario.IsSourceSetComplete &&
            _service.CanConfirmSources(_session.PersistenceSession.Domain,
                _session.PersistenceSession.EnergizationScenario);
    }

    private void OnCandidateSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_updating) return;
        CandidateDiagnostic.Text = CandidateList.SelectedItem is CandidateItem item
            ? item.Candidate.IsResolvable ? "可以作为电源边界" :
                EnergizationUiService.DiagnosticText(item.Candidate.Diagnostic)
            : "请选择设备及电气侧";
        UpdateButtons();
    }

    private void OnSeedSelected(object sender, SelectionChangedEventArgs e)
    {
        if (!_updating) UpdateButtons();
    }

    private void OnAddSeed(object sender, RoutedEventArgs e)
    {
        if (_session is null || CandidateList.SelectedItem is not CandidateItem item ||
            !item.Candidate.IsResolvable) return;
        _session.ExecuteScenarioCommand(EnergizationScenarioCommand.Add(
            _session.PersistenceSession.EnergizationScenario,
            new EnergizedSeed(Guid.NewGuid(), item.Candidate.DeviceId, item.Candidate.Side)));
    }

    private void OnRemoveSeed(object sender, RoutedEventArgs e)
    {
        if (_session is null || SeedList.SelectedItem is not SeedItem item) return;
        _session.ExecuteScenarioCommand(EnergizationScenarioCommand.Remove(
            _session.PersistenceSession.EnergizationScenario, item.Seed.Id));
    }

    private void OnReplaceSeed(object sender, RoutedEventArgs e)
    {
        if (_session is null || SeedList.SelectedItem is not SeedItem seed ||
            CandidateList.SelectedItem is not CandidateItem candidate ||
            !candidate.Candidate.IsResolvable) return;
        _session.ExecuteScenarioCommand(EnergizationScenarioCommand.Replace(
            _session.PersistenceSession.EnergizationScenario,
            new EnergizedSeed(seed.Seed.Id, candidate.Candidate.DeviceId,
                candidate.Candidate.Side)));
    }

    private void OnConfirmSources(object sender, RoutedEventArgs e)
    {
        if (_session is null || !ConfirmButton.IsEnabled) return;
        _session.ExecuteScenarioCommand(EnergizationScenarioCommand.SetComplete(
            _session.PersistenceSession.EnergizationScenario, true));
    }

    private void OnExecuteAnalysis(object sender, RoutedEventArgs e) =>
        _session?.ExecuteEnergizationAnalysis();

    private static string SideText(EnergizationSide side) => side switch
    {
        EnergizationSide.Bus => "母线侧",
        EnergizationSide.Line => "线路侧",
        EnergizationSide.SmallerNumber => "小号侧",
        EnergizationSide.LargerNumber => "大号侧",
        _ => side.ToString()
    };
}
