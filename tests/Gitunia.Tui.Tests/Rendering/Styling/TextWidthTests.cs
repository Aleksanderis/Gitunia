using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Styling;

[Trait("Os", "Independent")]
public class TextWidthTests
{
    [Theory]
    [InlineData("hello", 5, "hello")] // Text that fits is unchanged.
    [InlineData("hello world", 5, "hell…")] // The ellipsis fills the last column.
    [InlineData("ab修正", 4, "ab…")] // A wide character at the edge is dropped, not split.
    [InlineData("ab\U0001F468‍\U0001F469‍\U0001F467", 4, "ab\U0001F468‍\U0001F469‍\U0001F467")]
    [InlineData("a\U0001F468‍\U0001F469‍\U0001F467cdef", 4, "a\U0001F468‍\U0001F469‍\U0001F467…")] // It is kept whole.
    [InlineData("abc\U0001F468‍\U0001F469‍\U0001F467", 4, "abc…")]
    [InlineData("hello", 0, "")] // No room: the result is empty.
    public void GivenText_WhenFitting_ThenItIsCutToTheColumnsWithoutSplittingACharacter(
        string text,
        int columns,
        string expected)
    {
        var fitted = TextWidth.Fit(text, columns, "…");

        Assert.Equal(expected, fitted);
        Assert.True(TextWidth.Of(fitted) <= columns);
    }

    [Theory]
    [InlineData("src/a", 5, "src/a")] // Text that fits is unchanged.
    [InlineData("foo/bar", 6, "…o/bar")] // The end stays under a leading ellipsis.
    [InlineData("修正修正", 3, "…正")] // A wide character at the cut is not split.
    [InlineData("ae\u0301b", 2, "…b")] // A combining mark at the cut does not lead the result.
    [InlineData("abc", 0, "")] // No columns: the result is empty.
    public void GivenText_WhenFittingTheStart_ThenTheEndStaysUnderALeadingEllipsis(
        string text,
        int columns,
        string expected)
    {
        Assert.Equal(expected, TextWidth.FitStart(text, columns, "…"));
    }

    [Theory]
    [InlineData("worktree-agent-a6435a5cab5", 14, "worktre~a5cab5")]
    [InlineData("short", 14, "short")]
    [InlineData("abcdefghij", 5, "ab~ij")]
    [InlineData("abcdefghij", 3, "ab~")]
    public void GivenText_WhenFittingInTheMiddle_ThenHeadAndTailStay(string text, int columns, string expected)
    {
        var fitted = TextWidth.FitMiddle(text, columns, "~");

        Assert.True(TextWidth.Of(fitted) <= columns);
        Assert.Equal(expected, fitted);
    }

    [Theory]
    [InlineData("hello", 0, 0, 0)]
    [InlineData("hello", 2, 2, 0)]
    [InlineData("hello", 9, 5, 0)]
    [InlineData("ab\u4f60\u597dc", 2, 2, 0)]
    [InlineData("ab\u4f60\u597dc", 3, 3, 1)]
    [InlineData("ab\u4f60\u597dc", 4, 3, 0)]
    [InlineData("ae\u0301b", 2, 3, 0)]
    public void GivenColumnsToSkip_WhenSkipping_ThenTheWideCharacterIsNotSplitAndMarksStayWithTheirBase(
        string text,
        int columns,
        int index,
        int overshoot)
    {
        var skip = TextWidth.SkipColumns(text, columns);

        Assert.Equal(index, skip.Index);
        Assert.Equal(overshoot, skip.Overshoot);
    }
}
