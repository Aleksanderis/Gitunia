using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Stash;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Stash.Models;
using Gitunia.Tui.App.Stash;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Stash;

// The actions only translate keys into commands that carry what the user sees; the decisions are in StashReducerTests.
public class StashActionsTests
{
    private static readonly StashEntry Entry = new(0, "stash@{0}", "abc123", "wip");

    private readonly TestShell _shell = new();
    private readonly Dispatcher _host;
    private readonly FixedSelection _selection = new();
    private readonly List<AppEffect> _effects = [];
    private readonly StashActions _actions;

    public StashActionsTests()
    {
        _host = _shell.Host;
        _host.Dispatch(new OpenRepositoryCommand(Location()));
        var tabId = _host.Workspace.ActiveTabId!.Value;
        _host.Dispatch(new TabCommand(tabId, new StashesLoadedCommand([Entry])));
        _host.Dispatch(new TabCommand(tabId, new FocusPaneCommand(PaneKind.Sidebar)));
        _host.EffectRaised += _effects.Add;
        _actions = new StashActions(_host, _selection);
    }

    [Theory]
    [InlineData(false, "Tracked changes, message optional")]
    [InlineData(true, "Tracked and untracked files, message optional")]
    public void GivenThePushKeys_WhenPressed_ThenTheMessagePromptOpensFromState(bool untracked, string context)
    {
        var action = untracked ? ActionIds.StashPushUntracked : ActionIds.StashPush;

        Assert.True(_actions.TryExecute(action, null));

        Assert.Equal("Stash changes", _host.State.Shell.Prompt!.Title);
        Assert.Equal(context, _host.State.Shell.Prompt.Context);
        Assert.Equal(KeyMode.Insert, _host.State.Shell.Mode);
    }

    [Fact]
    public void GivenAStashRow_WhenApplying_ThenTheStashOfThatRowIsApplied()
    {
        _selection.Commit = "abc123";

        Assert.True(_actions.TryExecute(ActionIds.StashApply, null));

        var apply = Assert.IsType<ApplyStashEffect>(Assert.Single(_effects));
        Assert.Equal(Entry, apply.Entry);
        Assert.False(apply.Pop);
    }

    [Fact]
    public void GivenAStashRow_WhenPopping_ThenTheEffectPops()
    {
        _selection.Commit = "abc123";

        _actions.TryExecute(ActionIds.StashPop, null);

        Assert.True(Assert.IsType<ApplyStashEffect>(Assert.Single(_effects)).Pop);
    }

    [Fact]
    public void GivenAStashRow_WhenDropping_ThenTheDialogOpensAndNothingRunsYet()
    {
        _selection.Commit = "abc123";

        _actions.TryExecute(ActionIds.StashDrop, null);

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(_effects));
        Assert.Equal("Drop stash", confirm.Prompt.Title);
        Assert.Equal(new StashDropConfirmed(_host.Workspace.ActiveTabId!.Value, Entry), confirm.OnAccepted);
    }

    [Fact]
    public void GivenNoStashRow_WhenApplying_ThenTheRefusalIsPostedAndNothingRuns()
    {
        _actions.TryExecute(ActionIds.StashApply, null);

        Assert.Equal(StashReducer.NoStashNotice, Assert.IsType<PostStatusEffect>(Assert.Single(_effects)).Text);
    }

    [Fact]
    public void GivenAFileUnderTheCursor_WhenRestoring_ThenTheRestoreChecksItFirst()
    {
        var change = new CommitFileChange("src/a.txt", null, FileChangeKind.Modified, null);
        _selection.File = new StashFileTarget(Entry, new StashFileChange(change, StashFileOrigin.Tracked));

        _actions.TryExecute(ActionIds.StashRestoreFile, null);

        Assert.Equal(_selection.File, Assert.IsType<CheckStashRestoreEffect>(Assert.Single(_effects)).Target);
    }

    [Fact]
    public void GivenNoFileUnderTheCursor_WhenRestoring_ThenItIsRefusedWithoutGit()
    {
        _actions.TryExecute(ActionIds.StashRestoreFile, null);

        Assert.Equal(StashReducer.NoFileNotice, Assert.IsType<PostStatusEffect>(Assert.Single(_effects)).Text);
    }

    [Fact]
    public void GivenAnUnrelatedAction_WhenExecuting_ThenTheActionsLeaveItAlone()
    {
        Assert.False(_actions.TryExecute(ActionIds.TagCreate, null));
        Assert.Empty(_effects);
    }

    private static RepositoryLocation Location()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-stash-actions-tests");
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    private sealed class FixedSelection : IStashSelectionSource
    {
        public string? Commit { get; set; }

        public StashFileTarget? File { get; set; }

        public string? CursorStashCommit() => Commit;

        public StashFileTarget? FileUnderCursor() => File;
    }
}
