using System.Windows;
using System.Windows.Controls;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Desktop.ViewModels;
using DistributionDrawing.Desktop.WorkTickets;
using DistributionDrawing.Rendering.Wpf.Interaction;

namespace DistributionDrawing.Desktop;

public partial class MainWindow
{
    private sealed record TicketSideChoice(BoundarySide Side, string Display);
    private sealed record TicketRangeChoice(Guid Id, string Display);
    private readonly TicketBoundarySlotCollection _ticketBoundarySlots = new();
    private readonly List<WorkScopeItem> _ticketScopeItems = [];
    private readonly TicketRangePickerState _ticketRangePicker = new();
    private readonly WorkTicketRangeNavigationState _workTicketRangeNavigation = new();

    private void OnOpenTicketRange(object sender, RoutedEventArgs e)
    {
        if (_workspace.CurrentSession is not { } session) return;
        bool openedFromTicketWorkspace = TicketWorkspace.Visibility == Visibility.Visible;
        if (openedFromTicketWorkspace)
            _workTicketRangeNavigation.RangeOpenedFromTicketWorkspace();
        TicketWorkspace.BeginRangeEdit();
        DrawingWorkspace.Visibility = Visibility.Visible;
        TicketWorkspace.Visibility = Visibility.Collapsed;
        _drawingTools.Cancel();
        CancelProfessionalPicking();
        _shellViewModel.Toolbox.SetSelectedMode(DesktopToolMode.Select);
        WorkTicketSession? ticket = TicketWorkspace.PendingSetup ?? TicketWorkspace.SelectedTicket;
        WorkTask? task = ticket?.Task;
        TicketRangeTaskContent.Text = task?.Content ?? "";
        TicketRangeTaskObject.Text = task?.WorkObject ?? "";
        _ticketBoundarySlots.Load(ticket?.IsolationBoundaries ?? [], boundary =>
            WorkTicketRangeSetup.TryResolve(session.PersistenceSession.Domain,
                boundary.DeviceId, boundary.Side, out IsolationBoundary? resolved, out _)
                ? resolved : null);
        _ticketScopeItems.Clear();
        if (ticket is not null)
        {
            _ticketScopeItems.AddRange(ticket.WorkScopeIds.Select(id =>
                new WorkScopeItem(WorkScopeItemKind.ElectricalRange, id)));
            _ticketScopeItems.AddRange(ticket.WorkScopeItems);
        }
        TicketElectricalRangeChoice.ItemsSource = session.PersistenceSession.Domain.WorkScopes
            .Select(scope => new TicketRangeChoice(scope.WorkScopeId, scope.Description)).ToArray();
        TicketRangePanel.Visibility = Visibility.Visible;
        TicketRangeStatus.Text = "请选择每个边界设备及电气侧，并指定实际工作范围。";
        RefreshTicketRangePanel();
    }

    private void OnAddTicketBoundarySlot(object sender, RoutedEventArgs e)
    {
        _ticketBoundarySlots.Add();
        RefreshTicketRangePanel();
    }

    private void OnRemoveTicketBoundarySlot(object sender, RoutedEventArgs e)
    {
        RemoveTicketBoundarySlot(_ticketBoundarySlots.Count - 1);
    }

    private void RemoveTicketBoundarySlot(int index)
    {
        if (_ticketBoundarySlots.Count <= 1 || index < 0) return;
        CancelTicketRangePicking();
        _ticketBoundarySlots.Remove(index);
        RefreshTicketRangePanel();
    }

    private void RefreshTicketRangePanel()
    {
        DrawingDocument? drawing = _workspace.CurrentSession?.PersistenceSession.Domain;
        TicketBoundaryRows.Children.Clear();
        for (int index = 0; index < _ticketBoundarySlots.Count; index++)
        {
            TicketBoundarySlot slot = _ticketBoundarySlots[index];
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            row.Children.Add(new TextBlock
            {
                Text = $"Boundary {WorkTicketRangeSetup.SlotName(index)}",
                FontWeight = FontWeights.SemiBold
            });
            var actions = new WrapPanel { Margin = new Thickness(0, 3, 0, 2) };
            var select = new Button { Content = "选择", Tag = index, Margin = new Thickness(0, 0, 4, 0) };
            select.Click += OnPickTicketBoundary;
            actions.Children.Add(select);
            var remove = new Button { Content = "移除", Tag = index,
                IsEnabled = _ticketBoundarySlots.Count > 1 };
            remove.Click += (sender, args) => RemoveTicketBoundarySlot((int)((Button)sender).Tag);
            actions.Children.Add(remove);
            row.Children.Add(actions);
            if (slot.DeviceId is Guid deviceId && drawing?.Devices.OfType<SwitchDevice>()
                    .SingleOrDefault(device => device.Id == deviceId) is { } device)
            {
                row.Children.Add(new TextBlock
                {
                    Text = WorkTicketWorkspace.FormatBoundaryDisplay(drawing,
                        slot.Resolved ?? new IsolationBoundary(deviceId, BoundarySide.Unknown)),
                    TextWrapping = TextWrapping.Wrap
                });
                var side = new ComboBox { Tag = index, DisplayMemberPath = "Display",
                    Margin = new Thickness(0, 3, 0, 0) };
                TicketSideChoice[] choices = WorkTicketRangeSetup.AvailableSides(device)
                    .Select(value => new TicketSideChoice(value,
                        WorkTicketWorkspace.BoundarySideName(value))).ToArray();
                side.ItemsSource = choices;
                side.SelectedItem = choices.FirstOrDefault(value => value.Side == slot.Side);
                side.SelectionChanged += OnTicketBoundarySideChanged;
                row.Children.Add(side);
                if (slot.Resolved is null)
                    row.Children.Add(new TextBlock { Text = "待确认 / 无法确定电气侧",
                        TextWrapping = TextWrapping.Wrap });
            }
            else row.Children.Add(new TextBlock { Text = "未选择" });
            TicketBoundaryRows.Children.Add(row);
        }

        TicketScopeRows.Children.Clear();
        for (int index = 0; index < _ticketScopeItems.Count; index++)
        {
            WorkScopeItem item = _ticketScopeItems[index];
            string name = item.Kind == WorkScopeItemKind.Equipment
                ? drawing?.Devices.FirstOrDefault(device => device.Id == item.TargetId)?.DisplayName ?? "设备已移除"
                : drawing?.WorkScopes.FirstOrDefault(scope => scope.WorkScopeId == item.TargetId)?.Description ?? "电气区段已移除";
            var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
            row.Children.Add(new TextBlock { Text = name, Width = 205, TextWrapping = TextWrapping.Wrap });
            var remove = new Button { Content = "移除", Tag = index };
            remove.Click += (sender, args) =>
            {
                _ticketScopeItems.RemoveAt((int)((Button)sender).Tag);
                RefreshTicketRangePanel();
            };
            row.Children.Add(remove);
            TicketScopeRows.Children.Add(row);
        }
    }

    private void OnPickTicketBoundary(object sender, RoutedEventArgs e)
    {
        CancelTicketRangePicking();
        int index = (int)((Button)sender).Tag;
        _ticketRangePicker.BeginBoundary(index, _ticketBoundarySlots[index]);
        TicketRangeStatus.Text = $"请在图纸中点击 Boundary {WorkTicketRangeSetup.SlotName(index)} 的开关设备；Esc 或右键取消。";
        UpdateCanvasStatus();
    }

    private void OnTicketBoundarySideChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { SelectedItem: TicketSideChoice choice, Tag: int index } ||
            _workspace.CurrentSession is not { } session ||
            _ticketBoundarySlots[index].DeviceId is not Guid deviceId) return;
        TicketBoundarySlot slot = _ticketBoundarySlots[index];
        bool resolved = WorkTicketRangeSetup.TryResolve(session.PersistenceSession.Domain,
            deviceId, choice.Side, out IsolationBoundary? boundary, out string issue);
        _ticketBoundarySlots.Replace(index, slot with
        {
            Side = choice.Side,
            Resolved = resolved ? boundary : null
        });
        TicketRangeStatus.Text = resolved ? "电气侧已解析。" : issue;
        if (resolved && _ticketRangePicker.Mode == TicketRangePickMode.ChoosingBoundarySide &&
            _ticketRangePicker.BoundaryIndex == index)
        {
            _ticketRangePicker.SideChosen();
            UpdateCanvasStatus();
        }
        RefreshTicketRangePanel();
    }

    private void OnPickTicketEquipment(object sender, RoutedEventArgs e)
    {
        CancelTicketRangePicking();
        _ticketRangePicker.BeginEquipment();
        TicketRangeStatus.Text = "请在图纸中点击实际工作设备；Esc 或右键取消。";
        UpdateCanvasStatus();
    }

    private void OnAddTicketElectricalRange(object sender, RoutedEventArgs e)
    {
        if (TicketElectricalRangeChoice.SelectedItem is not TicketRangeChoice choice) return;
        var item = new WorkScopeItem(WorkScopeItemKind.ElectricalRange, choice.Id);
        if (!_ticketScopeItems.Contains(item)) _ticketScopeItems.Add(item);
        RefreshTicketRangePanel();
    }

    private bool HandleTicketRangePick(SelectionReference? target)
    {
        if (_ticketRangePicker.Mode == TicketRangePickMode.Idle) return false;
        DrawingDocument? drawing = _workspace.CurrentSession?.PersistenceSession.Domain;
        if (drawing is null) return true;
        var selected = _selectionResolver.Resolve(target);
        if (_ticketRangePicker.Mode == TicketRangePickMode.PickingBoundaryDevice)
        {
            SwitchDevice? device = selected?.SwitchDevice ?? selected?.AttachedDevice as SwitchDevice;
            if (device is null || WorkTicketRangeSetup.AvailableSides(device).Count == 0)
            {
                TicketRangeStatus.Text = "请选择受支持的开关或隔离刀闸。";
                return true;
            }
            int index = _ticketRangePicker.BoundaryIndex!.Value;
            _ticketBoundarySlots.Replace(index, new TicketBoundarySlot(device.Id));
            _ticketRangePicker.DevicePicked();
            TicketRangeStatus.Text = "请选择该设备的专业电气侧。";
        }
        else if (_ticketRangePicker.Mode == TicketRangePickMode.ChoosingBoundarySide)
        {
            TicketRangeStatus.Text = "请在右侧选择该设备的专业电气侧；画布设备拾取已结束。";
            return true;
        }
        else if (_ticketRangePicker.Mode == TicketRangePickMode.PickingWorkScopeEquipment)
        {
            Guid? deviceId = selected?.AttachedDevice?.Id ?? selected?.SwitchDevice?.Id ??
                selected?.RingCabinet?.Id ?? selected?.Pole?.Id ??
                selected?.Transformer?.Id ?? selected?.CustomerStation?.Id ??
                selected?.CableTermination?.Id;
            if (deviceId is not Guid id || !drawing.Devices.Any(device => device.Id == id))
            {
                TicketRangeStatus.Text = "请选择图纸中的设备。";
                return true;
            }
            var item = new WorkScopeItem(WorkScopeItemKind.Equipment, id);
            if (!_ticketScopeItems.Contains(item)) _ticketScopeItems.Add(item);
            TicketRangeStatus.Text = "实际工作设备已选择。";
        }
        if (_ticketRangePicker.Mode == TicketRangePickMode.PickingWorkScopeEquipment)
            _ticketRangePicker.Cancel();
        RefreshTicketRangePanel();
        UpdateCanvasStatus();
        return true;
    }

    private void OnConfirmTicketRange(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_ticketRangePicker.Mode != TicketRangePickMode.Idle)
                throw new InvalidOperationException("请先完成或取消当前选择。");
            TicketWorkspace.ApplyRange(_ticketBoundarySlots.Slots.Select(slot => slot.Resolved).ToArray(),
                _ticketScopeItems.ToArray(),
                new WorkTask(TicketRangeTaskContent.Text.Trim(), TicketRangeTaskObject.Text.Trim()));
            _workTicketRangeNavigation.RangeConfirmed();
            TicketWorkspace.CompleteRangeEdit();
            TicketRangeStatus.Text = string.IsNullOrWhiteSpace(TicketRangeTaskContent.Text) ||
                string.IsNullOrWhiteSpace(TicketRangeTaskObject.Text)
                ? "范围已确认；请补全工作内容和工作对象后分析。"
                : "本次工作范围已确认，可切换到工作票分析。";
            TicketRangePanel.Visibility = Visibility.Collapsed;
            UpdateCanvasStatus();
            ShowTransientFeedback(TicketRangeStatus.Text);
        }
        catch (InvalidOperationException error)
        {
            TicketRangeStatus.Text = error.Message;
        }
    }

    private void OnCancelTicketRange(object sender, RoutedEventArgs e)
    {
        CancelTicketRangePicking();
        DiscardTicketRangeBuffer();
        TicketRangePanel.Visibility = Visibility.Collapsed;
        UpdateCanvasStatus();
    }

    private void DiscardTicketRangeBuffer()
    {
        _ticketBoundarySlots.Discard();
        _ticketScopeItems.Clear();
        TicketRangeTaskContent.Clear();
        TicketRangeTaskObject.Clear();
    }

    private void CancelTicketRangePicking()
    {
        (int Index, TicketBoundarySlot Previous)? rollback = _ticketRangePicker.Cancel();
        if (rollback is { } value && value.Index < _ticketBoundarySlots.Count)
            _ticketBoundarySlots.Replace(value.Index, value.Previous);
        if (TicketRangePanel.Visibility == Visibility.Visible) RefreshTicketRangePanel();
    }
}
