using DistributionDrawing.Desktop.TransformerCreationUi;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class TransformerNamingFinalSliceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreationDialogNormalization_RejectsMissingName(string? input)
    {
        Assert.False(TransformerCreationDialog.TryNormalizeDisplayName(
            input,
            out string normalized));
        Assert.Empty(normalized);
    }

    [Fact]
    public void CreationDialogNormalization_TrimsNameWithoutFabricatingIt()
    {
        Assert.True(TransformerCreationDialog.TryNormalizeDisplayName(
            "  用户输入名称  ",
            out string normalized));
        Assert.Equal("用户输入名称", normalized);
    }
}
