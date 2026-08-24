using System.Globalization;
using System.Windows;
using DistributionDrawing.Rendering.Wpf.Metrics;

namespace DistributionDrawing.Desktop.DrawingTypography;

public partial class DrawingTypographyDialog : Window
{
    private readonly DrawingTypographyMetrics _metrics;

    public DrawingTypographyDialog(DrawingTypographyMetrics? metrics = null)
    {
        InitializeComponent();
        _metrics = metrics ?? DrawingMetrics.Default.Typography;
        CabinetNameFontSizeInput.Text = Format(_metrics.CabinetNameFontSize);
        LineNameFontSizeInput.Text = Format(_metrics.LineNameFontSize);
        IntervalNumberFontSizeInput.Text = Format(_metrics.IntervalNumberFontSize);
        SwitchNumberFontSizeInput.Text = Format(_metrics.SwitchNumberFontSize);
        PoleNumberFontSizeInput.Text = Format(_metrics.PoleNumberFontSize);
        PTLabelFontSizeInput.Text = Format(_metrics.PTLabelFontSize);
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        if (!TryParse(CabinetNameFontSizeInput.Text, out double cabinetName) ||
            !TryParse(LineNameFontSizeInput.Text, out double lineName) ||
            !TryParse(IntervalNumberFontSizeInput.Text, out double intervalNumber) ||
            !TryParse(SwitchNumberFontSizeInput.Text, out double switchNumber) ||
            !TryParse(PoleNumberFontSizeInput.Text, out double poleNumber) ||
            !TryParse(PTLabelFontSizeInput.Text, out double ptLabel))
        {
            ValidationMessage.Text = "请输入大于 0 的有效字号。";
            return;
        }

        _metrics.Update(
            cabinetName,
            lineName,
            intervalNumber,
            switchNumber,
            poleNumber,
            ptLabel);
        DialogResult = true;
    }

    private static bool TryParse(string input, out double value) =>
        double.TryParse(
            input,
            NumberStyles.Float,
            CultureInfo.CurrentCulture,
            out value) && double.IsFinite(value) && value > 0;

    private static string Format(double value) =>
        value.ToString("0.###", CultureInfo.CurrentCulture);
}
