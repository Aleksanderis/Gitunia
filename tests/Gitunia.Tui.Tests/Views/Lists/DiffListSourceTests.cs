using System.Drawing;
using Gitunia.Core.Diff;
using Gitunia.Core.Lists;
using Gitunia.Core.Mouse;
using Gitunia.Tui.Rendering.Diff;
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

    [Fact]
    public void GivenTheUnstagedSide_WhenClickingTheStageChipOnTheFirstLineOfTheGroup_ThenItIsTheStageButton()
    {
        var source = NewSource(WorkingTreeButtons.Unstaged);
        var list = Sized(source, 80);
        list.SelectedItem = 1;
        var buttons = ChangeGroupButtons.Layout(78, 1, WorkingTreeButtons.Unstaged);

        Assert.Equal(DefaultMouseMap.DiffStageButton, source.ButtonAt(list, buttons[0].Start + 1, source.LineOf(0)));
        Assert.Equal(DefaultMouseMap.DiffDiscardButton, source.ButtonAt(list, buttons[1].Start + 1, source.LineOf(0)));
    }

    [Fact]
    public void GivenTheStagedSide_WhenClickingTheChip_ThenItIsTheUnstageButton()
    {
        var source = NewSource(WorkingTreeButtons.Staged);
        var list = Sized(source, 80);
        list.SelectedItem = 1;
        var buttons = ChangeGroupButtons.Layout(78, 1, WorkingTreeButtons.Staged);

        Assert.Equal(DefaultMouseMap.DiffUnstageButton, source.ButtonAt(list, buttons[0].Start, source.LineOf(0)));
    }

    [Fact]
    public void GivenTheChipColumns_WhenClickingOtherLinesOrOtherColumns_ThenThereIsNoButton()
    {
        var source = NewSource(WorkingTreeButtons.Unstaged);
        var list = Sized(source, 80);
        list.SelectedItem = 1;
        var buttons = ChangeGroupButtons.Layout(78, 1, WorkingTreeButtons.Unstaged);
        var column = buttons[0].Start + 1;

        Assert.Null(source.ButtonAt(list, column, source.LineOf(1)));
        Assert.Null(source.ButtonAt(list, column, source.LineOf(4)));
        Assert.Null(source.ButtonAt(list, 5, source.LineOf(0)));
        Assert.Null(source.ButtonAt(list, buttons[0].Start - 1, source.LineOf(0)));
    }

    [Fact]
    public void GivenANarrowPane_WhenClickingWhereTheChipsWouldBe_ThenThereIsNoButton()
    {
        var source = NewSource(WorkingTreeButtons.Unstaged);
        var list = Sized(source, 36);
        list.SelectedItem = 1;

        for (var column = 0; column < 36; column++)
        {
            Assert.Null(source.ButtonAt(list, column, source.LineOf(0)));
        }
    }

    [Fact]
    public void GivenAVisualRangeOrNoSide_WhenClickingTheChipColumns_ThenThereIsNoButton()
    {
        var source = NewSource(WorkingTreeButtons.Unstaged);
        var list = Sized(source, 80);
        list.SelectedItem = 1;
        var column = ChangeGroupButtons.Layout(78, 1, WorkingTreeButtons.Unstaged)[0].Start + 1;
        source.Range = new RowRange(0, 1);

        Assert.Null(source.ButtonAt(list, column, source.LineOf(0)));

        source.Range = null;
        source.GroupButtons = () => WorkingTreeButtons.None;

        Assert.Null(source.ButtonAt(list, column, source.LineOf(0)));
    }

    [Fact]
    public void GivenALongFirstLine_WhenClickingWhereTheChipsWouldBe_ThenNoButtonCoversTheText()
    {
        var rows = new[] { new DiffRow(DiffRowKind.Added, new string('x', 70), null, 1, 0) };
        var source = new DiffListSource(GraphGlyphSet.Unicode)
        {
            HighlightsGroup = true,
            GroupButtons = () => WorkingTreeButtons.Unstaged,
        };
        source.Update(new DiffDocument(rows, [0], [], [["f"]], 5));
        var list = Sized(source, 80);
        list.SelectedItem = 0;

        for (var column = 0; column < 80; column++)
        {
            Assert.Null(source.ButtonAt(list, column, source.LineOf(0)));
        }
    }

    private static List<int> Marked(DiffListSource source, ListView list) =>
        [.. Enumerable.Range(0, source.Count).Where(item => source.IsInCursorGroup(list, item))];

    private static DiffListSource NewSource(WorkingTreeButtons buttons = WorkingTreeButtons.None)
    {
        var source = new DiffListSource(GraphGlyphSet.Unicode) { HighlightsGroup = true, GroupButtons = () => buttons };
        source.Update(new DiffDocument(Rows, [0, 4], [], [["f"]], 5));

        return source;
    }

    private static ListView Sized(DiffListSource source, int width = 30)
    {
        var list = new ListView { X = 0, Y = 0, Width = width, Height = 10, Source = source };
        list.SetRelativeLayout(new Size(width + 10, 20));

        return list;
    }
}
