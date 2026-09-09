using System.Windows;
using System.Windows.Controls;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Desktop.TransformerCreationUi;

public sealed class TransformerCreationDialog : Window
{
    private sealed record KindOption(string Text, TransformerKind Value)
    {
        public override string ToString() => Text;
    }

    private sealed record OrientationOption(string Text, TransformerOrientation Value)
    {
        public override string ToString() => Text;
    }

    private readonly ComboBox _kind = new();
    private readonly ComboBox _orientation = new();

    public TransformerCreationDialog()
    {
        Title = "新增变压器";
        Width = 340;
        Height = 230;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _kind.ItemsSource = new[]
        {
            new KindOption("柱上公变", TransformerKind.PublicPoleMounted),
            new KindOption("柱上专变", TransformerKind.DedicatedPoleMounted),
            new KindOption("站内公变", TransformerKind.PublicIndoor)
        };
        _kind.SelectedIndex = 0;
        _kind.SelectionChanged += (_, _) => UpdateOrientation();

        _orientation.ItemsSource = new[]
        {
            new OrientationOption("水平", TransformerOrientation.Horizontal),
            new OrientationOption("垂直", TransformerOrientation.Vertical)
        };
        _orientation.SelectedIndex = 0;

        var confirm = new Button { Content = "确定", Width = 80, IsDefault = true };
        confirm.Click += (_, _) =>
        {
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
        panel.Children.Add(new TextBlock { Text = "业务类型", Margin = new Thickness(0, 0, 0, 4) });
        panel.Children.Add(_kind);
        panel.Children.Add(new TextBlock { Text = "站内公变方向", Margin = new Thickness(0, 14, 0, 4) });
        panel.Children.Add(_orientation);
        panel.Children.Add(new Border { Height = 18 });
        panel.Children.Add(buttons);
        Content = panel;
        UpdateOrientation();
    }

    public TransformerKind SelectedKind => ((KindOption)_kind.SelectedItem).Value;

    public TransformerOrientation SelectedOrientation => SelectedKind == TransformerKind.PublicIndoor
        ? ((OrientationOption)_orientation.SelectedItem).Value
        : TransformerOrientation.Vertical;

    private void UpdateOrientation()
    {
        bool isIndoor = SelectedKind == TransformerKind.PublicIndoor;
        _orientation.IsEnabled = isIndoor;
        _orientation.SelectedIndex = isIndoor ? 0 : 1;
    }
}
