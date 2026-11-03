using Gitunia.Core.Mouse;
using Gitunia.Core.Reflog;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Reflog.Models;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Overlays;

public class ReflogFrameBuilderTests
{
    [Fact]
    public void GivenEntries_WhenRenderingAt80x24_ThenEachRowShowsHashSelectorActionAndDate()
    {
        var frame = ReflogFrameBuilder.Build(State(3), 80, 24, GraphGlyphSet.Unicode);

        Assert.NotNull(frame);
        var lines = frame.Lines.Select(line => line.Text).ToList();
        Assert.Contains("Reflog: HEAD", lines[0], StringComparison.Ordinal);
        Assert.Contains("aaaaaaa  HEAD@{0}  commit: message 0", lines[1], StringComparison.Ordinal);
        Assert.EndsWith("0 hours ago │", lines[1], StringComparison.Ordinal);
        Assert.Contains("HEAD@{2}", lines[3], StringComparison.Ordinal);
        Assert.Contains("1/3", lines[^2], StringComparison.Ordinal);
        Assert.True(frame.Lines[1].IsSelected);
        Assert.False(frame.Lines[2].IsSelected);
    }

    [Fact]
    public void GivenAnEntryWithoutMessage_WhenRendering_ThenOnlyTheActionShows()
    {
        var entries = new[] { new ReflogEntry(0, new string('b', 40), "branch created", string.Empty, "now") };
        var state = new ReflogState(new TabId(1), "refs/heads/x", "x", entries);

        var frame = ReflogFrameBuilder.Build(state, 80, 24, GraphGlyphSet.Unicode);

        Assert.Contains("bbbbbbb  x@{0}  branch created", frame!.Lines[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNoEntries_WhenRendering_ThenItSaysSo()
    {
        var frame = ReflogFrameBuilder.Build(State(0), 80, 24, GraphGlyphSet.Unicode);

        Assert.Contains("no reflog entries", frame!.Lines[1].Text, StringComparison.Ordinal);
        Assert.Contains("0/0", frame.Lines[^2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenMoreEntriesThanRows_WhenTheSelectionIsFarDown_ThenTheWindowScrollsToIt()
    {
        var state = State(100).MoveTo(60);

        var frame = ReflogFrameBuilder.Build(state, 80, 12, GraphGlyphSet.Unicode);

        Assert.Equal(3, ReflogFrameBuilder.VisibleRows(state, 12));
        Assert.Single(frame!.Lines, line => line.IsSelected);
        Assert.Contains("HEAD@{60}", frame.Lines.Single(line => line.IsSelected).Text, StringComparison.Ordinal);
        var selectedLine = Array.FindIndex([.. frame.Lines], line => line.IsSelected);
        Assert.Equal(60, ReflogFrameBuilder.EntryIndexAt(state, selectedLine, 12));
    }

    [Theory]
    [InlineData(10, 24)]
    [InlineData(80, 5)]
    [InlineData(80, 4)]
    public void GivenATinyScreen_WhenRendering_ThenNoFrameIsDrawnAndNothingThrows(int width, int height)
    {
        Assert.Null(ReflogFrameBuilder.Build(State(5), width, height, GraphGlyphSet.Unicode));
    }

    [Fact]
    public void GivenAnAsciiTerminal_WhenRendering_ThenTheFrameUsesAsciiGlyphs()
    {
        var frame = ReflogFrameBuilder.Build(State(2), 80, 24, GraphGlyphSet.Ascii);

        Assert.All(frame!.Lines, line => Assert.True(line.Text.All(character => character < 128)));
    }

    [Fact]
    public void GivenAChromeLine_WhenAskingForTheEntry_ThenThereIsNone()
    {
        var state = State(3);

        Assert.Null(ReflogFrameBuilder.EntryIndexAt(state, 0, 24));
        Assert.Null(ReflogFrameBuilder.EntryIndexAt(state, 4, 24));
        Assert.Equal(2, ReflogFrameBuilder.EntryIndexAt(state, 3, 24));
    }

    [Fact]
    public void GivenEntries_WhenAskingForTheActionUnderALine_ThenTheRowsBelowTheEntriesMapToTheirActions()
    {
        var state = State(3);
        var frame = ReflogFrameBuilder.Build(state, 80, 24, GraphGlyphSet.Unicode)!;

        Assert.Equal(DefaultMouseMap.ReflogSelectButton, ReflogFrameBuilder.ActionAt(state, 4, 24));
        Assert.Equal(DefaultMouseMap.ReflogCheckoutButton, ReflogFrameBuilder.ActionAt(state, 7, 24));
        Assert.Contains("<space>bx", frame.Lines[5].Text, StringComparison.Ordinal);
        Assert.Null(ReflogFrameBuilder.ActionAt(state, 3, 24));
        Assert.Null(ReflogFrameBuilder.ActionAt(state, 8, 24));
    }

    [Fact]
    public void GivenAnActiveSearch_WhenRendering_ThenTheFooterShowsTheBoxAndTheMatchCount()
    {
        var state = State(5).OpenSearch().EditSearch("message 3");

        var editing = ReflogFrameBuilder.Build(state, 80, 24, GraphGlyphSet.Unicode)!;
        var accepted = ReflogFrameBuilder.Build(state.AcceptSearch(), 80, 24, GraphGlyphSet.Unicode)!;

        Assert.Contains("/message 3_  1/1 matches", editing.Lines[^2].Text, StringComparison.Ordinal);
        Assert.Contains("/message 3  1/1 matches  Esc clear", accepted.Lines[^2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenCaretsInsideTheTexts_WhenRendering_ThenTheSearchBoxAndThePickerShowThem()
    {
        var searching = State(5).OpenSearch().EditSearch("message 3", 4);
        var picking = State(3) with { Picker = new ReflogRefPicker(["HEAD", "main"], "mn", 0, 1) };

        var search = ReflogFrameBuilder.Build(searching, 80, 24, GraphGlyphSet.Unicode)!;
        var picker = ReflogFrameBuilder.Build(picking, 80, 24, GraphGlyphSet.Unicode)!;

        Assert.Contains("/mess|age 3", search.Lines[^2].Text, StringComparison.Ordinal);
        Assert.Contains("> m|n", picker.Lines[^2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheRefPicker_WhenRendering_ThenItListsTheChoicesAndMapsClicksToThem()
    {
        var picker = new ReflogRefPicker(["HEAD", "main"]);
        var state = State(3) with { Picker = picker };

        var frame = ReflogFrameBuilder.Build(state, 80, 24, GraphGlyphSet.Unicode)!;

        Assert.Contains("choose a ref", frame.Lines[0].Text, StringComparison.Ordinal);
        Assert.Contains("main", frame.Lines[2].Text, StringComparison.Ordinal);
        Assert.Contains("> _", frame.Lines[^2].Text, StringComparison.Ordinal);
        Assert.Equal(1, ReflogFrameBuilder.PickerIndexAt(picker, 2, 24));
        Assert.Null(ReflogFrameBuilder.PickerIndexAt(picker, 3, 24));
        Assert.Null(ReflogFrameBuilder.ActionAt(state, 4, 24));
    }

    private static ReflogState State(int count) => new(
        new TabId(1),
        "HEAD",
        "HEAD",
        [.. Enumerable.Range(0, count).Select(Entry)]);

    private static ReflogEntry Entry(int index) =>
        new(index, new string('a', 40), "commit", $"message {index}", $"{index} hours ago");
}
