using System.Drawing;
using Gitunia.Core.Diff;
using Gitunia.Core.Mouse;
using Gitunia.Core.Text;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Views;
using Gitunia.Tui.Views.Lists;
using Terminal.Gui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.Views.Lists;

public class PaneListViewTests
{
    [Fact]
    public void GivenAPaneThatIgnoresBlankDoubleClicks_WhenDoubleClickingBelowTheLastRow_ThenNoGestureIsRaised()
    {
        var gestures = DoubleClickAt(ignoresBlank: true, row: 5);

        Assert.Empty(gestures);
    }

    [Fact]
    public void GivenAPaneThatIgnoresBlankDoubleClicks_WhenDoubleClickingOnTheRow_ThenTheDoubleClickIsRaised()
    {
        var gestures = DoubleClickAt(ignoresBlank: true, row: 0);

        Assert.Equal([MouseGesture.DoubleClick], gestures);
    }

    [Fact]
    public void GivenAPaneThatKeepsBlankDoubleClicks_WhenDoubleClickingBelowTheLastRow_ThenTheDoubleClickIsRaised()
    {
        var gestures = DoubleClickAt(ignoresBlank: false, row: 5);

        Assert.Equal([MouseGesture.DoubleClick], gestures);
    }

    [Fact]
    public void GivenATextPane_WhenDraggingAcrossText_ThenTheStartCarriesTheOriginAndTheMovesCarryThePointerCharacter()
    {
        using var source = TextSource();
        using var view = TextView(source, selectsText: true);
        var pointed = new List<TextPointer>();
        view.TextPointed += pointed.Add;

        view.NewMouseEvent(new Mouse { Flags = MouseFlags.LeftButtonPressed, Position = new Point(TextColumn(view, 3), 0) });
        view.NewMouseEvent(new Mouse
        {
            Flags = MouseFlags.LeftButtonPressed | MouseFlags.PositionReport,
            Position = new Point(TextColumn(view, 5), 0),
        });
        view.NewMouseEvent(new Mouse
        {
            Flags = MouseFlags.LeftButtonPressed | MouseFlags.PositionReport,
            Position = new Point(TextColumn(view, 2), 1),
        });

        Assert.Equal(TextPointerKind.DragStart, pointed[0].Kind);
        Assert.Equal(new TextPosition(0, 3), pointed[0].Origin);
        Assert.Equal(new TextPosition(0, 5), pointed[0].Position);
        Assert.Equal(TextPointerKind.DragMove, pointed[^1].Kind);
        Assert.Equal(new TextPosition(1, 2), pointed[^1].Position);
    }

    [Fact]
    public void GivenAPaneThatSelectsText_WhenDoubleAndTripleClicking_ThenThePointerCharacterIsReported()
    {
        using var source = TextSource();
        using var view = TextView(source, selectsText: true);
        var pointed = new List<TextPointer>();
        view.TextPointed += pointed.Add;

        view.NewMouseEvent(new Mouse { Flags = MouseFlags.LeftButtonDoubleClicked, Position = new Point(TextColumn(view, 4), 1) });
        view.NewMouseEvent(new Mouse { Flags = MouseFlags.LeftButtonTripleClicked, Position = new Point(TextColumn(view, 4), 1) });

        Assert.Equal(
            [
                new TextPointer(TextPointerKind.DoubleClick, new TextPosition(1, 4)),
                new TextPointer(TextPointerKind.TripleClick, new TextPosition(1, 4)),
            ],
            pointed);
    }

    [Fact]
    public void GivenAPaneThatDoesNotSelectText_WhenDoubleClicking_ThenNoPointerEventIsRaisedButTheGestureIs()
    {
        using var source = TextSource();
        using var view = TextView(source, selectsText: false);
        var pointed = new List<TextPointer>();
        var gestures = new List<MouseGesture>();
        view.TextPointed += pointed.Add;
        view.GestureRaised += (gesture, _) => gestures.Add(gesture);

        view.NewMouseEvent(new Mouse { Flags = MouseFlags.LeftButtonDoubleClicked, Position = new Point(TextColumn(view, 4), 1) });

        Assert.Empty(pointed);
        Assert.Equal([MouseGesture.DoubleClick], gestures);
    }

    [Fact]
    public void GivenAWrappedDiff_WhenDragging_ThenThePointerCharactersOfTheWrappedLinesAreReported()
    {
        using var source = LongTextSource();
        using var view = TextView(source, selectsText: true);
        source.SetWrapped(true);
        source.Width = PanePadding.TextWidth(view.Viewport.Width, ScreenPadding.Columns(view));
        var pointed = new List<TextPointer>();
        view.TextPointed += pointed.Add;

        view.NewMouseEvent(new Mouse { Flags = MouseFlags.LeftButtonPressed, Position = new Point(TextColumn(view, 3), 0) });
        view.NewMouseEvent(new Mouse
        {
            Flags = MouseFlags.LeftButtonPressed | MouseFlags.PositionReport,
            Position = new Point(TextColumn(view, 4), 1),
        });

        var columns = source.Width - 9;
        Assert.Equal(TextPointerKind.DragStart, pointed[0].Kind);
        Assert.Equal(new TextPosition(0, 3), pointed[0].Origin);
        Assert.Equal(new TextPosition(1, columns + 4), pointed[0].Position);
    }

    [Fact]
    public void GivenAPaneThatSelectsLines_WhenDoubleAndTripleClicking_ThenOnlyTheTripleClickIsReportedAsText()
    {
        using var source = TextSource();
        using var view = TextView(source, selectsText: false);
        view.SelectsLine = true;
        var pointed = new List<TextPointer>();
        view.TextPointed += pointed.Add;

        view.NewMouseEvent(new Mouse { Flags = MouseFlags.LeftButtonDoubleClicked, Position = new Point(TextColumn(view, 4), 1) });
        view.NewMouseEvent(new Mouse { Flags = MouseFlags.LeftButtonTripleClicked, Position = new Point(TextColumn(view, 4), 1) });

        Assert.Equal([TextPointerKind.TripleClick], pointed.Select(found => found.Kind));
    }

    [Fact]
    public void GivenAPaneThatSelectsWords_WhenDoubleAndTripleClicking_ThenOnlyTheDoubleClickIsReportedAsText()
    {
        using var source = TextSource();
        using var view = TextView(source, selectsText: false);
        view.SelectsWord = true;
        var pointed = new List<TextPointer>();
        view.TextPointed += pointed.Add;

        view.NewMouseEvent(new Mouse { Flags = MouseFlags.LeftButtonDoubleClicked, Position = new Point(TextColumn(view, 4), 1) });
        view.NewMouseEvent(new Mouse { Flags = MouseFlags.LeftButtonTripleClicked, Position = new Point(TextColumn(view, 4), 1) });

        Assert.Equal([TextPointerKind.DoubleClick], pointed.Select(found => found.Kind));
    }

    private static DiffListSource LongTextSource()
    {
        var source = new DiffListSource(GraphGlyphSet.Unicode);
        source.Update(new DiffDocument(
            [new(DiffRowKind.Context, new string('x', 80), 1, 1, 0), new(DiffRowKind.Context, "end", 2, 2, 0)],
            [],
            [],
            [["f"]],
            2));

        return source;
    }

    private static DiffListSource TextSource()
    {
        var source = new DiffListSource(GraphGlyphSet.Unicode);
        source.Update(new DiffDocument(
            [new(DiffRowKind.Context, "first line", 1, 1, 0), new(DiffRowKind.Context, "second line", 2, 2, 0)],
            [],
            [],
            [["f"]],
            2));

        return source;
    }

    private static PaneListView TextView(DiffListSource source, bool selectsText)
    {
        var view = new PaneListView { Width = 40, Height = 10, SelectsText = selectsText, Source = source };
        view.Layout();

        return view;
    }

    // The pointer column of the character at the given index of a plain-ASCII row (padding and 3-digit gutter before it).
    private static int TextColumn(PaneListView view, int index) => ScreenPadding.Columns(view) + 9 + index;

    private static List<MouseGesture> DoubleClickAt(bool ignoresBlank, int row)
    {
        using var view = new PaneListView();
        view.Width = 20;
        view.Height = 10;
        view.IgnoresBlankDoubleClick = ignoresBlank;
        view.Source = new PlaceholderListSource("note", GraphGlyphSet.Unicode);
        view.Layout();

        var gestures = new List<MouseGesture>();
        view.GestureRaised += (gesture, _) => gestures.Add(gesture);
        view.NewMouseEvent(new Mouse { Flags = MouseFlags.LeftButtonDoubleClicked, Position = new Point(3, row) });

        return gestures;
    }
}
