using DistributionDrawing.Application.Export;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class PngExportSizingCalculatorTests
{
    private readonly PngExportSizingCalculator _calculator = new();

    [Fact]
    public void Calculate_Returns300_WhenRequestedDpiIsSafe()
    {
        PngExportSizingDecision decision = _calculator.Calculate(100, 80);

        Assert.True(decision.IsSuccess);
        Assert.Equal(300, decision.Result!.SelectedDpi);
    }

    [Fact]
    public void Calculate_Returns299_WhenItIsTheHighestSafeDpi()
    {
        const int maxWidth = 2_990;
        double width = MillimetersForPixels(maxWidth, 299);
        PngExportSizingPolicy policy = LenientPolicy() with
        {
            MaxRequestedDpi = 300,
            MinimumReadableDpi = 100,
            MaxDimensionPixels = maxWidth
        };

        PngExportSizingDecision decision = _calculator.Calculate(width, 1, policy);

        Assert.Equal(299, decision.Result!.SelectedDpi);
        Assert.Equal(maxWidth, decision.Result.PixelWidth);
    }

    [Fact]
    public void Calculate_AllowsExactMaximumWidth()
    {
        PngExportSizingPolicy policy = FixedDpiPolicy(100) with { MaxDimensionPixels = 100 };

        PngExportSizingDecision decision = _calculator.Calculate(
            MillimetersForPixels(100, 100),
            MillimetersForPixels(1, 100),
            policy);

        Assert.Equal(100, decision.Result!.PixelWidth);
    }

    [Fact]
    public void Calculate_RejectsOnePixelOverMaximumWidth()
    {
        PngExportSizingPolicy policy = FixedDpiPolicy(100) with { MaxDimensionPixels = 100 };

        PngExportSizingDecision decision = _calculator.Calculate(
            MillimetersForPixels(101, 100),
            MillimetersForPixels(1, 100),
            policy);

        Assert.Equal(PngExportSizingFailure.NoSafeReadableDpi, decision.Failure);
    }

    [Fact]
    public void Calculate_AllowsExactMaximumHeight()
    {
        PngExportSizingPolicy policy = FixedDpiPolicy(100) with { MaxDimensionPixels = 100 };

        PngExportSizingDecision decision = _calculator.Calculate(
            MillimetersForPixels(1, 100),
            MillimetersForPixels(100, 100),
            policy);

        Assert.Equal(100, decision.Result!.PixelHeight);
    }

    [Fact]
    public void Calculate_RejectsOnePixelOverMaximumHeight()
    {
        PngExportSizingPolicy policy = FixedDpiPolicy(100) with { MaxDimensionPixels = 100 };

        PngExportSizingDecision decision = _calculator.Calculate(
            MillimetersForPixels(1, 100),
            MillimetersForPixels(101, 100),
            policy);

        Assert.Equal(PngExportSizingFailure.NoSafeReadableDpi, decision.Failure);
    }

    [Fact]
    public void Calculate_AllowsExactMaximumPixelCount()
    {
        PngExportSizingPolicy policy = FixedDpiPolicy(100) with { MaxPixels = 10_000 };

        PngExportSizingDecision decision = _calculator.Calculate(
            MillimetersForPixels(10_000, 100),
            MillimetersForPixels(1, 100),
            policy);

        Assert.Equal(10_000, decision.Result!.PixelCount);
    }

    [Fact]
    public void Calculate_RejectsOnePixelOverMaximumPixelCount()
    {
        PngExportSizingPolicy policy = FixedDpiPolicy(100) with { MaxPixels = 10_000 };

        PngExportSizingDecision decision = _calculator.Calculate(
            MillimetersForPixels(10_001, 100),
            MillimetersForPixels(1, 100),
            policy);

        Assert.Equal(PngExportSizingFailure.NoSafeReadableDpi, decision.Failure);
    }

    [Fact]
    public void Calculate_AllowsExactMemoryBudget()
    {
        PngExportSizingPolicy policy = FixedDpiPolicy(100) with
        {
            MaxPixels = 1_000,
            BytesPerPixel = 4,
            PeakSurfaceFactor = 1,
            FixedHeadroomBytes = 0,
            MaxExportWorkingBytes = 400
        };

        PngExportSizingDecision decision = _calculator.Calculate(
            MillimetersForPixels(10, 100),
            MillimetersForPixels(10, 100),
            policy);

        Assert.Equal(400, decision.Result!.EstimatedPeakBytes);
    }

    [Fact]
    public void Calculate_RejectsOnePixelOverMemoryBudget()
    {
        PngExportSizingPolicy policy = FixedDpiPolicy(100) with
        {
            MaxPixels = 1_000,
            BytesPerPixel = 4,
            PeakSurfaceFactor = 1,
            FixedHeadroomBytes = 0,
            MaxExportWorkingBytes = 400
        };

        PngExportSizingDecision decision = _calculator.Calculate(
            MillimetersForPixels(101, 100),
            MillimetersForPixels(1, 100),
            policy);

        Assert.Equal(PngExportSizingFailure.NoSafeReadableDpi, decision.Failure);
    }

    [Fact]
    public void Calculate_UsesCeilingForFractionalPixels()
    {
        PngExportSizingPolicy policy = FixedDpiPolicy(100);
        double width = 100.01 * 25.4 / 100;

        PngExportSizingDecision decision = _calculator.Calculate(width, 1, policy);

        Assert.Equal(101, decision.Result!.PixelWidth);
    }

    [Fact]
    public void Calculate_RejectsVeryWideDrawing()
    {
        PngExportSizingDecision decision = _calculator.Calculate(100_000, 1);

        Assert.Equal(PngExportSizingFailure.NoSafeReadableDpi, decision.Failure);
    }

    [Fact]
    public void Calculate_RejectsVeryTallDrawing()
    {
        PngExportSizingDecision decision = _calculator.Calculate(1, 100_000);

        Assert.Equal(PngExportSizingFailure.NoSafeReadableDpi, decision.Failure);
    }

    [Fact]
    public void Calculate_RejectsHugeArea()
    {
        PngExportSizingDecision decision = _calculator.Calculate(10_000, 10_000);

        Assert.Equal(PngExportSizingFailure.NoSafeReadableDpi, decision.Failure);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Calculate_RejectsInvalidDrawingDimensions(double dimension)
    {
        PngExportSizingDecision width = _calculator.Calculate(dimension, 10);
        PngExportSizingDecision height = _calculator.Calculate(10, dimension);

        Assert.Equal(PngExportSizingFailure.InvalidDrawingSize, width.Failure);
        Assert.Equal(PngExportSizingFailure.InvalidDrawingSize, height.Failure);
    }

    [Fact]
    public void Calculate_TreatsArithmeticOverflowAsUnsafe()
    {
        PngExportSizingPolicy policy = new(
            MaxRequestedDpi: 1,
            MinimumReadableDpi: 1,
            MaxDimensionPixels: int.MaxValue,
            MaxPixels: long.MaxValue,
            BytesPerPixel: int.MaxValue,
            PeakSurfaceFactor: 1,
            FixedHeadroomBytes: 0,
            MaxExportWorkingBytes: long.MaxValue,
            MaxStrideBytes: long.MaxValue);
        double extent = MillimetersForPixels(int.MaxValue, 1);

        PngExportSizingDecision decision = _calculator.Calculate(extent, extent, policy);

        Assert.Equal(PngExportSizingFailure.NoSafeReadableDpi, decision.Failure);
    }

    [Fact]
    public void Calculate_RejectsStrideOverLimit()
    {
        PngExportSizingPolicy policy = FixedDpiPolicy(100) with { MaxStrideBytes = 399 };

        PngExportSizingDecision decision = _calculator.Calculate(
            MillimetersForPixels(100, 100),
            MillimetersForPixels(1, 100),
            policy);

        Assert.Equal(PngExportSizingFailure.NoSafeReadableDpi, decision.Failure);
    }

    [Fact]
    public void Calculate_AllowsMinimumReadableDpiWhenItIsExactlySafe()
    {
        PngExportSizingPolicy policy = LenientPolicy() with
        {
            MaxRequestedDpi = 300,
            MinimumReadableDpi = 100,
            MaxDimensionPixels = 1_000
        };
        double width = MillimetersForPixels(1_000, 100);

        PngExportSizingDecision decision = _calculator.Calculate(width, 1, policy);

        Assert.Equal(100, decision.Result!.SelectedDpi);
    }

    [Fact]
    public void Calculate_DefaultBinarySearchLowerBoundIsFrozenAt100Dpi()
    {
        double width = MillimetersForPixels(32_768, 100);

        PngExportSizingDecision decision = _calculator.Calculate(width, 0.1);

        Assert.True(decision.IsSuccess);
        Assert.Equal(100, decision.Result!.SelectedDpi);
    }

    [Fact]
    public void Calculate_ReturnsTypedFailureWhenMinimumReadableDpiIsUnsafe()
    {
        PngExportSizingPolicy policy = LenientPolicy() with
        {
            MaxRequestedDpi = 300,
            MinimumReadableDpi = 100,
            MaxDimensionPixels = 999
        };
        double width = MillimetersForPixels(1_000, 100);

        PngExportSizingDecision decision = _calculator.Calculate(width, 1, policy);

        Assert.False(decision.IsSuccess);
        Assert.Equal(PngExportSizingFailure.NoSafeReadableDpi, decision.Failure);
    }

    [Fact]
    public void Calculate_SafetyIsMonotonicAcrossDpiRange()
    {
        PngExportSizingPolicy baseline = LenientPolicy() with
        {
            MinimumReadableDpi = 100,
            MaxDimensionPixels = 2_000
        };
        bool unsafeSeen = false;
        for (int dpi = 100; dpi <= 300; dpi++)
        {
            PngExportSizingDecision decision = _calculator.Calculate(
                200,
                10,
                baseline with { MaxRequestedDpi = dpi });
            bool requestedDpiIsSafe = decision.IsSuccess && decision.Result!.SelectedDpi == dpi;
            if (!requestedDpiIsSafe) unsafeSeen = true;
            if (unsafeSeen) Assert.False(requestedDpiIsSafe);
        }
    }

    [Fact]
    public void Calculate_BinarySearchReturnsMaximalSafeDpi()
    {
        PngExportSizingPolicy policy = LenientPolicy() with
        {
            MaxRequestedDpi = 300,
            MinimumReadableDpi = 100,
            MaxDimensionPixels = 2_000
        };

        PngExportSizingDecision decision = _calculator.Calculate(200, 10, policy);
        int selected = decision.Result!.SelectedDpi;
        PngExportSizingDecision next = _calculator.Calculate(
            200,
            10,
            policy with
            {
                MaxRequestedDpi = selected + 1,
                MinimumReadableDpi = selected + 1
            });

        Assert.True(decision.IsSuccess);
        Assert.Equal(PngExportSizingFailure.NoSafeReadableDpi, next.Failure);
    }

    [Fact]
    public void Calculate_ResultIsDeterministicAndIndependentOfSystemMemory()
    {
        PngExportSizingDecision first = _calculator.Calculate(600, 400);
        PngExportSizingDecision second = _calculator.Calculate(600, 400);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Calculate_RejectsInvalidPolicyWithoutThrowing()
    {
        PngExportSizingDecision decision = _calculator.Calculate(
            10,
            10,
            new PngExportSizingPolicy(MinimumReadableDpi: 301));

        Assert.Equal(PngExportSizingFailure.InvalidPolicy, decision.Failure);
    }

    private static PngExportSizingPolicy FixedDpiPolicy(int dpi) =>
        LenientPolicy() with
        {
            MaxRequestedDpi = dpi,
            MinimumReadableDpi = dpi
        };

    private static PngExportSizingPolicy LenientPolicy() => new(
        MaxRequestedDpi: 300,
        MinimumReadableDpi: 1,
        MaxDimensionPixels: int.MaxValue,
        MaxPixels: long.MaxValue,
        BytesPerPixel: 4,
        PeakSurfaceFactor: 1,
        FixedHeadroomBytes: 0,
        MaxExportWorkingBytes: long.MaxValue,
        MaxStrideBytes: long.MaxValue);

    private static double MillimetersForPixels(int pixels, int dpi) =>
        (pixels - 0.25) * 25.4 / dpi;
}
