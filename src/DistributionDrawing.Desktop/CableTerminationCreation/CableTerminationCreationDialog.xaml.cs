using System.Windows;

namespace DistributionDrawing.Desktop.CableTerminationCreation;

public partial class CableTerminationCreationDialog : Window
{
    public CableTerminationCreationDialog()
    {
        InitializeComponent();
    }

    public string? DisplayName { get; private set; }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        DisplayName = string.IsNullOrWhiteSpace(DisplayNameInput.Text)
            ? null
            : DisplayNameInput.Text.Trim();
        DialogResult = true;
    }
}
