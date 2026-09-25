using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using DistributionDrawing.Application.Export;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

public sealed record DrawingSceneBitmapOptions(
    int Dpi = 300,
    double MarginMillimeters = 10,
    int MaximumDimensionPixels = 32768,
    long MaximumPixelCount = 32_000_000,
    long MaximumEstimatedBytes = 384L * 1024 * 1024,
    int MinimumReadableDpi = 100,
    int BytesPerPixel = 4,
    double PeakSurfaceFactor = 3.0,
    long FixedHeadroomBytes = 64L * 1024 * 1024,
    long MaximumStrideBytes = int.MaxValue);

public sealed record DrawingSceneBitmapResult(
    int WidthPixels,
    int HeightPixels,
    double Dpi,
    DocumentRect ContentBounds,
    DocumentRect ExportBounds)
{
    public int SelectedDpi => checked((int)Dpi);
}

public interface IDrawingSceneBitmapRenderer
{
    DrawingSceneBitmapResult RenderPng(
        DrawingScene scene,
        Stream output,
        DrawingSceneBitmapOptions? options = null);
}

public sealed class PngExportSizeException : InvalidOperationException
{
    public PngExportSizeException()
        : base("图纸范围过大，在最低可读分辨率下仍无法安全导出 PNG。")
    {
    }
}

public sealed class PngExportRenderException : InvalidOperationException
{
    public PngExportRenderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class DrawingSceneBitmapRenderer : IDrawingSceneBitmapRenderer
{
    private const double MillimetersPerInch = 25.4;
    private const double DipsPerInch = 96;
    private readonly DrawingSceneRenderer _sceneRenderer;
    private readonly PngExportSizingCalculator _sizingCalculator;

    public DrawingSceneBitmapRenderer(
        DrawingSceneRenderer? sceneRenderer = null,
        PngExportSizingCalculator? sizingCalculator = null)
    {
        _sceneRenderer = sceneRenderer ?? new DrawingSceneRenderer();
        _sizingCalculator = sizingCalculator ?? new PngExportSizingCalculator();
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
        PngExportSizingDecision sizing = _sizingCalculator.Calculate(
            exportBounds.WidthMillimeters,
            exportBounds.HeightMillimeters,
            CreateSizingPolicy(settings));
        if (sizing.Failure is PngExportSizingFailure.InvalidDrawingSize or
            PngExportSizingFailure.InvalidPolicy)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        if (!sizing.IsSuccess)
        {
            throw new PngExportSizeException();
        }

        PngExportSizingResult selected = sizing.Result!;
        int widthPixels = selected.PixelWidth;
        int heightPixels = selected.PixelHeight;
        int selectedDpi = selected.SelectedDpi;

        double widthDips = widthPixels * DipsPerInch / selectedDpi;
        double heightDips = heightPixels * DipsPerInch / selectedDpi;
        double offsetXDips = -exportBounds.XMillimeters * DipsPerInch / MillimetersPerInch;
        double offsetYDips = -exportBounds.YMillimeters * DipsPerInch / MillimetersPerInch;
        if (!IsPositiveFinite(widthDips) ||
            !IsPositiveFinite(heightDips) ||
            !IsFinite(offsetXDips) ||
            !IsFinite(offsetYDips))
        {
            throw new PngExportSizeException();
        }

        try
        {
            var exportVisual = new DrawingVisual();
            using (DrawingContext context = exportVisual.RenderOpen())
            {
                context.DrawRectangle(Brushes.White, null, new Rect(0, 0, widthDips, heightDips));
                context.PushTransform(new TranslateTransform(offsetXDips, offsetYDips));
                context.DrawDrawing(_sceneRenderer.RenderDrawing(scene, selectedDpi / DipsPerInch));
                context.Pop();
            }

            var bitmap = new RenderTargetBitmap(
                widthPixels,
                heightPixels,
                selectedDpi,
                selectedDpi,
                PixelFormats.Pbgra32);
            bitmap.Render(exportVisual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(output);
        }
        catch (InvalidOperationException exception) when (IsOutputWriteFailure(exception))
        {
            throw new IOException("无法写入 PNG 输出流。", exception);
        }
        catch (Exception exception) when (
            exception is OutOfMemoryException or COMException or
                InvalidOperationException or ArgumentException or NotSupportedException)
        {
            throw new PngExportRenderException("PNG 渲染或编码失败。", exception);
        }

        return new DrawingSceneBitmapResult(
            widthPixels,
            heightPixels,
            selectedDpi,
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
        if (options.Dpi <= 0 ||
            options.MarginMillimeters < 0 ||
            double.IsNaN(options.MarginMillimeters) ||
            double.IsInfinity(options.MarginMillimeters) ||
            options.MaximumDimensionPixels <= 0 ||
            options.MaximumPixelCount <= 0 ||
            options.MaximumEstimatedBytes <= 0 ||
            options.MinimumReadableDpi <= 0 ||
            options.BytesPerPixel <= 0 ||
            !IsPositiveFinite(options.PeakSurfaceFactor) ||
            options.FixedHeadroomBytes < 0 ||
            options.MaximumStrideBytes <= 0)
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

    private static PngExportSizingPolicy CreateSizingPolicy(DrawingSceneBitmapOptions options) =>
        new(
            MaxRequestedDpi: options.Dpi,
            MinimumReadableDpi: options.MinimumReadableDpi,
            MaxDimensionPixels: options.MaximumDimensionPixels,
            MaxPixels: options.MaximumPixelCount,
            BytesPerPixel: options.BytesPerPixel,
            PeakSurfaceFactor: options.PeakSurfaceFactor,
            FixedHeadroomBytes: options.FixedHeadroomBytes,
            MaxExportWorkingBytes: options.MaximumEstimatedBytes,
            MaxStrideBytes: options.MaximumStrideBytes);

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
