using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Reset;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Reset;

// The actions only turn the key into a command that carries the selected commit; the decisions are in
// ResetReducerTests and the work on the repository in the App tests.
public class ResetActionsTests
{
    private const string Commit = "2222222222222222222222222222222222222222";

    private readonly TestShell _shell = new();
    private readonly Dispatcher _host;
    private readonly FixedSelection _selection = new();
    private readonly List<AppEffect> _effects = [];
    private readonly ResetActions _actions;

    public ResetActionsTests()
    {
        _host = _shell.Host;
        _host.Dispatch(new OpenRepositoryCommand(Location()));
        var tabId = _host.Workspace.ActiveTabId!.Value;
        _host.Dispatch(new TabCommand(tabId, new HeadLoadedCommand(new HeadState(HeadKind.Branch, "main", "abc"))));
        _host.EffectRaised += _effects.Add;
        _actions = new ResetActions(_host, _selection);
    }

    [Fact]
    public void GivenAGraphCommit_WhenPressingTheResetKey_ThenTheModePromptOpensAtIt()
    {
        _selection.Current = new BranchSelection(null, null, Commit);

        Assert.True(_actions.TryExecute(ActionIds.ResetBranch, null));

        Assert.Equal("Move main to 2222222", _host.State.Shell.Prompt!.Context);
        Assert.Equal(KeyMode.Insert, _host.State.Shell.Mode);
    }

    [Theory]
    [InlineData(SidebarRowKind.LocalBranch)]
    [InlineData(SidebarRowKind.RemoteBranch)]
    [InlineData(SidebarRowKind.Tag)]
    public void GivenARefRow_WhenPressingTheResetKey_ThenThePromptOpensAtItsCommit(SidebarRowKind kind)
    {
        _selection.Current = new BranchSelection(kind, "x", Commit);

        _actions.TryExecute(ActionIds.ResetBranch, null);

        Assert.Equal("Move main to 2222222", _host.State.Shell.Prompt!.Context);
    }

    [Theory]
    [InlineData(SidebarRowKind.Stash)]
    [InlineData(SidebarRowKind.Worktree)]
    [InlineData(SidebarRowKind.Section)]
    public void GivenARowThatIsNotARef_WhenPressingTheResetKey_ThenNoPromptOpensAndTheUserIsTold(SidebarRowKind kind)
    {
        _selection.Current = new BranchSelection(kind, "x", Commit);

        _actions.TryExecute(ActionIds.ResetBranch, null);

        Assert.Null(_host.State.Shell.Prompt);
        var post = Assert.IsType<PostStatusEffect>(Assert.Single(_effects));
        Assert.StartsWith("Select a commit", post.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNothingSelected_WhenPressingTheResetKey_ThenNoPromptOpens()
    {
        _actions.TryExecute(ActionIds.ResetBranch, null);

        Assert.Null(_host.State.Shell.Prompt);
        Assert.Equal(KeyMode.Normal, _host.State.Shell.Mode);
    }

    [Fact]
    public void GivenACommitFromAnotherView_WhenBegun_ThenTheSameFlowRunsAndNoRefusalIsReturned()
    {
        var notice = _actions.Begin(_host.Workspace.ActiveTabId!.Value, Commit);

        Assert.Equal(string.Empty, notice);
        Assert.NotNull(_host.State.Shell.Prompt);
    }

    [Fact]
    public void GivenADetachedHead_WhenBegun_ThenTheRefusalIsReturnedForTheCaller()
    {
        var tabId = _host.Workspace.ActiveTabId!.Value;
        _host.Dispatch(new TabCommand(tabId, new HeadLoadedCommand(new HeadState(HeadKind.Detached, null, "abc"))));

        _host.EffectRaised += effect =>
        {
            // The runner posts the status of a refusal; the test shell does not run that effect.
            if (effect is PostStatusEffect post)
            {
                _shell.Statuses.Post(post.Text, post.Severity);
            }
        };

        var notice = _actions.Begin(tabId, Commit);

        Assert.Equal("HEAD is detached: switch to a branch first", notice);
        Assert.Null(_host.State.Shell.Prompt);
    }

    [Fact]
    public void GivenAnUnrelatedAction_WhenExecuting_ThenTheActionsLeaveItAlone()
    {
        Assert.False(_actions.TryExecute(ActionIds.TagCreate, null));
        Assert.Empty(_effects);
    }

    private static RepositoryLocation Location()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-reset-actions-tests");
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    private sealed class FixedSelection : IBranchSelectionSource
    {
        public BranchSelection Current { get; set; } = new(null, null, null);

        BranchSelection IBranchSelectionSource.Current() => Current;
    }
}
