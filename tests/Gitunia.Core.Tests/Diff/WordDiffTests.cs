using Gitunia.Core.Diff;
using Xunit;

namespace Gitunia.Core.Tests.Diff;

[Trait("Os", "Independent")]
public class WordDiffTests
{
    [Fact]
    public void GivenOneChangedWord_WhenComparing_ThenOnlyThatWordIsMarkedOnEachSide()
    {
        var result = WordDiff.Compare("var total = count + 1;", "var total = amount + 1;");

        Assert.Equal(["count"], Texts("var total = count + 1;", result.Old));
        Assert.Equal(["amount"], Texts("var total = amount + 1;", result.New));
    }

    [Fact]
    public void GivenAnInsertedWord_WhenComparing_ThenTheOldSideHasNothingToMark()
    {
        var result = WordDiff.Compare("call(a, b)", "call(a, extra, b)");

        Assert.Empty(result.Old);
        Assert.Equal(["extra, "], Texts("call(a, extra, b)", result.New));
    }

    [Fact]
    public void GivenTwoChangedWordsApartFromEachOther_WhenComparing_ThenTheyAreTwoStretches()
    {
        var result = WordDiff.Compare("alpha keep beta", "gamma keep delta");

        Assert.Equal(["alpha", "beta"], Texts("alpha keep beta", result.Old));
        Assert.Equal(["gamma", "delta"], Texts("gamma keep delta", result.New));
    }

    [Fact]
    public void GivenNeighbouringChangedWordsSeparatedByASpace_WhenComparing_ThenTheyAreOneStretch()
    {
        var result = WordDiff.Compare("keep one two end", "keep three four end");

        Assert.Equal(["one two"], Texts("keep one two end", result.Old));
        Assert.Equal(["three four"], Texts("keep three four end", result.New));
    }

    [Fact]
    public void GivenOnlyIndentationChanged_WhenComparing_ThenTheWhitespaceIsMarked()
    {
        var result = WordDiff.Compare("    return x;", "\t\treturn x;");

        Assert.Equal(["    "], Texts("    return x;", result.Old));
        Assert.Equal(["\t\t"], Texts("\t\treturn x;", result.New));
    }

    [Fact]
    public void GivenAGainedCarriageReturn_WhenComparing_ThenItIsMarked()
    {
        var result = WordDiff.Compare("line", "line\r");

        Assert.Empty(result.Old);
        Assert.Equal(["\r"], Texts("line\r", result.New));
    }

    [Fact]
    public void GivenIdenticalLines_WhenComparing_ThenNothingIsMarked()
    {
        Assert.Same(WordDiffResult.None, WordDiff.Compare("same", "same"));
    }

    [Fact]
    public void GivenLinesThatShareNothingButSpaces_WhenComparing_ThenNothingIsMarked()
    {
        var result = WordDiff.Compare("one two", "three four");

        Assert.Empty(result.Old);
        Assert.Empty(result.New);
    }

    [Fact]
    public void GivenAnEmptyLine_WhenComparing_ThenNothingIsMarked()
    {
        Assert.Empty(WordDiff.Compare(string.Empty, "text").New);
        Assert.Empty(WordDiff.Compare("text", string.Empty).Old);
    }

    [Fact]
    public void GivenSurrogatePairsInAWord_WhenComparing_ThenThePairIsNeverSplit()
    {
        var result = WordDiff.Compare("a 😀 b", "a 😁 b");

        Assert.Equal(["😀"], Texts("a 😀 b", result.Old));
        Assert.Equal(["😁"], Texts("a 😁 b", result.New));
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
    public void GivenAMinifiedLineWithOneEdit_WhenComparing_ThenItIsFast()
    {
        var oldText = string.Concat(Enumerable.Repeat("foo(bar,1);", 180));
        var newText = oldText.Replace("foo(bar,1);", "foo(bar,2);", StringComparison.Ordinal);
        var watch = System.Diagnostics.Stopwatch.StartNew();

        for (var run = 0; run < 50; run++)
        {
            WordDiff.Compare(oldText, newText);
        }

        Assert.True(watch.ElapsedMilliseconds < 2000, $"{watch.ElapsedMilliseconds} ms");
    }

    private static List<string> Texts(string text, IReadOnlyList<ChangedSpan> spans) =>
        [.. spans.Select(span => text.Substring(span.Start, span.Length))];
}
