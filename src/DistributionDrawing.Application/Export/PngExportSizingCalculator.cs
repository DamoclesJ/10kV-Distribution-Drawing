namespace DistributionDrawing.Application.Export;

public sealed record PngExportSizingPolicy(
    int MaxRequestedDpi = 300,
    int MinimumReadableDpi = 96,
    int MaxDimensionPixels = 32768,
    long MaxPixels = 32_000_000,
    int BytesPerPixel = 4,
    double PeakSurfaceFactor = 3.0,
    long FixedHeadroomBytes = 64L * 1024 * 1024,
    long MaxExportWorkingBytes = 384L * 1024 * 1024,
    long MaxStrideBytes = int.MaxValue);

public sealed record PngExportSizingResult(
    int SelectedDpi,
    int PixelWidth,
    int PixelHeight,
    long PixelCount,
    long RawBytes,
    long EstimatedPeakBytes);

public enum PngExportSizingFailure
{
    None,
    InvalidDrawingSize,
    InvalidPolicy,
    NoSafeReadableDpi
}

public sealed record PngExportSizingDecision(
    PngExportSizingResult? Result,
    PngExportSizingFailure Failure)
{
    public bool IsSuccess => Result is not null && Failure == PngExportSizingFailure.None;
}

public sealed class PngExportSizingCalculator
{
    private const double MillimetersPerInch = 25.4;

    public PngExportSizingDecision Calculate(
        double exportWidthMillimeters,
        double exportHeightMillimeters,
        PngExportSizingPolicy? policy = null)
    {
        PngExportSizingPolicy settings = policy ?? new PngExportSizingPolicy();
        if (!IsPositiveFinite(exportWidthMillimeters) ||
            !IsPositiveFinite(exportHeightMillimeters))
        {
            return Failure(PngExportSizingFailure.InvalidDrawingSize);
        }

        if (!IsValid(settings))
        {
            return Failure(PngExportSizingFailure.InvalidPolicy);
        }

        if (TryEvaluate(
                exportWidthMillimeters,
                exportHeightMillimeters,
                settings.MaxRequestedDpi,
                settings,
                out PngExportSizingResult requested))
        {
            return Success(requested);
        }

        if (!TryEvaluate(
                exportWidthMillimeters,
                exportHeightMillimeters,
                settings.MinimumReadableDpi,
                settings,
                out PngExportSizingResult minimum))
        {
            return Failure(PngExportSizingFailure.NoSafeReadableDpi);
        }

        int low = settings.MinimumReadableDpi;
        int high = settings.MaxRequestedDpi - 1;
        PngExportSizingResult best = minimum;
        while (low <= high)
        {
            int candidateDpi = low + (high - low) / 2;
            if (TryEvaluate(
                    exportWidthMillimeters,
                    exportHeightMillimeters,
                    candidateDpi,
                    settings,
                    out PngExportSizingResult candidate))
            {
                best = candidate;
                low = candidateDpi + 1;
            }
            else
            {
                high = candidateDpi - 1;
            }
        }

        return Success(best);
    }

    private static bool TryEvaluate(
        double widthMillimeters,
        double heightMillimeters,
        int dpi,
        PngExportSizingPolicy policy,
        out PngExportSizingResult result)
    {
        result = null!;
        if (!TryToPixels(widthMillimeters, dpi, out int widthPixels) ||
            !TryToPixels(heightMillimeters, dpi, out int heightPixels) ||
            widthPixels > policy.MaxDimensionPixels ||
            heightPixels > policy.MaxDimensionPixels)
        {
            return false;
        }

        try
        {
            long strideBytes = checked((long)widthPixels * policy.BytesPerPixel);
            if (strideBytes > policy.MaxStrideBytes)
            {
                return false;
            }

            long pixelCount = checked((long)widthPixels * heightPixels);
            if (pixelCount > policy.MaxPixels)
            {
                return false;
            }

            long rawBytes = checked(pixelCount * policy.BytesPerPixel);
            double surfaceBytes = rawBytes * policy.PeakSurfaceFactor;
            if (!double.IsFinite(surfaceBytes) || surfaceBytes > long.MaxValue)
            {
                return false;
            }

            long estimatedPeakBytes = checked(
                policy.FixedHeadroomBytes + (long)Math.Ceiling(surfaceBytes));
            if (estimatedPeakBytes > policy.MaxExportWorkingBytes)
            {
                return false;
            }

            result = new PngExportSizingResult(
                dpi,
                widthPixels,
                heightPixels,
                pixelCount,
                rawBytes,
                estimatedPeakBytes);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static bool TryToPixels(double millimeters, int dpi, out int pixels)
    {
        double calculated = Math.Ceiling(millimeters / MillimetersPerInch * dpi);
        if (!IsPositiveFinite(calculated) || calculated > int.MaxValue)
        {
            pixels = 0;
            return false;
        }

        pixels = (int)calculated;
        return true;
    }

    private static bool IsValid(PngExportSizingPolicy policy) =>
        policy.MaxRequestedDpi > 0 &&
        policy.MinimumReadableDpi > 0 &&
        policy.MinimumReadableDpi <= policy.MaxRequestedDpi &&
        policy.MaxDimensionPixels > 0 &&
        policy.MaxPixels > 0 &&
        policy.BytesPerPixel > 0 &&
        IsPositiveFinite(policy.PeakSurfaceFactor) &&
        policy.FixedHeadroomBytes >= 0 &&
        policy.MaxExportWorkingBytes > 0 &&
        policy.FixedHeadroomBytes < policy.MaxExportWorkingBytes &&
        policy.MaxStrideBytes > 0;

    private static bool IsPositiveFinite(double value) => value > 0 && double.IsFinite(value);

    private static PngExportSizingDecision Success(PngExportSizingResult result) =>
        new(result, PngExportSizingFailure.None);

    private static PngExportSizingDecision Failure(PngExportSizingFailure failure) =>
        new(null, failure);
}
