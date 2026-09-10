using System.Windows;
using System.Windows.Controls;
using DistributionDrawing.Domain.Devices.CustomerStations;

namespace DistributionDrawing.Desktop.CustomerStationCreationUi;

public sealed class CustomerStationCreationDialog : Window
{
    private sealed record KindOption(string Text, StationKind Value)
    {
        public override string ToString() => Text;
    }

    private readonly ComboBox _kind = new();
    private readonly ComboBox _feederCount = new();
    private readonly TextBox _nameA = new();
    private readonly TextBox _nameB = new();
    private readonly StackPanel _nameBPanel = new();

    public CustomerStationCreationDialog()
    {
        Title = "新增用户站";
        Width = 380;
        Height = 330;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _kind.ItemsSource = new[]
        {
            new KindOption("箱式用户站", StationKind.BoxStation),
            new KindOption("室内用户站", StationKind.IndoorStation)
        };
        _kind.SelectedIndex = 0;
        _kind.SelectionChanged += (_, _) => RefreshInputs();
        _feederCount.ItemsSource = new[] { "单电源", "双电源" };
        _feederCount.SelectedIndex = 0;
        _feederCount.SelectionChanged += (_, _) => RefreshInputs();

        var confirm = new Button { Content = "确定", Width = 80, IsDefault = true };
        confirm.Click += (_, _) => Confirm();
        var cancel = new Button
        {
            Content = "取消",
            Width = 80,
            IsCancel = true,
            Margin = new Thickness(8, 0, 0, 0)
        };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { confirm, cancel }
        };
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(Label("用户站类型"));
        panel.Children.Add(_kind);
        panel.Children.Add(Label("电源数量"));
        panel.Children.Add(_feederCount);
        panel.Children.Add(Label("进线 1 名称"));
        panel.Children.Add(_nameA);
        _nameBPanel.Children.Add(Label("进线 2 名称"));
        _nameBPanel.Children.Add(_nameB);
        panel.Children.Add(_nameBPanel);
        panel.Children.Add(new Border { Height = 18 });
        panel.Children.Add(buttons);
        Content = panel;
        RefreshInputs();
    }

    public StationKind SelectedKind => ((KindOption)_kind.SelectedItem).Value;

    public IReadOnlyList<string> FeederDisplayNames { get; private set; } = [];

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        Margin = new Thickness(0, 8, 0, 4)
    };

    private void RefreshInputs()
    {
        bool indoor = SelectedKind == StationKind.IndoorStation;
        _feederCount.IsEnabled = indoor;
        if (!indoor) _feederCount.SelectedIndex = 0;
        _nameBPanel.Visibility = indoor && _feederCount.SelectedIndex == 1
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void Confirm()
    {
        string[] names = SelectedKind == StationKind.IndoorStation &&
            _feederCount.SelectedIndex == 1
            ? [_nameA.Text, _nameB.Text]
            : [_nameA.Text];
        if (names.Any(string.IsNullOrWhiteSpace))
        {
            MessageBox.Show(this, "每路进线名称均不能为空。", "输入无效",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        FeederDisplayNames = names.Select(name => name.Trim()).ToArray();
        DialogResult = true;
        Close();
    }
}
