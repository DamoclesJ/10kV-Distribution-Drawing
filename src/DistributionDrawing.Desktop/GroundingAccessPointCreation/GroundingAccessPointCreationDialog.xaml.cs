using System.Windows;
using System.Windows.Controls;
using DistributionDrawing.Domain.Professional;

namespace DistributionDrawing.Desktop.GroundingAccessPointCreation;

public partial class GroundingAccessPointCreationDialog : Window
{
    public GroundingAccessPointCreationDialog(
        IReadOnlyList<GroundingAccessCandidate> candidates)
    {
        InitializeComponent();
        CandidateInput.ItemsSource = candidates ?? throw new ArgumentNullException(nameof(candidates));
        CandidateInput.SelectedIndex = candidates.Count > 0 ? 0 : -1;
    }

    public GroundingAccessCandidate? SelectedCandidate =>
        CandidateInput.SelectedItem as GroundingAccessCandidate;

    public GroundingAccessLineSide SelectedLineSide =>
        ((ComboBoxItem)LineSideInput.SelectedItem).Tag?.ToString() switch
        {
            "LargerNumberSide" => GroundingAccessLineSide.LargerNumberSide,
            "TransformerSide" => GroundingAccessLineSide.TransformerSide,
            _ => GroundingAccessLineSide.SmallerNumberSide
        };

    private void OnCandidateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedCandidate is not { } candidate)
        {
            return;
        }
        GroundingAccessCandidateLineSideState state =
            GroundingAccessPointCreationService.ResolveLineSideState(candidate);
        LineSideInput.IsEnabled = !state.IsLocked;
        GroundingAccessLineSide? recommendation = state.SelectedLineSide;
        if (recommendation is null)
        {
            LineSideInput.SelectedIndex = -1;
            RecommendationText.Text = state.Message;
            return;
        }
        LineSideInput.SelectedIndex = recommendation switch
        {
            GroundingAccessLineSide.SmallerNumberSide => 0,
            GroundingAccessLineSide.LargerNumberSide => 1,
            _ => 2
        };
        RecommendationText.Text = state.Message;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (SelectedCandidate is null || LineSideInput.SelectedItem is null)
        {
            MessageBox.Show(this, "请选择线路物理方向和专业线路侧。", "无法创建");
            return;
        }
        DialogResult = true;
    }
}
