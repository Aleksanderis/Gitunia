using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Reflog;
using Gitunia.Core.Status;
using Gitunia.Core.Tests.Fixtures;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Reflog.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.Reflog;

// The reflog overlay through the root reducer: state plus command gives state plus effects, nothing else.
public class ReflogReducerTests
{
    private const int Page = 10;

    [Fact]
    public void GivenNoBranchUnderTheCursor_WhenOpening_ThenTheReadOfHeadIsRequestedForTheActiveTab()
    {
        var state = Opened("alpha", "beta");
        var active = state.Workspace.ActiveTabId!.Value;

        var update = AppReducer.Reduce(state, new ReflogOpenRequested(null));

        var read = Assert.IsType<ReadReflogEffect>(Assert.Single(update.Effects));
        Assert.Equal(active, read.TabId);
        Assert.Equal("HEAD", read.Reference);
        Assert.Equal("HEAD", read.Title);
        Assert.False(read.Refresh);
        Assert.True(update.State.Shell.Reflog.IsReading);
        Assert.False(update.State.Shell.Reflog.IsOpen);
    }

    [Fact]
    public void GivenALocalBranchRow_WhenOpening_ThenThatBranchsReflogIsRead()
    {
        var update = AppReducer.Reduce(Opened("alpha"), new ReflogOpenRequested("topic"));

        var read = Assert.IsType<ReadReflogEffect>(Assert.Single(update.Effects));
        Assert.Equal("refs/heads/topic", read.Reference);
        Assert.Equal("topic", read.Title);
    }

    [Fact]
    public void GivenNoTab_WhenOpening_ThenNothingHappens()
    {
        var update = AppReducer.Reduce(AppState.Empty, new ReflogOpenRequested(null));

        Assert.Empty(update.Effects);
        Assert.False(update.State.Shell.Reflog.IsReading);
    }

    [Fact]
    public void GivenAReadInFlight_WhenOpeningAgain_ThenOnlyOneReadRuns()
    {
        var reading = AppReducer.Reduce(Opened("alpha"), new ReflogOpenRequested(null)).State;

        var update = AppReducer.Reduce(reading, new ReflogOpenRequested("topic"));

        Assert.Empty(update.Effects);
        Assert.Equal(reading.Shell.Reflog, update.State.Shell.Reflog);
    }

    [Fact]
    public void GivenTheResultOfTheRead_WhenItArrives_ThenTheListOpensOnTheEntries()
    {
        var reading = AppReducer.Reduce(Opened("alpha"), new ReflogOpenRequested(null));
        var read = Assert.IsType<ReadReflogEffect>(Assert.Single(reading.Effects));

        var update = AppReducer.Reduce(reading.State, Loaded(read, Entries(3)));

        var list = update.State.Shell.Reflog.List!;
        Assert.Equal("HEAD", list.Title);
        Assert.Equal(3, list.Entries.Count);
        Assert.Equal(read.TabId, list.TabId);
        Assert.False(update.State.Shell.Reflog.IsReading);
    }

    [Fact]
    public void GivenTheListWasClosedMeanwhile_WhenTheReadArrives_ThenTheStaleResultIsDropped()
    {
        var reading = AppReducer.Reduce(Opened("alpha"), new ReflogOpenRequested(null));
        var read = Assert.IsType<ReadReflogEffect>(Assert.Single(reading.Effects));
        var closed = AppReducer.Reduce(reading.State, new ReflogTabClosed()).State;

        var update = AppReducer.Reduce(closed, Loaded(read, Entries(3)));

        Assert.False(update.State.Shell.Reflog.IsOpen);
        Assert.False(update.State.Shell.Reflog.IsReading);
    }

    [Fact]
    public void GivenAFailingRead_WhenItsResultArrives_ThenNoListOpensAndTheErrorIsReported()
    {
        var reading = AppReducer.Reduce(Opened("alpha"), new ReflogOpenRequested(null)).State;

        var update = AppReducer.Reduce(reading, new ReflogReadFailed("Could not read the reflog of HEAD: boom"));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal(StatusSeverity.Error, post.Severity);
        Assert.Equal("Could not read the reflog of HEAD: boom", post.Text);
        Assert.False(update.State.Shell.Reflog.IsOpen);
        Assert.False(update.State.Shell.Reflog.IsReading);
    }

    [Fact]
    public void GivenACancelledRead_WhenItsResultArrives_ThenANewReadIsPossible()
    {
        var reading = AppReducer.Reduce(Opened("alpha"), new ReflogOpenRequested(null)).State;

        var cancelled = AppReducer.Reduce(reading, new ReflogReadCancelled()).State;

        Assert.False(cancelled.Shell.Reflog.IsReading);
        Assert.Single(AppReducer.Reduce(cancelled, new ReflogOpenRequested(null)).Effects);
    }

    [Fact]
    public void GivenTheTabWasClosedMeanwhile_WhenTheReadArrives_ThenNoListOpensForTheClosedTab()
    {
        var state = Opened("alpha", "beta");
        var reading = AppReducer.Reduce(state, new ReflogOpenRequested(null));
        var read = Assert.IsType<ReadReflogEffect>(Assert.Single(reading.Effects));
        var closed = AppReducer.Reduce(reading.State, new CloseTabCommand(read.TabId)).State;

        var update = AppReducer.Reduce(closed, Loaded(read, Entries(2)));

        Assert.Null(closed.Workspace.FindTab(read.TabId));
        Assert.False(update.State.Shell.Reflog.IsOpen);
        Assert.False(update.State.Shell.Reflog.IsReading);
    }

    [Fact]
    public void GivenAnOpenList_WhenItsTabIsClosed_ThenTheListClosesWithIt()
    {
        var state = OpenList(Opened("alpha", "beta"), 3);
        var tab = state.Shell.Reflog.List!.TabId;

        var update = AppReducer.Reduce(state, new CloseTabCommand(tab));

        Assert.False(update.State.Shell.Reflog.IsOpen);
    }

    [Fact]
    public void GivenAnOpenSearchBox_WhenItsTabIsClosed_ThenTheKeyModeGoesBackToNormal()
    {
        var state = OpenList(Opened("alpha", "beta"), 3);
        state = Press(state, ActionIds.SearchOpen);
        var tab = state.Shell.Reflog.List!.TabId;

        var update = AppReducer.Reduce(state, new CloseTabCommand(tab));

        Assert.False(update.State.Shell.Reflog.IsOpen);
        Assert.Contains(update.Effects, effect => effect is SetKeyModeEffect { Mode: KeyMode.Normal });
    }

    [Fact]
    public void GivenAnOpenList_WhenMovingAndClosing_ThenTheHighlightFollowsAndEveryActionIsSwallowed()
    {
        var state = OpenList(Opened("alpha"), 5);

        state = Press(state, ActionIds.ListDown);
        state = Press(state, ActionIds.ListDown);
        Assert.Equal(2, state.Shell.Reflog.List!.SelectedIndex);
        state = Press(state, ActionIds.ListBottom);
        Assert.Equal(4, state.Shell.Reflog.List!.SelectedIndex);
        state = Press(state, ActionIds.ListTop, 3);
        Assert.Equal(2, state.Shell.Reflog.List!.SelectedIndex);
        state = Press(state, ActionIds.PaneNext);
        Assert.Equal(2, state.Shell.Reflog.List!.SelectedIndex);
        state = Press(state, ActionIds.Cancel);

        Assert.False(state.Shell.Reflog.IsOpen);
    }

    [Fact]
    public void GivenAnOpenList_WhenQuitting_ThenTheListClosesAndNothingElseHappens()
    {
        var update = AppReducer.Reduce(OpenList(Opened("alpha"), 2), Pressed(ActionIds.AppQuit));

        Assert.False(update.State.Shell.Reflog.IsOpen);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAnOpenList_WhenTheRepositoryMoves_ThenRefreshReadsItAgain()
    {
        var state = OpenList(Opened("alpha"), 3);

        var update = AppReducer.Reduce(state, Pressed(ActionIds.RepositoryRefresh));

        var read = Assert.IsType<ReadReflogEffect>(Assert.Single(update.Effects));
        Assert.True(read.Refresh);
        Assert.Equal("HEAD", read.Reference);
        Assert.True(update.State.Shell.Reflog.IsOpen);
    }

    [Fact]
    public void GivenARefreshedRead_WhenItArrives_ThenTheHighlightStaysOnItsRowInTheFreshEntries()
    {
        var state = Press(OpenList(Opened("alpha"), 5), ActionIds.ListTop, 4);
        var refresh = AppReducer.Reduce(state, Pressed(ActionIds.RepositoryRefresh));
        var read = Assert.IsType<ReadReflogEffect>(Assert.Single(refresh.Effects));

        var update = AppReducer.Reduce(refresh.State, Loaded(read, Entries(2)));

        var list = update.State.Shell.Reflog.List!;
        Assert.Equal(2, list.Entries.Count);
        Assert.Equal(1, list.SelectedIndex);
    }

    [Theory]
    [InlineData(ReflogEntryKind.SelectCommit)]
    [InlineData(ReflogEntryKind.Reset)]
    [InlineData(ReflogEntryKind.CreateBranch)]
    [InlineData(ReflogEntryKind.CheckoutDetached)]
    public void GivenAnEntry_WhenUsingIt_ThenTheShellIsAskedToStartThatFlowForTheHighlightedCommit(
        ReflogEntryKind kind)
    {
        var state = Press(OpenList(Opened("alpha"), 3), ActionIds.ListDown);

        var update = AppReducer.Reduce(state, Pressed(ActionFor(kind)));

        var effect = Assert.IsType<ReflogEntryActionEffect>(Assert.Single(update.Effects));
        Assert.Equal(kind, effect.Kind);
        Assert.Equal(Hash(1), effect.Hash);
        Assert.Equal(state.Shell.Reflog.List!.TabId, effect.TabId);
        Assert.True(update.State.Shell.Reflog.IsOpen);
    }

    [Fact]
    public void GivenAnEmptyReflog_WhenActingOnAnEntry_ThenNothingStartsAndTheStatusWarns()
    {
        var state = OpenList(Opened("alpha"), 0);

        var update = AppReducer.Reduce(state, Pressed(ActionIds.ResetBranch));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal("No reflog entry selected", post.Text);
        Assert.Equal(StatusSeverity.Warning, post.Severity);
    }

    [Fact]
    public void GivenARefusedFlow_WhenTheShellAnswers_ThenTheListStaysOpenAndTheReasonIsShown()
    {
        var state = OpenList(Opened("alpha"), 3);

        var update = AppReducer.Reduce(state, new ReflogEntryActionRefused("HEAD is detached"));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal("HEAD is detached", post.Text);
        Assert.Equal(StatusSeverity.Warning, post.Severity);
        Assert.True(update.State.Shell.Reflog.IsOpen);
    }

    [Fact]
    public void GivenAStartedFlow_WhenTheShellAnswers_ThenTheListCloses()
    {
        var update = AppReducer.Reduce(OpenList(Opened("alpha"), 3), new ReflogEntryActionStarted());

        Assert.False(update.State.Shell.Reflog.IsOpen);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAClickOnARow_WhenSelectingByIndex_ThenThatEntryIsHighlighted()
    {
        var update = AppReducer.Reduce(OpenList(Opened("alpha"), 4), new ReflogEntryClicked(2));

        Assert.Equal(2, update.State.Shell.Reflog.List!.SelectedIndex);
    }

    [Fact]
    public void GivenTypedText_WhenSearching_ThenTheHighlightFollowsAndTheKeysWork()
    {
        var state = OpenList(Opened("alpha"), 4, message: index => index == 2 ? "needle here" : "plain");
        state = Press(state, ActionIds.SearchOpen);
        Assert.True(state.Shell.Reflog.List!.IsEditingSearch);

        var typing = AppReducer.Reduce(state, new ReflogKeyTyped(new KeyEvent("n")));
        state = Type(typing.State, "eedle");

        Assert.Equal("needle", state.Shell.Reflog.List!.SearchText);
        Assert.Equal(2, state.Shell.Reflog.List.SelectedIndex);
        state = Press(state, ActionIds.InputAccept);
        Assert.False(state.Shell.Reflog.List!.IsEditingSearch);
        Assert.True(state.Shell.Reflog.List.HasSearch);
        state = Press(state, ActionIds.SearchNext);
        Assert.Equal(2, state.Shell.Reflog.List!.SelectedIndex);
    }

    [Fact]
    public void GivenTheSearchBox_WhenItOpensAndCloses_ThenTheKeyModeFollows()
    {
        var state = OpenList(Opened("alpha"), 3);

        var opened = AppReducer.Reduce(state, Pressed(ActionIds.SearchOpen));
        var accepted = AppReducer.Reduce(opened.State, Pressed(ActionIds.InputAccept));

        Assert.Equal(KeyMode.Insert, Assert.IsType<SetKeyModeEffect>(Assert.Single(opened.Effects)).Mode);
        Assert.Equal(KeyMode.Normal, Assert.IsType<SetKeyModeEffect>(Assert.Single(accepted.Effects)).Mode);
    }

    [Fact]
    public void GivenNoMatch_WhenSteppingTheSearch_ThenTheStatusSaysSo()
    {
        var state = Type(Press(OpenList(Opened("alpha"), 3), ActionIds.SearchOpen), "zzz");
        state = Press(state, ActionIds.InputAccept);

        var update = AppReducer.Reduce(state, Pressed(ActionIds.SearchNext));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal("No match for zzz", post.Text);
        Assert.Equal(StatusSeverity.Warning, post.Severity);
    }

    [Fact]
    public void GivenNoSearch_WhenSteppingIt_ThenTheStatusAsksToPressSlashFirst()
    {
        var update = AppReducer.Reduce(OpenList(Opened("alpha"), 3), Pressed(ActionIds.SearchNext));

        Assert.Equal("No search: press / first", Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text);
    }

    [Fact]
    public void GivenTheSearchBox_WhenEscapeIsPressed_ThenTheSearchIsDroppedAndTheListStaysOpen()
    {
        var state = Type(Press(OpenList(Opened("alpha"), 3), ActionIds.SearchOpen), "x");

        var update = AppReducer.Reduce(state, Pressed(ActionIds.Cancel));

        Assert.True(update.State.Shell.Reflog.IsOpen);
        Assert.False(update.State.Shell.Reflog.List!.HasSearch);
        Assert.Equal(KeyMode.Normal, Assert.IsType<SetKeyModeEffect>(Assert.Single(update.Effects)).Mode);
    }

    [Fact]
    public void GivenAnAcceptedSearch_WhenEscapeIsPressed_ThenOnlyTheSearchIsDropped()
    {
        var state = Press(Type(Press(OpenList(Opened("alpha"), 3), ActionIds.SearchOpen), "x"), ActionIds.InputAccept);

        var cleared = Press(state, ActionIds.Cancel);
        var closed = Press(cleared, ActionIds.Cancel);

        Assert.True(cleared.Shell.Reflog.IsOpen);
        Assert.False(cleared.Shell.Reflog.List!.HasSearch);
        Assert.False(closed.Shell.Reflog.IsOpen);
    }

    [Fact]
    public void GivenTheSearchBox_WhenQuitting_ThenTheListClosesAndTheModeGoesBack()
    {
        var state = Press(OpenList(Opened("alpha"), 3), ActionIds.SearchOpen);

        var update = AppReducer.Reduce(state, Pressed(ActionIds.AppQuit));

        Assert.False(update.State.Shell.Reflog.IsOpen);
        Assert.Equal(KeyMode.Normal, Assert.IsType<SetKeyModeEffect>(Assert.Single(update.Effects)).Mode);
    }

    [Fact]
    public void GivenBranches_WhenOpeningThePicker_ThenHeadAndTheLocalBranchesAreOfferedInInsertMode()
    {
        var state = WithRefs(OpenList(Opened("alpha"), 3), Branch("main"), Branch("topic"), Tag("v1"),
            Remote("origin/main"));

        var update = AppReducer.Reduce(state, Pressed(ActionIds.ReflogOpen));

        var picker = update.State.Shell.Reflog.List!.Picker!;
        Assert.Equal(["HEAD", "main", "topic"], picker.Names);
        Assert.Equal(KeyMode.Insert, Assert.IsType<SetKeyModeEffect>(Assert.Single(update.Effects)).Mode);
    }

    [Fact]
    public void GivenTheRefPicker_WhenTypingAndAccepting_ThenThatBranchReflogIsRead()
    {
        var state = WithRefs(OpenList(Opened("alpha"), 3), Branch("main"), Branch("topic"));
        state = Type(Press(state, ActionIds.ReflogOpen), "top");
        Assert.Equal(["topic"], state.Shell.Reflog.List!.Picker!.Candidates);

        var update = AppReducer.Reduce(state, Pressed(ActionIds.InputAccept));

        var read = update.Effects.OfType<ReadReflogEffect>().Single();
        Assert.Equal("refs/heads/topic", read.Reference);
        Assert.Equal("topic", read.Title);
        Assert.Equal(KeyMode.Normal, update.Effects.OfType<SetKeyModeEffect>().Single().Mode);
        Assert.Null(update.State.Shell.Reflog.List!.Picker);
    }

    [Fact]
    public void GivenTheRefPicker_WhenMovingWithTheNavigationKeys_ThenTheHighlightWraps()
    {
        var state = WithRefs(OpenList(Opened("alpha"), 3), Branch("main"));
        state = Press(state, ActionIds.ReflogOpen);

        var down = AppReducer.Reduce(state, new ReflogKeyTyped(new KeyEvent(KeyNames.Down))).State;
        var wrapped = AppReducer.Reduce(down, new ReflogKeyTyped(new KeyEvent(KeyNames.Down))).State;

        Assert.Equal(1, down.Shell.Reflog.List!.Picker!.Index);
        Assert.Equal(0, wrapped.Shell.Reflog.List!.Picker!.Index);
    }

    [Fact]
    public void GivenTheRefPicker_WhenEscapeIsPressed_ThenTheListComesBack()
    {
        var state = Press(OpenList(Opened("alpha"), 3), ActionIds.ReflogOpen);

        var update = AppReducer.Reduce(state, Pressed(ActionIds.Cancel));

        Assert.Null(update.State.Shell.Reflog.List!.Picker);
        Assert.Equal(KeyMode.Normal, Assert.IsType<SetKeyModeEffect>(Assert.Single(update.Effects)).Mode);
    }

    [Fact]
    public void GivenTheRefPicker_WhenOtherActionsArrive_ThenOnlyThePickerActs()
    {
        var state = Press(OpenList(Opened("alpha"), 3), ActionIds.ReflogOpen);

        var update = AppReducer.Reduce(state, Pressed(ActionIds.ResetBranch));

        Assert.Empty(update.Effects);
        Assert.NotNull(update.State.Shell.Reflog.List!.Picker);
        Assert.Equal(0, update.State.Shell.Reflog.List.SelectedIndex);
    }

    [Fact]
    public void GivenAReadInFlight_WhenAcceptingThePicker_ThenNothingRunsAndTheStatusAsksToWait()
    {
        var state = WithRefs(OpenList(Opened("alpha"), 3), Branch("main"));
        state = Press(state, ActionIds.ReflogOpen);
        state = AppReducer.Reduce(state, new ReflogOpenRequested(null)).State;
        state = state with { Shell = state.Shell with { Reflog = state.Shell.Reflog with { IsReading = true } } };

        var update = AppReducer.Reduce(state, Pressed(ActionIds.InputAccept));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal("The reflog is still being read: choose again in a moment", post.Text);
        Assert.NotNull(update.State.Shell.Reflog.List!.Picker);
    }

    [Fact]
    public void GivenTheRefPicker_WhenClickingARow_ThenThatRowIsHighlighted()
    {
        var state = WithRefs(OpenList(Opened("alpha"), 3), Branch("main"), Branch("topic"));
        state = Press(state, ActionIds.ReflogOpen);

        var update = AppReducer.Reduce(state, new ReflogPickerRowClicked(2));

        Assert.Equal(2, update.State.Shell.Reflog.List!.Picker!.Index);
    }

    [Fact]
    public void GivenTheRefPicker_WhenClickingAnEntryRow_ThenNothingChanges()
    {
        var state = Press(OpenList(Opened("alpha"), 3), ActionIds.ReflogOpen);

        var update = AppReducer.Reduce(state, new ReflogEntryClicked(2));

        Assert.Equal(0, update.State.Shell.Reflog.List!.SelectedIndex);
    }

    [Fact]
    public void GivenTextForTheListOfAnotherTab_WhenTyping_ThenTheKeyIsIgnored()
    {
        var state = Press(OpenList(Opened("alpha", "beta"), 3), ActionIds.SearchOpen);
        var other = state.Workspace.Tabs.First(tab => tab.Id != state.Shell.Reflog.List!.TabId).Id;
        state = AppReducer.Reduce(state, new ActivateTabCommand(other)).State;

        var update = AppReducer.Reduce(state, new ReflogKeyTyped(new KeyEvent("x")));

        Assert.Equal(string.Empty, update.State.Shell.Reflog.List!.SearchText);
    }

    [Fact]
    public void GivenEveryConcreteReflogCommand_WhenReducing_ThenTheReducerHasACaseForIt()
    {
        var commands = ConcreteSubtypes.InstancesOf<ReflogCommand>();
        Assert.NotEmpty(commands);
        var context = new ReflogContext(null, null);

        foreach (var command in commands)
        {
            try
            {
                ReflogReducer.Reduce(ReflogSession.Empty, command, context);
            }
            catch (UnhandledCommandException)
            {
                Assert.Fail($"{command.GetType().Name} has no case in ReflogReducer");
            }
            catch (Exception)
            {
                // The empty command was handled by its case and tripped over its empty fields.
            }
        }
    }

    private static ReflogActionPressed Pressed(ActionId action, int? count = null) => new(action, count, Page);

    private static AppState Press(AppState state, ActionId action, int? count = null) =>
        AppReducer.Reduce(state, Pressed(action, count)).State;

    private static AppState Type(AppState state, string text)
    {
        foreach (var character in text)
        {
            state = AppReducer.Reduce(state, new ReflogKeyTyped(new KeyEvent(character.ToString()))).State;
        }

        return state;
    }

    private static ReflogLoaded Loaded(ReadReflogEffect read, IReadOnlyList<ReflogEntry> entries) =>
        new(read.TabId, read.Generation, read.Reference, read.Title, read.Refresh, entries);

    // Opens the list through the real command chain: the request, then the result of its read.
    private static AppState OpenList(AppState state, int count, Func<int, string>? message = null)
    {
        var reading = AppReducer.Reduce(state, new ReflogOpenRequested(null));
        var read = Assert.IsType<ReadReflogEffect>(Assert.Single(reading.Effects));

        return AppReducer.Reduce(reading.State, Loaded(read, Entries(count, message))).State;
    }

    private static List<ReflogEntry> Entries(int count, Func<int, string>? message = null) =>
        [.. Enumerable.Range(0, count).Select(index => new ReflogEntry(index, Hash(index), "commit",
            message?.Invoke(index) ?? "msg", "now"))];

    private static string Hash(int index) => new((char)('a' + index), 40);

    private static ActionId ActionFor(ReflogEntryKind kind) => kind switch
    {
        ReflogEntryKind.SelectCommit => ActionIds.ReflogSelectCommit,
        ReflogEntryKind.Reset => ActionIds.ResetBranch,
        ReflogEntryKind.CreateBranch => ActionIds.BranchCreate,
        _ => ActionIds.BranchSwitch,
    };

    private static AppState WithRefs(AppState state, params GitRef[] refs)
    {
        var tabId = state.Shell.Reflog.List!.TabId;
        var tabs = state.Workspace.Tabs;
        var index = tabs.ToList().FindIndex(tab => tab.Id == tabId);
        var changed = tabs[index] with { Refs = [.. refs] };

        return state with { Workspace = state.Workspace with { Tabs = tabs.SetItem(index, changed) } };
    }

    private static GitRef Branch(string name) => Ref($"refs/heads/{name}", name, RefKind.LocalBranch);

    private static GitRef Tag(string name) => Ref($"refs/tags/{name}", name, RefKind.Tag);

    private static GitRef Remote(string name) => Ref($"refs/remotes/{name}", name, RefKind.RemoteBranch);

    private static GitRef Ref(string fullName, string name, RefKind kind) =>
        new(fullName, name, kind, Hash(0), Hash(0), null, null, null, false, false, null, null);

    private static AppState Opened(params string[] names)
    {
        var state = AppState.Empty;
        foreach (var name in names)
        {
            state = AppReducer.Reduce(state, new OpenRepositoryCommand(Location(name))).State;
        }

        return state;
    }

    private static RepositoryLocation Location(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-reflog-reducer-tests", name);
        var gitDirectory = Path.Combine(root, ".git");

        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }
}
