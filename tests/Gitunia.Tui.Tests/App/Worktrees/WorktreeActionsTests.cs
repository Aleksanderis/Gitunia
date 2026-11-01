using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Worktrees;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Worktrees;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Worktrees;

// The class only turns keys into commands; the decisions are in the Core worktree reducer tests.
public class WorktreeActionsTests
{
    private const string Commit = "1111111111111111111111111111111111111111";

    private readonly Dispatcher _host;
    private readonly FixedSelection _selection = new();
    private readonly List<AppEffect> _effects = [];
    private readonly WorktreeActions _actions;

    public WorktreeActionsTests()
    {
        _host = new TestShell().Host;
        _host.Dispatch(new OpenRepositoryCommand(Location("alpha")));
        _host.EffectRaised += _effects.Add;
        _actions = new WorktreeActions(_host, _selection);
    }

    [Fact]
    public void GivenAGraphCommit_WhenAdding_ThenTheBranchPromptKeepsTheCommitTheUserSaw()
    {
        _selection.Value = new WorktreeTarget(null, null, null, null, Commit);

        var handled = _actions.TryExecute(ActionIds.WorktreeAdd);

        Assert.True(handled);
        var prompt = Assert.IsType<ShowPromptEffect>(Assert.Single(_effects)).Request;
        Assert.Equal("New worktree", prompt.Title);
        Assert.Equal(new WorktreeBranchAnswer(_host.Workspace.ActiveTabId!.Value, Commit), prompt.OnAccepted);
    }

    [Fact]
    public void GivenAFreeBranchRow_WhenOpening_ThenOnlyThePathIsAsked()
    {
        var tab = _host.Workspace.ActiveTabId!.Value;
        _host.Dispatch(new TabCommand(tab, new RefsLoadedCommand([Local("topic")])));
        _effects.Clear();
        _selection.Value = new WorktreeTarget(SidebarRowKind.LocalBranch, "topic", null, Commit, null);

        var handled = _actions.TryExecute(ActionIds.WorktreeOpen);

        Assert.True(handled);
        Assert.Equal("Worktree path", Assert.IsType<ShowPromptEffect>(Assert.Single(_effects)).Request.Title);
    }

    [Fact]
    public void GivenNothingPrunable_WhenPruning_ThenTheNoticeIsPosted()
    {
        var handled = _actions.TryExecute(ActionIds.WorktreePrune);

        Assert.True(handled);
        Assert.Equal("No worktree entry to prune", Assert.IsType<PostStatusEffect>(Assert.Single(_effects)).Text);
    }

    [Theory]
    [InlineData("worktree.remove")]
    [InlineData("worktree.lock")]
    [InlineData("worktree.unlock")]
    [InlineData("worktree.switch")]
    public void GivenAWorktreeKeyWithoutAWorktreeRow_WhenPressed_ThenItIsHandledAndTheNoticeIsPosted(string name)
    {
        var handled = _actions.TryExecute(new ActionId(name));

        Assert.True(handled);
        Assert.Equal(
            WorktreeTexts.NoWorktreeNotice,
            Assert.IsType<PostStatusEffect>(Assert.Single(_effects)).Text);
    }

    [Fact]
    public void GivenAnUnrelatedAction_WhenExecuting_ThenItIsNotHandled()
    {
        var handled = _actions.TryExecute(ActionIds.Cancel);

        Assert.False(handled);
        Assert.Empty(_effects);
    }

    [Fact]
    public void GivenNoTab_WhenAdding_ThenItIsNotHandled()
    {
        var host = new TestShell().Host;
        var actions = new WorktreeActions(host, _selection);

        Assert.False(actions.TryExecute(ActionIds.WorktreeAdd));
    }

    private static GitRef Local(string name) => new(
        $"refs/heads/{name}",
        name,
        RefKind.LocalBranch,
        Commit,
        Commit,
        null,
        null,
        null,
        false,
        false,
        null,
        null);

    private static RepositoryLocation Location(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-worktree-handler-tests", name);
        var gitDirectory = Path.Combine(root, ".git");

        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    private sealed class FixedSelection : IWorktreeSelectionSource
    {
        public WorktreeTarget Value { get; set; } = new(null, null, null, null, null);

        public WorktreeTarget Current() => Value;
    }
}
