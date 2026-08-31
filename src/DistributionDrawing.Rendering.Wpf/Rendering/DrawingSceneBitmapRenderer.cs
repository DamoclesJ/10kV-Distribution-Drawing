using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Runtime.InteropServices;
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
        if (!output.CanWrite)
        {
            throw new ArgumentException("PNG 输出流不可写。", nameof(output));
        }

        DrawingSceneBitmapOptions settings = options ?? new DrawingSceneBitmapOptions();
        ValidateOptions(settings);
        ValidateSceneGeometry(scene);
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
        if (!IsPositiveFinite(widthDips) ||
            !IsPositiveFinite(heightDips) ||
            !IsFinite(offsetXDips) ||
            !IsFinite(offsetYDips))
        {
            throw TooLarge();
        }

        var exportVisual = new DrawingVisual();
        using (DrawingContext context = exportVisual.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, widthDips, heightDips));
            context.PushTransform(new TranslateTransform(offsetXDips, offsetYDips));
            context.DrawDrawing(_sceneRenderer.RenderDrawing(scene, settings.Dpi / DipsPerInch));
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
        try
        {
            encoder.Save(output);
        }
        catch (InvalidOperationException exception) when (IsOutputWriteFailure(exception))
        {
            throw new IOException("无法写入 PNG 输出流。", exception);
        }

        return new DrawingSceneBitmapResult(
            widthPixels,
            heightPixels,
            settings.Dpi,
            contentBounds,
            exportBounds);
    }

    private static bool IsOutputWriteFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is IOException ||
                current is COMException { HResult: unchecked((int)0x88982F71) })
            {
                return true;
            }
        }

        return false;
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

    private static void ValidateSceneGeometry(DrawingScene scene)
    {
        foreach (SceneElement element in scene.Elements)
        {
            bool valid = element switch
            {
                SceneLogicalBounds logical => IsValidRect(logical.Bounds, allowEmpty: true),
                SceneLine line =>
                    IsValidPoint(line.Start) &&
                    IsValidPoint(line.End) &&
                    IsPositiveFinite(line.ThicknessMillimeters),
                SceneRectangle rectangle =>
                    IsValidRect(rectangle.Bounds) &&
                    IsPositiveFinite(rectangle.ThicknessMillimeters),
                SceneEllipse ellipse =>
                    IsValidRect(ellipse.Bounds) &&
                    IsPositiveFinite(ellipse.ThicknessMillimeters),
                ScenePolyline polyline =>
                    polyline.Points.All(IsValidPoint) &&
                    IsValidRect(polyline.Bounds, allowEmpty: true) &&
                    IsPositiveFinite(polyline.ThicknessMillimeters),
                SceneArc arc =>
                    IsValidPoint(arc.Center) &&
                    IsPositiveFinite(arc.RadiusMillimeters) &&
                    IsFinite(arc.StartAngleDegrees) &&
                    IsFinite(arc.SweepAngleDegrees) &&
                    IsValidRect(arc.Bounds) &&
                    IsPositiveFinite(arc.ThicknessMillimeters),
                SceneText text =>
                    IsValidPoint(text.Origin) &&
                    text.Text is not null &&
                    IsPositiveFinite(text.FontSizeMillimeters),
                _ => true
            };
            if (!valid)
            {
                throw new InvalidOperationException("图纸包含无效的几何坐标，无法导出。");
            }
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
        if (widthPixels > options.MaximumDimensionPixels ||
            heightPixels > options.MaximumDimensionPixels)
        {
            throw TooLarge();
        }

        long pixelCount;
        try
        {
            pixelCount = checked((long)widthPixels * heightPixels);
        }
        catch (OverflowException)
        {
            throw TooLarge();
        }

        if (pixelCount > options.MaximumPixelCount ||
            pixelCount > options.MaximumEstimatedBytes / 4)
        {
            throw TooLarge();
        }
    }

    private static InvalidOperationException TooLarge() =>
        new("图纸范围过大，无法按当前分辨率导出。");

    private static bool IsPositiveFinite(double value) =>
        value > 0 && IsFinite(value);

    private static bool IsValidPoint(DocumentPoint point) =>
        IsFinite(point.XMillimeters) && IsFinite(point.YMillimeters);

    private static bool IsValidRect(DocumentRect rect, bool allowEmpty = false) =>
        IsFinite(rect.XMillimeters) &&
        IsFinite(rect.YMillimeters) &&
        IsFinite(rect.WidthMillimeters) &&
        IsFinite(rect.HeightMillimeters) &&
        (allowEmpty ? rect.WidthMillimeters >= 0 : rect.WidthMillimeters > 0) &&
        (allowEmpty ? rect.HeightMillimeters >= 0 : rect.HeightMillimeters > 0);

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);
}
