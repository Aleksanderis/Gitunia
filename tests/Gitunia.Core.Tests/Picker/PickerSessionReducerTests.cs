using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Picker;
using Gitunia.Core.Overlays;
using Gitunia.Core.Shell;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Xunit;

namespace Gitunia.Core.Tests.Picker;

// The picker session end to end through the root reducer: (state, command) -> (state, effects), no terminal, no disk.
public class PickerSessionReducerTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GivenClosedPicker_WhenOpening_ThenItOpensTakesTheKeysAndScansOnce()
    {
        var update = AppReducer.Reduce(Empty(), Open());

        Assert.True(update.State.Shell.Picker.IsOpen);
        Assert.True(update.State.Shell.Picker.IsScanning);
        var scan = Assert.Single(update.Effects.OfType<ScanRepositoriesEffect>());
        Assert.Equal(PickerFixtures.Home, scan.HomeDirectory);
        Assert.Null(scan.Roots);
        Assert.Equal(KeyMode.Insert, Assert.Single(update.Effects.OfType<SetKeyModeEffect>()).Mode);
    }

    [Fact]
    public void GivenAScanThatRuns_WhenOpeningAgain_ThenNoSecondScanStarts()
    {
        var opened = AppReducer.Reduce(Empty(), Open()).State;

        var again = AppReducer.Reduce(opened, Open(Start.AddMinutes(5)));

        Assert.Empty(again.Effects.OfType<ScanRepositoriesEffect>());
    }

    [Theory]
    [InlineData(10, false)]
    [InlineData(31, true)]
    public void GivenAFinishedScan_WhenOpeningAgain_ThenItRescansOnlyAfterThirtySeconds(int seconds, bool rescans)
    {
        var state = AppReducer.Reduce(Empty(), Open()).State;
        state = AppReducer.Reduce(state, new PickerScanFinished([])).State;
        state = AppReducer.Reduce(state, new PickerCloseRequested()).State;

        var update = AppReducer.Reduce(state, Open(Start.AddSeconds(seconds)));

        Assert.Equal(rescans, update.Effects.OfType<ScanRepositoriesEffect>().Any());
    }

    [Fact]
    public void GivenChangedScanRoots_WhenOpeningAgain_ThenItRescansAtOnceInThoseFolders()
    {
        var roots = ImmutableArray.Create(PickerFixtures.PathOf("work"));
        var state = AppReducer.Reduce(Empty(), Open()).State;
        state = AppReducer.Reduce(state, new PickerScanFinished([])).State;
        state = AppReducer.Reduce(state, new PickerCloseRequested()).State;
        state = AppReducer.Reduce(state, new PickerScanRootsChanged(roots)).State;

        var update = AppReducer.Reduce(state, Open(Start.AddSeconds(1)));

        var scan = Assert.Single(update.Effects.OfType<ScanRepositoriesEffect>());
        Assert.Equal(roots, scan.Roots!.Value);
    }

    [Fact]
    public void GivenAScan_WhenItFinishes_ThenTheFoundRepositoriesAreRows()
    {
        var state = AppReducer.Reduce(Empty(), Open()).State;
        var found = PickerFixtures.PathOf("code", "alpha");

        state = AppReducer.Reduce(state, new PickerScanFinished([found])).State;

        Assert.False(state.Shell.Picker.IsScanning);
        Assert.Equal([found], state.Shell.Picker.State!.Rows.Select(row => row.Path));
    }

    [Fact]
    public void GivenOpenPicker_WhenTypingAPath_ThenTheListingIsStartedForThatPath()
    {
        var state = AppReducer.Reduce(Empty(), Open()).State;

        var update = AppReducer.Reduce(state, new TypeTextCommand("~/co"));

        var list = Assert.Single(update.Effects.OfType<ListPickerDirectoriesEffect>());
        Assert.Equal("~/co", list.Query);
        Assert.Equal(PickerFixtures.Home, list.HomeDirectory);
        Assert.Equal(PickerFixtures.Home, list.BaseDirectory);
    }

    [Fact]
    public void GivenAListing_WhenTheQueryStopsBeingAPath_ThenTheListingIsCancelled()
    {
        var state = AppReducer.Reduce(Empty(), Open(initialQuery: "~/")).State;

        var update = AppReducer.Reduce(state, new ClearQueryCommand());

        Assert.Single(update.Effects.OfType<CancelPickerListingEffect>());
        Assert.False(update.State.Shell.Picker.State!.IsListing);
    }

    [Fact]
    public void GivenAListing_WhenClosingThePicker_ThenTheListingIsCancelledAndTheKeysGoBack()
    {
        var state = AppReducer.Reduce(Empty(), Open(initialQuery: "~/")).State;

        var update = AppReducer.Reduce(state, new PickerCloseRequested());

        Assert.False(update.State.Shell.Picker.IsOpen);
        Assert.Single(update.Effects.OfType<CancelPickerListingEffect>());
        Assert.Equal(KeyMode.Normal, Assert.Single(update.Effects.OfType<SetKeyModeEffect>()).Mode);
    }

    [Fact]
    public void GivenTabDuringListing_WhenTheListingCompletes_ThenThePathIsCompleted()
    {
        var state = AppReducer.Reduce(Empty(), Open(initialQuery: "~/al")).State;
        state = AppReducer.Reduce(state, new CompletePathCommand()).State;
        Assert.True(state.Shell.Picker.CompleteAfterListing);

        var update = AppReducer.Reduce(
            state,
            new ListingCompletedCommand("~/al", PickerFixtures.Listed("alpha")));

        Assert.Equal("~/alpha/", update.State.Shell.Picker.State!.Query);
        Assert.False(update.State.Shell.Picker.CompleteAfterListing);
        Assert.Single(update.Effects.OfType<ListPickerDirectoriesEffect>());
    }

    [Fact]
    public void GivenListingForAnOlderQuery_WhenItCompletes_ThenTheNewerListingStaysPending()
    {
        var state = AppReducer.Reduce(Empty(), Open(initialQuery: "~/a")).State;
        state = AppReducer.Reduce(state, new TypeTextCommand("l")).State;

        var update = AppReducer.Reduce(state, new ListingCompletedCommand("~/a", PickerFixtures.Listed("alpha")));

        Assert.True(update.State.Shell.Picker.State!.IsListing);
        Assert.Empty(update.State.Shell.Picker.State.Rows);
    }

    [Fact]
    public void GivenNoTabs_WhenCancelling_ThenTheProgramIsAskedToQuit()
    {
        var state = AppReducer.Reduce(Empty(), Open()).State;

        var update = AppReducer.Reduce(state, new PickerCancelPressed(false));

        Assert.True(update.State.Shell.Picker.IsOpen);
        Assert.False(Assert.Single(update.Effects.OfType<QuitEffect>()).CancelWork);
    }

    [Fact]
    public void GivenATab_WhenCancelling_ThenOnlyThePickerCloses()
    {
        var state = AppReducer.Reduce(Opened("alpha"), Open()).State;

        var update = AppReducer.Reduce(state, new PickerCancelPressed(false));

        Assert.False(update.State.Shell.Picker.IsOpen);
        Assert.Empty(update.Effects.OfType<QuitEffect>());
    }

    [Fact]
    public void GivenTypedPath_WhenAccepting_ThenTheDirectoryIsDiscoveredAndTheMessageSaysSo()
    {
        var state = AppReducer.Reduce(Empty(), Open()).State;
        state = AppReducer.Reduce(state, new TypeTextCommand("~/code/alpha")).State;

        var update = AppReducer.Reduce(state, new PickerAcceptPressed());

        var discover = Assert.Single(update.Effects.OfType<DiscoverRepositoryEffect>());
        Assert.Equal(PickerFixtures.PathOf("code", "alpha"), discover.Path);
        Assert.Null(discover.Replacing);
        Assert.Equal($"opening {discover.Path}...", update.State.Shell.Picker.State!.Message);
        Assert.Equal(discover.Request, update.State.Shell.Picker.OpenRequest);
    }

    [Fact]
    public void GivenARelativePath_WhenAccepting_ThenItIsTakenFromTheActiveRepository()
    {
        var state = AppReducer.Reduce(Opened("alpha"), Open()).State;
        state = AppReducer.Reduce(state, new TypeTextCommand("../beta")).State;

        var update = AppReducer.Reduce(state, new PickerAcceptPressed());

        var discover = Assert.Single(update.Effects.OfType<DiscoverRepositoryEffect>());
        Assert.Equal(PickerFixtures.PathOf("code", "beta"), discover.Path);
    }

    [Fact]
    public void GivenNoRowAndNoPath_WhenAccepting_ThenTheMessageShowsHowToTypeOne()
    {
        var state = AppReducer.Reduce(Empty(), Open()).State;

        var update = AppReducer.Reduce(state, new PickerAcceptPressed());

        Assert.StartsWith("nothing to open: type a path such as", update.State.Shell.Picker.State!.Message);
        Assert.Empty(update.Effects.OfType<DiscoverRepositoryEffect>());
    }

    [Fact]
    public void GivenRepositoryAlreadyOpen_WhenAcceptingItsRow_ThenItsTabIsActivatedWithoutDiscovery()
    {
        var state = Opened("alpha", "beta");
        var first = state.Workspace.Tabs[0];
        state = AppReducer.Reduce(state, Open()).State;
        state = AppReducer.Reduce(state, new TypeTextCommand("alpha")).State;

        var update = AppReducer.Reduce(state, new PickerAcceptPressed());

        Assert.Equal(first.Id, update.State.Workspace.ActiveTabId);
        Assert.False(update.State.Shell.Picker.IsOpen);
        Assert.Equal(2, update.State.Workspace.Tabs.Length);
        Assert.Empty(update.Effects.OfType<DiscoverRepositoryEffect>());
    }

    [Fact]
    public void GivenTheLocateMode_WhenAcceptingAnOpenRow_ThenItIsDiscoveredForTheMissingTab()
    {
        var state = Opened("alpha", "beta");
        var missing = state.Workspace.Tabs[0].Id;
        state = AppReducer.Reduce(state, new PickerOpenRequested(missing, string.Empty, Start)).State;
        state = AppReducer.Reduce(state, new TypeTextCommand("beta")).State;

        var update = AppReducer.Reduce(state, new PickerAcceptPressed());

        var discover = Assert.Single(update.Effects.OfType<DiscoverRepositoryEffect>());
        Assert.Equal(missing, discover.Replacing);
    }

    [Fact]
    public void GivenADiscoveredRepository_WhenTheResultArrives_ThenItOpensInATabAndThePickerCloses()
    {
        var state = Requested("~/code/alpha");
        var request = state.Shell.Picker.OpenRequest;

        var update = AppReducer.Reduce(
            state,
            new PickerRepositoryFound(request, PickerFixtures.Location("code", "alpha"), null));

        var tab = Assert.Single(update.State.Workspace.Tabs);
        Assert.Equal(PickerFixtures.PathOf("code", "alpha"), tab.Location.RootPath);
        Assert.False(update.State.Shell.Picker.IsOpen);
        Assert.Contains(tab.Location.RootPath, update.State.Workspace.Recents);
    }

    [Fact]
    public void GivenThePickerClosedMeanwhile_WhenTheResultArrives_ThenItIsStale()
    {
        var state = Requested("~/code/alpha");
        var request = state.Shell.Picker.OpenRequest;
        state = AppReducer.Reduce(state, new PickerCloseRequested()).State;

        var update = AppReducer.Reduce(
            state,
            new PickerRepositoryFound(request, PickerFixtures.Location("code", "alpha"), null));

        Assert.Empty(update.State.Workspace.Tabs);
    }

    [Fact]
    public void GivenADirectoryOutsideAnyRepository_WhenTheFailureArrives_ThenTheMessageShowsAndThePickerStays()
    {
        var state = Requested("~/code/none");

        var update = AppReducer.Reduce(
            state,
            new PickerDiscoveryFailed(state.Shell.Picker.OpenRequest, "no git repository found here"));

        Assert.True(update.State.Shell.Picker.IsOpen);
        Assert.Equal("no git repository found here", update.State.Shell.Picker.State!.Message);
    }

    [Fact]
    public void GivenANewerRequest_WhenAnOlderFailureArrives_ThenItIsIgnored()
    {
        var state = Requested("~/code/none");
        var older = state.Shell.Picker.OpenRequest;
        state = AppReducer.Reduce(state, new PickerAcceptPressed()).State;

        var update = AppReducer.Reduce(state, new PickerDiscoveryFailed(older, "old failure"));

        Assert.StartsWith("opening", update.State.Shell.Picker.State!.Message);
    }

    [Fact]
    public void GivenMissingTab_WhenItsRepositoryIsLocatedElsewhere_ThenTheDraftMoveIsAskedBeforeAnythingCloses()
    {
        var state = Opened("gone");
        var missing = state.Workspace.Tabs[0].Id;
        state = AppReducer.Reduce(state, new PickerOpenRequested(missing, string.Empty, Start)).State;
        var found = new PickerRepositoryFound(
            state.Shell.Picker.OpenRequest,
            PickerFixtures.Location("code", "new"),
            missing);

        var update = AppReducer.Reduce(state, found);

        var move = Assert.Single(update.Effects.OfType<MoveCommitDraftEffect>());
        Assert.Equal(missing, move.Missing);
        Assert.Equal(PickerFixtures.PathOf("code", "new"), update.State.Workspace.FindTab(move.Target)!.Location.RootPath);
        Assert.Equal(2, update.State.Workspace.Tabs.Length);
        Assert.True(update.State.Shell.Picker.IsOpen);
    }

    [Fact]
    public void GivenTheDraftMoved_WhenTheAnswerArrives_ThenTheMissingTabClosesAndThePickerToo()
    {
        var state = LocatedElsewhere(out var missing);

        var update = AppReducer.Reduce(state, new PickerDraftMoved(missing, true));

        Assert.Null(update.State.Workspace.FindTab(missing));
        Assert.Single(update.State.Workspace.Tabs);
        Assert.False(update.State.Shell.Picker.IsOpen);
    }

    [Fact]
    public void GivenBothTabsHoldAMessage_WhenTheAnswerArrives_ThenTheMissingTabStaysAndTheMessageSaysSo()
    {
        var state = LocatedElsewhere(out var missing);

        var update = AppReducer.Reduce(state, new PickerDraftMoved(missing, false));

        Assert.NotNull(update.State.Workspace.FindTab(missing));
        Assert.Equal(2, update.State.Workspace.Tabs.Length);
        Assert.True(update.State.Shell.Picker.IsOpen);
        Assert.Contains("Both tabs hold an unsent commit message", update.State.Shell.Picker.State!.Message);
    }

    [Fact]
    public void GivenMissingTabLocatedAtItsOwnPath_WhenTheRepositoryOpens_ThenTheTabStaysWithoutMovingItsDraft()
    {
        var state = Opened("alpha");
        var tab = state.Workspace.Tabs[0].Id;
        state = AppReducer.Reduce(state, new PickerOpenRequested(tab, string.Empty, Start)).State;
        var found = new PickerRepositoryFound(
            state.Shell.Picker.OpenRequest,
            PickerFixtures.Location("code", "alpha"),
            tab);

        var update = AppReducer.Reduce(state, found);

        Assert.Equal(tab, Assert.Single(update.State.Workspace.Tabs).Id);
        Assert.Empty(update.Effects.OfType<MoveCommitDraftEffect>());
        Assert.False(update.State.Shell.Picker.IsOpen);
    }

    [Fact]
    public void GivenHighlightedRepository_WhenPinning_ThenItIsPinnedAndUnpinnedAgain()
    {
        var path = PickerFixtures.PathOf("code", "alpha");
        var state = Empty();
        state = state with { Workspace = state.Workspace with { Recents = [path] } };
        state = AppReducer.Reduce(state, Open()).State;

        state = AppReducer.Reduce(state, new PickerPinPressed()).State;
        Assert.True(state.Workspace.IsPinned(path));
        Assert.True(state.Shell.Picker.State!.HighlightedRow!.IsPinned);

        state = AppReducer.Reduce(state, new PickerPinPressed()).State;
        Assert.False(state.Workspace.IsPinned(path));
    }

    [Fact]
    public void GivenDirectoryRow_WhenPinning_ThenNothingChanges()
    {
        var state = AppReducer.Reduce(Empty(), Open(initialQuery: "~/")).State;
        state = AppReducer.Reduce(state, new ListingCompletedCommand("~/", PickerFixtures.Listed("alpha"))).State;

        var update = AppReducer.Reduce(state, new PickerPinPressed());

        Assert.Empty(update.State.Workspace.Pins);
    }

    [Fact]
    public void GivenRecentRepository_WhenRemovingIt_ThenOnlyTheRecentEntryGoesAndTheMessageSaysNothingWasDeleted()
    {
        var path = PickerFixtures.PathOf("code", "alpha");
        var state = Empty();
        state = state with { Workspace = state.Workspace with { Recents = [path] } };
        state = AppReducer.Reduce(state, Open()).State;
        Assert.Equal(PickerTags.Recent, state.Shell.Picker.State!.HighlightedRow!.Tags & PickerTags.Recent);

        var update = AppReducer.Reduce(state, new PickerRemoveRecentPressed());

        Assert.Empty(update.State.Workspace.Recents);
        Assert.Contains("removed from recents", update.State.Shell.Picker.State!.Message, StringComparison.Ordinal);
        Assert.Contains("nothing was deleted on disk", update.State.Shell.Picker.State.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenARowThatIsNotRecent_WhenRemovingIt_ThenTheMessageSaysSo()
    {
        var state = AppReducer.Reduce(Empty(), Open()).State;
        state = AppReducer.Reduce(state, new PickerScanFinished([PickerFixtures.PathOf("code", "alpha")])).State;

        var update = AppReducer.Reduce(state, new PickerRemoveRecentPressed());

        Assert.Equal("not in recents", update.State.Shell.Picker.State!.Message);
    }

    [Fact]
    public void GivenAnAppCreatedRepository_WhenItIsReported_ThenItOpensInANewTabAndThePickerCloses()
    {
        var state = AppReducer.Reduce(Empty(), Open()).State;

        var update = AppReducer.Reduce(state, new PickerRepositoryCreated(PickerFixtures.Location("code", "made")));

        Assert.Equal(PickerFixtures.PathOf("code", "made"), Assert.Single(update.State.Workspace.Tabs).Location.RootPath);
        Assert.False(update.State.Shell.Picker.IsOpen);
    }

    [Fact]
    public void GivenAConfirmDialog_WhenThePickerOpens_ThenTheKeyModeIsLeftToTheDialog()
    {
        var state = Empty();
        state = state with { Shell = state.Shell with { Confirm = new ConfirmDialog(new ConfirmPrompt("T", ["Q"], "U", "L"), new NextTabCommand(), KeyMode.Normal) } };

        var update = AppReducer.Reduce(state, Open());

        Assert.Empty(update.Effects.OfType<SetKeyModeEffect>());
    }

    [Fact]
    public void GivenTheHighlightOnTheMoreLine_WhenAccepting_ThenTheSectionExpandsAndNothingIsOpened()
    {
        var state = OnRecentMore();

        var update = AppReducer.Reduce(state, new PickerAcceptPressed());

        Assert.Empty(update.Effects.OfType<DiscoverRepositoryEffect>());
        var picker = update.State.Shell.Picker;
        Assert.True(picker.IsOpen);
        Assert.Equal(PickerSection.Recent, picker.State!.Expanded);
        Assert.Equal(string.Empty, picker.State.Message);
    }

    [Fact]
    public void GivenAnExpandedSection_WhenAcceptingTheLessLine_ThenItCollapsesAgain()
    {
        var state = AppReducer.Reduce(OnRecentMore(), new PickerAcceptPressed()).State;
        state = AppReducer.Reduce(state, new SetHighlightCommand(11, true)).State;

        var update = AppReducer.Reduce(state, new PickerAcceptPressed());

        Assert.Equal(PickerSection.None, update.State.Shell.Picker.State!.Expanded);
        Assert.Empty(update.Effects.OfType<DiscoverRepositoryEffect>());
    }

    [Fact]
    public void GivenTheHighlightOnTheMoreLine_WhenPinningOrRemoving_ThenNothingHappens()
    {
        var state = OnRecentMore();

        var pinned = AppReducer.Reduce(state, new PickerPinPressed());
        var removed = AppReducer.Reduce(state, new PickerRemoveRecentPressed());

        Assert.Equal(state.Workspace, pinned.State.Workspace);
        Assert.Equal(state.Workspace, removed.State.Workspace);
        Assert.Equal(state.Shell.Picker.State, pinned.State.Shell.Picker.State);
        Assert.Equal(state.Shell.Picker.State, removed.State.Shell.Picker.State);
    }

    [Fact]
    public void GivenAnExpandedSection_WhenThePickerClosesAndOpensAgain_ThenTheSectionIsCollapsed()
    {
        var expanded = AppReducer.Reduce(OnRecentMore(), new PickerAcceptPressed()).State;

        var closed = AppReducer.Reduce(expanded, new PickerCloseRequested()).State;
        var reopened = AppReducer.Reduce(closed, Open(Start.AddMinutes(1))).State;

        Assert.Equal(PickerSection.None, reopened.Shell.Picker.State!.Expanded);
    }

    [Fact]
    public void GivenAClickOnTheMoreLine_WhenMovingDownFromIt_ThenTheHighlightGoesToTheNextSectionsFirstRow()
    {
        var state = AppReducer.Reduce(OnRecentMore(), new MoveHighlightCommand(1)).State;

        var picker = state.Shell.Picker.State!;

        Assert.False(picker.IsOnToggle);
        Assert.Equal(PickerTags.Discovered, picker.HighlightedRow!.Tags & PickerTags.Discovered);
    }

    // Twelve recents and four found repositories, the highlight on the "N more" line of the recents.
    private static AppState OnRecentMore()
    {
        var recents = Enumerable.Range(1, 12).Select(number => PickerFixtures.PathOf("code", $"recent{number:00}"));
        var found = Enumerable.Range(1, 4).Select(number => PickerFixtures.PathOf("code", $"found{number:00}"));
        var state = Empty();
        state = state with { Workspace = state.Workspace with { Recents = [.. recents] } };
        state = AppReducer.Reduce(state, Open()).State;
        state = AppReducer.Reduce(state, new PickerScanFinished([.. found])).State;

        return AppReducer.Reduce(state, new SetHighlightCommand(4, true)).State;
    }

    private static PickerOpenRequested Open(DateTimeOffset? now = null, string initialQuery = "") =>
        new(null, initialQuery, now ?? Start);

    private static AppState Empty() => AppState.Empty with
    {
        Shell = ShellState.Empty with { Picker = PickerSession.Empty with { HomeDirectory = PickerFixtures.Home } },
    };

    private static AppState Opened(params string[] names)
    {
        var state = Empty();
        foreach (var name in names)
        {
            state = AppReducer.Reduce(state, new OpenRepositoryCommand(PickerFixtures.Location("code", name))).State;
        }

        return state;
    }

    private static AppState Requested(string path)
    {
        var state = AppReducer.Reduce(Empty(), Open()).State;
        state = AppReducer.Reduce(state, new TypeTextCommand(path)).State;

        return AppReducer.Reduce(state, new PickerAcceptPressed()).State;
    }

    private static AppState LocatedElsewhere(out TabId missing)
    {
        var state = Opened("gone");
        missing = state.Workspace.Tabs[0].Id;
        state = AppReducer.Reduce(state, new PickerOpenRequested(missing, string.Empty, Start)).State;
        var found = new PickerRepositoryFound(
            state.Shell.Picker.OpenRequest,
            PickerFixtures.Location("code", "new"),
            missing);

        return AppReducer.Reduce(state, found).State;
    }
}
