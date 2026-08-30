using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

public sealed record DrawingSceneBitmapOptions(
    double Dpi = 300,
    double MarginMillimeters = 10,
    int MaximumDimensionPixels = 32768,
    long MaximumPixelCount = 100_000_000,
    long MaximumEstimatedBytes = 400_000_000);

public sealed record DrawingSceneBitmapResult(
    int WidthPixels,
    int HeightPixels,
    double Dpi,
    DocumentRect ContentBounds,
    DocumentRect ExportBounds);

public sealed class DrawingSceneBitmapRenderer
{
    private const double MillimetersPerInch = 25.4;
    private const double DipsPerInch = 96;
    private readonly DrawingSceneRenderer _sceneRenderer;

    public DrawingSceneBitmapRenderer(DrawingSceneRenderer? sceneRenderer = null)
    {
        _sceneRenderer = sceneRenderer ?? new DrawingSceneRenderer();
    }

    public DrawingSceneBitmapResult RenderPng(
        DrawingScene scene,
        Stream output,
        DrawingSceneBitmapOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(output);
        DrawingSceneBitmapOptions settings = options ?? new DrawingSceneBitmapOptions();
        ValidateOptions(settings);
        if (!DrawingSceneBoundsCalculator.TryCalculate(scene, out DocumentRect contentBounds))
        {
            throw new InvalidOperationException("当前图纸没有可导出的内容。");
        }

        DocumentRect exportBounds = Expand(contentBounds, settings.MarginMillimeters);
        int widthPixels = ToPixels(exportBounds.WidthMillimeters, settings.Dpi);
        int heightPixels = ToPixels(exportBounds.HeightMillimeters, settings.Dpi);
        ValidatePixelBudget(widthPixels, heightPixels, settings);

        double widthDips = widthPixels * DipsPerInch / settings.Dpi;
        double heightDips = heightPixels * DipsPerInch / settings.Dpi;
        double offsetXDips = -exportBounds.XMillimeters * DipsPerInch / MillimetersPerInch;
        double offsetYDips = -exportBounds.YMillimeters * DipsPerInch / MillimetersPerInch;
        var exportVisual = new DrawingVisual();
        using (DrawingContext context = exportVisual.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, widthDips, heightDips));
            context.PushTransform(new TranslateTransform(offsetXDips, offsetYDips));
            context.DrawDrawing(_sceneRenderer.RenderDrawing(scene, 1));
            context.Pop();
        }

        var bitmap = new RenderTargetBitmap(
            widthPixels,
            heightPixels,
            settings.Dpi,
            settings.Dpi,
            PixelFormats.Pbgra32);
        bitmap.Render(exportVisual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(output);
        return new DrawingSceneBitmapResult(
            widthPixels,
            heightPixels,
            settings.Dpi,
            contentBounds,
            exportBounds);
    }

    private static void ValidateOptions(DrawingSceneBitmapOptions options)
    {
        if (!IsPositiveFinite(options.Dpi) ||
            options.MarginMillimeters < 0 ||
            double.IsNaN(options.MarginMillimeters) ||
            double.IsInfinity(options.MarginMillimeters) ||
            options.MaximumDimensionPixels <= 0 ||
            options.MaximumPixelCount <= 0 ||
            options.MaximumEstimatedBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
    }

    private static DocumentRect Expand(DocumentRect bounds, double margin)
    {
        return new DocumentRect(
            bounds.XMillimeters - margin,
            bounds.YMillimeters - margin,
            Math.Max(0, bounds.WidthMillimeters) + margin * 2,
            Math.Max(0, bounds.HeightMillimeters) + margin * 2);
    }

    private static int ToPixels(double millimeters, double dpi)
    {
        double pixels = Math.Ceiling(millimeters / MillimetersPerInch * dpi);
        if (!IsPositiveFinite(pixels) || pixels > int.MaxValue)
        {
            throw TooLarge();
        }

        return (int)pixels;
    }

    private static void ValidatePixelBudget(
        int widthPixels,
        int heightPixels,
        DrawingSceneBitmapOptions options)
    {
        long pixelCount = checked((long)widthPixels * heightPixels);
        long estimatedBytes = checked(pixelCount * 4);
        if (widthPixels > options.MaximumDimensionPixels ||
            heightPixels > options.MaximumDimensionPixels ||
            pixelCount > options.MaximumPixelCount ||
            estimatedBytes > options.MaximumEstimatedBytes)
        {
            throw TooLarge();
        }
    }

    private static InvalidOperationException TooLarge() =>
        new("图纸范围过大，无法按当前分辨率导出。");

    private static bool IsPositiveFinite(double value) =>
        value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
}
