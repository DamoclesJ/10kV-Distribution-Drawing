using System.Windows;
using System.Windows.Controls;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Interaction;

namespace DistributionDrawing.Desktop.WorkTickets;

public partial class WorkTicketWorkspace : UserControl
{
    private sealed record Choice(Guid Id, string Display);
    private sealed record BoundaryChoice(IsolationBoundary Boundary, string Display);
    private sealed record FactKindChoice(string Code, string Display);
    private sealed record FactChoice(UserTicketFact Fact, string Display);
    private sealed record DraftChoice(string Section, DraftItem Item, string Display);
    private sealed record SectionChoice(string Code, string Display);
    private readonly WorkTicketAnalyzer _analyzer = new();
    private readonly IWorkTicketTextExporter _exporter = new WorkTicketTextExporter();
    private ProjectRuntimeSession? _session;
    private Guid? _ticketId;
    private List<IsolationBoundary> _boundaries = [];
    private List<UserTicketFact> _facts = [];
    private bool _binding;

    public WorkTicketWorkspace()
    {
        InitializeComponent();
        BoundarySide.ItemsSource = Enum.GetValues<BoundarySide>();
        BoundarySide.SelectedItem = DistributionDrawing.Application.WorkTickets.BoundarySide.Line;
        FactKind.ItemsSource = new[]
        {
            new FactKindChoice("RetainedLive", "保留/邻近带电"),
            new FactKindChoice("RedCloth61", "6.1 红布幔位置"),
            new FactKindChoice("RedCloth63", "6.3 红布幔位置"),
            new FactKindChoice("Barrier", "围栏位置"),
            new FactKindChoice("Sign", "标示牌位置"),
            new FactKindChoice("SimpleOperation", "工作班简单操作"),
            new FactKindChoice("OtherReversible", "其他可恢复措施"),
            new FactKindChoice("Risk", "风险/注意事项")
        };
        FactKind.DisplayMemberPath = "Display";
        FactKind.SelectedIndex = 0;
    }

    public event Action<TicketReference>? LocateRequested;
    public event Action? SelectionChanged;
    public WorkTicketSession? SelectedTicket => CurrentTicket();

    public void Bind(ProjectRuntimeSession? session)
    {
        if (_session is not null) _session.CommandStack.StateChanged -= OnCommandStateChanged;
        _session = session;
        _ticketId = null;
        if (_session is not null) _session.CommandStack.StateChanged += OnCommandStateChanged;
        Refresh();
    }

    public void ProposeBoundary(Guid switchDeviceId)
    {
        BoundaryDevice.SelectedItem = (BoundaryDevice.ItemsSource as IEnumerable<Choice>)?
            .FirstOrDefault(item => item.Id == switchDeviceId);
        BoundaryDevice.Focus();
    }

    public void Refresh()
    {
        _binding = true;
        try
        {
            DrawingDocument? drawing = _session?.PersistenceSession.Domain;
            TicketList.ItemsSource = _session?.PersistenceSession.WorkTickets.Tickets
                .Select((ticket, index) => new Choice(ticket.Id, $"工作票 {index + 1}: {ticket.Task.WorkObject}"))
                .ToArray() ?? [];
            if (_ticketId is not null && !(_session?.PersistenceSession.WorkTickets.Tickets
                    .Any(ticket => ticket.Id == _ticketId) ?? false)) _ticketId = null;
            _ticketId ??= _session?.PersistenceSession.WorkTickets.Tickets.FirstOrDefault()?.Id;
            TicketList.SelectedItem = (TicketList.ItemsSource as IEnumerable<Choice>)?
                .FirstOrDefault(item => item.Id == _ticketId);
            BoundaryDevice.ItemsSource = drawing?.Devices.OfType<SwitchDevice>()
                .OrderBy(device => device.DisplayName)
                .Select(device => new Choice(device.Id, DescribeSwitch(drawing, device))).ToArray() ?? [];
            ScopeList.ItemsSource = drawing?.WorkScopes
                .Select(scope => new Choice(scope.WorkScopeId, scope.Description)).ToArray() ?? [];
            GroundList.ItemsSource = drawing?.GroundingPoints
                .Select(point => new Choice(point.GroundingPointId,
                    $"{point.Number ?? "未编号"} — {point.Location} ({point.Target.Kind})")).ToArray() ?? [];
            WorkTicketSession? ticket = CurrentTicket();
            TaskContent.Text = ticket?.Task.Content ?? "";
            TaskObject.Text = ticket?.Task.WorkObject ?? "";
            _boundaries = ticket?.IsolationBoundaries.ToList() ?? [];
            RefreshBoundaries();
            _facts = ticket?.UserFacts.ToList() ?? [];
            RefreshFacts();
            foreach (Choice choice in ScopeList.Items)
                if (ticket?.WorkScopeIds.Contains(choice.Id) == true) ScopeList.SelectedItems.Add(choice);
            foreach (Choice choice in GroundList.Items)
                if (ticket?.GroundingPointIds.Contains(choice.Id) == true) GroundList.SelectedItems.Add(choice);
            TaskPreview.Text = ticket is null ? "工作地点 / 设备：________    工作内容：________"
                : $"工作地点 / 设备：{ticket.Task.WorkObject}\n工作内容：{ticket.Task.Content}";
            WorkTicketDraft? draft = ticket?.Draft;
            DraftItemList.ItemsSource = draft?.Sections.SelectMany(section => section.Items.Select(item =>
                new DraftChoice(section.Code, item,
                    $"{section.Code} [{item.Source} / {item.Origin}]  {item.CurrentText}"))).ToArray() ?? [];
            bool stale = drawing is not null && ticket is not null && _analyzer.IsStale(drawing, ticket);
            SectionList.ItemsSource = new[] { "6.1", "6.2", "6.3", "6.4", "6.5", "16.1" }
                .Select(code => new SectionChoice(code,
                    $"{code}  {Label(ticket?.EffectiveCompletion(code, stale) ?? SectionCompletion.Empty)}"))
                .ToArray();
            ShowSection("6.1", Status61, Text61, ticket, stale);
            ShowSection("6.2", Status62, Text62, ticket, stale);
            ShowSection("6.3", Status63, Text63, ticket, stale);
            ShowSection("6.4", Status64, Text64, ticket, stale);
            ShowSection("6.5", Status65, Text65, ticket, stale);
            ShowSection("16.1", Status161, Text161, ticket, stale);
            IssueSummary.Text = ticket is null ? "请创建工作票。" : stale
                ? "图纸或工作票准备内容发生变化；六栏需重新分析。"
                : draft is null ? "请填写工作任务、隔离边界和实际工作范围，然后运行分析。"
                : string.Join("   ", draft.Sections.Select(section =>
                    $"{section.Code} {Label(ticket!.EffectiveCompletion(section.Code, stale))}")) +
                  (ticket.Analysis?.Issues.Count > 0 ? "\n待现场核实：" +
                      string.Join("；", ticket.Analysis.Issues) : "");
        }
        finally { _binding = false; }
    }

    private void ShowSection(string code, TextBlock status, TextBox editor, WorkTicketSession? ticket, bool stale)
    {
        SectionDraft? section = ticket?.Draft?.Sections.SingleOrDefault(item => item.Code == code);
        status.Text = $"{code}  {Label(ticket?.EffectiveCompletion(code, stale) ?? SectionCompletion.Empty)}";
        editor.Text = section?.Text ?? "";
        editor.IsEnabled = _session is not null && _ticketId is not null;
    }

    private static string Label(SectionCompletion state) => state switch
    {
        SectionCompletion.Empty => "未生成",
        SectionCompletion.Generated => "已生成",
        SectionCompletion.NeedsInput => "待补充",
        SectionCompletion.NeedsConfirmation => "待确认",
        SectionCompletion.Completed => "已完成",
        SectionCompletion.Stale => "需重新分析",
        _ => state.ToString()
    };

    private static string DescribeSwitch(DrawingDocument drawing, SwitchDevice device)
    {
        RingCabinetInterval? interval = drawing.Devices.OfType<RingCabinet>()
            .SelectMany(cabinet => cabinet.Intervals)
            .SingleOrDefault(item => item.SwitchDevices.Any(sw => sw.Id == device.Id));
        return interval is null ? $"{device.DisplayName} ({device.SwitchKind})"
            : $"{interval.DisplayName} / {device.DisplayName} ({interval.IntervalKind}, {interval.GroundingStructureKind})";
    }

    private WorkTicketSession? CurrentTicket() => _session?.PersistenceSession.WorkTickets.Selected(_ticketId);

    private void OnCommandStateChanged(object? sender, EventArgs e) => Refresh();

    private void OnTicketSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_binding) return;
        Guid? target = (e.AddedItems.OfType<Choice>().FirstOrDefault() ??
            TicketList.SelectedItem as Choice)?.Id;
        CommitPendingEdits();
        _ticketId = target;
        Refresh();
        SelectionChanged?.Invoke();
    }

    private void OnSectionSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_binding || SectionList.SelectedItem is not SectionChoice selected) return;
        TextBox editor = Editors().Single(item => item.Code == selected.Code).Editor;
        editor.BringIntoView();
        editor.Focus();
    }

    private void OnCreate(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        CommitPendingEdits();
        WorkTicketSession ticket = WorkTicketSession.Create();
        _session.CommandStack.ExecuteCommand(new WorkTicketChangeCommand(
            _session.PersistenceSession.WorkTickets, null, ticket));
        _ticketId = ticket.Id;
        Refresh();
        SelectionChanged?.Invoke();
    }

    private void OnAddBoundary(object sender, RoutedEventArgs e)
    {
        if (BoundaryDevice.SelectedItem is not Choice device || BoundarySide.SelectedItem is not BoundarySide side)
            return;
        IsolationBoundary boundary = new(device.Id, side,
            (BoundaryTerminal.SelectedItem as Choice)?.Id);
        if (!_boundaries.Contains(boundary)) _boundaries.Add(boundary);
        RefreshBoundaries();
    }

    private void OnBoundaryDeviceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_session?.PersistenceSession.Domain is not { } drawing ||
            BoundaryDevice.SelectedItem is not Choice selected)
        {
            BoundaryTerminal.ItemsSource = null;
            return;
        }
        SwitchDevice device = drawing.Devices.OfType<SwitchDevice>().Single(item => item.Id == selected.Id);
        BoundaryTerminal.ItemsSource = device.TerminalIds.Select(id =>
        {
            var terminal = drawing.Terminals.Single(item => item.Id == id);
            return new Choice(id, terminal.Role);
        }).ToArray();
        BoundaryTerminal.SelectedIndex = -1;
    }

    private void OnRemoveBoundary(object sender, RoutedEventArgs e)
    {
        if (BoundaryList.SelectedItem is BoundaryChoice selected)
        {
            _boundaries.Remove(selected.Boundary);
            RefreshBoundaries();
        }
    }

    private void RefreshBoundaries()
    {
        DrawingDocument? drawing = _session?.PersistenceSession.Domain;
        BoundaryList.ItemsSource = _boundaries.Select(item => new BoundaryChoice(item,
            $"{drawing?.Devices.FirstOrDefault(device => device.Id == item.DeviceId)?.DisplayName} — {item.Side} 侧 / " +
            $"{drawing?.Terminals.FirstOrDefault(terminal => terminal.Id == item.TerminalId)?.Role ?? "未指定端子"}"))
            .ToArray();
    }

    private void RefreshFacts() => FactList.ItemsSource = _facts.Select(item =>
        new FactChoice(item, $"{item.Kind}: {item.Text}" +
            (item.RestorationText is null ? "" : $" → {item.RestorationText}") +
            $" {(item.Confirmed ? "已确认" : "待确认")}"))
        .ToArray();

    private void OnAddFact(object sender, RoutedEventArgs e)
    {
        if (FactKind.SelectedItem is not FactKindChoice kind || string.IsNullOrWhiteSpace(FactText.Text)) return;
        if (kind.Code == "OtherReversible" && string.IsNullOrWhiteSpace(FactRestorationText.Text))
        {
            MessageBox.Show(Window.GetWindow(this), "请填写对应的现场恢复措施。", "工作票准备");
            return;
        }
        TicketReference[] references = BoundaryDevice.SelectedItem is Choice device
            ? [new TicketReference(TicketReferenceKind.Device, device.Id)] : [];
        _facts.Add(new UserTicketFact(kind.Code, FactText.Text.Trim(), references,
            FactConfirmed.IsChecked == true,
            string.IsNullOrWhiteSpace(FactRestorationText.Text) ? null : FactRestorationText.Text.Trim()));
        FactText.Clear();
        FactRestorationText.Clear();
        RefreshFacts();
    }

    private void OnRemoveFact(object sender, RoutedEventArgs e)
    {
        if (FactList.SelectedItem is FactChoice selected)
        {
            _facts.Remove(selected.Fact);
            RefreshFacts();
        }
    }

    private WorkTicketSession CaptureSetup(WorkTicketSession ticket) => ticket with
    {
        Task = new WorkTask(TaskContent.Text.Trim(), TaskObject.Text.Trim()),
        IsolationBoundaries = _boundaries.ToArray(),
        WorkScopeIds = ScopeList.SelectedItems.Cast<Choice>().Select(item => item.Id).ToArray(),
        GroundingPointIds = GroundList.SelectedItems.Cast<Choice>().Select(item => item.Id).ToArray(),
        UserFacts = _facts.ToArray()
    };

    public void CommitPendingEdits()
    {
        if (_session is null || CurrentTicket() is not { } before) return;
        WorkTicketSession after = CaptureEdits(CaptureSetup(before));
        bool setupChanged = before.Task != after.Task ||
            !before.IsolationBoundaries.SequenceEqual(after.IsolationBoundaries) ||
            !before.WorkScopeIds.SequenceEqual(after.WorkScopeIds) ||
            !before.GroundingPointIds.SequenceEqual(after.GroundingPointIds) ||
            !before.UserFacts.SequenceEqual(after.UserFacts);
        if (!setupChanged && ReferenceEquals(before.Draft, after.Draft)) return;
        _session.CommandStack.ExecuteCommand(new WorkTicketChangeCommand(
            _session.PersistenceSession.WorkTickets, before, after));
        Refresh();
    }

    private WorkTicketSession CaptureEdits(WorkTicketSession ticket)
    {
        if (ticket.Draft is null) return ticket;
        foreach ((string code, TextBox editor) in Editors())
            if (editor.Text != ticket.Draft!.Section(code).Text)
                ticket = ticket.EditSection(code, editor.Text);
        return ticket;
    }

    private IEnumerable<(string Code, TextBox Editor)> Editors()
    {
        yield return ("6.1", Text61); yield return ("6.2", Text62);
        yield return ("6.3", Text63); yield return ("6.4", Text64);
        yield return ("6.5", Text65); yield return ("16.1", Text161);
    }

    private void OnSaveEdit(object sender, RoutedEventArgs e)
    {
        CommitPendingEdits();
    }

    private void OnAnalyze(object sender, RoutedEventArgs e)
    {
        if (_session is null || CurrentTicket() is not { } before) return;
        try
        {
            WorkTicketSession setup = CaptureEdits(CaptureSetup(before));
            WorkTicketSession after = _analyzer.Analyze(_session.PersistenceSession.Domain, setup);
            _session.CommandStack.ExecuteCommand(new WorkTicketChangeCommand(
                _session.PersistenceSession.WorkTickets, before, after));
            Refresh();
        }
        catch (Exception error)
        {
            MessageBox.Show(Window.GetWindow(this), error.Message, "工作票分析", MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnCopySection(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string code } && CurrentTicket() is { } ticket &&
            CanCopy(ticket, code) &&
            CaptureEdits(ticket).Draft is { } draft)
        {
            string text = _exporter.Section(draft, code);
            if (!string.IsNullOrWhiteSpace(text)) System.Windows.Clipboard.SetText(text);
            else MessageBox.Show(Window.GetWindow(this), "该栏还没有可复制的已确认文字。", "工作票复制");
        }
    }

    private void OnCopyAll(object sender, RoutedEventArgs e)
    {
        if (CurrentTicket() is { } ticket && CanCopy(ticket, null) &&
            CaptureEdits(ticket).Draft is { } draft)
        {
            if (draft.Sections.Any(section => string.IsNullOrWhiteSpace(
                    _exporter.Section(draft, section.Code))))
            {
                MessageBox.Show(Window.GetWindow(this), "仍有待补充或待确认的空白栏目。", "工作票复制");
                return;
            }
            System.Windows.Clipboard.SetText(_exporter.SixSections(draft));
        }
    }

    private bool CanCopy(WorkTicketSession ticket, string? code)
    {
        bool staleSection = ticket.Draft?.Sections.Any(section =>
            (code is null || section.Code == code) && section.Completion == SectionCompletion.Stale) == true;
        bool missingInput = ticket.Draft?.Sections.Any(section =>
            (code is null || section.Code == code) && section.Completion == SectionCompletion.NeedsInput &&
            section.Items.Any(item => item.CurrentText.Contains("待填写编号", StringComparison.Ordinal))) == true;
        bool staleFingerprint = _session is not null &&
            _analyzer.IsStale(_session.PersistenceSession.Domain, ticket);
        bool unconfirmed = ticket.Draft?.Sections.Any(section =>
            (code is null || section.Code == code) && section.Completion != SectionCompletion.Completed) != false;
        if (!staleSection && !staleFingerprint && !missingInput && !unconfirmed) return true;
        MessageBox.Show(Window.GetWindow(this), missingInput
                ? "请先补全工作接地线编号。" : staleSection || staleFingerprint
                    ? "相关栏目已失效，请重新分析后复制。" : "请先确认该栏目。", "工作票复制",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    private void OnConfirmSection(object sender, RoutedEventArgs e)
    {
        if (_session is null || CurrentTicket() is not { } before || sender is not Button { Tag: string code })
            return;
        WorkTicketSession setup = CaptureSetup(before);
        if (before.Draft is null || _analyzer.IsStale(_session.PersistenceSession.Domain, setup))
        {
            MessageBox.Show(Window.GetWindow(this), "请先重新分析，再确认栏目。", "工作票确认");
            return;
        }
        try
        {
            WorkTicketSession after = CaptureEdits(setup).ConfirmSection(code);
            _session.CommandStack.ExecuteCommand(new WorkTicketChangeCommand(
                _session.PersistenceSession.WorkTickets, before, after));
            Refresh();
        }
        catch (InvalidOperationException error)
        {
            MessageBox.Show(Window.GetWindow(this), error.Message, "工作票确认",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnRestoreGenerated63(object sender, RoutedEventArgs e)
    {
        if (_session is null || CurrentTicket() is not { Draft: not null } before) return;
        WorkTicketSession after = CaptureEdits(CaptureSetup(before)).RestoreGeneratedSection("6.3");
        _session.CommandStack.ExecuteCommand(new WorkTicketChangeCommand(
            _session.PersistenceSession.WorkTickets, before, after));
        Refresh();
    }

    private void OnLocateItem(object sender, RoutedEventArgs e)
    {
        if (DraftItemList.SelectedItem is DraftChoice selected &&
            selected.Item.RelatedModelRefs.FirstOrDefault() is { } reference)
            LocateRequested?.Invoke(reference);
    }
}

internal sealed class WorkTicketChangeCommand(
    WorkTicketDataRoot root, WorkTicketSession? before, WorkTicketSession after) : ICommand
{
    public void Execute()
    {
        if (before is null) root.Add(after); else root.Replace(after);
    }

    public void Undo()
    {
        if (before is null) root.Remove(after.Id); else root.Replace(before);
    }

    public void Redo() => Execute();
}
