using System.Reflection;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class DrawingMetricsTests
{
    [Fact]
    public void DefaultMetrics_AreStableFirstVersionEngineeringValues()
    {
        DrawingMetrics metrics = DrawingMetrics.Default;

        Assert.Same(metrics, DrawingMetrics.Default);
        Assert.Equal(0.8, metrics.General.StandardStrokeThickness);
        Assert.Equal(0.6, metrics.General.ThinStrokeThickness);
        Assert.Equal(4, metrics.General.StandardFontSize);
        Assert.Equal(3.5, metrics.General.SmallFontSize);
        Assert.Equal(60, metrics.RingCabinet.StandardIntervalWidth);
        Assert.Equal(125, metrics.RingCabinet.StandardIntervalHeight);
        Assert.Equal(25, metrics.RingCabinet.BusbarOffset);
        Assert.Equal(5, metrics.RingCabinet.IntervalSpacing);
        Assert.Equal(16, metrics.Switch.StandardSwitchLength);
        Assert.Equal(7, metrics.PT.CoilRadius);
        Assert.Equal(7, metrics.Pole.PoleRadius);
        Assert.Equal(10, metrics.CableTermination.TriangleWidth);
        Assert.Equal(0.8, metrics.Line.ConnectionThickness);
        Assert.Equal(4, metrics.LineJump.Radius);
    }

    [Fact]
    public void MetricsContracts_DoNotDependOnDomainTypes()
    {
        Assembly domainAssembly = typeof(DrawingDocument).Assembly;
        Type[] metricTypes = typeof(DrawingMetrics).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(DrawingMetrics).Namespace)
            .ToArray();

        Assert.DoesNotContain(
            metricTypes.SelectMany(type => type.GetProperties()),
            property => property.PropertyType.Assembly == domainAssembly);
        Assert.DoesNotContain(
            metricTypes.SelectMany(type => type.GetConstructors())
                .SelectMany(constructor => constructor.GetParameters()),
            parameter => parameter.ParameterType.Assembly == domainAssembly);
    }

    [Fact]
    public void RuntimeLayout_DoesNotExposeDrawingMetrics()
    {
        Assert.DoesNotContain(
            typeof(RuntimeLayoutDocument).GetProperties(),
            property => property.PropertyType == typeof(DrawingMetrics));
        Assert.DoesNotContain(
            typeof(RuntimeLayoutDocument).GetConstructors()
                .SelectMany(constructor => constructor.GetParameters()),
            parameter => parameter.ParameterType == typeof(DrawingMetrics));
    }
}
