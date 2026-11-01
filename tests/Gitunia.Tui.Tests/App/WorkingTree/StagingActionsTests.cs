using Gitunia.Core.Keymap;
using Gitunia.Tui.Tests.Fakes;
using Gitunia.Core;
using Gitunia.Core.Effects;
using Gitunia.Core.Lists;
using Gitunia.Core.Shell;
using Gitunia.Core.Staging;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.App.WorkingTree;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Tui.Tests.App.WorkingTree;

// The view class only turns keys and requests into commands; what they do is the Core reducer's tests.
public class StagingActionsTests
{
    [Fact]
    public void GivenAFileUnderTheCursor_WhenTheStageKeyIsPressed_ThenTheStageCommandReachesTheReducer()
    {
        using var harness = new Harness(Modified("a.txt"), Modified("b.txt"));

        var notice = harness.Staging.Execute(harness.Tab, ActionIds.WorkingTreeStage);

        Assert.Empty(notice);
        var effect = Assert.IsType<StagePathsEffect>(Assert.Single(harness.Effects));
        Assert.Equal(["a.txt"], effect.Plan.Paths);
        Assert.True(harness.Staging.IsRunning(harness.Tab.Id));
    }

    [Fact]
    public void GivenTheStageAllKey_WhenPressed_ThenEveryFileIsInTheEffect()
    {
        using var harness = new Harness(Modified("a.txt"), Modified("b.txt"));

        harness.Staging.Execute(harness.Tab, ActionIds.WorkingTreeStageAll);

        var effect = Assert.IsType<StagePathsEffect>(Assert.Single(harness.Effects));
        Assert.Equal(["a.txt", "b.txt"], effect.Plan.Paths);
    }

    [Fact]
    public void GivenTheUnstageKeys_WhenPressedInTheStagedList_ThenTheyUnstage()
    {
        using var harness = new Harness(StagedModified("a.txt"), StagedModified("b.txt"));
        harness.Send(new FocusPaneCommand(PaneKind.WorkingStaged));

        harness.Staging.Execute(harness.Tab, ActionIds.WorkingTreeUnstage);
        var single = Assert.IsType<StagePathsEffect>(Assert.Single(harness.Effects));

        Assert.False(single.IsStage);
        Assert.Equal(["a.txt"], single.Plan.Paths);
    }

    [Fact]
    public void GivenAVisualRange_WhenTheStageKeyIsPressed_ThenTheRangeTravelsWithTheKey()
    {
        using var harness = new Harness(Modified("a.txt"), Modified("b.txt"), Modified("c.txt"));

        harness.Staging.Execute(harness.Tab, ActionIds.WorkingTreeStage, new RowRange(1, 2));

        Assert.Equal(["b.txt", "c.txt"], Assert.IsType<StagePathsEffect>(Assert.Single(harness.Effects)).Plan.Paths);
    }

    [Fact]
    public void GivenAStageThatRuns_WhenAnotherKeyIsPressed_ThenTheReducerRefusesItAsBusy()
    {
        using var harness = new Harness(Modified("a.txt"), Modified("b.txt"));
        harness.Staging.Execute(harness.Tab, ActionIds.WorkingTreeStage);
        harness.Effects.Clear();

        var notice = harness.Staging.Execute(harness.Tab, ActionIds.WorkingTreeStage);

        Assert.Empty(notice);
        Assert.Contains(StagingReducer.BusyNotice, harness.Statuses, StringComparer.Ordinal);
    }

    [Fact]
    public void GivenACommitWaitsForStageAll_WhenAsked_ThenTheActionTravelsAsTheCommandThatFollows()
    {
        using var harness = new Harness(Modified("a.txt"));
        var ran = false;

        var notice = harness.Staging.StageAllThen(harness.Tab, () => ran = true);

        Assert.Empty(notice);
        var follow = Assert.IsType<StagePathsEffect>(Assert.Single(harness.Effects)).Follow;
        Assert.False(ran);
        Assert.IsType<RunActionCommand>(follow.Then).Run();
        Assert.True(ran);
    }

    [Fact]
    public void GivenTheConflictEditorSavedAFile_WhenAskedToStageIt_ThenTheResolvedFileTravelsWithTheEffect()
    {
        using var harness = new Harness(Conflict("c.txt"));

        var notice = harness.Staging.StageResolvedFor(harness.Tab, "c.txt");

        Assert.Empty(notice);
        var follow = Assert.IsType<StagePathsEffect>(Assert.Single(harness.Effects)).Follow;
        Assert.Equal(new ResolvedFile("c.txt", "c.txt"), follow.Resolved);
    }

    [Fact]
    public void GivenTheKeysOfTheStagingActions_WhenAskedWhatTheyHandle_ThenOnlyTheFourFileKeysAreTheirs()
    {
        Assert.True(StagingActions.Handles(ActionIds.WorkingTreeStage));
        Assert.True(StagingActions.Handles(ActionIds.WorkingTreeStageAll));
        Assert.True(StagingActions.Handles(ActionIds.WorkingTreeUnstage));
        Assert.True(StagingActions.Handles(ActionIds.WorkingTreeUnstageAll));
        Assert.False(StagingActions.Handles(ActionIds.WorkingTreeDiscard));
    }

    private sealed class Harness : IDisposable
    {
        private readonly TestShell _shell = new();

        public Harness(params StatusEntry[] entries)
        {
            var root = Path.Combine(Path.GetTempPath(), "gitunia-staging-actions-tests");
            var location = new RepositoryLocation(root, Path.Combine(root, ".git"), Path.Combine(root, ".git"), false, false);
            _shell.Host.Dispatch(new OpenRepositoryCommand(location));
            Send(new WorkingTreeLoadedCommand(Snapshot(entries)));
            Send(new OpenWorkingTreeCommand());
            Send(new FocusPaneCommand(PaneKind.WorkingUnstaged));
            Staging = new StagingActions(_shell.Host);
            _shell.Host.EffectRaised += Record;
        }

        public StagingActions Staging { get; }

        /// <summary>The staging effects the reducer returned (a runner would perform them).</summary>
        public List<StagingEffect> Effects { get; } = [];

        /// <summary>The status messages the reducer asked for.</summary>
        public List<string> Statuses { get; } = [];

        public RepositoryState Tab => _shell.Host.Workspace.ActiveTab!;

        public void Send(RepositoryCommand command) =>
            _shell.Host.Dispatch(new TabCommand(_shell.Host.Workspace.ActiveTabId!.Value, command));

        public void Dispose() => _shell.Host.EffectRaised -= Record;

        private void Record(AppEffect effect)
        {
            if (effect is StagingEffect staging)
            {
                Effects.Add(staging);
            }
            else if (effect is PostStatusEffect post)
            {
                Statuses.Add(post.Text);
            }
        }
    }
}
