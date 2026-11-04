using Gitunia.Core.Blame;
using Gitunia.Core.Effects;
using Gitunia.Core.FileHistory;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Text;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Blame.Models;
using Gitunia.Git.History.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.Blame;

// Visual ranges in the blame view: whole lines and characters of the code column, through the root reducer.
public class BlameSelectionTests
{
    private const int Page = 10;
    private const string Commit = "1111111111111111111111111111111111111111";

    [Fact]
    public void GivenTheView_WhenPressingV_ThenALineSelectionStartsAtTheCursorAndTheKeyModeIsVisual()
    {
        var update = AppReducer.Reduce(Viewing(5, cursor: 2), Pressed(ActionIds.VisualToggle));

        var selection = update.State.Shell.Blame.View!.Selection;
        Assert.Equal(new BlameSelection(VisualKind.Lines, 2), selection);
        Assert.Equal(KeyMode.Visual, Assert.IsType<SetKeyModeEffect>(Assert.Single(update.Effects)).Mode);
    }

    [Fact]
    public void GivenALineSelection_WhenMovingTheCursor_ThenTheRangeSpansAnchorAndCursor()
    {
        var state = Viewing(6, cursor: 1);
        state = Reduce(state, Pressed(ActionIds.VisualToggle));
        state = Reduce(state, Pressed(ActionIds.ListDown, count: 3));

        var view = state.Shell.Blame.View!;
        Assert.Equal(1, view.Selection!.RangeTo(view.Cursor).First);
        Assert.Equal(4, view.Selection!.RangeTo(view.Cursor).Last);
    }

    [Fact]
    public void GivenALineSelection_WhenPressingVOrEscOrQ_ThenItEndsAndTheModeReturnsToNormal()
    {
        foreach (var ending in new[] { ActionIds.VisualToggle, ActionIds.Cancel, ActionIds.AppQuit })
        {
            var started = Reduce(Viewing(3), Pressed(ActionIds.VisualToggle));

            var update = AppReducer.Reduce(started, Pressed(ending));

            Assert.Null(update.State.Shell.Blame.View!.Selection);
            Assert.True(update.State.Shell.Blame.IsOpen);
            Assert.Equal(KeyMode.Normal, Assert.IsType<SetKeyModeEffect>(Assert.Single(update.Effects)).Mode);
        }
    }

    [Fact]
    public void GivenALineSelection_WhenPressingV_ThenACharacterSelectionBecomesWholeLines()
    {
        var state = Reduce(Viewing(3), Pressed(ActionIds.VisualToggleChars));
        Assert.Equal(VisualKind.Chars, state.Shell.Blame.View!.Selection!.Kind);

        var update = AppReducer.Reduce(state, Pressed(ActionIds.VisualToggle));

        Assert.Equal(VisualKind.Lines, update.State.Shell.Blame.View!.Selection!.Kind);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenALineSelection_WhenPressingAltV_ThenItBecomesCharactersAndPressingAgainEndsIt()
    {
        var lines = Reduce(Viewing(3), Pressed(ActionIds.VisualToggle));

        var chars = Reduce(lines, Pressed(ActionIds.VisualToggleChars));
        var ended = AppReducer.Reduce(chars, Pressed(ActionIds.VisualToggleChars));

        Assert.Equal(VisualKind.Chars, chars.Shell.Blame.View!.Selection!.Kind);
        Assert.Null(ended.State.Shell.Blame.View!.Selection);
    }

    [Fact]
    public void GivenACharacterSelection_WhenMovingByMotions_ThenTheCursorColumnFollowsTheLineText()
    {
        var state = Reduce(Viewing(3), Pressed(ActionIds.VisualToggleChars));

        state = Reduce(state, Pressed(ActionIds.VisualWordNext, text: "foo bar baz"));
        Assert.Equal(4, state.Shell.Blame.View!.Selection!.CursorColumn);

        state = Reduce(state, Pressed(ActionIds.VisualLineEnd, text: "foo bar baz"));
        Assert.Equal(10, state.Shell.Blame.View!.Selection!.CursorColumn);

        state = Reduce(state, Pressed(ActionIds.VisualCharLeft, count: 2, text: "foo bar baz"));
        Assert.Equal(8, state.Shell.Blame.View!.Selection!.CursorColumn);

        state = Reduce(state, Pressed(ActionIds.VisualLineStart, text: "foo bar baz"));
        Assert.Equal(0, state.Shell.Blame.View!.Selection!.CursorColumn);
    }

    [Fact]
    public void GivenASelection_WhenSelectingWordOrLine_ThenTheCursorLineIsSelectedByCharacter()
    {
        var started = Reduce(Viewing(3), Pressed(ActionIds.VisualToggleChars));
        started = Reduce(started, Pressed(ActionIds.VisualCharRight, count: 5, text: "foo bar baz"));

        var word = Reduce(started, Pressed(ActionIds.VisualSelectWord, text: "foo bar baz"));
        var line = Reduce(started, Pressed(ActionIds.VisualSelectLine, text: "foo bar baz"));

        Assert.Equal(new BlameSelection(VisualKind.Chars, 0, 4, 6), word.Shell.Blame.View!.Selection);
        Assert.Equal(new BlameSelection(VisualKind.Chars, 0, 0, 10), line.Shell.Blame.View!.Selection);
    }

    [Fact]
    public void GivenALineSelection_WhenCopying_ThenTheCopyEffectNamesTheLinesAndTheSelectionEnds()
    {
        var state = Reduce(Viewing(6, cursor: 4), Pressed(ActionIds.VisualToggle));
        state = Reduce(state, Pressed(ActionIds.ListUp, count: 2));

        var update = AppReducer.Reduce(state, Pressed(ActionIds.VisualCopy));

        var copy = update.Effects.OfType<CopyBlameSelectionEffect>().Single();
        Assert.Equal((2, 4), (copy.FirstLine, copy.LastLine));
        Assert.Null(copy.Chars);
        Assert.Equal(state.Shell.Blame.Generation, copy.Generation);
        Assert.Null(update.State.Shell.Blame.View!.Selection);
        Assert.Contains(update.Effects, effect => effect is SetKeyModeEffect { Mode: KeyMode.Normal });
    }

    [Fact]
    public void GivenACharacterSelection_WhenCopying_ThenTheEffectCarriesTheExactCharacters()
    {
        var update = AppReducer.Reduce(Viewing(3), new BlameTextSelected(new TextPosition(1, 2), new TextPosition(2, 3), 0));
        var started = update.State;

        var copy = AppReducer.Reduce(started, Pressed(ActionIds.VisualCopy)).Effects.OfType<CopyBlameSelectionEffect>().Single();

        Assert.Equal(new TextSelection(new TextPosition(1, 2), new TextPosition(2, 3)), copy.Chars);
        Assert.Equal((1, 2), (copy.FirstLine, copy.LastLine));
    }

    [Theory]
    [InlineData(VisualCopyMode.OnSelect)]
    [InlineData(VisualCopyMode.CtrlC)]
    public void GivenACharacterSelection_WhenCopiedOnSelectOrWithCtrlC_ThenTheCopyIsAskedAndTheSelectionAndModeStay(
        VisualCopyMode mode)
    {
        var started = AppReducer.Reduce(
            Viewing(3),
            new BlameTextSelected(new TextPosition(1, 2), new TextPosition(2, 3), 0)).State;

        var update = AppReducer.Reduce(started, new BlameCopyRequested(mode));

        var copy = Assert.Single(update.Effects.OfType<CopyBlameSelectionEffect>());
        Assert.Equal(new TextSelection(new TextPosition(1, 2), new TextPosition(2, 3)), copy.Chars);
        Assert.NotNull(update.State.Shell.Blame.View!.Selection);
        Assert.DoesNotContain(update.Effects, effect => effect is SetKeyModeEffect or PostStatusEffect);
    }

    [Fact]
    public void GivenAYankRequest_WhenCopying_ThenItEndsTheSelectionLikeTheKey()
    {
        var state = Reduce(Viewing(6, cursor: 4), Pressed(ActionIds.VisualToggle));

        var update = AppReducer.Reduce(state, new BlameCopyRequested(VisualCopyMode.Yank));

        Assert.Single(update.Effects.OfType<CopyBlameSelectionEffect>());
        Assert.Null(update.State.Shell.Blame.View!.Selection);
    }

    [Fact]
    public void GivenNoSelection_WhenCopyIsRequested_ThenNothingHappens()
    {
        var update = AppReducer.Reduce(Viewing(3), new BlameCopyRequested(VisualCopyMode.OnSelect));

        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTheMouse_WhenDraggingCharactersOrLines_ThenTheSelectionAndCursorFollowThePointer()
    {
        var text = AppReducer.Reduce(Viewing(8), new BlameTextSelected(new TextPosition(1, 4), new TextPosition(3, 2), 0));
        var lines = AppReducer.Reduce(Viewing(8), new BlameLinesSelected(5, 2, 0));

        var textView = text.State.Shell.Blame.View!;
        Assert.Equal(new BlameSelection(VisualKind.Chars, 1, 4, 2), textView.Selection);
        Assert.Equal(3, textView.Cursor);
        var linesView = lines.State.Shell.Blame.View!;
        Assert.Equal(new BlameSelection(VisualKind.Lines, 5), linesView.Selection);
        Assert.Equal(2, linesView.Cursor);
        Assert.Equal(KeyMode.Visual, Assert.IsType<SetKeyModeEffect>(Assert.Single(text.Effects)).Mode);
    }

    [Fact]
    public void GivenASelectionStarted_WhenTheMouseExtendsIt_ThenNoSecondModeEffectIsSent()
    {
        var first = AppReducer.Reduce(Viewing(8), new BlameTextSelected(new TextPosition(1, 4), new TextPosition(1, 4), 0));

        var more = AppReducer.Reduce(first.State, new BlameTextSelected(new TextPosition(1, 4), new TextPosition(2, 0), 0));

        Assert.Empty(more.Effects);
    }

    [Fact]
    public void GivenAFileWithoutLines_WhenStartingASelection_ThenItSaysThereIsNothingToSelect()
    {
        var update = AppReducer.Reduce(Viewing(0), Pressed(ActionIds.VisualToggle));

        Assert.Null(update.State.Shell.Blame.View!.Selection);
        var notice = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal(BlameReducer.NothingToSelectNotice, notice.Text);
    }

    [Fact]
    public void GivenASelection_WhenClickingALine_ThenTheSelectionEndsAndTheCursorMoves()
    {
        var state = Reduce(Viewing(6), Pressed(ActionIds.VisualToggle));

        var update = AppReducer.Reduce(state, new BlameRowClicked(4, 0));

        Assert.Null(update.State.Shell.Blame.View!.Selection);
        Assert.Equal(4, update.State.Shell.Blame.View!.Cursor);
        Assert.Equal(KeyMode.Normal, Assert.IsType<SetKeyModeEffect>(Assert.Single(update.Effects)).Mode);
    }

    [Fact]
    public void GivenASelection_WhenTheViewCloses_ThenTheKeyModeReturnsToNormal()
    {
        var state = Reduce(Viewing(3), Pressed(ActionIds.VisualToggle));

        var update = AppReducer.Reduce(state, Pressed(ActionIds.FileHistoryOpen));

        Assert.False(update.State.Shell.Blame.IsOpen);
        Assert.Contains(update.Effects, effect => effect is SetKeyModeEffect { Mode: KeyMode.Normal });
    }

    [Fact]
    public void GivenALineSelectionOfOneCommit_WhenOpeningTheLineHistory_ThenTheWalkCoversTheWholeRange()
    {
        var state = Reduce(Viewing(8, cursor: 3), Pressed(ActionIds.VisualToggle));
        state = Reduce(state, Pressed(ActionIds.ListDown, count: 2));
        var pressed = new BlameActionPressed(
            ActionIds.LineHistoryOpen,
            null,
            Page,
            Commit,
            "old.txt",
            10,
            CursorOwnPath: "old.txt",
            RangeLines: 3,
            RangeIsOneCommit: true);

        var update = AppReducer.Reduce(state, pressed);

        Assert.False(update.State.Shell.Blame.IsOpen);
        var read = update.Effects.OfType<ReadFileHistoryEffect>().Single();
        Assert.Equal("old.txt", read.Path);
        Assert.Equal(Commit, read.Revision);
        Assert.Equal(new LineRange(11, 13), read.Range);
    }

    [Fact]
    public void GivenALineSelectionOfSeveralCommitsInACommittedFile_WhenOpeningTheLineHistory_ThenTheShownFileIsUsed()
    {
        var state = Reduce(Viewing(8, cursor: 3, revision: Commit), Pressed(ActionIds.VisualToggle));
        state = Reduce(state, Pressed(ActionIds.ListDown, count: 2));
        var pressed = new BlameActionPressed(
            ActionIds.LineHistoryOpen,
            null,
            Page,
            Commit,
            RangeLines: 3,
            RangeIsOneCommit: false);

        var update = AppReducer.Reduce(state, pressed);

        var read = update.Effects.OfType<ReadFileHistoryEffect>().Single();
        Assert.Equal("a.txt", read.Path);
        Assert.Equal(Commit, read.Revision);
        Assert.Equal(new LineRange(4, 6), read.Range);
    }

    [Fact]
    public void GivenALineSelectionOfSeveralCommitsInTheWorkingTreeFile_WhenOpeningTheLineHistory_ThenItWarnsAndStaysOpen()
    {
        var state = Reduce(Viewing(8, cursor: 3), Pressed(ActionIds.VisualToggle));
        state = Reduce(state, Pressed(ActionIds.ListDown, count: 2));
        var pressed = new BlameActionPressed(ActionIds.LineHistoryOpen, null, Page, Commit, RangeLines: 3, RangeIsOneCommit: false);

        var update = AppReducer.Reduce(state, pressed);

        Assert.True(update.State.Shell.Blame.IsOpen);
        Assert.Equal(BlameReducer.MixedLinesNotice, Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text);
        Assert.Empty(update.Effects.OfType<ReadFileHistoryEffect>());
    }

    [Fact]
    public void GivenASelection_WhenTheFileIsReadAgain_ThenTheSelectionIsDropped()
    {
        var state = Reduce(Viewing(5), Pressed(ActionIds.VisualToggle));
        var parent = new BlameActionPressed(ActionIds.BlameParent, null, Page, Commit, "a.txt", 2);

        var update = AppReducer.Reduce(state, parent);

        // The parent key is not bound in Visual mode, but a re-read from any path must not keep a stale selection.
        var view = update.State.Shell.Blame.View!;
        Assert.Null(view.Selection);
        Assert.Contains(update.Effects, effect => effect is SetKeyModeEffect { Mode: KeyMode.Normal });
    }

    [Fact]
    public void GivenTheDefaultKeymap_WhenListingTheBlameViewBindings_ThenTheSelectionKeysAreBoundInTheRightModes()
    {
        var table = DefaultKeymap.Create();
        var normal = table.EffectiveBindings(KeyMode.Normal, DefaultKeymap.BlameView).Select(binding => binding.Action).ToList();
        var visual = table.EffectiveBindings(KeyMode.Visual, DefaultKeymap.BlameView).Select(binding => binding.Action).ToList();

        Assert.Contains(ActionIds.VisualToggle, normal);
        Assert.Contains(ActionIds.VisualToggleChars, normal);
        Assert.DoesNotContain(ActionIds.VisualCopy, normal);
        foreach (var action in new[] { ActionIds.VisualCopy, ActionIds.VisualCharLeft, ActionIds.VisualCharRight, ActionIds.VisualWordNext, ActionIds.VisualWordPrevious, ActionIds.VisualLineStart, ActionIds.VisualLineEnd, ActionIds.VisualSelectWord, ActionIds.VisualSelectLine, ActionIds.LineHistoryOpen, ActionIds.ListDown })
        {
            Assert.Contains(action, visual);
        }
    }

    private static AppState Reduce(AppState state, BlameCommand command) => AppReducer.Reduce(state, command).State;

    private static BlameActionPressed Pressed(ActionId action, int? count = null, string text = "") =>
        new(action, count, Page, Commit, CursorText: text);

    private static AppState Viewing(int lines, int cursor = 0, string? revision = null)
    {
        var opened = AppReducer.Reduce(Opened(), new BlameOpenRequested("a.txt", revision)).State;
        var tab = opened.Shell.Blame.View!.TabId;
        var loaded = AppReducer
            .Reduce(opened, new BlameLoaded(tab, opened.Shell.Blame.Generation, BlameContentKind.Text, lines))
            .State;
        var view = loaded.Shell.Blame.View!.MoveTo(cursor);

        return loaded with { Shell = loaded.Shell with { Blame = loaded.Shell.Blame with { View = view } } };
    }

    private static AppState Opened()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-blame-selection-tests", "repo");
        var gitDirectory = Path.Combine(root, ".git");
        var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);

        return AppReducer.Reduce(AppState.Empty, new OpenRepositoryCommand(location)).State;
    }
}
