using Gitunia.Core;
using Gitunia.Core.Effects;
using Gitunia.Core.Integration;
using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Operations;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Operations;

// The actions only turn the keys into a request for the active tab; the decisions are in OperationReducerTests and
// the work on the repository in the App tests.
public class OperationActionsTests
{
    private readonly List<AppEffect> _effects = [];
    private readonly OperationActions _actions;

    public OperationActionsTests()
    {
        var host = new TestShell().Host;
        host.Dispatch(new OpenRepositoryCommand(Location()));
        var tabId = host.Workspace.ActiveTabId!.Value;
        var info = new OperationInfo(RepositoryOperation.Rebase, "topic", null, null, null, "3333333", "s", null);
        host.Dispatch(new TabCommand(tabId, new OperationLoadedCommand(info)));
        host.EffectRaised += _effects.Add;
        _actions = new OperationActions(host);
    }

    [Fact]
    public void GivenTheContinueKey_WhenPressed_ThenTheContinueStartsOnTheTabsOperation()
    {
        Assert.True(_actions.TryExecute(ActionIds.OperationContinue, null));

        Assert.IsType<ContinueOperationEffect>(Assert.Single(_effects));
    }

    [Fact]
    public void GivenTheAbortKey_WhenPressed_ThenTheDialogOpensAndNothingRunsYet()
    {
        Assert.True(_actions.TryExecute(ActionIds.OperationAbort, null));

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(_effects));
        Assert.Equal("Abort rebase", confirm.Prompt.Title);
    }

    [Fact]
    public void GivenTheSkipKey_WhenPressedAndAnswered_ThenTheSkipStarts()
    {
        Assert.True(_actions.TryExecute(ActionIds.OperationSkip, null));

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(_effects));
        Assert.IsType<OperationSkipConfirmed>(confirm.OnAccepted);
    }

    [Fact]
    public void GivenAnUnrelatedAction_WhenExecuting_ThenTheActionsLeaveItAlone()
    {
        Assert.False(_actions.TryExecute(ActionIds.TagCreate, null));
        Assert.Empty(_effects);
    }

    private static RepositoryLocation Location()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-operation-actions-tests");
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }
}
