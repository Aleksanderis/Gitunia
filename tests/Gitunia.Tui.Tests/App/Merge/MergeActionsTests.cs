using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.Effects;
using Gitunia.Core.Integration;
using Gitunia.Core.Keymap;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Merge;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Merge;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Merge;

// The actions only turn the keys into a command that carries what is selected; the decisions are in
// MergeReducerTests and the work on the repository in the App tests.
public class MergeActionsTests
{
    private const string Commit = "2222222222222222222222222222222222222222";

    private readonly Dispatcher _host = new TestShell().Host;
    private readonly FixedSelection _selection = new();
    private readonly List<AppEffect> _effects = [];
    private readonly MergeActions _actions;

    public MergeActionsTests()
    {
        _host.Dispatch(new OpenRepositoryCommand(Location()));
        var tabId = _host.Workspace.ActiveTabId!.Value;
        _host.Dispatch(new TabCommand(tabId, new HeadLoadedCommand(new HeadState(HeadKind.Branch, "main", "abc"))));
        _host.EffectRaised += _effects.Add;
        _actions = new MergeActions(_host, _selection);
    }

    [Fact]
    public void GivenACommit_WhenPressingTheMergeKeys_ThenEachKeyStartsItsModeByHash()
    {
        _selection.Current = new BranchSelection(null, null, Commit);

        Assert.True(_actions.TryExecute(ActionIds.MergeBranch, null));
        Complete();
        Assert.True(_actions.TryExecute(ActionIds.MergeBranchNoFastForward, null));
        Complete();
        Assert.True(_actions.TryExecute(ActionIds.MergeBranchSquash, null));

        var merges = _effects.OfType<MergeBranchEffect>().ToArray();
        Assert.Equal([MergeMode.Default, MergeMode.NoFastForward, MergeMode.Squash], merges.Select(merge => merge.Mode));
        Assert.All(merges, merge => Assert.Equal(new MergeTarget(Commit, "2222222"), merge.Target));
    }

    [Fact]
    public void GivenALocalBranchRow_WhenMerging_ThenTheFullRefIsPassedSoATagOfTheSameNameCannotWin()
    {
        _selection.Current = new BranchSelection(SidebarRowKind.LocalBranch, "topic", Commit);

        _actions.TryExecute(ActionIds.MergeBranch, null);

        var merge = Assert.IsType<MergeBranchEffect>(Assert.Single(_effects));
        Assert.Equal(new MergeTarget("refs/heads/topic", "topic"), merge.Target);
    }

    [Fact]
    public void GivenARemoteBranchRow_WhenMerging_ThenTheRemoteRefIsPassed()
    {
        _selection.Current = new BranchSelection(SidebarRowKind.RemoteBranch, "origin/topic", Commit);

        _actions.TryExecute(ActionIds.MergeBranch, null);

        var merge = Assert.IsType<MergeBranchEffect>(Assert.Single(_effects));
        Assert.Equal(new MergeTarget("refs/remotes/origin/topic", "origin/topic"), merge.Target);
    }

    [Fact]
    public void GivenATagRow_WhenMerging_ThenTheTagCommitIsMergedAndNamedByTheTag()
    {
        _selection.Current = new BranchSelection(SidebarRowKind.Tag, "v1", Commit);

        _actions.TryExecute(ActionIds.MergeBranch, null);

        Assert.Equal(new MergeTarget(Commit, "v1"), Assert.IsType<MergeBranchEffect>(Assert.Single(_effects)).Target);
    }

    [Theory]
    [InlineData(SidebarRowKind.Stash)]
    [InlineData(SidebarRowKind.Worktree)]
    [InlineData(SidebarRowKind.Section)]
    public void GivenARowThatIsNotARef_WhenMerging_ThenNothingRunsAndTheUserIsTold(SidebarRowKind kind)
    {
        _selection.Current = new BranchSelection(kind, "row", Commit);

        _actions.TryExecute(ActionIds.MergeBranch, null);

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(_effects));
        Assert.StartsWith("Select a commit", post.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNothingSelected_WhenMerging_ThenNothingRuns()
    {
        _actions.TryExecute(ActionIds.MergeBranch, null);

        Assert.IsType<PostStatusEffect>(Assert.Single(_effects));
    }

    [Fact]
    public void GivenAnUnrelatedAction_WhenExecuting_ThenItIsNotHandled()
    {
        Assert.False(_actions.TryExecute(ActionIds.TagCreate, null));
        Assert.Empty(_effects);
    }

    // The merge result that frees the tab for the next key.
    private void Complete() =>
        _host.Dispatch(new IntegrationCancelled(_host.Workspace.ActiveTabId!.Value));

    private static RepositoryLocation Location()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-merge-actions-tests");
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    private sealed class FixedSelection : IBranchSelectionSource
    {
        public BranchSelection Current { get; set; } = new(null, null, null);

        BranchSelection IBranchSelectionSource.Current() => Current;
    }
}
