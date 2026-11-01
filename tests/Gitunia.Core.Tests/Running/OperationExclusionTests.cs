using Gitunia.Core.Effects;
using Gitunia.Core.Rebase;
using Gitunia.Core.Remotes;
using Gitunia.Core.Running;
using Gitunia.Core.Stash;
using Gitunia.Core.Sync;
using Gitunia.Core.Tags;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash;
using Xunit;
using static Gitunia.Core.Tests.Rebase.RebaseTestData;

namespace Gitunia.Core.Tests.Running;

// The exclusion table, and the root reducer asking it for stash, sync, the rebase editor, remotes and tags.
public class OperationExclusionTests
{
    private static readonly StashEntry Entry = new(0, "stash@{0}", "abc123", "wip");

    private const string BusyNotice = "Busy: another operation of this tab is still running";

    private static readonly RunningOperationKind[] Writers =
    [
        RunningOperationKind.Branches,
        RunningOperationKind.Commit,
        RunningOperationKind.Conflicts,
        RunningOperationKind.Discard,
        RunningOperationKind.Integration,
        RunningOperationKind.Pick,
        RunningOperationKind.Rebase,
        RunningOperationKind.Staging,
        RunningOperationKind.Stash,
    ];

    public static TheoryData<RunningOperationKind, RunningOperationKind> WriterPairs()
    {
        var data = new TheoryData<RunningOperationKind, RunningOperationKind>();
        foreach (var first in Writers)
        {
            foreach (var second in Writers)
            {
                data.Add(first, second);
            }
        }

        return data;
    }

    public static TheoryData<RunningOperationKind> WriterKinds()
    {
        var data = new TheoryData<RunningOperationKind>();
        foreach (var writer in Writers)
        {
            data.Add(writer);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(WriterPairs))]
    public void GivenTwoWorkingTreeWriters_WhenAskingTheTable_ThenEachExcludesTheOther(
        RunningOperationKind first,
        RunningOperationKind second)
    {
        Assert.True(OperationExclusion.Excludes(first, second));
        Assert.True(OperationExclusion.Excludes(second, first));
    }

    [Theory]
    [MemberData(nameof(WriterKinds))]
    public void GivenAWriterOrSync_WhenAskingTheTable_ThenSyncAndTheWriterExcludeEachOther(RunningOperationKind writer)
    {
        Assert.True(OperationExclusion.Excludes(RunningOperationKind.Sync, writer));
        Assert.True(OperationExclusion.Excludes(writer, RunningOperationKind.Sync));
    }

    [Theory]
    [InlineData(RunningOperationKind.Remotes)]
    [InlineData(RunningOperationKind.Tags)]
    public void GivenRemoteManagementOrTags_WhenAskingTheTable_ThenSyncAndItExcludeEachOther(RunningOperationKind kind)
    {
        Assert.True(OperationExclusion.Excludes(RunningOperationKind.Sync, kind));
        Assert.True(OperationExclusion.Excludes(kind, RunningOperationKind.Sync));
    }

    [Theory]
    [InlineData(RunningOperationKind.Remotes)]
    [InlineData(RunningOperationKind.Tags)]
    [InlineData(RunningOperationKind.Worktrees)]
    public void GivenAnOperationThatLeavesTheTreeAlone_WhenAskingAboutAWriter_ThenNoneExcludesTheOther(
        RunningOperationKind other)
    {
        Assert.All(Writers, writer => Assert.False(OperationExclusion.Excludes(writer, other)));
        Assert.All(Writers, writer => Assert.False(OperationExclusion.Excludes(other, writer)));
    }

    [Fact]
    public void GivenOnlyItsOwnMark_WhenAsking_ThenNothingExcludes()
    {
        var running = new RunningOperations([new RunningOperation(1, RunningOperationKind.Stash)], 1);

        Assert.False(running.Excludes(RunningOperationKind.Stash));
    }

    [Theory]
    [MemberData(nameof(WriterKinds))]
    public void GivenAWorkingTreeWriterRuns_WhenAStashApplyIsRequested_ThenItIsRefusedAndNothingStarts(
        RunningOperationKind running)
    {
        var (state, tabId) = Opened();
        var busy = AppReducerTestState.WithRunning(state, tabId, running);

        var update = AppReducer.Reduce(busy, new StashApplyRequested(tabId, "abc123", false));

        Assert.DoesNotContain(update.Effects, effect => effect is OperationEffect);
        Assert.Single(update.Effects.OfType<PostStatusEffect>());
    }

    [Theory]
    [InlineData(RunningOperationKind.Remotes)]
    [InlineData(RunningOperationKind.Tags)]
    [InlineData(RunningOperationKind.Worktrees)]
    public void GivenAnOperationThatLeavesTheTreeAlone_WhenAStashApplyIsRequested_ThenItStarts(
        RunningOperationKind running)
    {
        var (state, tabId) = Opened();
        var busy = AppReducerTestState.WithRunning(state, tabId, running);

        var update = AppReducer.Reduce(busy, new StashApplyRequested(tabId, "abc123", false));

        Assert.IsType<ApplyStashEffect>(Assert.Single(update.Effects));
    }

    [Fact]
    public void GivenARunningSync_WhenAStashApplyIsRequested_ThenItIsRefusedBecauseAPullWritesTheTree()
    {
        var (state, tabId) = Opened();
        var busy = AppReducerTestState.WithRunning(state, tabId, RunningOperationKind.Sync);

        var update = AppReducer.Reduce(busy, new StashApplyRequested(tabId, "abc123", false));

        Assert.DoesNotContain(update.Effects, effect => effect is OperationEffect);
    }

    [Theory]
    [MemberData(nameof(WriterKinds))]
    public void GivenAWorkingTreeWriterRuns_WhenAPullIsRequested_ThenItIsRefused(RunningOperationKind running)
    {
        var (state, tabId) = Opened();
        var busy = AppReducerTestState.WithRunning(state, tabId, running);

        var update = AppReducer.Reduce(busy, new SyncRequested(tabId, SyncKind.Pull));

        Assert.DoesNotContain(update.Effects, effect => effect is OperationEffect);
    }

    [Fact]
    public void GivenARunningSync_WhenARemoteOperationIsRequested_ThenItIsRefused()
    {
        var (state, tabId) = Opened();
        var busy = AppReducerTestState.WithRunning(state, tabId, RunningOperationKind.Sync);

        var update = AppReducer.Reduce(
            busy,
            new RemoteActionRequested(tabId, RemoteAction.Add, new RemoteTarget(null, null)));

        Assert.DoesNotContain(update.Effects, effect => effect is OperationEffect or ShowPromptEffect);
        Assert.Single(update.Effects.OfType<PostStatusEffect>());
    }

    [Fact]
    public void GivenARunningSync_WhenATagIsCreated_ThenItIsRefused()
    {
        var (state, tabId) = Opened();
        var busy = AppReducerTestState.WithRunning(state, tabId, RunningOperationKind.Sync);

        var update = AppReducer.Reduce(busy, new TagCreateRequested(tabId, new TagTarget(null, null, "abc123")));

        Assert.DoesNotContain(update.Effects, effect => effect is OperationEffect or ShowPromptEffect);
        Assert.Single(update.Effects.OfType<PostStatusEffect>());
    }

    [Theory]
    [MemberData(nameof(WriterKinds))]
    public void GivenAWorkingTreeWriterRuns_WhenTheRebaseEditorIsOpened_ThenItIsRefusedAndNothingStarts(
        RunningOperationKind running)
    {
        var (state, tabId) = Opened();
        var busy = AppReducerTestState.WithRunning(state, tabId, running);

        var update = AppReducer.Reduce(
            busy,
            new RebaseEditorOpenRequested(tabId, new RebaseSelection(null, null, Target), false));

        Assert.DoesNotContain(update.Effects, effect => effect is OperationEffect);
        Assert.Equal(BusyNotice, Assert.Single(update.Effects.OfType<PostStatusEffect>()).Text);
    }

    private static (AppState State, TabId TabId) Opened()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-exclusion-tests", "alpha");
        var gitDirectory = Path.Combine(root, ".git");
        var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
        var state = AppReducer.Reduce(AppState.Empty, new OpenRepositoryCommand(location)).State;
        var tabId = state.Workspace.Tabs[0].Id;
        state = AppReducer.Reduce(state, new TabCommand(tabId, new HeadLoadedCommand(OnBranch()))).State;
        state = AppReducer.Reduce(state, new TabCommand(tabId, new StashesLoadedCommand([Entry]))).State;
        state = AppReducer.Reduce(state, new TabCommand(tabId, new FocusPaneCommand(PaneKind.Sidebar))).State;

        return (state, tabId);
    }
}
