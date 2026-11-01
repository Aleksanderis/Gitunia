using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Sync;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Remote;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Remote;

// The handler only turns keys into commands; the decisions are in the Core SyncReducer tests and the git work in the
// App SyncEffectHandler tests.
public class SyncActionsTests
{
    private const string Commit = "1111111111111111111111111111111111111111";

    private readonly Dispatcher _host;
    private readonly FixedSelection _selection = new(new BranchSelection(null, null, Commit));
    private readonly List<AppEffect> _effects = [];
    private readonly SyncActions _actions;

    public SyncActionsTests()
    {
        _host = new TestShell().Host;
        _host.Dispatch(new OpenRepositoryCommand(Location()));
        _host.EffectRaised += _effects.Add;
        _actions = new SyncActions(_host, _selection);
    }

    [Fact]
    public void GivenTheFetchKey_WhenExecuted_ThenTheCurrentBranchRemoteIsFetched()
    {
        Assert.True(_actions.TryExecute(ActionIds.RemoteFetch, null));

        Assert.False(Assert.IsType<FetchEffect>(Assert.Single(_effects)).AllRemotes);
    }

    [Fact]
    public void GivenTheFetchAllKey_WhenExecuted_ThenEveryRemoteIsFetched()
    {
        Assert.True(_actions.TryExecute(ActionIds.RemoteFetchAll, null));

        Assert.True(Assert.IsType<FetchEffect>(Assert.Single(_effects)).AllRemotes);
    }

    [Fact]
    public void GivenThePullKey_WhenExecuted_ThenAPullStarts()
    {
        Assert.True(_actions.TryExecute(ActionIds.RemotePull, null));

        Assert.IsType<PullEffect>(Assert.Single(_effects));
    }

    [Fact]
    public void GivenThePushKeyAndNoBranch_WhenExecuted_ThenThePlainPushStarts()
    {
        Assert.True(_actions.TryExecute(ActionIds.RemotePush, null));

        Assert.IsType<PushEffect>(Assert.Single(_effects));
    }

    [Fact]
    public void GivenTheForcePushKeyAndNoBranch_WhenExecuted_ThenTheReducerRefusesIt()
    {
        Assert.True(_actions.TryExecute(ActionIds.RemotePushForce, null));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(_effects));
        Assert.Equal("No branch is checked out: nothing to force push", post.Text);
    }

    [Fact]
    public void GivenNothingRuns_WhenEscIsPressed_ThenItIsLeftToOthers()
    {
        Assert.False(_actions.TryExecute(ActionIds.Cancel, null));

        Assert.Empty(_effects);
    }

    [Fact]
    public void GivenAFetchRuns_WhenEscIsPressed_ThenItIsCancelled()
    {
        _actions.TryExecute(ActionIds.RemoteFetch, null);
        _effects.Clear();

        Assert.True(_actions.TryExecute(ActionIds.Cancel, null));

        Assert.IsType<CancelSyncEffect>(Assert.Single(_effects));
        Assert.True(_host.Workspace.ActiveTab!.Sync!.IsCancelling);
    }

    [Fact]
    public void GivenNothingRuns_WhenTheCancelActionRuns_ThenItSaysSo()
    {
        Assert.True(_actions.TryExecute(ActionIds.RemoteCancel, null));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(_effects));
        Assert.Equal("No fetch, pull or push is running", post.Text);
    }

    [Fact]
    public void GivenAFetchRuns_WhenPushing_ThenItIsRefusedAsBusy()
    {
        _actions.TryExecute(ActionIds.RemoteFetch, null);
        _effects.Clear();

        _actions.TryExecute(ActionIds.RemotePush, null);

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(_effects));
        Assert.StartsWith("Busy:", post.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GivenTheCursorOnARemoteBranchRow_WhenDeleting_ThenTheRemotesAreReadWithThatBranch(bool viaBranchDelete)
    {
        LoadRefs(RemoteBranch("origin/topic"));
        _selection.Value = new BranchSelection(SidebarRowKind.RemoteBranch, "origin/topic", Commit);

        Assert.True(_actions.TryExecute(viaBranchDelete ? ActionIds.BranchDelete : ActionIds.RemoteDeleteBranch, null));

        var list = Assert.IsType<ListSyncRemotesEffect>(Assert.Single(_effects));
        Assert.Equal(SyncRemotesPurpose.Delete, list.Purpose);
        Assert.Equal("origin/topic", list.DirectName);
    }

    [Fact]
    public void GivenAGraphCommitWithALocalBranch_WhenDeletingWithTheBranchKey_ThenItIsLeftToTheBranchActions()
    {
        LoadRefs(RemoteBranch("origin/topic"), LocalBranch("topic"));

        Assert.False(_actions.TryExecute(ActionIds.BranchDelete, null));

        Assert.Empty(_effects);
    }

    [Fact]
    public void GivenAnUnrelatedAction_WhenExecuted_ThenItIsNotClaimed()
    {
        Assert.False(_actions.TryExecute(ActionIds.RepositoryRefresh, null));
        Assert.Empty(_effects);
    }

    [Fact]
    public void GivenNoTab_WhenExecuted_ThenNothingIsHandled()
    {
        var actions = new SyncActions(new TestShell().Host, _selection);

        Assert.False(actions.TryExecute(ActionIds.RemoteFetch, null));
    }

    private static RepositoryLocation Location()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-sync-view-tests");
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    private static GitRef RemoteBranch(string name) => Ref($"refs/remotes/{name}", name, RefKind.RemoteBranch);

    private static GitRef LocalBranch(string name) => Ref($"refs/heads/{name}", name, RefKind.LocalBranch);

    private static GitRef Ref(string fullName, string name, RefKind kind) =>
        new(fullName, name, kind, Commit, Commit, null, null, null, false, false, null, null);

    private void LoadRefs(params GitRef[] refs)
    {
        var tabId = _host.Workspace.ActiveTabId!.Value;
        _host.Dispatch(new TabCommand(tabId, new RefsLoadedCommand([.. refs])));
    }

    private sealed class FixedSelection(BranchSelection value) : IBranchSelectionSource
    {
        public BranchSelection Value { get; set; } = value;

        public BranchSelection Current() => Value;
    }
}
