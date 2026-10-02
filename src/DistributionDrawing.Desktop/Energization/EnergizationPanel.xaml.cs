using System.Windows;
using System.Windows.Controls;
using DistributionDrawing.Application.Energization;
using DistributionDrawing.Domain.Energization;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Desktop.SwitchOperation;

namespace DistributionDrawing.Desktop.Energization;

public partial class EnergizationPanel : UserControl
{
    private sealed record CandidateItem(
        Guid DeviceId,
        IReadOnlyList<EnergizationBoundaryCandidate> Options,
        string DisplayText);
    private sealed record SideItem(EnergizationBoundaryCandidate Candidate,
        string DisplayText);
    private sealed record SeedItem(EnergizedSeed Seed, string DisplayText);

    private readonly EnergizationUiService _service = new();
    private ProjectRuntimeSession? _session;
    private Guid? _selectedObjectId;
    private bool _updating;

    public EnergizationPanel()
    {
        InitializeComponent();
        Refresh();
    }

    public event EventHandler? VisualStateChanged;
    public event EventHandler? SwitchOperationApplied;
    public event EventHandler? ExitRequested;
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
        SetVisualizationDiagnostics([]);
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
                AnalysisText.Text = "尚未执行带电分析";
                DiagnosticList.ItemsSource = null;
                SetVisualizationDiagnostics([]);
                UpdateButtons();
                return;
            }

            EnergizationScenario scenario = _session.PersistenceSession.EnergizationScenario;
            SeedItem[] seeds = scenario.Seeds.Select(seed =>
            {
                EnergizationSeedDisplay display = _service.DescribeSeed(
                    _session.PersistenceSession.Domain, seed);
                string status = display.IsResolvable ? "已解析" :
                    $"无法解析：{EnergizationUiService.DiagnosticText(display.Diagnostic)}";
                return new SeedItem(seed,
                    $"{display.DeviceName}    {SideText(seed.Side)}\n{status}");
            }).ToArray();
            SeedList.ItemsSource = seeds;
            SeedList.SelectedItem = seeds.FirstOrDefault(item => item.Seed.Id == selectedSeedId);
            EnergizationAnalysisState state = _session.Energization;
            AnalysisText.Text = state.Freshness switch
            {
                EnergizationFreshness.NotAnalyzed => "尚未执行带电分析",
                EnergizationFreshness.Stale => "图纸或电源配置已发生变化，请重新执行带电分析。",
                _ => state.LatestResult?.Validity switch
                {
                    EnergizationValidity.NoSeeds => "当前未配置电源点",
                    EnergizationValidity.Complete => "分析完成；红色表示带电，其余保持原图外观",
                    _ when state.LatestResult is not null => "带电分析失败；请处理电源点或拓扑诊断",
                    _ => "尚未执行带电分析"
                }
            };
            if (state.CurrentResult is null) SetVisualizationDiagnostics([]);
            DiagnosticList.ItemsSource = _session.EnergizationReferenceDiagnostics.Concat(
                state.Freshness == EnergizationFreshness.Current
                ? state.LatestDiagnostics.Select(item =>
                    item.SeedId is Guid id
                        ? $"电源点 {Array.FindIndex(seeds, seed => seed.Seed.Id == id) + 1}：{item.Message}"
                        : $"图纸：{item.Message}").ToArray()
                : []).ToArray();
            UpdateButtons();
        }
        finally
        {
            _updating = false;
        }
    }

    private void RefreshCandidates()
    {
        Guid? selectedDeviceId = (CandidateList.SelectedItem as CandidateItem)?.DeviceId;
        EnergizationSide? selectedSide = (SourceSideComboBox.SelectedItem as SideItem)?.Candidate.Side;
        EnergizationBoundaryCandidate[] options = _session is not null && _selectedObjectId is Guid id
            ? _service.Candidates(_session.PersistenceSession.Domain, id).ToArray()
            : [];
        CandidateItem[] candidates = options.GroupBy(item => item.DeviceId)
            .Select(group => new CandidateItem(group.Key, group.ToArray(),
                group.First().DeviceName))
            .ToArray();
        CandidateList.ItemsSource = candidates;
        CandidateList.SelectedItem = candidates.FirstOrDefault(item =>
            item.DeviceId == selectedDeviceId);
        SetSideOptions(CandidateList.SelectedItem as CandidateItem, selectedSide);
        CandidateDiagnostic.Text = candidates.Length == 0
            ? "选择可用的开关设备、柜体、间隔或杆塔"
            : "请选择候选设备和电源方向";
        UpdateButtons();
    }

    private void SetSideOptions(CandidateItem? item, EnergizationSide? preferredSide = null)
    {
        SideItem[] sides = item?.Options.Select(option =>
            new SideItem(option, SideText(option.Side))).ToArray() ?? [];
        SourceSideComboBox.ItemsSource = sides;
        SourceSideComboBox.DisplayMemberPath = nameof(SideItem.DisplayText);
        SourceSideComboBox.SelectedItem = sides.FirstOrDefault(side =>
            side.Candidate.Side == preferredSide);
    }

    private void UpdateButtons()
    {
        SwitchDevice? selectedSwitch = _session?.SelectionManager.HasSingleSelection == true
            ? _session.SelectionResolver.Resolve(_session.SelectionManager.Selected)?.SwitchDevice : null;
        OpenSwitchButton.IsEnabled = selectedSwitch?.SwitchState == SwitchState.Closed;
        CloseSwitchButton.IsEnabled = selectedSwitch?.SwitchState == SwitchState.Open;
        SwitchStateText.Text = selectedSwitch is null ? "选择开关后可直接分闸 / 合闸" :
            $"当前开关：{selectedSwitch.DisplayName ?? "开关"}；{(selectedSwitch.SwitchState == SwitchState.Closed ? "合" : "分")}";
        SideItem? side = SourceSideComboBox.SelectedItem as SideItem;
        SourceSideComboBox.IsEnabled = CandidateList.SelectedItem is CandidateItem;
        bool validCandidate = side?.Candidate.IsResolvable == true;
        AddButton.IsEnabled = _session is not null && validCandidate;
        ReplaceButton.IsEnabled = AddButton.IsEnabled && SeedList.SelectedItem is SeedItem;
        RemoveButton.IsEnabled = _session is not null && SeedList.SelectedItem is SeedItem;
        AnalyzeButton.IsEnabled = _session is not null;
    }

    private void OnCandidateSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_updating) return;
        CandidateDiagnostic.Text = CandidateList.SelectedItem is CandidateItem item
            ? "请选择电源方向"
            : "请选择候选设备";
        SetSideOptions(CandidateList.SelectedItem as CandidateItem);
        UpdateButtons();
    }

    private void OnSourceSideSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_updating) return;
        CandidateDiagnostic.Text = SourceSideComboBox.SelectedItem is SideItem item
            ? item.Candidate.IsResolvable ? "可以作为电源边界" :
                EnergizationUiService.DiagnosticText(item.Candidate.Diagnostic)
            : CandidateList.SelectedItem is CandidateItem
                ? "请选择电源方向"
                : "请选择候选设备";
        UpdateButtons();
    }

    private void OnSeedSelected(object sender, SelectionChangedEventArgs e)
    {
        if (!_updating) UpdateButtons();
    }

    private void OnAddSeed(object sender, RoutedEventArgs e)
    {
        if (_session is null || SourceSideComboBox.SelectedItem is not SideItem item ||
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
            SourceSideComboBox.SelectedItem is not SideItem candidate ||
            !candidate.Candidate.IsResolvable) return;
        _session.ExecuteScenarioCommand(EnergizationScenarioCommand.Replace(
            _session.PersistenceSession.EnergizationScenario,
            new EnergizedSeed(seed.Seed.Id, candidate.Candidate.DeviceId,
                candidate.Candidate.Side)));
    }

    public void SetVisualizationDiagnostics(IReadOnlyList<string> diagnostics) =>
        VisualizationDiagnosticList.ItemsSource = diagnostics;

    private void OnExecuteAnalysis(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        _session.ExecuteEnergizationAnalysis();
        if (_session.Energization.CurrentResult is not null)
            ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnExit(object sender, RoutedEventArgs e) =>
        ExitRequested?.Invoke(this, EventArgs.Empty);

    private static string SideText(EnergizationSide side) => side switch
    {
        EnergizationSide.Bus => "母线侧",
        EnergizationSide.Line => "线路侧",
        EnergizationSide.SmallerNumber => "小号侧",
        EnergizationSide.LargerNumber => "大号侧",
        _ => side.ToString()
    };
}
