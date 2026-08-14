using DistributionDrawing.Desktop.Services;

namespace DistributionDrawing.Desktop.ViewModels;

public sealed class MainWindowViewModel
{
    public MainWindowViewModel(DesktopShellService shellService)
    {
        ArgumentNullException.ThrowIfNull(shellService);
        StatusText = shellService.InitialStatus;
    }

    public string CanvasTitle => "绘图区";

    public string InspectorTitle => "属性检查器";

    public string StatusText { get; }
}
