using Gitunia.Core.Compare;
using Gitunia.Core.Effects;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Xunit;

namespace Gitunia.Core.Tests.Compare;

[Trait("Os", "Independent")]
public class CompareReducerTests
{
    private static readonly TabId Tab = new(1);
    private static readonly ObjectId Newest = Id('1');
    private static readonly ObjectId Middle = Id('2');
    private static readonly ObjectId Oldest = Id('3');

    [Fact]
    public void GivenNoMark_WhenPressingTheKey_ThenTheCommitIsMarkedAndTheNoticeSaysSo()
    {
        var update = Press(Middle, mark: null);

        Assert.Equal(new SetCompareMarkCommand(Middle), Assert.Single(update.TabCommands));
        Assert.StartsWith("Marked 2222222", Notice(update), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNoCommitUnderTheCursor_WhenPressingTheKey_ThenOnlyTheNoticeComes()
    {
        var update = Press(null, mark: null);

        Assert.Empty(update.TabCommands);
        Assert.Equal(CompareReducer.NoCommitNotice, Notice(update));
    }

    [Fact]
    public void GivenAMark_WhenPressingTheKeyOnTheMarkedCommit_ThenTheMarkAndTheComparisonAreCleared()
    {
        var update = Press(Middle, mark: Middle);

        Assert.Equal(new SetCompareMarkCommand(null), Assert.Single(update.TabCommands));
        Assert.Contains(new ClearComparisonEffect(Tab), update.Effects);
        Assert.Equal(CompareReducer.ClearedNotice, Notice(update));
    }

    [Fact]
    public void GivenAMark_WhenPressingTheKeyOnAnotherCommit_ThenTheMarkMovesAndTheNextMoveComparesAgain()
    {
        var followed = new CompareFollow(Oldest, true);

        var update = CompareReducer.Reduce(followed, new CompareMarkPressed(Tab, Middle), Context(Newest));

        Assert.Equal(new SetCompareMarkCommand(Middle), Assert.Single(update.TabCommands));
        Assert.Contains(new ClearComparisonEffect(Tab), update.Effects);
        Assert.Contains("moved", Notice(update), StringComparison.Ordinal);
        Assert.Null(update.Follow.Followed);
        Assert.True(update.Follow.ShownChanges);
    }

    [Fact]
    public void GivenANewMark_WhenPressingTheKey_ThenChangesWillBeShownAgain()
    {
        var update = CompareReducer.Reduce(
            new CompareFollow(null, true),
            new CompareMarkPressed(Tab, Middle),
            Context(null));

        Assert.False(update.Follow.ShownChanges);
    }

    [Fact]
    public void GivenAMarkAndTheGraphFocused_WhenEscapeIsPressed_ThenTheMarkIsCleared()
    {
        var update = CompareReducer.Reduce(CompareFollow.None, new CompareEscapePressed(Tab), Context(Newest));

        Assert.Equal(new SetCompareMarkCommand(null), Assert.Single(update.TabCommands));
        Assert.Equal(CompareReducer.ClearedNotice, Notice(update));
    }

    [Fact]
    public void GivenAMarkInAnotherPane_WhenEscapeIsPressed_ThenNothingHappensAndEscapeIsNotClaimed()
    {
        var context = new CompareContext(Tab, Newest, PaneKind.Detail);

        var update = CompareReducer.Reduce(CompareFollow.None, new CompareEscapePressed(Tab), context);

        Assert.Empty(update.TabCommands);
        Assert.Empty(update.Effects);
        Assert.False(CompareReducer.ClaimsEscape(Newest, PaneKind.Detail));
        Assert.False(CompareReducer.ClaimsEscape(null, PaneKind.Graph));
        Assert.True(CompareReducer.ClaimsEscape(Newest, PaneKind.Graph));
    }

    [Fact]
    public void GivenAMarkNewerThanTheLoadedCommit_WhenDetailsChange_ThenTheOlderIsTheLoadedCommit()
    {
        var update = Changed(Loaded(Oldest, markIndex: 0, commitIndex: 2), mark: Newest);

        var start = Assert.IsType<StartComparisonEffect>(Assert.Single(update.Effects));
        Assert.Equal(new StartComparisonEffect(Tab, Oldest, Newest, Oldest), start);
    }

    [Fact]
    public void GivenAMarkOlderThanTheLoadedCommit_WhenDetailsChange_ThenTheOrderIsTheSame()
    {
        var update = Changed(Loaded(Newest, markIndex: 2, commitIndex: 0), mark: Oldest);

        Assert.Equal(new StartComparisonEffect(Tab, Oldest, Newest, Newest), Assert.Single(update.Effects));
    }

    [Fact]
    public void GivenTheMarkedCommitLoaded_WhenDetailsChange_ThenNothingIsCompared()
    {
        var update = Changed(Loaded(Newest, markIndex: 0, commitIndex: 0), mark: Newest);

        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenNoMark_WhenDetailsChange_ThenNothingIsCompared()
    {
        var update = Changed(Loaded(Middle, markIndex: -1, commitIndex: 1), mark: null);

        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenDetailsThatAreStillLoading_WhenTheyChange_ThenNothingIsComparedYet()
    {
        var facts = new CompareFacts(false, Middle, false, 0, 1);

        Assert.Empty(Changed(facts, mark: Newest).Effects);
    }

    [Fact]
    public void GivenDetailsThatAreAComparisonAlready_WhenTheyChange_ThenNothingIsComparedAgain()
    {
        var facts = new CompareFacts(true, Middle, true, 0, 1);

        Assert.Empty(Changed(facts, mark: Newest).Effects);
    }

    [Fact]
    public void GivenAMarkOutsideTheGraph_WhenDetailsChange_ThenNothingIsCompared()
    {
        var update = Changed(Loaded(Middle, markIndex: -1, commitIndex: 1), mark: Newest);

        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTheCommitWasAskedFor_WhenDetailsChangeAgain_ThenItIsNotAskedForTwice()
    {
        var update = CompareReducer.Reduce(
            new CompareFollow(Middle, true),
            new CompareDetailsChanged(Tab, Loaded(Middle, 0, 1)),
            Context(Newest));

        Assert.Empty(update.Effects);
        Assert.Equal(Middle, update.Follow.Followed);
    }

    [Fact]
    public void GivenTheCommitWasAskedFor_WhenAnotherCommitLoads_ThenTheOldAskIsForgotten()
    {
        var update = CompareReducer.Reduce(
            new CompareFollow(Middle, true),
            new CompareDetailsChanged(Tab, Loaded(Oldest, 0, 2)),
            Context(Newest));

        Assert.Single(update.Effects);
        Assert.Null(update.Follow.Followed);
    }

    [Fact]
    public void GivenTheStoreStartedTheComparison_WhenItIsReported_ThenChangesShowOnceAndTheCommitIsRemembered()
    {
        var first = CompareReducer.Reduce(CompareFollow.None, new CompareStarted(Tab, Middle), Context(Newest));

        Assert.Equal(new SetBottomTabCommand(BottomTab.Changes), Assert.Single(first.TabCommands));
        Assert.Equal(new CompareFollow(Middle, true), first.Follow);

        var second = CompareReducer.Reduce(first.Follow, new CompareStarted(Tab, Oldest), Context(Newest));

        Assert.Empty(second.TabCommands);
        Assert.Equal(Oldest, second.Follow.Followed);
    }

    private static CompareFacts Loaded(ObjectId commit, int markIndex, int commitIndex) =>
        new(true, commit, false, markIndex, commitIndex);

    private static CompareContext Context(ObjectId? mark) => new(Tab, mark, PaneKind.Graph);

    private static CompareUpdate Press(ObjectId? selected, ObjectId? mark) =>
        CompareReducer.Reduce(CompareFollow.None, new CompareMarkPressed(Tab, selected), Context(mark));

    private static CompareUpdate Changed(CompareFacts facts, ObjectId? mark) =>
        CompareReducer.Reduce(CompareFollow.None, new CompareDetailsChanged(Tab, facts), Context(mark));

    private static string Notice(CompareUpdate update)
    {
        var post = Assert.IsType<PostStatusEffect>(update.Effects.Single(effect => effect is PostStatusEffect));
        Assert.Equal(StatusSeverity.Info, post.Severity);

        return post.Text;
    }

    private static ObjectId Id(char digit)
    {
        Assert.True(ObjectId.TryParse(new string(digit, 40), out var id));

        return id;
    }
}
