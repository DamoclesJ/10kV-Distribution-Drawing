using System.Windows;
using System.Windows.Media;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Desktop;

public partial class MainWindow : Window
{
    private readonly DrawingSceneRenderer _renderer = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnDrawTestContent(object sender, RoutedEventArgs e)
    {
        var scene = new DrawingScene(
        [
            new SceneLine(
                new DocumentPoint(20, 20),
                new DocumentPoint(150, 20),
                Colors.Black,
                0.5),
            new SceneRectangle(
                new DocumentRect(20, 35, 130, 70),
                Colors.Black,
                0.5,
                Colors.White),
            new SceneText(
                new DocumentPoint(25, 45),
                "10kV 配电绘图测试",
                Colors.Black,
                6)
        ]);

        double pixelsPerDip = VisualTreeHelper.GetDpi(DrawingSurface).PixelsPerDip;
        DrawingSurface.Show(_renderer.Render(scene, pixelsPerDip));
    }

    private void OnClearDrawing(object sender, RoutedEventArgs e)
    {
        DrawingSurface.Clear();
    }
}
