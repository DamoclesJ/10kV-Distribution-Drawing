namespace DistributionDrawing.Desktop.Viewport;

public sealed record DocumentViewState
{
    public DocumentViewState(
        double zoom,
        double panX,
        double panY)
    {
        if (!double.IsFinite(zoom) || zoom <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zoom));
        }

        if (!double.IsFinite(panX))
        {
            throw new ArgumentOutOfRangeException(nameof(panX));
        }

        if (!double.IsFinite(panY))
        {
            throw new ArgumentOutOfRangeException(nameof(panY));
        }

        Zoom = zoom;
        PanX = panX;
        PanY = panY;
    }

    public static DocumentViewState Default { get; } = new(1, 0, 0);

    public double Zoom { get; }

    public double PanX { get; }

    public double PanY { get; }
}
