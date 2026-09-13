using System.Windows;
using System.Windows.Controls;
using DistributionDrawing.Domain.Devices;

namespace DistributionDrawing.Desktop.TransformerCreationUi;

public sealed class TransformerCreationDialog : Window
{
    private sealed record KindOption(string Text, TransformerKind Value)
    {
        public override string ToString() => Text;
    }

    private readonly ComboBox _kind = new();
    private readonly TextBox _displayName = new();
    private string? _validatedDisplayName;

    public TransformerCreationDialog()
    {
        Title = "新增变压器";
        Width = 340;
        Height = 220;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _kind.ItemsSource = new[]
        {
            new KindOption("柱上公变", TransformerKind.PublicPoleMounted),
            new KindOption("柱上专变", TransformerKind.DedicatedPoleMounted),
            new KindOption("站内公变", TransformerKind.PublicIndoor)
        };
        _kind.SelectedIndex = 0;

        var confirm = new Button { Content = "确定", Width = 80, IsDefault = true };
        confirm.Click += (_, _) =>
        {
            if (!TryNormalizeDisplayName(_displayName.Text, out string displayName))
            {
                MessageBox.Show(
                    this,
                    "请输入变压器名称。",
                    "输入无效",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                _displayName.Focus();
                return;
            }

            _validatedDisplayName = displayName;
            DialogResult = true;
            Close();
        };
        var cancel = new Button { Content = "取消", Width = 80, IsCancel = true, Margin = new Thickness(8, 0, 0, 0) };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { confirm, cancel }
        };
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = "变压器名称", Margin = new Thickness(0, 0, 0, 4) });
        panel.Children.Add(_displayName);
        panel.Children.Add(new Border { Height = 12 });
        panel.Children.Add(new TextBlock { Text = "业务类型", Margin = new Thickness(0, 0, 0, 4) });
        panel.Children.Add(_kind);
        panel.Children.Add(new Border { Height = 18 });
        panel.Children.Add(buttons);
        Content = panel;
    }

    public TransformerKind SelectedKind => ((KindOption)_kind.SelectedItem).Value;

    public string DisplayName => _validatedDisplayName ?? throw new InvalidOperationException(
        "Transformer creation has not been confirmed.");

    internal static bool TryNormalizeDisplayName(string? input, out string displayName)
    {
        displayName = input?.Trim() ?? string.Empty;
        return displayName.Length > 0;
    }
}
