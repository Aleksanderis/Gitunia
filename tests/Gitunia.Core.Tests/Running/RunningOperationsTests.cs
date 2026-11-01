using Gitunia.Core.Running;
using Gitunia.Core.Remotes;
using Gitunia.Core.Tags;
using Gitunia.Core.Tests.Fixtures;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
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

    [Fact]
    public void GivenEveryConcreteTagEffect_WhenInspectingIt_ThenItIsAnOperationEffectOfKindTags()
    {
        var effects = ConcreteSubtypes.InstancesOf<TagEffect>();

        // Instances are uninitialised (the constructor does not run), so only a built one shows the kind.
        Assert.NotEmpty(effects);
        Assert.All(effects, effect => Assert.IsAssignableFrom<OperationEffect>(effect));
        Assert.Equal(RunningOperationKind.Tags, Delete(Tab).Kind);
    }

    [Fact]
    public void GivenEveryConcreteResultCommand_WhenInspectingIt_ThenItEndsTheKindOfItsFeature()
    {
        var results = ConcreteSubtypes.InstancesOf<TagCommand>().OfType<IOperationResult>().ToArray();

        Assert.Equal(5, results.Length);
        Assert.All(results, result => Assert.Equal(RunningOperationKind.Tags, result.Kind));
    }

    [Fact]
    public void GivenEveryConcreteRemoteEffect_WhenInspectingIt_ThenItIsAnOperationEffectOfKindRemotes()
    {
        var effects = ConcreteSubtypes.InstancesOf<RemoteEffect>();
        var location = Location();

        // Instances are uninitialised (the constructor does not run), so one built per type shows the kind.
        Assert.NotEmpty(effects);
        Assert.All(effects, effect => Assert.IsAssignableFrom<OperationEffect>(effect));
        RemoteEffect[] built =
        [
            new ListRemotesEffect(Tab, location, RemoteAction.Add, new RemoteTarget(null, null)),
            new AddRemoteEffect(Tab, location, "a", "u"),
            new RemoveRemoteEffect(Tab, location, "a", "u"),
            new RenameRemoteEffect(Tab, location, "a", "b"),
            new SetRemoteUrlEffect(Tab, location, "a", "u", "v"),
        ];
        Assert.Equal(effects.Count, built.Length);
        Assert.All(built, effect => Assert.Equal(RunningOperationKind.Remotes, effect.Kind));
    }

    [Fact]
    public void GivenEveryConcreteRemoteResultCommand_WhenInspectingIt_ThenItEndsTheKindRemotes()
    {
        var results = ConcreteSubtypes.InstancesOf<RemoteCommand>().OfType<IOperationResult>().ToArray();

        Assert.Equal(5, results.Length);
        Assert.All(results, result => Assert.Equal(RunningOperationKind.Remotes, result.Kind));
    }

    private static DeleteTagEffect Delete(TabId tab) => new(tab, Location(), "v1", "x");

    private static RepositoryLocation Location()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-running-tests");
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }
}
