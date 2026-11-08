using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Sync;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.Sync;

public class SyncReducerTests
{
    private const string Tip = "1111111111111111111111111111111111111111";
    private const string Mine = "2222222222222222222222222222222222222222";
    private static readonly TabId Tab = new(1);
    private static readonly SyncTarget Nothing = new(null, null, null);

    [Theory]
    [InlineData(SyncKind.Fetch, false)]
    [InlineData(SyncKind.FetchAll, true)]
    public void GivenAFetchKey_WhenRequested_ThenItStartsAndTheTabShowsTheProgressLine(SyncKind kind, bool all)
    {
        var update = Reduce(new SyncRequested(Tab, kind));

        var fetch = Assert.IsType<FetchEffect>(Assert.Single(update.Effects));
        Assert.Equal(all, fetch.AllRemotes);
        Assert.Equal("Fetching...", update.State!.ProgressLine);
        Assert.Equal("Fetch", update.State.Noun);
    }

    [Fact]
    public void GivenThePullKey_WhenRequested_ThenAPullStarts()
    {
        var update = Reduce(new SyncRequested(Tab, SyncKind.Pull));

        Assert.IsType<PullEffect>(Assert.Single(update.Effects));
        Assert.Equal("Pulling...", update.State!.ProgressLine);
    }

    [Fact]
    public void GivenAnotherOperationOfTheTab_WhenPulling_ThenItIsRefusedWithoutGit()
    {
        var update = Reduce(new SyncRequested(Tab, SyncKind.Pull), otherBusy: true);

        Assert.Equal(SyncReducer.OtherBusyNotice, Warning(update.Effects));
        Assert.Null(update.State);
    }

    [Theory]
    [InlineData(SyncKind.Fetch)]
    [InlineData(SyncKind.FetchAll)]
    [InlineData(SyncKind.Pull)]
    [InlineData(SyncKind.Push)]
    [InlineData(SyncKind.PushForce)]
    public void GivenARunningTransfer_WhenAnotherIsRequested_ThenItIsRefusedAsBusyAndKeepsTheRunningOne(SyncKind kind)
    {
        var running = new SyncState("Fetching", "Fetch", null, false);

        var update = Reduce(new SyncRequested(Tab, kind), busy: true, state: running);

        Assert.Equal(SyncReducer.BusyNotice, Warning(update.Effects));
        Assert.Equal(running, update.State);
    }

    [Fact]
    public void GivenABranchWithAnUpstream_WhenPushing_ThenThePlainPushStartsForThatBranch()
    {
        var update = Reduce(new SyncRequested(Tab, SyncKind.Push), Refs(Local("topic", "refs/remotes/origin/topic")));

        var push = Assert.IsType<PushEffect>(Assert.Single(update.Effects));
        Assert.Equal(new PushRequest(null), push.Request);
        Assert.Equal("Pushed the current branch", push.Done);
        Assert.Equal("topic", push.Branch);
        Assert.Equal("Pushing...", update.State!.ProgressLine);
    }

    [Fact]
    public void GivenABranchWhoseUpstreamIsGone_WhenPushing_ThenTheRemotesAreReadForAFirstPush()
    {
        var gone = Local("topic", "refs/remotes/origin/topic") with { IsUpstreamGone = true };

        var update = Reduce(new SyncRequested(Tab, SyncKind.Push), Refs(gone));

        var list = Assert.IsType<ListSyncRemotesEffect>(Assert.Single(update.Effects));
        Assert.Equal(SyncRemotesPurpose.FirstPush, list.Purpose);
    }

    [Fact]
    public void GivenABranchWithoutUpstream_WhenPushing_ThenTheRemotesAreReadFirstAndNothingIsRunningYet()
    {
        var update = Reduce(new SyncRequested(Tab, SyncKind.Push), Refs(Local("lonely", null)));

        var list = Assert.IsType<ListSyncRemotesEffect>(Assert.Single(update.Effects));
        Assert.Equal(SyncRemotesPurpose.FirstPush, list.Purpose);
        Assert.Equal("lonely", list.Branch);
        Assert.Null(update.State);
    }

    [Fact]
    public void GivenNoCheckedOutBranch_WhenPushing_ThenGitGetsThePlainPushToExplain()
    {
        var update = Reduce(new SyncRequested(Tab, SyncKind.Push));

        var push = Assert.IsType<PushEffect>(Assert.Single(update.Effects));
        Assert.Equal(string.Empty, push.Branch);
    }

    [Theory]
    [InlineData(null, "No branch is checked out: nothing to force push")]
    [InlineData("lonely", "lonely has no upstream: push it first")]
    public void GivenNothingToForce_WhenForcePushing_ThenItIsRefusedWithoutAConfirm(string? branch, string notice)
    {
        var refs = branch is null ? Refs() : Refs(Local(branch, null));

        var update = Reduce(new SyncRequested(Tab, SyncKind.PushForce), refs);

        Assert.Equal(notice, Warning(update.Effects));
    }

    [Fact]
    public void GivenABranchWithAnUpstream_WhenForcePushing_ThenTheRemotesAreReadForTheConfirm()
    {
        var update = Reduce(
            new SyncRequested(Tab, SyncKind.PushForce),
            Refs(Local("topic", "refs/remotes/origin/topic")));

        var list = Assert.IsType<ListSyncRemotesEffect>(Assert.Single(update.Effects));
        Assert.Equal(SyncRemotesPurpose.Force, list.Purpose);
        Assert.Equal("topic", list.Branch);
    }

    [Fact]
    public void GivenARunningTransfer_WhenCancelling_ThenGitIsStoppedAndTheLineSaysSo()
    {
        var running = new SyncState("Fetching", "Fetch", new RemoteProgress("Receiving objects", 45), false);

        var update = Reduce(new SyncCancelRequested(Tab), state: running, busy: true);

        Assert.IsType<CancelSyncEffect>(Assert.Single(update.Effects));
        Assert.Equal("Fetching: Receiving objects 45% (cancelling)", update.State!.ProgressLine);
    }

    [Fact]
    public void GivenNothingRunning_WhenCancelling_ThenItSaysSo()
    {
        var update = Reduce(new SyncCancelRequested(Tab));

        Assert.Equal(SyncReducer.NothingRunningNotice, Warning(update.Effects));
        Assert.Null(update.State);
    }

    [Fact]
    public void GivenProgress_WhenReported_ThenTheLineShowsGitsLatestReportAndNoMessageIsPosted()
    {
        var running = new SyncState("Fetching", "Fetch", null, false);

        var update = Reduce(new SyncProgressed(Tab, new RemoteProgress("Receiving objects", 45)), state: running);

        Assert.Equal("Fetching: Receiving objects 45%", update.State!.ProgressLine);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenNothingRunning_WhenLateProgressArrives_ThenItIsIgnored()
    {
        var update = Reduce(new SyncProgressed(Tab, new RemoteProgress("Receiving objects", 45)));

        Assert.Null(update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenASuccess_WhenItArrives_ThenTheTextIsPostedTheTabRefreshedAndTheLineGone()
    {
        var running = new SyncState("Fetching", "Fetch", null, false);

        var update = Reduce(new SyncSucceeded(Tab, "Fetched all remotes"), state: running, busy: true);

        var post = Assert.IsType<PostStatusEffect>(update.Effects[0]);
        Assert.Equal(("Fetched all remotes", StatusSeverity.Info), (post.Text, post.Severity));
        Assert.Equal(new RefreshTabEffect(Tab), update.Effects[1]);
        Assert.Null(update.State);
    }

    [Fact]
    public void GivenAFailure_WhenItArrives_ThenTheErrorIsPostedAndTheTabStillRefreshes()
    {
        var update = Reduce(new SyncFailed(Tab, "Pull failed: x", null, null), busy: true);

        var post = Assert.IsType<PostStatusEffect>(update.Effects[0]);
        Assert.Equal(StatusSeverity.Error, post.Severity);
        Assert.Equal(new RefreshTabEffect(Tab), update.Effects[1]);
        Assert.Equal(2, update.Effects.Length);
    }

    [Fact]
    public void GivenAFailureForLackOfCredentials_WhenItArrives_ThenTheTerminalRetryIsOffered()
    {
        var retry = new SyncRetry("Fetch", ["fetch", "--prune"]);

        var update = Reduce(new SyncFailed(Tab, "Fetch failed: x", retry, new SyncRejection("topic", null)));

        var offer = Assert.IsType<OfferTerminalRetryEffect>(update.Effects[2]);
        Assert.Equal(("Fetch", Tab), (offer.Noun, offer.TabId));
        Assert.Equal(["fetch", "--prune"], offer.Arguments);
        Assert.Equal(3, update.Effects.Length);
    }

    [Fact]
    public void GivenAFailureThatIsNotAboutCredentials_WhenItArrives_ThenNoTerminalIsOffered()
    {
        var update = Reduce(new SyncFailed(Tab, "Fetch failed: x", null, null));

        Assert.DoesNotContain(update.Effects, effect => effect is OfferTerminalRetryEffect);
    }

    [Fact]
    public void GivenACancelByTheUser_WhenItEnds_ThenItIsWarnedAboutAndTheTabRefreshes()
    {
        var running = new SyncState("Fetching", "Fetch", null, true);

        var update = Reduce(new SyncCancelled(Tab), state: running, busy: true);

        Assert.Equal("Fetch cancelled", Warning(update.Effects));
        Assert.Contains(new RefreshTabEffect(Tab), update.Effects);
        Assert.DoesNotContain(update.Effects, effect => effect is OfferTerminalRetryEffect);
        Assert.Null(update.State);
    }

    [Fact]
    public void GivenACancelNobodyAskedFor_WhenItEnds_ThenNothingIsSaid()
    {
        var running = new SyncState("Fetching", "Fetch", null, false);

        var update = Reduce(new SyncCancelled(Tab), state: running, busy: true);

        Assert.Equal(new RefreshTabEffect(Tab), Assert.Single(update.Effects));
    }

    [Fact]
    public void GivenNoRemote_WhenTheRemotesOfAFirstPushAreRead_ThenItSaysToAddOne()
    {
        var update = Reduce(FirstPushListed());

        Assert.Equal("There is no remote: add one first", Warning(update.Effects));
    }

    [Fact]
    public void GivenAnotherTabIsActive_WhenTheRemotesAreRead_ThenNothingOpens()
    {
        var update = Reduce(FirstPushListed("origin", "second"), Refs(Local("lonely", null)), active: false);

        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenOneRemote_WhenTheRemotesOfAFirstPushAreRead_ThenItIsPushedWithoutAskingAndSetsTheUpstream()
    {
        var update = Reduce(FirstPushListed("origin"), Refs(Local("lonely", null)), busy: true);

        var push = Assert.IsType<PushEffect>(Assert.Single(update.Effects));
        Assert.Equal(new PushRequest("origin", "lonely", true), push.Request);
        Assert.Equal("Pushed lonely to origin and set it as its upstream", push.Done);
        Assert.Equal("Pushing...", update.State!.ProgressLine);
    }

    [Fact]
    public void GivenTwoRemotes_WhenTheRemotesOfAFirstPushAreRead_ThenThePickerListsOriginFirst()
    {
        var update = Reduce(FirstPushListed("second", "origin"), Refs(Local("lonely", null)), busy: true);

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(update.Effects)).Request;
        Assert.Equal("Push and set upstream", request.Title);
        Assert.Equal("Remote to push lonely to", request.Context);
        Assert.Equal(["origin", "second"], request.Choices!.Value.Select(choice => choice.Value));
        Assert.Equal(Tab, request.Owner);
        Assert.Equal(new SyncRemoteAnswer(Tab, "lonely"), request.OnAccepted);
        Assert.Null(update.State);
    }

    [Fact]
    public void GivenARemoteChosen_WhenAnswered_ThenTheFirstPushStartsForTheBranchThePromptWasOpenedFor()
    {
        var update = Reduce(new SyncRemoteChosen(Tab, "lonely", "second"), Refs(Local("lonely", null)));

        var push = Assert.IsType<PushEffect>(Assert.Single(update.Effects));
        Assert.Equal(new PushRequest("second", "lonely", true), push.Request);
    }

    [Fact]
    public void GivenAnotherBranchIsCheckedOut_WhenTheRemoteIsChosen_ThenNothingIsPushed()
    {
        var update = Reduce(new SyncRemoteChosen(Tab, "lonely", "second"), Refs(Local("other", null)));

        Assert.Equal("The checked-out branch changed: nothing was pushed", Warning(update.Effects));
        Assert.Null(update.State);
    }

    [Fact]
    public void GivenATransferStartedWhilePromptWasOpen_WhenTheRemoteIsChosen_ThenNothingIsPushed()
    {
        var update = Reduce(new SyncRemoteChosen(Tab, "lonely", "second"), Refs(Local("lonely", null)), busy: true);

        Assert.StartsWith(SyncReducer.BusyNotice, Warning(update.Effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheRemotesOfAForcePush_WhenRead_ThenTheConfirmNamesTheRemoteBranchTheLeaseAndTheUndo()
    {
        var refs = Refs(Local("topic", "refs/remotes/origin/topic"), Remote("origin/topic"));

        var update = Reduce(ForceListed("origin"), refs, busy: true);

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update.Effects));
        Assert.Equal("Force push", confirm.Prompt.Title);
        Assert.Contains("origin/topic", confirm.Prompt.Lines[0], StringComparison.Ordinal);
        Assert.Contains(Tip[..7], confirm.Prompt.Lines[0], StringComparison.Ordinal);
        Assert.Contains("lease", confirm.Prompt.Lines[1], StringComparison.Ordinal);
        Assert.Equal(UndoNotes.FromHistory, confirm.Prompt.Consequence);
        Assert.Equal($"Undo: git push --force-with-lease origin {Tip}:topic", Assert.IsType<SyncForceConfirmed>(confirm.OnAccepted).DoneDetail);
        Assert.Equal("Force push", confirm.Prompt.YesLabel);
        var accepted = Assert.IsType<SyncForceConfirmed>(confirm.OnAccepted);
        Assert.Equal(new PushRequest("origin", "topic", false, new PushLease("topic", Tip)), accepted.Request);
        Assert.StartsWith("Force pushed topic to origin/topic (was 1111111)", accepted.Done, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnUpstreamOfNoKnownRemote_WhenTheForceIsAsked_ThenTheStatusSaysToFetchFirst()
    {
        var refs = Refs(Local("topic", "refs/remotes/origin/topic"), Remote("origin/topic"));

        var update = Reduce(ForceListed("elsewhere"), refs, busy: true);

        Assert.Equal("The upstream of topic is not a known remote branch: fetch first", Warning(update.Effects));
    }

    [Fact]
    public void GivenTheForceConfirmed_WhenAnswered_ThenTheLeasePushStarts()
    {
        var request = new PushRequest("origin", "topic", false, new PushLease("topic", Tip));

        var update = Reduce(
            new SyncForceConfirmed(Tab, request, "Force pushed"),
            Refs(Local("topic", "refs/remotes/origin/topic")));

        var push = Assert.IsType<PushEffect>(Assert.Single(update.Effects));
        Assert.Equal(request, push.Request);
        Assert.Equal("Force pushed", push.Done);
        Assert.NotNull(update.State);
    }

    [Fact]
    public void GivenATransferRunsMeanwhile_WhenTheForceIsConfirmed_ThenNothingIsPushed()
    {
        var request = new PushRequest("origin", "topic", false, new PushLease("topic", Tip));
        var running = new SyncState("Fetching", "Fetch", null, false);

        var update = Reduce(new SyncForceConfirmed(Tab, request, "Force pushed"), busy: true, state: running);

        Assert.EndsWith("nothing was pushed", Warning(update.Effects), StringComparison.Ordinal);
        Assert.Equal(running, update.State);
    }

    [Fact]
    public void GivenARewrittenBranchRejected_WhenThePlainPushFails_ThenTheForceConfirmIsOfferedByReadingTheRemotes()
    {
        var refs = Refs(Local("topic", "refs/remotes/origin/topic", ahead: 1));

        var update = Reduce(Rejected(new SyncRejection("topic", null)), refs, busy: true);

        var list = Assert.IsType<ListSyncRemotesEffect>(update.Effects[2]);
        Assert.Equal((SyncRemotesPurpose.Force, "topic"), (list.Purpose, list.Branch));
    }

    [Theory]
    [InlineData("topic", 0, true, false)] // The branch is only behind, so a force would lose commits.
    [InlineData("other", 1, true, false)] // Another branch is checked out now.
    [InlineData("topic", 1, false, false)] // The tab is not the active one.
    [InlineData("topic", 1, true, true)] // An overlay is open and must not be covered.
    public void GivenARejectedPlainPush_WhenAForceCannotBeOffered_ThenOnlyTheErrorAndTheRefreshFollow(
        string checkedOut,
        int ahead,
        bool active,
        bool overlay)
    {
        var refs = Refs(Local(checkedOut, $"refs/remotes/origin/{checkedOut}", ahead: ahead));

        var update = Reduce(
            Rejected(new SyncRejection("topic", null)),
            refs,
            active: active,
            busy: true,
            overlay: overlay);

        Assert.Equal(2, update.Effects.Length);
    }

    [Fact]
    public void GivenAFirstPushRejectedAndTheRemoteBranchKnown_WhenItFails_ThenTheTipsAreCompared()
    {
        var refs = Refs(Local("lonely", null, hash: Mine), Remote("origin/lonely"));

        var update = Reduce(Rejected(new SyncRejection("lonely", "origin")), refs, busy: true);

        var check = Assert.IsType<CheckBehindEffect>(update.Effects[2]);
        Assert.Equal(("lonely", Mine, "origin", Tip), (check.Branch, check.CommitHash, check.Remote, check.TrackingHash));
    }

    [Fact]
    public void GivenAFirstPushRejectedAndTheRemoteBranchUnknown_WhenItFails_ThenTheStatusSaysToFetchFirst()
    {
        var refs = Refs(Local("lonely", null));

        var update = Reduce(Rejected(new SyncRejection("lonely", "origin")), refs, busy: true);

        var post = Assert.IsType<PostStatusEffect>(update.Effects[2]);
        Assert.Contains("origin/lonely is not known here: fetch first", post.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheLocalBranchIsNotBehind_WhenCompared_ThenTheForceConfirmAlsoSetsTheUpstream()
    {
        var refs = Refs(Local("lonely", null, hash: Mine));

        var update = Reduce(new SyncBehindChecked(Tab, "lonely", Mine, "origin", Tip, false), refs, busy: true);

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update.Effects));
        var accepted = Assert.IsType<SyncForceConfirmed>(confirm.OnAccepted);
        Assert.Equal(new PushRequest("origin", "lonely", true, new PushLease("lonely", Tip)), accepted.Request);
        Assert.Equal($"Undo: git push --force-with-lease origin {Tip}:lonely", accepted.DoneDetail);
    }

    [Fact]
    public void GivenTheLocalBranchIsBehind_WhenCompared_ThenNoForceIsOfferedAndThePullIsSuggested()
    {
        var refs = Refs(Local("lonely", null, hash: Mine));

        var update = Reduce(new SyncBehindChecked(Tab, "lonely", Mine, "origin", Tip, true), refs, busy: true);

        Assert.Contains("pull or rebase it, no force push offered", Warning(update.Effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheBranchMovedMeanwhile_WhenCompared_ThenNothingIsOffered()
    {
        var refs = Refs(Local("lonely", null, hash: Tip));

        var update = Reduce(new SyncBehindChecked(Tab, "lonely", Mine, "origin", Tip, false), refs, busy: true);

        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenARemoteBranchRow_WhenDeleting_ThenTheRemotesAreReadAndTheRowIsRemembered()
    {
        var refs = Refs(Remote("origin/topic"));
        var target = new SyncTarget(SidebarRowKind.RemoteBranch, "origin/topic", Tip);

        var update = Reduce(new SyncDeleteRequested(Tab, target), refs);

        var list = Assert.IsType<ListSyncRemotesEffect>(Assert.Single(update.Effects));
        Assert.Equal((SyncRemotesPurpose.Delete, "origin/topic"), (list.Purpose, list.DirectName));
    }

    [Fact]
    public void GivenSeveralRemoteBranchesOnTheCommit_WhenDeleting_ThenNoBranchIsRemembered()
    {
        var refs = Refs(Remote("origin/a"), Remote("origin/b"));

        var update = Reduce(new SyncDeleteRequested(Tab, new SyncTarget(null, null, Tip)), refs);

        Assert.Null(Assert.IsType<ListSyncRemotesEffect>(Assert.Single(update.Effects)).DirectName);
    }

    [Fact]
    public void GivenARunningTransfer_WhenDeleting_ThenItIsRefusedAsBusy()
    {
        var update = Reduce(new SyncDeleteRequested(Tab, Nothing), busy: true);

        Assert.Equal(SyncReducer.BusyNotice, Warning(update.Effects));
    }

    [Fact]
    public void GivenTheCursorNamedOneBranch_WhenTheRemotesAreRead_ThenOnlyTheConfirmComesFirst()
    {
        var refs = Refs(Remote("origin/topic"));

        var update = Reduce(DeleteListed("origin/topic", "origin"), refs, busy: true);

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update.Effects));
        Assert.Equal("Delete branch on remote", confirm.Prompt.Title);
        Assert.Contains("topic", confirm.Prompt.Lines[0], StringComparison.Ordinal);
        Assert.Contains("origin", confirm.Prompt.Lines[0], StringComparison.Ordinal);
        Assert.Equal(UndoNotes.FromHistory, confirm.Prompt.Consequence);
        Assert.Equal(new SyncDeleteConfirmed(Tab, "origin", "topic", Tip), confirm.OnAccepted);
    }

    [Fact]
    public void GivenTheCursorNamedANameOfNoBranch_WhenTheRemotesAreRead_ThenNothingIsDeleted()
    {
        var update = Reduce(DeleteListed("stranger/topic", "origin"), Refs(Remote("origin/topic")), busy: true);

        Assert.Equal("stranger/topic is not a branch on a remote: nothing to delete", Warning(update.Effects));
    }

    [Fact]
    public void GivenNoRemoteBranchKnown_WhenTheRemotesAreRead_ThenTheStatusSaysToFetchFirst()
    {
        var update = Reduce(DeleteListed(null, "origin"), Refs(), busy: true);

        Assert.Equal("No remote branches known: fetch first", Warning(update.Effects));
    }

    [Fact]
    public void GivenOneRemoteWithBranches_WhenTheRemotesAreRead_ThenTheBranchPickerOpensWithoutAskingForTheRemote()
    {
        var update = Reduce(DeleteListed(null, "origin", "empty"), Refs(Remote("origin/a"), Remote("origin/b")), busy: true);

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(update.Effects)).Request;
        Assert.Equal("Delete remote branch", request.Title);
        Assert.Equal("Branch to delete on origin", request.Context);
        Assert.Equal(["a", "b"], request.Choices!.Value.Select(choice => choice.Value));
    }

    [Fact]
    public void GivenTwoRemotesWithBranches_WhenTheRemotesAreRead_ThenTheRemotePickerComesFirst()
    {
        var refs = Refs(Remote("origin/a"), Remote("second/b"));

        var update = Reduce(DeleteListed(null, "origin", "second"), refs, busy: true);

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(update.Effects)).Request;
        Assert.Equal("Remote to delete a branch from", request.Context);
        Assert.Equal(["origin", "second"], request.Choices!.Value.Select(choice => choice.Value));
        var answer = Assert.IsType<SyncDeleteRemoteAnswer>(request.OnAccepted);
        Assert.Equal(2, answer.Branches.Length);
    }

    [Fact]
    public void GivenARemotePicked_WhenAnswered_ThenTheBranchesOfThatRemoteAreOffered()
    {
        var branches = Branches(("origin", "a"), ("second", "b"));

        var update = Reduce(new SyncDeleteRemoteChosen(Tab, branches, "second"));

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(update.Effects)).Request;
        Assert.Equal(["b"], request.Choices!.Value.Select(choice => choice.Value));
        Assert.Equal(new SyncDeleteBranchAnswer(Tab, branches, "second"), request.OnAccepted);
    }

    [Fact]
    public void GivenABranchPicked_WhenAnswered_ThenTheConfirmNamesItsTipAndHowToPutItBack()
    {
        var update = Reduce(new SyncDeleteBranchChosen(Tab, Branches(("origin", "a")), "origin", "a"));

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update.Effects));
        Assert.Contains("Local branches are not touched.", confirm.Prompt.Lines);
        Assert.Equal(UndoNotes.FromHistory, confirm.Prompt.Consequence);
        Assert.Equal(SyncReducer.TooSmallNotice, confirm.UnavailableNotice);
    }

    [Fact]
    public void GivenTheDeleteConfirmed_WhenAnswered_ThenTheDeleteStartsGuardedByTheTipTheUserSaw()
    {
        var update = Reduce(new SyncDeleteConfirmed(Tab, "origin", "a", Tip));

        var delete = Assert.IsType<DeleteRemoteBranchEffect>(Assert.Single(update.Effects));
        Assert.Equal(("origin", "a", Tip), (delete.Remote, delete.Branch, delete.CommitHash));
    }

    [Fact]
    public void GivenATransferRunsMeanwhile_WhenTheDeleteIsConfirmed_ThenNothingIsDeleted()
    {
        var update = Reduce(new SyncDeleteConfirmed(Tab, "origin", "a", Tip), busy: true);

        Assert.EndsWith("nothing was deleted", Warning(update.Effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheListFailed_WhenItArrives_ThenTheErrorIsPostedWithoutARefresh()
    {
        var update = Reduce(new SyncListFailed(Tab, "Could not list the remotes: x"), busy: true);

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal(StatusSeverity.Error, post.Severity);
    }

    private static SyncUpdate Reduce(
        SyncCommand command,
        ImmutableArray<GitRef>? refs = null,
        bool active = true,
        bool busy = false,
        bool otherBusy = false,
        bool overlay = false,
        SyncState? state = null) =>
        SyncReducer.Reduce(
            command,
            new SyncContext(Tab, Location(), refs ?? [], active, busy, otherBusy, overlay, state));

    private static SyncFailed Rejected(SyncRejection rejection) =>
        new(Tab, "Push failed: ! [rejected]", null, rejection);

    private static SyncRemotesListed FirstPushListed(params string[] remotes) =>
        new(Tab, SyncRemotesPurpose.FirstPush, "lonely", null, [.. remotes]);

    private static SyncRemotesListed ForceListed(params string[] remotes) =>
        new(Tab, SyncRemotesPurpose.Force, "topic", null, [.. remotes]);

    private static SyncRemotesListed DeleteListed(string? direct, params string[] remotes) =>
        new(Tab, SyncRemotesPurpose.Delete, null, direct, [.. remotes]);

    private static ImmutableArray<RemoteBranchRef> Branches(params (string Remote, string Branch)[] names) =>
        [.. names.Select(name => new RemoteBranchRef(new RemoteBranchName(name.Remote, name.Branch), Tip))];

    private static string Warning(ImmutableArray<AppEffect> effects)
    {
        var post = Assert.IsType<PostStatusEffect>(Assert.Single(effects.OfType<PostStatusEffect>()));
        Assert.Equal(StatusSeverity.Warning, post.Severity);
        return post.Text;
    }

    private static ImmutableArray<GitRef> Refs(params GitRef[] refs) => [.. refs];

    private static GitRef Local(string name, string? upstream, int ahead = 1, string hash = Mine) => new(
        $"refs/heads/{name}",
        name,
        RefKind.LocalBranch,
        hash,
        hash,
        upstream,
        upstream is null ? null : ahead,
        upstream is null ? null : 0,
        false,
        true,
        null,
        null);

    private static GitRef Remote(string name) => new(
        $"refs/remotes/{name}",
        name,
        RefKind.RemoteBranch,
        Tip,
        Tip,
        null,
        null,
        null,
        false,
        false,
        null,
        null);

    private static RepositoryLocation Location()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-sync-tests");
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }
}
