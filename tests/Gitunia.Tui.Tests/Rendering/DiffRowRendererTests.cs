using Gitunia.Core.Diff;
using Gitunia.Tui.Rendering;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering;

public class DiffRowRendererTests
{
    private static readonly GraphGlyphSet Glyphs = GraphGlyphSet.Unicode;

    [Fact]
    public void GivenAnAddedLine_WhenRendering_ThenGutterMarkerAndTextFillTheWidth()
    {
        var row = new DiffRow(DiffRowKind.Added, "hello", null, 12, 0);

        var line = DiffRowRenderer.Render(row, 30, 3, null, false, Glyphs);

        Assert.StartsWith("     12 +hello", line.Text);
        Assert.Equal(30, TextWidth.Of(line.Text));
        Assert.Equal(SegmentRole.DiffAdded, line.Segments.First(segment => segment.Text == "hello").Role);
    }

    [Fact]
    public void GivenARemovedAndAContextLine_WhenRendering_ThenBothNumbersShowForContext()
    {
        var context = DiffRowRenderer.Render(new DiffRow(DiffRowKind.Context, "same", 4, 5, 0), 20, 3, null, false, Glyphs);
        var removed = DiffRowRenderer.Render(new DiffRow(DiffRowKind.Removed, "gone", 4, null, 0), 20, 3, null, false, Glyphs);

        Assert.StartsWith("  4   5  same", context.Text);
        Assert.StartsWith("  4     -gone", removed.Text);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(17)]
    public void GivenWideCharacters_WhenCutToAnyWidth_ThenTheLineNeverOverflowsOrSplitsACharacter(int width)
    {
        var row = new DiffRow(DiffRowKind.Added, "日本語のテキストと絵文字😀です", null, 1, 0);

        var line = DiffRowRenderer.Render(row, width, 3, null, false, Glyphs);

        Assert.Equal(width, TextWidth.Of(line.Text));
        Assert.DoesNotContain('�', line.Text);
    }

    [Fact]
    public void GivenAVeryLongLine_WhenRendering_ThenItIsCutToTheWidthWithAnEllipsisAndMatchesInViewStayHighlighted()
    {
        var text = "needle" + string.Concat(Enumerable.Repeat("\tx", 500_000));
        var row = new DiffRow(DiffRowKind.Added, text, null, 1, 0);

        var line = DiffRowRenderer.Render(row, 40, 3, "needle", false, Glyphs);

        var shown = string.Concat(line.Segments.Select(segment => segment.Text));
        Assert.Equal(40, TextWidth.Of(shown));
        Assert.EndsWith(Glyphs.Ellipsis, shown, StringComparison.Ordinal);
        Assert.Contains(line.Segments, segment => segment.Role == SegmentRole.SearchMatch && segment.Text == "needle");
    }

    [Fact]
    public void GivenALongLineEndingInASurrogatePair_WhenRendering_ThenThePairIsNeverSplit()
    {
        var text = new string('a', 30) + string.Concat(Enumerable.Repeat("\U0001F600", 100));
        var row = new DiffRow(DiffRowKind.Context, text, 1, 1, 0);

        var line = DiffRowRenderer.Render(row, 10, 3, null, false, Glyphs);

        Assert.DoesNotContain(line.Segments, segment => segment.Text.Any(char.IsLowSurrogate) && !segment.Text.Any(char.IsHighSurrogate));
    }

    [Fact]
    public void GivenTabsInTheText_WhenRendering_ThenTheyExpandToTabStops()
    {
        Assert.Equal("a   b       c", DiffRowRenderer.ExpandTabs("a\tb\t\tc"));
        Assert.Equal("ab  c", DiffRowRenderer.ExpandTabs("ab\tc"));
        Assert.Equal("日  x", DiffRowRenderer.ExpandTabs("日\tx"));
    }

    [Fact]
    public void GivenControlCharacters_WhenRendering_ThenTheyBecomeSpaces()
    {
        Assert.Equal("a b c", DiffRowRenderer.ExpandTabs("a\u0001b\rc"));
    }

    [Fact]
    public void GivenALongLine_WhenRendering_ThenItIsCutWithAnEllipsisNotWrapped()
    {
        var row = new DiffRow(DiffRowKind.Context, new string('x', 100), 1, 1, 0);

        var line = DiffRowRenderer.Render(row, 40, 3, null, false, Glyphs);

        Assert.Equal(40, TextWidth.Of(line.Text));
        Assert.EndsWith("…", line.Text);
    }

    [Fact]
    public void GivenAHighlight_WhenRendering_ThenMatchesGetTheSearchRoleIgnoringCase()
    {
        var row = new DiffRow(DiffRowKind.Context, "Foo bar FOO", 1, 1, 0);

        var line = DiffRowRenderer.Render(row, 40, 3, "foo", false, Glyphs);

        var matches = line.Segments.Where(segment => segment.Role == SegmentRole.SearchMatch).Select(segment => segment.Text).ToArray();
        Assert.Equal(["Foo", "FOO"], matches);
    }

    [Theory]
    [InlineData(DiffRowKind.FileHeader, SegmentRole.DiffFile)]
    [InlineData(DiffRowKind.HunkHeader, SegmentRole.DiffHunk)]
    [InlineData(DiffRowKind.Warning, SegmentRole.Error)]
    [InlineData(DiffRowKind.Note, SegmentRole.Faint)]
    public void GivenAHeaderRow_WhenRendering_ThenItHasItsRole(DiffRowKind kind, SegmentRole role)
    {
        var line = DiffRowRenderer.Render(new DiffRow(kind, "text", null, null, 0), 30, 3, null, false, Glyphs);

        Assert.Contains(line.Segments, segment => segment.Role == role && segment.Text.Contains("text", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenALargeLineNumber_WhenChoosingTheGutter_ThenItGrows()
    {
        Assert.Equal(3, DiffRowRenderer.GutterDigits(5));
        Assert.Equal(6, DiffRowRenderer.GutterDigits(123456));
    }
}
