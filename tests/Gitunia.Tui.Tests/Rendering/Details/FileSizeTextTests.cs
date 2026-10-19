using Gitunia.Tui.Rendering.Details;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Details;

public class FileSizeTextTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(10 * 1024, "10 KB")]
    [InlineData(1_500_000, "1.4 MB")]
    [InlineData(5L * 1024 * 1024 * 1024, "5.0 GB")]
    public void GivenAByteCount_WhenFormatting_ThenItUsesTheLargestFittingUnit(long bytes, string expected)
    {
        Assert.Equal(expected, FileSizeText.Of(bytes));
    }
}
