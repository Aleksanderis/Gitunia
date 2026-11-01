using Gitunia.Core.Pick;
using Gitunia.Core.Rebase;
using Gitunia.Core.Running;
using Gitunia.Core.Tests.Fixtures;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Pick;
using Gitunia.Git.Rebase;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.Running;

public class PickRebaseOperationTests
{
    private static readonly TabId Tab = new(1);

    [Fact]
    public void GivenEveryConcretePickEffect_WhenInspectingIt_ThenItIsAnOperationEffectOfKindPick()
    {
        var effects = ConcreteSubtypes.InstancesOf<PickEffect>();
        var location = Location();

        // Instances are uninitialised (the constructor does not run), so one built per type shows the kind.
        Assert.NotEmpty(effects);
        Assert.All(effects, effect => Assert.IsAssignableFrom<OperationEffect>(effect));
        PickEffect[] built =
        [
            new ReadPickCommitsEffect(Tab, location, PickKind.CherryPick, ["a"]),
            new RunPickEffect(Tab, location, PickKind.Revert, [], null, "main"),
        ];
        Assert.Equal(effects.Count, built.Length);
        Assert.All(built, effect => Assert.Equal(RunningOperationKind.Pick, effect.Kind));
    }

    [Fact]
    public void GivenEveryConcretePickResultCommand_WhenInspectingIt_ThenItEndsTheKindPick()
    {
        var results = ConcreteSubtypes.InstancesOf<PickCommand>().OfType<IOperationResult>().ToArray();

        Assert.Equal(5, results.Length);
        Assert.All(results, result => Assert.Equal(RunningOperationKind.Pick, result.Kind));
    }

    [Fact]
    public void GivenEveryConcreteRebaseEffect_WhenInspectingIt_ThenItIsAnOperationEffectOfKindRebase()
    {
        var effects = ConcreteSubtypes.InstancesOf<RebaseEffect>();
        var location = Location();
        var target = new RebaseTarget(Tab, "a", "b", "c", "d");
        var plan = new QuickRebasePlan(QuickRebaseKind.Drop, "a", "m", RebaseBase.Root, [], "h", null);

        // Instances are uninitialised (the constructor does not run), so one built per type shows the kind.
        Assert.NotEmpty(effects);
        Assert.All(effects, effect => Assert.IsAssignableFrom<OperationEffect>(effect));
        RebaseEffect[] built =
        [
            new CountRebaseMergesEffect(Tab, location, target),
            new RunRebaseEffect(Tab, location, target),
            new PlanQuickRebaseEffect(Tab, location, QuickRebaseKind.Drop, "a"),
            new RunQuickRebaseEffect(Tab, location, new QuickRebaseTarget(Tab, plan, "main", null)),
        ];
        Assert.Equal(effects.Count, built.Length);
        Assert.All(built, effect => Assert.Equal(RunningOperationKind.Rebase, effect.Kind));
    }

    [Fact]
    public void GivenEveryConcreteRebaseResultCommand_WhenInspectingIt_ThenItEndsTheKindRebase()
    {
        var results = ConcreteSubtypes.InstancesOf<RebaseCommand>().OfType<IOperationResult>().ToArray();

        Assert.Equal(7, results.Length);
        Assert.All(results, result => Assert.Equal(RunningOperationKind.Rebase, result.Kind));
    }

    private static RepositoryLocation Location()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-pick-rebase-kind-tests");
        var gitDirectory = Path.Combine(root, ".git");

        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }
}
