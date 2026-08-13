using System.Reflection;
using DistributionDrawing.Application.Templates.RingCabinets;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class BayTemplateTests
{
    [Fact]
    public void Constructor_PreservesExplicitIndexAndConfiguration()
    {
        var configuration = new LoadSwitchConfiguration();

        var bay = new BayTemplate(5, configuration);

        Assert.Equal(5, bay.Index);
        Assert.Same(configuration, bay.EquipmentConfiguration);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveIndex(int index)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BayTemplate(
            index,
            new LoadSwitchConfiguration()));
    }

    [Fact]
    public void Model_DoesNotExposeSequence()
    {
        PropertyInfo? sequence = typeof(BayTemplate).GetProperty("Sequence");

        Assert.Null(sequence);
    }

    [Fact]
    public void Model_DoesNotExposeFunction()
    {
        Assert.Null(typeof(BayTemplate).GetProperty("Function"));
    }
}
