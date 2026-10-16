using System.Drawing;
using Gitunia.Core.Diff;
using Gitunia.Core.Lists;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Views.Lists;
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.Views.Lists;

public class DiffListSourceTests
{
    // Rows: 0 -a, 1 +A (group 0), 2 context, 3 context, 4 -d, 5 +D (group 1), 6 context.
    private static readonly DiffRow[] Rows =
    [
        new(DiffRowKind.Removed, "a", 1, null, 0),
        new(DiffRowKind.Added, "A", null, 1, 0),
        new(DiffRowKind.Context, "b", 2, 2, 0),
        new(DiffRowKind.Context, "c", 3, 3, 0),
        new(DiffRowKind.Removed, "d", 4, null, 0),
        new(DiffRowKind.Added, "D", null, 4, 0),
        new(DiffRowKind.Context, "e", 5, 5, 0),
    ];

    [Fact]
    public void GivenTheCursorInAGroup_WhenAskingWhichLinesAreMarked_ThenOnlyThatGroupIs()
    {
        var source = NewSource();
        var list = Sized(source);
        list.SelectedItem = 1;

        Assert.Equal([0, 1], Marked(source, list));
    }

    [Fact]
    public void GivenTheCursorOnContextBelowAGroup_WhenAskingWhichLinesAreMarked_ThenTheGroupAboveIs()
    {
        var source = NewSource();
        var list = Sized(source);
        list.SelectedItem = 3;

        Assert.Equal([0, 1], Marked(source, list));
    }

    [Fact]
    public void GivenTheCursorMoves_WhenAskingAgain_ThenTheOtherGroupIsMarked()
    {
        var source = NewSource();
        var list = Sized(source);
        list.SelectedItem = 1;
        Marked(source, list);
        list.SelectedItem = 5;

        Assert.Equal([4, 5], Marked(source, list));
    }

    [Fact]
    public void GivenAnotherDocument_WhenAskingAgain_ThenTheNewDocumentsGroupIsMarked()
    {
        var source = NewSource();
        var list = Sized(source);
        list.SelectedItem = 1;
        Marked(source, list);
        source.Update(new DiffDocument(Rows[2..], [2], [], [["f"]], 5));

        Assert.Equal([2, 3], Marked(source, list));
    }

    [Fact]
    public void GivenAVisualRange_WhenAskingWhichLinesAreMarked_ThenNoneIs()
    {
        var source = NewSource();
        var list = Sized(source);
        list.SelectedItem = 1;
        source.Range = new RowRange(0, 1);

        Assert.Empty(Marked(source, list));
    }

    [Fact]
    public void GivenTheHighlightIsOff_WhenAskingWhichLinesAreMarked_ThenNoneIs()
    {
        var source = NewSource();
        source.HighlightsGroup = false;
        var list = Sized(source);
        list.SelectedItem = 1;

        Assert.Empty(Marked(source, list));
    }

    [Fact]
    public void GivenWrappedRows_WhenTheCursorIsInAGroup_ThenEveryScreenLineOfItsRowsIsMarked()
    {
        var source = NewSource();
        source.Width = 8;
        source.SetWrapped(true);
        var list = Sized(source);
        list.SelectedItem = source.LineOf(1);

        var marked = Marked(source, list);

        Assert.Equal(Enumerable.Range(source.LineOf(0), source.LineOf(2) - source.LineOf(0)), marked);
    }

    private static List<int> Marked(DiffListSource source, ListView list) =>
        [.. Enumerable.Range(0, source.Count).Where(item => source.IsInCursorGroup(list, item))];

    private static DiffListSource NewSource()
    {
        var source = new DiffListSource(GraphGlyphSet.Unicode) { HighlightsGroup = true };
        source.Update(new DiffDocument(Rows, [0, 4], [], [["f"]], 5));

        return source;
    }

    private static ListView Sized(DiffListSource source)
    {
        var list = new ListView { X = 0, Y = 0, Width = 30, Height = 10, Source = source };
        list.SetRelativeLayout(new Size(40, 20));

        return list;
    }
}
