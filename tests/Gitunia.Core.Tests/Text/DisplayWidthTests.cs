using Gitunia.Core.Text;
using Xunit;

namespace Gitunia.Core.Tests.Text;

public class DisplayWidthTests
{
    [Theory]
    [InlineData("abc", 3)]
    [InlineData("中文", 4)]
    [InlineData("\U0001F600", 2)]
    [InlineData("é", 1)]
    [InlineData("\t", 1)]
    [InlineData("", 0)]
    public void GivenText_WhenMeasuring_ThenWideTakesTwoCombiningNoneAndControlsOne(string text, int expected)
    {
        Assert.Equal(expected, DisplayWidth.Of(text));
    }
}
