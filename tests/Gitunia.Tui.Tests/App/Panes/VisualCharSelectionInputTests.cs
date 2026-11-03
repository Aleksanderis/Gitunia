using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.Diff;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Text;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Tests.Fakes;
using Gitunia.Tui.Views.Lists;
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Panes;

/// <summary>The terminal side of character-wise selection in the diff: keys, motions and the pointer (#1161).</summary>
public sealed class VisualCharSelectionInputTests : IDisposable
{
    private readonly DiffListSource _source = new(GraphGlyphSet.Unicode);
    private readonly ListView _list;
    private readonly Dispatcher _host = new(AppState.Empty);
    private readonly List<AppEffect> _effects = [];
    private readonly VisualRangeInput _input;

    public VisualCharSelectionInputTests()
    {
        _source.Update(new DiffDocument(
            [
                new(DiffRowKind.Context, "let value_1 = foo.bar;", 1, 1, 0),
                new(DiffRowKind.Added, "second line", null, 2, 0),
            ],
            [],
            [],
            [["f"]],
            2));
        _list = new ListView { Source = _source };
        var root = Path.Combine(Path.GetTempPath(), "gitunia-visual-chars");
        var gitDirectory = Path.Combine(root, ".git");
        _host.Dispatch(new OpenRepositoryCommand(new RepositoryLocation(root, gitDirectory, gitDirectory, false, false)));
        _host.Dispatch(new TabCommand(_host.Workspace.ActiveTabId!.Value, new FocusPaneCommand(PaneKind.Diff)));
        _host.EffectRaised += _effects.Add;
        _input = new VisualRangeInput(_host, new FakePaneLists(_list));
    }

    public void Dispose()
    {
        _list.Dispose();
        _source.Dispose();
    }

    [Fact]
    public void GivenTheDiff_WhenTheCharacterKeyIsPressed_ThenACharacterSelectionStartsAtTheCursorRowAndIsDrawn()
    {
        _list.SelectedItem = 1;

        var handled = _input.TryExecute(ActionIds.VisualToggleChars, null);

        Assert.True(handled);
        Assert.Equal(VisualKind.Chars, _host.State.Shell.Visual!.Kind);
        Assert.Equal(new TextSelection(new TextPosition(1, 0), new TextPosition(1, 0)), _source.Text);
        Assert.Equal(KeyMode.Visual, _host.State.Shell.Mode);
    }

    [Fact]
    public void GivenACharacterSelection_WhenMovingByWordsAndToTheLineEnd_ThenTheCursorEndFollowsAndTheAnchorStays()
    {
        _list.SelectedItem = 0;
        _input.TryExecute(ActionIds.VisualToggleChars, null);

        _input.TryExecute(ActionIds.VisualWordNext, null);
        _input.TryExecute(ActionIds.VisualWordNext, 2);
        Assert.Equal(new TextSelection(new TextPosition(0, 0), new TextPosition(0, 14)), _source.Text);

        _input.TryExecute(ActionIds.VisualLineEnd, null);
        Assert.Equal(21, _source.Text!.Value.Cursor.Column);

        _input.TryExecute(ActionIds.VisualCharLeft, 2);
        Assert.Equal(19, _source.Text!.Value.Cursor.Column);

        _input.TryExecute(ActionIds.VisualWordPrevious, null);
        Assert.Equal(18, _source.Text!.Value.Cursor.Column);

        _input.TryExecute(ActionIds.VisualLineStart, null);
        Assert.Equal(new TextSelection(new TextPosition(0, 0), new TextPosition(0, 0)), _source.Text);
    }

    [Fact]
    public void GivenACharacterSelection_WhenTheCursorMovesToAnotherRow_ThenTheSelectionSpansBothRowsAndCopyCarriesIt()
    {
        _list.SelectedItem = 0;
        _input.TryExecute(ActionIds.VisualToggleChars, null);
        _input.TryExecute(ActionIds.VisualCharRight, 4);
        _list.SelectedItem = 1;
        _input.OnCursorMoved();

        _input.TryExecute(ActionIds.VisualCopy, null);

        var copy = Assert.Single(_effects.OfType<CopyVisualRangeEffect>());
        Assert.Equal(new TextSelection(new TextPosition(0, 0), new TextPosition(1, 4)), copy.Chars);
    }

    [Fact]
    public void GivenARowSelection_WhenAMotionKeyArrives_ThenNothingMovesButTheKeyIsConsumed()
    {
        _list.SelectedItem = 0;
        _input.TryExecute(ActionIds.VisualToggle, null);

        var handled = _input.TryExecute(ActionIds.VisualCharRight, null);

        Assert.True(handled);
        Assert.Null(_source.Text);
        Assert.Equal(VisualKind.Lines, _host.State.Shell.Visual!.Kind);
    }

    [Fact]
    public void GivenADragAcrossTwoRows_WhenThePointerEvents_ThenTheSelectionRunsFromTheCharacterPressedToTheCharacterUnderThePointer()
    {
        _list.SelectedItem = 0;
        _input.TryExecute(ActionIds.VisualToggle, null);

        _input.OnTextPointed(new TextPointer(
            TextPointerKind.DragStart,
            new TextPosition(0, 5),
            new TextPosition(0, 4)));
        _input.OnTextPointed(new TextPointer(TextPointerKind.DragMove, new TextPosition(1, 3)));

        Assert.Equal(new TextSelection(new TextPosition(0, 4), new TextPosition(1, 3)), _source.Text);
        Assert.Equal(new RowRange(0, 1), _source.Range);
    }

    [Fact]
    public void GivenADragThatEndsBeforeItsStart_WhenSelecting_ThenTheSelectionStillCoversBothEnds()
    {
        _input.OnTextPointed(new TextPointer(TextPointerKind.DragStart, new TextPosition(1, 5), new TextPosition(1, 5)));
        _input.OnTextPointed(new TextPointer(TextPointerKind.DragMove, new TextPosition(0, 2)));

        var selection = _source.Text!.Value;

        Assert.Equal("t value_1 = foo.bar;\nsecond", selection.TextIn(_source));
    }

    [Fact]
    public void GivenADoubleClickOnAWord_WhenTheWordActionRuns_ThenExactlyThatWordIsSelected()
    {
        _input.OnTextPointed(new TextPointer(TextPointerKind.DoubleClick, new TextPosition(0, 8)));

        _input.TryExecute(ActionIds.VisualSelectWord, null);

        Assert.Equal("value_1", _source.Text!.Value.TextIn(_source));
    }

    [Fact]
    public void GivenATripleClick_WhenTheLineActionRuns_ThenTheWholeTextOfTheLineIsSelected()
    {
        _input.OnTextPointed(new TextPointer(TextPointerKind.TripleClick, new TextPosition(1, 3)));

        _input.TryExecute(ActionIds.VisualSelectLine, null);

        Assert.Equal("second line", _source.Text!.Value.TextIn(_source));
    }

    [Fact]
    public void GivenACharacterSelection_WhenTheWordKeyIsPressed_ThenTheWordAtTheCursorColumnIsSelected()
    {
        _list.SelectedItem = 0;
        _input.TryExecute(ActionIds.VisualToggleChars, null);
        _input.TryExecute(ActionIds.VisualWordNext, null);

        _input.TryExecute(ActionIds.VisualSelectWord, null);

        Assert.Equal("value_1", _source.Text!.Value.TextIn(_source));
    }

    [Fact]
    public void GivenAClickBeforeTheKey_WhenTheWordKeyRuns_ThenTheStalePointerIsNotUsed()
    {
        _input.OnTextPointed(new TextPointer(TextPointerKind.DoubleClick, new TextPosition(0, 8)));
        _input.OnTextPointed(new TextPointer(TextPointerKind.Click, new TextPosition(1, 0)));
        _list.SelectedItem = 1;

        _input.TryExecute(ActionIds.VisualSelectWord, null);

        Assert.Equal("second", _source.Text!.Value.TextIn(_source));
    }

    [Fact]
    public void GivenAWrappedDiff_WhenTheCharacterKeyIsPressed_ThenNothingStartsAndTheUserIsTold()
    {
        _source.SetWrapped(true);
        _list.SelectedItem = 0;

        _input.TryExecute(ActionIds.VisualToggleChars, null);

        Assert.False(_input.IsActive);
        Assert.Contains(_effects, effect => effect is PostStatusEffect { Text: VisualReducer.NoCharsNotice });
    }

    [Fact]
    public void GivenACharacterSelection_WhenTheDiffIsWrapped_ThenTheSelectionEnds()
    {
        _list.SelectedItem = 0;
        _input.TryExecute(ActionIds.VisualToggleChars, null);

        _source.SetWrapped(true);

        Assert.False(_input.IsActive);
        Assert.Null(_source.Text);
    }

    [Fact]
    public void GivenACharacterSelection_WhenItEnds_ThenTheListForgetsTheCharacters()
    {
        _list.SelectedItem = 0;
        _input.TryExecute(ActionIds.VisualToggleChars, null);

        _input.TryExecute(ActionIds.Cancel, null);

        Assert.Null(_source.Text);
        Assert.Null(_source.Range);
    }
}
