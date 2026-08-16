using System.Globalization;
using System.Windows;

namespace DistributionDrawing.Desktop.CableConnection;

public partial class CableCreationDialog : Window
{
    public CableCreationDialog()
    {
        InitializeComponent();
        CableTypeInput.Text = "YJV22-8.7/15kV";
        LengthInput.Text = "10";
    }

    public string CableType { get; private set; } = string.Empty;

    public double Length { get; private set; }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        string cableType = CableTypeInput.Text.Trim();
        if (cableType.Length == 0)
        {
            MessageBox.Show(this, "请输入电缆型号。", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!double.TryParse(
                LengthInput.Text,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out double length) ||
            double.IsNaN(length) ||
            double.IsInfinity(length) ||
            length <= 0)
        {
            MessageBox.Show(this, "请输入大于零的电缆长度。", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        CableType = cableType;
        Length = length;
        DialogResult = true;
    }
}
