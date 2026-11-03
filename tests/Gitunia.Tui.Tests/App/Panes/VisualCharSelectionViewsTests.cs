using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.Diff;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Text;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Tests.Fakes;
using Gitunia.Tui.Views.Lists;
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Panes;

/// <summary>
/// Character-wise selection in wrapped and side-by-side diffs and in the Details tab: keys, the pointer and what is
/// copied (#1167).
/// </summary>
public sealed class VisualCharSelectionViewsTests : IDisposable
{
    private const int PaneWidth = 40;
    private const int SplitWidth = 81;
    private const int Prefix = 9;

    private readonly string _long = new string('a', 30) + new string('b', 30) + new string('c', 20);
    private readonly DiffListSource _diff = new(GraphGlyphSet.Unicode);
    private readonly DetailListSource _detail = new(GraphGlyphSet.Unicode);
    private readonly List<AppEffect> _effects = [];
    private ListView? _list;
    private VisualRangeInput? _input;
    private Dispatcher? _host;

    public void Dispose()
    {
        _list?.Dispose();
        _diff.Dispose();
        _detail.Dispose();
    }

    [Fact]
    public void GivenAWrappedRow_WhenTheLineEndKeyMovesTheSelection_ThenTheCursorLineFollowsAndTheWholeRowIsCopied()
    {
        StartDiff(WrappedDocument(), PaneWidth, isWrapped: true, isSideBySide: false);
        _list!.SelectedItem = 0;

        _input!.TryExecute(ActionIds.VisualToggleChars, null);
        _input.TryExecute(ActionIds.VisualLineEnd, null);
        _input.OnCursorMoved();

        var columns = PaneWidth - Prefix;
        Assert.Equal(_long.Length / columns, _list.SelectedItem);
        Assert.Equal(_list.SelectedItem, _host!.State.Shell.Visual!.Range.Cursor);
        Assert.Equal(_long, _diff.CopyText(_diff.Text!.Value, false));
    }

    [Fact]
    public void GivenAWrappedRow_WhenDraggingFromItsFirstLineToItsLastLine_ThenTheCharactersBetweenAreCopiedOnce()
    {
        StartDiff(WrappedDocument(), PaneWidth, isWrapped: true, isSideBySide: false);
        var last = _long.Length / (PaneWidth - Prefix);

        _input!.OnTextPointed(new TextPointer(
            TextPointerKind.DragStart,
            new TextPosition(0, 3),
            new TextPosition(0, 3)));
        _input.OnTextPointed(new TextPointer(TextPointerKind.DragMove, new TextPosition(last, 65)));

        Assert.Equal(_long[3..66], _diff.CopyText(_diff.Text!.Value, false));
    }

    [Fact]
    public void GivenAWrappedRowAndTheNextRow_WhenDraggingAcrossBoth_ThenTheLineBreakIsOnlyBetweenRows()
    {
        StartDiff(WrappedDocument(), PaneWidth, isWrapped: true, isSideBySide: false);
        var items = _long.Length / (PaneWidth - Prefix) + 1;

        _input!.OnTextPointed(new TextPointer(
            TextPointerKind.DragStart,
            new TextPosition(1, 40),
            new TextPosition(1, 40)));
        _input.OnTextPointed(new TextPointer(TextPointerKind.DragMove, new TextPosition(items, 1)));

        Assert.Equal($"{_long[40..]}\nen", _diff.CopyText(_diff.Text!.Value, false));
    }

    [Fact]
    public void GivenTheSideBySideDiff_WhenDraggingOnTheOldSide_ThenTheSelectionStaysOnItAndCopiesItsRows()
    {
        StartDiff(SplitDocument(), SplitWidth, isWrapped: false, isSideBySide: true);

        _input!.OnTextPointed(new TextPointer(
            TextPointerKind.DragStart,
            new TextPosition(1, 0),
            new TextPosition(1, 0), true));
        _input.OnTextPointed(new TextPointer(TextPointerKind.DragMove, new TextPosition(3, 1), null, true));

        Assert.True(_host!.State.Shell.Visual!.IsOldSide);
        Assert.True(_diff.IsOldSide);
        Assert.Equal("old1\nold2\nen", _diff.CopyText(_diff.Text!.Value, _diff.IsOldSide));
    }

    [Fact]
    public void GivenTheSideBySideDiff_WhenDraggingOnTheNewSide_ThenTheCopyEffectCarriesTheSide()
    {
        StartDiff(SplitDocument(), SplitWidth, isWrapped: false, isSideBySide: true);

        _input!.OnTextPointed(new TextPointer(
            TextPointerKind.DragStart,
            new TextPosition(0, 0),
            new TextPosition(0, 0)));
        _input.OnTextPointed(new TextPointer(TextPointerKind.DragMove, new TextPosition(3, 1)));
        _input.TryExecute(ActionIds.VisualCopy, null);

        var copy = Assert.Single(_effects.OfType<CopyVisualRangeEffect>());
        Assert.False(copy.IsOldSide);
        Assert.Equal("ctx\nnew1\nen", _diff.CopyText(copy.Chars!.Value, copy.IsOldSide));
    }

    [Fact]
    public void GivenALineWithOnlyAnOldRow_WhenTheCharacterKeyIsPressed_ThenTheSelectionStartsOnTheOldSide()
    {
        StartDiff(SplitDocument(), SplitWidth, isWrapped: false, isSideBySide: true);
        _list!.SelectedItem = 2;

        _input!.TryExecute(ActionIds.VisualToggleChars, null);
        _input.TryExecute(ActionIds.VisualLineEnd, null);

        Assert.True(_host!.State.Shell.Visual!.IsOldSide);
        Assert.Equal("old2", _diff.CopyText(_diff.Text!.Value, true));
    }

    [Fact]
    public void GivenALineWithBothSides_WhenTheCharacterKeyIsPressed_ThenTheSelectionStartsOnTheNewSide()
    {
        StartDiff(SplitDocument(), SplitWidth, isWrapped: false, isSideBySide: true);
        _list!.SelectedItem = 1;

        _input!.TryExecute(ActionIds.VisualToggleChars, null);
        _input.TryExecute(ActionIds.VisualLineEnd, null);

        Assert.False(_host!.State.Shell.Visual!.IsOldSide);
        Assert.Equal("new1", _diff.CopyText(_diff.Text!.Value, false));
    }

    [Fact]
    public void GivenTheDetailsTab_WhenDragging_ThenTheCharactersOfTheRowsAreCopiedWithoutAGutter()
    {
        StartDetail(BottomTab.Details);

        _input!.OnTextPointed(new TextPointer(
            TextPointerKind.DragStart,
            new TextPosition(0, 8),
            new TextPosition(0, 8)));
        _input.OnTextPointed(new TextPointer(TextPointerKind.DragMove, new TextPosition(1, 3)));

        Assert.Equal(VisualKind.Chars, _host!.State.Shell.Visual!.Kind);
        Assert.Equal("Ann Lee\nDate", _detail.CopyText(_detail.Text!.Value, false));
    }

    [Fact]
    public void GivenTheDetailsTab_WhenTheCharacterKeyAndWordMotionsAreUsed_ThenTheSelectionFollowsTheText()
    {
        StartDetail(BottomTab.Details);
        _list!.SelectedItem = 0;

        _input!.TryExecute(ActionIds.VisualToggleChars, null);
        _input.TryExecute(ActionIds.VisualWordNext, 2);

        // Two words forward lands on the first letter of "Ann" (the colon is a word of its own), which is included.
        Assert.Equal("Author: A", _detail.CopyText(_detail.Text!.Value, false));
    }

    [Fact]
    public void GivenATripleClickInTheDetailsTab_WhenTheLineActionRuns_ThenTheWholeRowIsSelected()
    {
        StartDetail(BottomTab.Details);

        _input!.OnTextPointed(new TextPointer(TextPointerKind.TripleClick, new TextPosition(1, 2)));
        _input.TryExecute(ActionIds.VisualSelectLine, null);

        Assert.Equal("Date: today", _detail.CopyText(_detail.Text!.Value, false));
    }

    [Fact]
    public void GivenTheChangesTab_WhenTheCharacterKeyIsPressed_ThenNothingStartsAndTheUserIsTold()
    {
        StartDetail(BottomTab.Changes);
        _list!.SelectedItem = 0;

        _input!.TryExecute(ActionIds.VisualToggleChars, null);

        Assert.False(_input.IsActive);
        Assert.False(_detail.SupportsText);
        Assert.Contains(_effects, effect => effect is PostStatusEffect { Text: VisualReducer.NoCharsNotice });
    }

    private DiffDocument WrappedDocument() => new(
        [new(DiffRowKind.Context, _long, 1, 1, 0), new(DiffRowKind.Context, "end", 2, 2, 0)],
        [],
        [],
        [["f"]],
        2);

    // Lines: [ctx | ctx], [old1 | new1], [old2 | -], [end | end].
    private static DiffDocument SplitDocument() => new(
        [
            new(DiffRowKind.Context, "ctx", 1, 1, 0),
            new(DiffRowKind.Removed, "old1", 2, null, 0),
            new(DiffRowKind.Removed, "old2", 3, null, 0),
            new(DiffRowKind.Added, "new1", null, 2, 0),
            new(DiffRowKind.Context, "end", 4, 3, 0),
        ],
        [],
        [],
        [["f"]],
        4);

    private void StartDiff(DiffDocument document, int width, bool isWrapped, bool isSideBySide)
    {
        _diff.Update(document);
        _diff.Width = width;
        _diff.SetWrapped(isWrapped);
        _diff.SetSideBySide(isSideBySide);
        Start(_diff, PaneKind.Diff);
    }

    private void StartDetail(BottomTab tab)
    {
        List<DetailRow> rows =
        [
            new([new StyledSegment("Author: Ann Lee", SegmentRole.Plain)]),
            new([new StyledSegment("Date: today", SegmentRole.Plain)]),
        ];
        _detail.Update(new DetailContent("abc", rows, 2, [], tab));
        Start(_detail, PaneKind.Detail);
    }

    private void Start(IListDataSource source, PaneKind pane)
    {
        _list = new ListView { Source = source };
        _host = new Dispatcher(AppState.Empty);
        var root = Path.Combine(Path.GetTempPath(), "gitunia-visual-views");
        var gitDirectory = Path.Combine(root, ".git");
        var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
        _host.Dispatch(new OpenRepositoryCommand(location));
        _host.Dispatch(new TabCommand(_host.Workspace.ActiveTabId!.Value, new FocusPaneCommand(pane)));
        _host.EffectRaised += _effects.Add;
        _input = new VisualRangeInput(_host, new FakePaneLists(_list));
    }
}
