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
        ((ComboBoxItem)LineSideInput.SelectedItem).Tag?.ToString() == "LargerNumberSide"
            ? GroundingAccessLineSide.LargerNumberSide
            : GroundingAccessLineSide.SmallerNumberSide;

    private void OnCandidateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedCandidate is not { } candidate)
        {
            return;
        }
        GroundingAccessLineSide? recommendation =
            GroundingAccessPointCreationService.RecommendLineSide(
                candidate.PoleNumber,
                candidate.AdjacentPoleNumber);
        if (recommendation is null)
        {
            LineSideInput.SelectedIndex = -1;
            RecommendationText.Text = "杆号无法可靠解析，请人工选择小号侧或大号侧。";
            return;
        }
        LineSideInput.SelectedIndex = recommendation == GroundingAccessLineSide.SmallerNumberSide
            ? 0
            : 1;
        RecommendationText.Text = "已按简单杆号推荐；可人工覆盖，实际相邻杆方向不会改变。";
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
