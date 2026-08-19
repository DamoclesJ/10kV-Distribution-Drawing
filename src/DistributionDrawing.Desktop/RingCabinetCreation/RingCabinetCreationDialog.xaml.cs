using System.Windows;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;

namespace DistributionDrawing.Desktop.RingCabinetCreation;

public partial class RingCabinetCreationDialog : Window
{
    private readonly RingCabinetCreationViewModel _viewModel = new();

    public RingCabinetCreationDialog()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    public RingCabinetCreationConfiguration? Configuration { get; private set; }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.TryCreateConfiguration(out RingCabinetCreationConfiguration? configuration, out string error))
        {
            MessageBox.Show(this, error, "添加环网柜", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Configuration = configuration;
        DialogResult = true;
    }
}
