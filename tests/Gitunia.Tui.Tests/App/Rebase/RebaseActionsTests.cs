using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Rebase;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Rebase;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Rebase;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Rebase;

// The handler only translates keys into commands; the decisions are in RebaseReducerTests and
// QuickRebaseReducerTests.
public class RebaseActionsTests
{
    private const string Commit = "1111111111111111111111111111111111111111";
    private const string Tip = "9999999999999999999999999999999999999999";

    private readonly Dispatcher _host;
    private readonly Selection _selection = new();
    private readonly List<AppEffect> _effects = [];
    private readonly RebaseActions _actions;

    public RebaseActionsTests()
    {
        _host = new TestShell().Host;
        _host.Dispatch(new OpenRepositoryCommand(Location()));
        var tabId = _host.Workspace.ActiveTabId!.Value;
        _host.Dispatch(new TabCommand(tabId, new HeadLoadedCommand(new HeadState(HeadKind.Branch, "main", Tip))));
        _host.EffectRaised += _effects.Add;
        _actions = new RebaseActions(_host, _selection, _selection);
    }

    [Fact]
    public void GivenABranchRow_WhenRebasingOnto_ThenTheMergesAreCountedForThatRow()
    {
        _selection.Row = new BranchSelection(SidebarRowKind.LocalBranch, "topic", Commit);

        Assert.True(_actions.TryExecute(ActionIds.RebaseOnto, null));

        var count = Assert.IsType<CountRebaseMergesEffect>(Assert.Single(_effects));
        Assert.Equal("topic", count.Target.Label);
        Assert.Equal(Commit, count.Target.CommitHash);
    }

    [Theory]
    [InlineData("fixup", QuickRebaseKind.Fixup)]
    [InlineData("reword", QuickRebaseKind.Reword)]
    [InlineData("drop", QuickRebaseKind.Drop)]
    public void GivenTheGraphFocused_WhenAQuickRebaseKeyIsPressed_ThenTheSelectedCommitIsPlanned(
        string key,
        QuickRebaseKind kind)
    {
        _selection.Graph = Commit;
        var action = key switch
        {
            "fixup" => ActionIds.RebaseFixup,
            "reword" => ActionIds.RebaseReword,
            _ => ActionIds.RebaseDrop,
        };

        Assert.True(_actions.TryExecute(action, null));

        var plan = Assert.IsType<PlanQuickRebaseEffect>(Assert.Single(_effects));
        Assert.Equal(kind, plan.QuickKind);
        Assert.Equal(Commit, plan.CommitHash);
    }

    [Fact]
    public void GivenNothingSelectedInTheGraph_WhenDropping_ThenTheReducerAsksForASelection()
    {
        Assert.True(_actions.TryExecute(ActionIds.RebaseDrop, null));

        Assert.Equal("Select a commit in the graph first", Assert.Single(_effects.OfType<PostStatusEffect>()).Text);
    }

    [Fact]
    public void GivenTheSidebarFocused_WhenDropping_ThenTheKeyIsNotHandled()
    {
        _selection.Graph = Commit;
        _host.Dispatch(new TabCommand(_host.Workspace.ActiveTabId!.Value, new FocusPaneCommand(PaneKind.Sidebar)));

        Assert.False(_actions.TryExecute(ActionIds.RebaseDrop, null));
        Assert.Empty(_effects);
    }

    [Fact]
    public void GivenAnUnrelatedAction_WhenExecuting_ThenItIsNotHandled()
    {
        Assert.False(_actions.TryExecute(ActionIds.Cancel, null));
        Assert.Empty(_effects);
    }

    private static RepositoryLocation Location()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-rebase-view-tests");
        var gitDirectory = Path.Combine(root, ".git");

        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    private sealed class Selection : IBranchSelectionSource, IGraphSelectionSource
    {
        public BranchSelection Row { get; set; } = new(null, null, null);

        public string? Graph { get; set; }

        public BranchSelection Current() => Row;

        public string? SelectedCommit() => Graph;
    }
}
