using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Text;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.Visual;

public class VisualReducerTests
{
    [Fact]
    public void GivenACursor_WhenTogglingVisual_ThenARangeStartsAtTheCursorAndTheModeSwitches()
    {
        var state = WithTab();

        var update = Toggle(state, 4);

        Assert.Equal(new RowRange(4, 4), update.State.Shell.Visual!.Range);
        Assert.Equal(PaneKind.Graph, update.State.Shell.Visual.Pane);
        Assert.Equal(state.Workspace.ActiveTabId, update.State.Shell.Visual.TabId);
        Assert.Equal(KeyMode.Visual, update.State.Shell.Mode);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAnActiveRange_WhenTheCursorMoves_ThenTheFarEndFollowsAndTheAnchorStays()
    {
        var state = Toggle(WithTab(), 4).State;

        state = Reduce(state, new VisualCursorMoved(9)).State;
        state = Reduce(state, new VisualCursorMoved(2)).State;

        Assert.Equal(new RowRange(4, 2), state.Shell.Visual!.Range);
        Assert.Equal(3, state.Shell.Visual.Range.Count);
    }

    [Fact]
    public void GivenNoRange_WhenTheCursorMoves_ThenNothingChanges()
    {
        var state = WithTab();

        var update = Reduce(state, new VisualCursorMoved(3));

        Assert.Same(state, update.State);
    }

    [Fact]
    public void GivenAnActiveRange_WhenToggledAgain_ThenTheRangeEndsAndNormalModeReturns()
    {
        var state = Toggle(WithTab(), 4).State;

        var update = Toggle(state, 4);

        Assert.Null(update.State.Shell.Visual);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAnActiveRange_WhenEnded_ThenTheRangeEndsWithoutANotice()
    {
        var state = Toggle(WithTab(), 4).State;

        var update = Reduce(state, new VisualEnded());

        Assert.Null(update.State.Shell.Visual);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAnActiveRange_WhenCopying_ThenTheRangeIsHandedToTheTerminalLayerAndStaysUntilItAnswers()
    {
        var state = Toggle(WithTab(), 4).State;
        state = Reduce(state, new VisualCursorMoved(6)).State;

        var update = Reduce(state, new VisualCopyRequested());

        var copy = Assert.IsType<CopyVisualRangeEffect>(Assert.Single(update.Effects));
        Assert.Equal(PaneKind.Graph, copy.Pane);
        Assert.Equal(new RowRange(4, 6), copy.Range);
        Assert.NotNull(update.State.Shell.Visual);
    }

    [Fact]
    public void GivenNoRange_WhenCopying_ThenNothingIsAskedOfTheTerminalLayer()
    {
        var update = Reduce(WithTab(), new VisualCopyRequested());

        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenACopyThatWorked_WhenTheTerminalLayerAnswers_ThenTheRangeEndsWithItsNotice()
    {
        var state = Toggle(WithTab(), 4).State;

        var update = Reduce(state, new VisualCopied(true, "copy notice"));

        Assert.Null(update.State.Shell.Visual);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Equal("copy notice", NoticeOf(update));
    }

    [Fact]
    public void GivenACopyThatFailed_WhenTheTerminalLayerAnswers_ThenTheRangeStays()
    {
        var state = Toggle(WithTab(), 4).State;

        var update = Reduce(state, new VisualCopied(false, "Nothing to copy"));

        Assert.Equal(new RowRange(4, 4), update.State.Shell.Visual!.Range);
        Assert.Equal(KeyMode.Visual, update.State.Shell.Mode);
        Assert.Equal("Nothing to copy", NoticeOf(update));
    }

    [Fact]
    public void GivenTheSidebarFocused_WhenTogglingVisual_ThenItSaysThereAreNoRangesThere()
    {
        var state = WithTab(PaneKind.Sidebar);

        var update = Toggle(state, 4);

        Assert.Null(update.State.Shell.Visual);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Equal("No range selection in the sidebar", NoticeOf(update));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(3, false)]
    public void GivenAListWithoutRowsOrRanges_WhenTogglingVisual_ThenNothingStartsAndANoticeSaysSo(
        int? cursorRow,
        bool hasRangeSource)
    {
        var update = Reduce(WithTab(), new VisualToggled(cursorRow, hasRangeSource));

        Assert.Null(update.State.Shell.Visual);
        Assert.Equal("Nothing to select", NoticeOf(update));
    }

    [Theory]
    [InlineData(KeyMode.Insert)]
    [InlineData(KeyMode.Command)]
    [InlineData(KeyMode.Confirm)]
    public void GivenAPromptOwnsTheKeys_WhenTogglingVisual_ThenNoRangeStartsAndTheModeStays(KeyMode promptMode)
    {
        var state = WithMode(WithTab(), promptMode);

        var update = Toggle(state, 4);

        Assert.Null(update.State.Shell.Visual);
        Assert.Equal(promptMode, update.State.Shell.Mode);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAnActiveRange_WhenTheRowsChangeMeaning_ThenTheRangeEndsWithANotice()
    {
        var state = Toggle(WithTab(), 4).State;

        var update = Reduce(state, new VisualInvalidated());

        Assert.Null(update.State.Shell.Visual);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Equal(VisualReducer.InvalidatedNotice, NoticeOf(update));
    }

    [Fact]
    public void GivenAnEndedRange_WhenTheRowsChangeMeaning_ThenNothingHappensAgain()
    {
        var state = WithMode(WithTab(), KeyMode.Insert);

        var update = Reduce(state, new VisualInvalidated());

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenARangeWhoseModeWasReplaced_WhenEnding_ThenTheModeIsNotChanged()
    {
        var state = WithMode(Toggle(WithTab(), 4).State, KeyMode.Insert);

        var update = Reduce(state, new VisualEnded());

        Assert.Null(update.State.Shell.Visual);
        Assert.Equal(KeyMode.Insert, update.State.Shell.Mode);
    }

    [Fact]
    public void GivenAnActiveRange_WhenTheFocusMovesToAnotherPane_ThenTheRangeEnds()
    {
        var state = Toggle(WithTab(), 4).State;

        var update = AppReducer.Reduce(state, new TabCommand(TabOf(state), new FocusPaneCommand(PaneKind.Detail)));

        Assert.Null(update.State.Shell.Visual);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
    }

    [Fact]
    public void GivenAnActiveRange_WhenTheFocusStays_ThenTheRangeStays()
    {
        var state = Toggle(WithTab(), 4).State;

        var update = AppReducer.Reduce(state, new TabCommand(TabOf(state), new SelectCommitCommand(null)));

        Assert.Equal(new RowRange(4, 4), update.State.Shell.Visual!.Range);
    }

    [Fact]
    public void GivenAnActiveRange_WhenAnotherTabIsShown_ThenTheRangeEnds()
    {
        var state = Toggle(WithTab(), 4).State;
        state = Open(state, "second");

        Assert.Null(state.Shell.Visual);
        Assert.Equal(KeyMode.Normal, state.Shell.Mode);
    }

    [Fact]
    public void GivenADiffCursor_WhenStartingCharacterSelection_ThenACharacterSelectionStartsAtTheColumn()
    {
        var state = WithTab(PaneKind.Diff);

        var update = Reduce(state, new VisualCharsToggled(3, 5, true));

        var selection = update.State.Shell.Visual!;
        Assert.Equal(VisualKind.Chars, selection.Kind);
        Assert.Equal(new RowRange(3, 3), selection.Range);
        Assert.Equal(new TextSelection(new TextPosition(3, 5), new TextPosition(3, 5)), selection.Text);
        Assert.Equal(KeyMode.Visual, update.State.Shell.Mode);
    }

    [Fact]
    public void GivenAPaneWithoutText_WhenStartingCharacterSelection_ThenNothingStartsAndTheUserIsTold()
    {
        var update = Reduce(WithTab(), new VisualCharsToggled(3, 0, false));

        Assert.Null(update.State.Shell.Visual);
        Assert.Equal(VisualReducer.NoCharsNotice, NoticeOf(update));
    }

    [Fact]
    public void GivenACharacterSelection_WhenTheColumnMovesAndTheRowChanges_ThenBothFollowAndTheAnchorStays()
    {
        var state = Reduce(WithTab(PaneKind.Diff), new VisualCharsToggled(3, 5, true)).State;

        state = Reduce(state, new VisualColumnMoved(9)).State;
        state = Reduce(state, new VisualCursorMoved(4)).State;

        Assert.Equal(new TextSelection(new TextPosition(3, 5), new TextPosition(4, 9)), state.Shell.Visual!.Text);
    }

    [Fact]
    public void GivenARowSelection_WhenStartingCharacterSelection_ThenItBecomesCharactersOverTheSameRows()
    {
        var state = Toggle(WithTab(PaneKind.Diff), 2).State;
        state = Reduce(state, new VisualCursorMoved(4)).State;

        state = Reduce(state, new VisualCharsToggled(4, 7, true)).State;

        Assert.Equal(VisualKind.Chars, state.Shell.Visual!.Kind);
        Assert.Equal(new RowRange(2, 4), state.Shell.Visual.Range);
        Assert.Equal(new TextSelection(new TextPosition(2, 0), new TextPosition(4, 7)), state.Shell.Visual.Text);
    }

    [Fact]
    public void GivenACharacterSelection_WhenTheRowKeyIsPressed_ThenItBecomesWholeRows()
    {
        var state = Reduce(WithTab(PaneKind.Diff), new VisualCharsToggled(3, 5, true)).State;

        state = Toggle(state, 3).State;

        Assert.Equal(VisualKind.Lines, state.Shell.Visual!.Kind);
        Assert.Null(state.Shell.Visual.Text);
        Assert.Equal(KeyMode.Visual, state.Shell.Mode);
    }

    [Fact]
    public void GivenACharacterSelection_WhenTheCharacterKeyIsPressedAgain_ThenVisualEnds()
    {
        var state = Reduce(WithTab(PaneKind.Diff), new VisualCharsToggled(3, 5, true)).State;

        state = Reduce(state, new VisualCharsToggled(3, 5, true)).State;

        Assert.Null(state.Shell.Visual);
        Assert.Equal(KeyMode.Normal, state.Shell.Mode);
    }

    [Fact]
    public void GivenAMouseSelection_WhenReduced_ThenTheRowsAndColumnsOfBothEndsAreKept()
    {
        var state = WithTab(PaneKind.Diff);

        state = Reduce(state, new VisualCharsSelected(new TextPosition(2, 4), new TextPosition(5, 1))).State;

        Assert.Equal(new RowRange(2, 5), state.Shell.Visual!.Range);
        Assert.Equal(new TextSelection(new TextPosition(2, 4), new TextPosition(5, 1)), state.Shell.Visual.Text);
        Assert.Equal(KeyMode.Visual, state.Shell.Mode);
    }

    [Fact]
    public void GivenAPromptIsOpen_WhenTheMouseSelects_ThenNothingStartsBehindIt()
    {
        var state = WithMode(WithTab(PaneKind.Diff), KeyMode.Insert);

        var update = Reduce(state, new VisualCharsSelected(new TextPosition(2, 4), new TextPosition(5, 1)));

        Assert.Null(update.State.Shell.Visual);
    }

    [Fact]
    public void GivenACharacterSelection_WhenCopying_ThenTheCharactersAreHandedToTheTerminalLayer()
    {
        var state = Reduce(WithTab(PaneKind.Diff), new VisualCharsSelected(new TextPosition(2, 4), new TextPosition(5, 1))).State;

        var update = Reduce(state, new VisualCopyRequested());

        var effect = Assert.IsType<CopyVisualRangeEffect>(Assert.Single(update.Effects));
        Assert.Equal(new TextSelection(new TextPosition(2, 4), new TextPosition(5, 1)), effect.Chars);
    }

    private static AppUpdate Toggle(AppState state, int row) => Reduce(state, new VisualToggled(row, true));

    private static AppUpdate Reduce(AppState state, VisualCommand command) => AppReducer.Reduce(state, command);

    private static string NoticeOf(AppUpdate update) =>
        Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text;

    private static TabId TabOf(AppState state) => state.Workspace.ActiveTabId!.Value;

    private static AppState WithMode(AppState state, KeyMode mode) =>
        state with { Shell = state.Shell.WithMode(mode) };

    private static AppState Open(AppState state, string name)
    {
        var root = Path.Combine(Path.GetTempPath(), $"gitunia-visual-reducer-{name}");
        var gitDirectory = Path.Combine(root, ".git");
        var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);

        return AppReducer.Reduce(state, new OpenRepositoryCommand(location)).State;
    }

    private static AppState WithTab(PaneKind focus = PaneKind.Graph)
    {
        var state = Open(AppState.Empty, "first");

        return AppReducer.Reduce(state, new TabCommand(TabOf(state), new FocusPaneCommand(focus))).State;
    }
}
