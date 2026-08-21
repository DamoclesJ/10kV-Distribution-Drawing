using System.Windows;
using DistributionDrawing.Domain.Devices;

namespace DistributionDrawing.Desktop.PoleSwitchCreation;

public partial class PoleSwitchCreationDialog : Window
{
    public PoleSwitchCreationDialog()
    {
        InitializeComponent();
        SwitchKindInput.ItemsSource = new[]
        {
            SwitchKind.LoadSwitch,
            SwitchKind.IsolationSwitch,
            SwitchKind.CircuitBreaker,
            SwitchKind.DropoutFuse
        };
        SwitchKindInput.SelectedIndex = 0;
    }

    public SwitchKind SwitchKind => (SwitchKind)SwitchKindInput.SelectedItem;

    private void OnConfirm(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
