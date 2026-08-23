using System.Windows;
using DistributionDrawing.Domain.Devices;

namespace DistributionDrawing.Desktop.PoleSwitchCreation;

public partial class PoleSwitchCreationDialog : Window
{
    private sealed record SwitchKindOption(string Name, SwitchKind Kind);

    public PoleSwitchCreationDialog()
    {
        InitializeComponent();
        SwitchKindInput.ItemsSource = new[]
        {
            new SwitchKindOption("柱上负荷开关", SwitchKind.LoadSwitch),
            new SwitchKindOption("柱上隔离开关", SwitchKind.IsolationSwitch),
            new SwitchKindOption("柱上断路器", SwitchKind.CircuitBreaker),
            new SwitchKindOption("跌落式熔断器", SwitchKind.DropoutFuse)
        };
        SwitchKindInput.DisplayMemberPath = nameof(SwitchKindOption.Name);
        SwitchKindInput.SelectedIndex = 0;
    }

    public SwitchKind SwitchKind =>
        ((SwitchKindOption)SwitchKindInput.SelectedItem).Kind;

    private void OnConfirm(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
