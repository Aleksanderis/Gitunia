using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Remotes;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Remote;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Remote;

// The handler only turns keys into commands; the decisions are in RemoteReducerTests and the shared prompt tests.
public class RemoteManagementActionsTests
{
    private readonly Dispatcher _host;
    private readonly FixedSelection _selection = new(new BranchSelection(null, null, null));
    private readonly FixedRemoteOperations _remoteOperations = new();
    private readonly List<AppEffect> _effects = [];
    private readonly RemoteManagementActions _actions;

    public RemoteManagementActionsTests()
    {
        var shell = new TestShell();
        _host = shell.Host;
        _host.Dispatch(new OpenRepositoryCommand(Location()));
        _host.EffectRaised += _effects.Add;
        _actions = new RemoteManagementActions(_host, _selection, _remoteOperations);
    }

    [Theory]
    [InlineData("remote.add", RemoteAction.Add)]
    [InlineData("remote.remove", RemoteAction.Remove)]
    [InlineData("remote.rename", RemoteAction.Rename)]
    [InlineData("remote.set-url", RemoteAction.SetUrl)]
    public void GivenAManagementAction_WhenExecuted_ThenTheMatchingCommandReadsTheRemotes(
        string id,
        RemoteAction expected)
    {
        _selection.Value = new BranchSelection(SidebarRowKind.RemoteBranch, "origin/main", "1");

        Assert.True(_actions.TryExecute(new ActionId(id), null));

        var list = Assert.IsType<ListRemotesEffect>(Assert.Single(_effects));
        Assert.Equal(expected, list.Action);
        Assert.Equal(new RemoteTarget(SidebarRowKind.RemoteBranch, "origin/main"), list.Target);
    }

    [Fact]
    public void GivenAnotherAction_WhenExecuted_ThenItIsNotHandled()
    {
        Assert.False(_actions.TryExecute(ActionIds.InputAccept, null));
        Assert.False(_actions.TryExecute(ActionIds.RemoteFetch, null));
        Assert.Empty(_effects);
    }

    [Fact]
    public void GivenAFetchRuns_WhenManagingRemotes_ThenItIsRefusedWithANoticeAndNothingIsRequested()
    {
        _remoteOperations.Running = true;

        Assert.True(_actions.TryExecute(ActionIds.RemoteRemove, null));

        Assert.Contains("still running", _actions.Notice, StringComparison.Ordinal);
        Assert.Empty(_effects);
    }

    private static RepositoryLocation Location()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-remote-view-tests");
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    private sealed class FixedSelection(BranchSelection value) : IBranchSelectionSource
    {
        public BranchSelection Value { get; set; } = value;

        public BranchSelection Current() => Value;
    }

    private sealed class FixedRemoteOperations : IRemoteOperationSource
    {
        public bool Running { get; set; }

        public bool IsRunning(TabId tabId) => Running;
    }
}
