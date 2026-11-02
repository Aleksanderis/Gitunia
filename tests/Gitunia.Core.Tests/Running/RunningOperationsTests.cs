using Gitunia.Core.Discard;
using Gitunia.Core.Integration;
using Gitunia.Core.Running;
using Gitunia.Core.Pick;
using Gitunia.Core.Rebase;
using Gitunia.Core.Remotes;
using Gitunia.Core.Stash;
using Gitunia.Core.Tags;
using Gitunia.Core.Tests.Fixtures;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Core.Worktrees;
using Gitunia.Core.Staging;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Merge;
using Gitunia.Git.Pick;
using Gitunia.Git.Rebase;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Reset;
using Gitunia.Git.Stash;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Stash.Models;
using Gitunia.Git.Worktrees.Models;
using Xunit;

namespace Gitunia.Core.Tests.Running;

public class RunningOperationsTests
{
    private static readonly TabId Tab = new(1);
    private static readonly TabId Other = new(2);

    [Fact]
    public void GivenNothingRunning_WhenAnOperationEffectIsReturned_ThenTheKindRunsWithTheNextId()
    {
        var running = RunningOperations.Empty.Apply(Tab, new TagCreateRequested(Tab, new TagTarget(null, null, null)), [Delete(Tab)]);

        Assert.True(running.IsBusy);
        Assert.True(running.IsRunning(RunningOperationKind.Tags));
        Assert.Equal(new RunningOperation(1, RunningOperationKind.Tags), Assert.Single(running.Items));
    }

    [Fact]
    public void GivenAnEffectOfAnotherTab_WhenApplying_ThenThisTabStaysIdle()
    {
        var running = RunningOperations.Empty.Apply(Tab, new TagCreateRequested(Tab, new TagTarget(null, null, null)), [Delete(Other)]);

        Assert.False(running.IsBusy);
    }

    [Fact]
    public void GivenARunningOperation_WhenItsResultArrives_ThenTheTabIsIdleAndTheNextIdKeepsCounting()
    {
        var started = RunningOperations.Empty.Apply(Tab, new TagDeleteConfirmed(Tab, "v1", "x"), [Delete(Tab)]);

        var ended = started.Apply(Tab, new TagOperationSucceeded(Tab, "done"), []);
        var next = ended.Apply(Tab, new TagDeleteConfirmed(Tab, "v2", "x"), [Delete(Tab)]);

        Assert.False(ended.IsBusy);
        Assert.Equal(2, Assert.Single(next.Items).Id);
    }

    [Fact]
    public void GivenAResultWhileIdle_WhenApplying_ThenNothingChanges()
    {
        var running = RunningOperations.Empty.Apply(Tab, new TagOperationCancelled(Tab), []);

        Assert.Equal(RunningOperations.Empty, running);
    }

    [Fact]
    public void GivenAResultThatStartsTheNextStep_WhenApplying_ThenTheTabStaysBusy()
    {
        var started = RunningOperations.Empty.Apply(Tab, new TagDeleteConfirmed(Tab, "v1", "x"), [Delete(Tab)]);

        var stepped = started.Apply(Tab, new TagOperationSucceeded(Tab, "done"), [Delete(Tab)]);

        Assert.True(stepped.IsRunning(RunningOperationKind.Tags));
        Assert.Equal(2, Assert.Single(stepped.Items).Id);
    }

    [Theory]
    [InlineData(RunningOperationKind.Discard)]
    [InlineData(RunningOperationKind.Tags)]
    [InlineData(RunningOperationKind.Remotes)]
    [InlineData(RunningOperationKind.Stash)]
    [InlineData(RunningOperationKind.Integration)]
    [InlineData(RunningOperationKind.Worktrees)]
    [InlineData(RunningOperationKind.Pick)]
    [InlineData(RunningOperationKind.Rebase)]
    public void GivenEveryConcreteEffectOfAFeature_WhenInspectingIt_ThenItIsAnOperationEffectOfThatKind(RunningOperationKind kind)
    {
        // Built through the constructors: an uninitialised instance would show the enum's first value, not the kind.
        var (concrete, built) = EffectsOf(kind);

        Assert.NotEmpty(concrete);
        Assert.All(concrete, effect => Assert.IsAssignableFrom<OperationEffect>(effect));
        Assert.Equal(concrete.Count, built.Length);
        Assert.All(built, effect => Assert.Equal(kind, effect.Kind));
        Assert.All(built, effect => Assert.Equal(Tab, effect.TabId));
    }

    [Theory]
    [InlineData(RunningOperationKind.Discard)]
    [InlineData(RunningOperationKind.Tags)]
    [InlineData(RunningOperationKind.Remotes)]
    [InlineData(RunningOperationKind.Stash)]
    [InlineData(RunningOperationKind.Integration)]
    [InlineData(RunningOperationKind.Worktrees)]
    [InlineData(RunningOperationKind.Pick)]
    [InlineData(RunningOperationKind.Rebase)]
    public void GivenEveryConcreteResultCommandOfAFeature_WhenInspectingIt_ThenItEndsTheKindOfItsFeature(RunningOperationKind kind)
    {
        var results = ResultsOf(kind);

        Assert.NotEmpty(results);
        Assert.All(results, result => Assert.Equal(kind, result.Kind));
    }

    [Fact]
    public void GivenTheLocateEffect_WhenInspectingIt_ThenItIsNotAnOperationOfTheTab()
    {
        Assert.False(typeof(OperationEffect).IsAssignableFrom(typeof(LocateWorktreeEffect)));
    }

    private static IOperationResult[] ResultsOf(RunningOperationKind kind) => kind switch
    {
        RunningOperationKind.Discard => Results<DiscardCommand>(),
        RunningOperationKind.Tags => Results<TagCommand>(),
        RunningOperationKind.Remotes => Results<RemoteCommand>(),
        RunningOperationKind.Stash => Results<StashCommand>(),
        RunningOperationKind.Integration => Results<IntegrationCommand>(),
        RunningOperationKind.Worktrees => Results<WorktreeCommand>(),
        RunningOperationKind.Pick => Results<PickCommand>(),
        RunningOperationKind.Rebase => Results<RebaseCommand>(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static IOperationResult[] Results<T>()
        where T : class => [.. ConcreteSubtypes.InstancesOf<T>().OfType<IOperationResult>()];

    // The concrete effect types found by reflection, and one instance of each built through its constructor.
    private static EffectSet EffectsOf(RunningOperationKind kind)
    {
        var location = Location();
        switch (kind)
        {
            case RunningOperationKind.Discard:
                return new EffectSet(
                    ConcreteSubtypes.InstancesOf<DiscardEffect>(),
                    [
                        new DiscardFilesEffect(Tab, location, [], 0),
                        new DiscardSelectionEffect(
                            Tab,
                            location,
                            new StagingSelection(WorkingDiffSide.Unstaged, null!, [], false),
                            true,
                            null),
                    ]);
            case RunningOperationKind.Tags:
                return new EffectSet(
                    ConcreteSubtypes.InstancesOf<TagEffect>(),
                    [
                        new CreateTagEffect(Tab, location, "v1", "x", string.Empty, "x"),
                        new DeleteTagEffect(Tab, location, "v1", "x"),
                        new ListTagRemotesEffect(Tab, location, "v1", false),
                        new PushTagEffect(Tab, location, "origin", "v1"),
                        new DeleteTagOnRemoteEffect(Tab, location, "origin", "v1"),
                    ]);
            case RunningOperationKind.Remotes:
                return new EffectSet(
                    ConcreteSubtypes.InstancesOf<RemoteEffect>(),
                    [
                        new ListRemotesEffect(Tab, location, RemoteAction.Add, new RemoteTarget(null, null)),
                        new AddRemoteEffect(Tab, location, "a", "u"),
                        new RemoveRemoteEffect(Tab, location, "a", "u"),
                        new RenameRemoteEffect(Tab, location, "a", "b"),
                        new SetRemoteUrlEffect(Tab, location, "a", "u", "v"),
                    ]);
            case RunningOperationKind.Stash:
                var entry = new StashEntry(0, "stash@{0}", "abc", "m");
                var file = new StashFileChange(
                    new CommitFileChange("a", null, FileChangeKind.Added, null),
                    StashFileOrigin.Tracked);
                var target = new StashFileTarget(entry, file);
                return new EffectSet(
                    ConcreteSubtypes.InstancesOf<StashEffect>(),
                    [
                        new PushStashEffect(Tab, location, "m", false),
                        new ApplyStashEffect(Tab, location, entry, false),
                        new DropStashEffect(Tab, location, entry),
                        new CheckStashRestoreEffect(Tab, location, target),
                        new RestoreStashFileEffect(Tab, location, target),
                    ]);
            case RunningOperationKind.Integration:
                var reset = new ResetTarget("abc", "main", ResetMode.Soft);
                var info = new OperationInfo(RepositoryOperation.Merge, null, null, null, null, null, null, null);
                return new EffectSet(
                    ConcreteSubtypes.InstancesOf<IntegrationEffect>(),
                    [
                        new PrepareResetEffect(Tab, location, reset),
                        new ResetBranchEffect(Tab, location, reset),
                        new MergeBranchEffect(Tab, location, "main", new MergeTarget("t", "t"), MergeMode.Default),
                        new ContinueOperationEffect(Tab, location, info, "main"),
                        new AbortOperationEffect(Tab, location, info, "main"),
                        new SkipOperationEffect(Tab, location, info),
                    ]);
            case RunningOperationKind.Worktrees:
                return new EffectSet(
                    ConcreteSubtypes.InstancesOf<WorktreeEffect>(),
                    [
                        new AddWorktreeEffect(Tab, location, new WorktreeAddRequest("p", "b", null), false),
                        new RemoveWorktreeEffect(Tab, location, "p", false),
                        new LockWorktreeEffect(Tab, location, "p", string.Empty),
                        new UnlockWorktreeEffect(Tab, location, "p"),
                        new PruneWorktreesEffect(Tab, location),
                    ]);
            case RunningOperationKind.Pick:
                return new EffectSet(
                    ConcreteSubtypes.InstancesOf<PickEffect>(),
                    [
                        new ReadPickCommitsEffect(Tab, location, PickKind.CherryPick, ["a"]),
                        new RunPickEffect(Tab, location, PickKind.Revert, [], null, "main"),
                    ]);
            case RunningOperationKind.Rebase:
                var rebase = new RebaseTarget(Tab, "a", "b", "c", "d");
                var plan = new QuickRebasePlan(QuickRebaseKind.Drop, "a", "m", RebaseBase.Root, [], "h", null);
                return new EffectSet(
                    ConcreteSubtypes.InstancesOf<RebaseEffect>(),
                    [
                        new CountRebaseMergesEffect(Tab, location, rebase),
                        new RunRebaseEffect(Tab, location, rebase),
                        new PlanQuickRebaseEffect(Tab, location, QuickRebaseKind.Drop, "a"),
                        new RunQuickRebaseEffect(Tab, location, new QuickRebaseTarget(Tab, plan, "main", null)),
                    ]);
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private sealed record EffectSet(IReadOnlyCollection<OperationEffect> Concrete, OperationEffect[] Built);

    private static DeleteTagEffect Delete(TabId tab) => new(tab, Location(), "v1", "x");

    private static RepositoryLocation Location()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-running-tests");
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }
}
