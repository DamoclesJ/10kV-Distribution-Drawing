using System.Reflection;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Domain.Devices.RingCabinets;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class BayTemplateTests
{
    [Fact]
    public void Constructor_PreservesExplicitIndexFunctionAndConfiguration()
    {
        var configuration = new LoadSwitchConfiguration();

        var bay = new BayTemplate(5, BayFunction.Outgoing, configuration);

        Assert.Equal(5, bay.Index);
        Assert.Equal(BayFunction.Outgoing, bay.Function);
        Assert.Same(configuration, bay.EquipmentConfiguration);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveIndex(int index)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BayTemplate(
            index,
            BayFunction.Outgoing,
            new LoadSwitchConfiguration()));
    }

    [Fact]
    public void Constructor_RejectsUnknownFunction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BayTemplate(
            1,
            BayFunction.Unknown,
            new LoadSwitchConfiguration()));
    }

    [Fact]
    public void Constructor_RejectsUndefinedFunction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BayTemplate(
            1,
            (BayFunction)int.MaxValue,
            new LoadSwitchConfiguration()));
    }

    [Fact]
    public void Model_DoesNotExposeSequence()
    {
        PropertyInfo? sequence = typeof(BayTemplate).GetProperty("Sequence");

        Assert.Null(sequence);
    }
}
