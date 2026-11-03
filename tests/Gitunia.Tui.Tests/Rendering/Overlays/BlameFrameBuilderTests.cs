using Gitunia.App.Blame;
using Gitunia.Core.Blame;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Overlays;

[Trait("Os", "Independent")]
public sealed class BlameFrameBuilderTests
{
    private const int Width = 100;
    private const int Height = 30;

    [Fact]
    public void GivenTheSearchBoxWithTheCaretInside_WhenBuildingTheFrame_ThenTheFooterDrawsTheCaretThere()
    {
        var state = new BlameState(new TabId(1), "a.txt", "a.txt", LineCount: 4, SearchText: "todo", IsEditingSearch: true, SearchCaret: 2);

        var frame = Frame(state);

        Assert.Contains("/to|do", frame, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenGitStillBlaming_WhenBuildingTheFrame_ThenTheFooterShowsTheProgressHint()
    {
        var state = new BlameState(new TabId(1), "big.txt", "big.txt", LineCount: 4, BlamedLines: 0);

        var frame = Frame(state);

        Assert.Contains("blaming 0%", frame, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenHalfTheLinesBlamed_WhenBuildingTheFrame_ThenTheFooterShowsFiftyPercent()
    {
        var state = new BlameState(new TabId(1), "big.txt", "big.txt", LineCount: 4, BlamedLines: 2);

        var frame = Frame(state);

        Assert.Contains("blaming 50%", frame, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenGitFinished_WhenBuildingTheFrame_ThenTheProgressHintIsGone()
    {
        var state = new BlameState(new TabId(1), "big.txt", "big.txt", LineCount: 4, BlamedLines: 4, IsComplete: true);

        var frame = Frame(state);

        Assert.DoesNotContain("blaming", frame, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenACharacterSelection_WhenBuildingTheFrame_ThenOnlyTheSelectedCharactersCarryTheRangeTint()
    {
        var selection = new BlameSelection(VisualKind.Chars, 0, 4, 6);
        var state = new BlameState(new TabId(1), "a.txt", "a.txt", LineCount: 2, Selection: selection);
        var data = new BlameData(1, ["foo bar baz", "next"]);

        var frame = BlameFrameBuilder.Build(state, data, DateTimeOffset.UnixEpoch, Width, Height, GraphGlyphSet.Unicode)!;

        var marked = frame.Lines[1].Segments.Where(segment => segment.Selection == SegmentSelection.Range).ToList();
        Assert.Equal("bar", Assert.Single(marked).Text);
        Assert.DoesNotContain(frame.Lines[2].Segments, segment => segment.Selection == SegmentSelection.Range);
        Assert.Contains("VISUAL chars, 1 line", frame.Lines[^2].Text);
    }

    [Fact]
    public void GivenATabInTheLine_WhenSelectingAfterIt_ThenTheTintStartsAtTheTabStopColumn()
    {
        var selection = new BlameSelection(VisualKind.Chars, 0, 2, 3);
        var state = new BlameState(new TabId(1), "a.txt", "a.txt", LineCount: 1, Selection: selection);
        var data = new BlameData(1, ["a\tbc"]);

        var frame = BlameFrameBuilder.Build(state, data, DateTimeOffset.UnixEpoch, Width, Height, GraphGlyphSet.Unicode)!;

        var marked = frame.Lines[1].Segments.Single(segment => segment.Selection == SegmentSelection.Range);
        Assert.Equal("bc", marked.Text);
        Assert.Contains("a   bc", frame.Lines[1].Text);
        Assert.Equal(BlameFrameBuilder.CodeStart(Width) + 4, frame.Lines[1].Text.IndexOf("bc", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenALineSelection_WhenBuildingTheFrame_ThenEverySegmentOfTheSelectedLinesCarriesTheRangeTint()
    {
        var selection = new BlameSelection(VisualKind.Lines, 1);
        var state = new BlameState(new TabId(1), "a.txt", "a.txt", LineCount: 4, Cursor: 2, Selection: selection);

        var frame = BlameFrameBuilder.Build(
            state,
            new BlameData(1, ["a", "b", "c", "d"]),
            DateTimeOffset.UnixEpoch,
            Width,
            Height,
            GraphGlyphSet.Unicode)!;

        Assert.DoesNotContain(frame.Lines[1].Segments, segment => segment.Selection == SegmentSelection.Range);
        Assert.All(frame.Lines[2].Segments.Skip(1).SkipLast(1), segment => Assert.Equal(SegmentSelection.Range, segment.Selection));
        Assert.All(frame.Lines[3].Segments.Skip(1).SkipLast(1), segment => Assert.Equal(SegmentSelection.Range, segment.Selection));
        Assert.Contains("VISUAL 2 lines", frame.Lines[^2].Text);
    }

    private static string Frame(BlameState state)
    {
        var data = new BlameData(1, ["a", "b", "c", "d"]);
        var frame = BlameFrameBuilder.Build(state, data, DateTimeOffset.UnixEpoch, Width, Height, GraphGlyphSet.Unicode)!;

        return string.Join('\n', frame.Lines.Select(line => line.Text));
    }
}
