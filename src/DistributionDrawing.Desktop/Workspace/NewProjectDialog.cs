using System.Windows;
using System.Windows.Controls;

namespace DistributionDrawing.Desktop.Workspace;

internal sealed class NewProjectDialog : Window
{
    private readonly TextBox _titleBox = new();
    private readonly TextBox _descriptionBox = new();

    public NewProjectDialog()
    {
        Title = "新建工程";
        Width = 420;
        Height = 220;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "工程名称" });
        panel.Children.Add(_titleBox);
        panel.Children.Add(new TextBlock { Text = "工程说明", Margin = new Thickness(0, 8, 0, 0) });
        panel.Children.Add(_descriptionBox);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        var ok = new Button { Content = "确定", Width = 80, IsDefault = true };
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_titleBox.Text))
            {
                MessageBox.Show(this, "请输入工程名称。", "新建工程", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        };
        var cancel = new Button { Content = "取消", Width = 80, IsCancel = true, Margin = new Thickness(8, 0, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        Content = panel;
    }

    public string ProjectTitle => _titleBox.Text.Trim();

    public string? Description => string.IsNullOrWhiteSpace(_descriptionBox.Text)
        ? null
        : _descriptionBox.Text.Trim();
}
