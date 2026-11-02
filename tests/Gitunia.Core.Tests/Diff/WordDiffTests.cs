using Gitunia.Core.Diff;
using Xunit;

namespace Gitunia.Core.Tests.Diff;

[Trait("Os", "Independent")]
public class WordDiffTests
{
    // The marked stretches of each side are joined with '|'.
    [Theory]
    [InlineData("var total = count + 1;", "var total = amount + 1;", "count", "amount")] // Only the changed word.
    [InlineData("call(a, b)", "call(a, extra, b)", "", "extra, ")] // An inserted word: nothing to mark on the old side.
    [InlineData("alpha keep beta", "gamma keep delta", "alpha|beta", "gamma|delta")] // Words apart: two stretches.
    [InlineData("keep one two end", "keep three four end", "one two", "three four")] // Neighbours: one stretch.
    [InlineData("    return x;", "\t\treturn x;", "    ", "\t\t")] // Only the indentation changed.
    [InlineData("line", "line\r", "", "\r")] // A gained carriage return.
    [InlineData("a 😀 b", "a 😁 b", "😀", "😁")] // A surrogate pair is never split.
    public void GivenTwoLines_WhenComparing_ThenOnlyTheChangedStretchesAreMarkedOnEachSide(
        string oldText,
        string newText,
        string expectedOld,
        string expectedNew)
    {
        var result = WordDiff.Compare(oldText, newText);

        Assert.Equal(expectedOld, string.Join('|', Texts(oldText, result.Old)));
        Assert.Equal(expectedNew, string.Join('|', Texts(newText, result.New)));
    }

    [Theory]
    [InlineData("same", "same")]
    [InlineData("one two", "three four")] // Nothing shared but spaces.
    [InlineData("", "text")]
    [InlineData("text", "")]
    public void GivenLinesWithNothingToPair_WhenComparing_ThenNothingIsMarked(string oldText, string newText)
    {
        var result = WordDiff.Compare(oldText, newText);

        Assert.Empty(result.Old);
        Assert.Empty(result.New);
    }

    [Fact]
    public void GivenALineOverTheLengthLimit_WhenComparing_ThenItIsNotCompared()
    {
        var longLine = new string('x', WordDiff.MaximumLineLength + 1);

        var result = WordDiff.Compare($"a {longLine}", $"b {longLine}");

        Assert.Same(WordDiffResult.None, result);
    }

    [Fact]
    public void GivenAHugeChangedMiddle_WhenComparing_ThenTheTableStaysBoundedAndTheMiddleIsMarkedWhole()
    {
        var oldText = $"start {string.Join(' ', Enumerable.Range(0, 300).Select(index => $"a{index}"))} end";
        var newText = $"start {string.Join(' ', Enumerable.Range(0, 300).Select(index => $"b{index}"))} end";

        var result = WordDiff.Compare(oldText, newText);

        var stretch = Assert.Single(result.Old);
        Assert.Equal("a0", oldText.Substring(stretch.Start, 2));
        Assert.EndsWith("a299", oldText.Substring(stretch.Start, stretch.Length), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAMinifiedLineWithOneEdit_WhenComparing_ThenTheEditedStretchIsMarkedWholeWithoutFillingATable()
    {
        var oldText = string.Concat(Enumerable.Repeat("foo(bar,1);", 180));
        var newText = oldText.Replace("foo(bar,1);", "foo(bar,2);", StringComparison.Ordinal);

        var result = WordDiff.Compare(oldText, newText);

        var stretch = Assert.Single(result.Old);
        Assert.Equal(oldText.IndexOf('1', StringComparison.Ordinal), stretch.Start);
        Assert.Equal(oldText.LastIndexOf('1'), stretch.End - 1);
    }

    private static List<string> Texts(string text, IReadOnlyList<ChangedSpan> spans) =>
        [.. spans.Select(span => text.Substring(span.Start, span.Length))];
}
