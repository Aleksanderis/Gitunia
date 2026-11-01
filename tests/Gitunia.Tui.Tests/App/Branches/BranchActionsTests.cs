using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Prompts;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Branches;

// The handlers only turn keys into commands and draw the popup from state; the decisions are in the Core
// BranchReducer tests and PromptReducerTests, the git work in the App BranchEffectHandler tests.
public class BranchActionsTests
{
    private const string SelectNotice = "Select a local branch (sidebar) or a commit with one branch on it";
    private const string Commit = "1111111111111111111111111111111111111111";

    private readonly Dispatcher _host;
    private readonly FixedSelection _selection = new(new BranchSelection(null, null, Commit));
    private readonly List<AppEffect> _effects = [];
    private readonly BranchActions _actions;
    private readonly PromptInputHandler _prompt;

    public BranchActionsTests()
    {
        _host = new TestShell().Host;
        _host.Dispatch(new OpenRepositoryCommand(Location("alpha")));
        _host.EffectRaised += _effects.Add;
        _actions = new BranchActions(_host, _selection);
        _prompt = new PromptInputHandler(_host);
    }

    public static TheoryData<string, string> Actions =>
        new()
        {
            { ActionIds.BranchCreate.Value, "New branch" },
            { ActionIds.BranchCreateOnly.Value, "New branch" },
            { ActionIds.BranchSwitch.Value, "Switch branch" },
            { ActionIds.BranchRename.Value, SelectNotice },
            { ActionIds.BranchDelete.Value, SelectNotice },
            { ActionIds.BranchSetUpstream.Value, SelectNotice },
            { ActionIds.BranchUnsetUpstream.Value, SelectNotice },
            { ActionIds.BranchFastForward.Value, SelectNotice },
        };

    // Each key reaches the reducer as its own command: a prompt opens, or the reducer's refusal is posted.
    [Theory]
    [MemberData(nameof(Actions))]
    public void GivenABranchKey_WhenExecuted_ThenItsCommandReachesTheReducer(string action, string expected)
    {
        Assert.True(_actions.TryExecute(new ActionId(action), null));

        var posted = _effects.OfType<PostStatusEffect>().Select(post => post.Text).SingleOrDefault();
        Assert.Equal(expected, _host.State.Shell.Prompt?.Title ?? posted);
    }

    [Fact]
    public void GivenCreateWithoutCheckout_WhenExecuted_ThenThePromptSaysNotCheckedOut()
    {
        _selection.Value = new BranchSelection(SidebarRowKind.LocalBranch, "topic", Commit);

        _actions.TryExecute(ActionIds.BranchCreateOnly, null);

        Assert.Equal("At topic (not checked out)", _host.State.Shell.Prompt!.Context);
        Assert.Equal(KeyMode.Insert, _host.State.Shell.Mode);
        Assert.Contains("New branch", PopupText.Of(_prompt), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheNamePopup_WhenTypingAndCancelling_ThenTheTextIsInStateAndTheModeGoesBack()
    {
        _actions.TryExecute(ActionIds.BranchCreate, null);

        foreach (var character in "nope")
        {
            Assert.True(_prompt.HandleTextKey(new KeyEvent(character.ToString())));
        }

        Assert.Contains("> nope_", PopupText.Of(_prompt), StringComparison.Ordinal);
        Assert.True(_prompt.TryExecute(ActionIds.Cancel, null));
        Assert.Null(_host.State.Shell.Prompt);
        Assert.Equal(KeyMode.Normal, _host.State.Shell.Mode);
    }

    [Fact]
    public void GivenAnActionThatIsNotABranchAction_WhenExecuted_ThenItIsLeftToTheNextHandler()
    {
        Assert.False(_actions.TryExecute(ActionIds.StatusDismiss, null));
        Assert.Empty(_effects);
    }

    [Fact]
    public void GivenARemoteBranchRow_WhenDeleting_ThenTheSyncActionsGetItAndNoBranchCommandRuns()
    {
        var tabId = _host.Workspace.ActiveTabId!.Value;
        _host.Dispatch(new TabCommand(tabId, new RefsLoadedCommand([RemoteBranch("origin/topic")])));
        _selection.Value = new BranchSelection(SidebarRowKind.RemoteBranch, "origin/topic", Commit);

        Assert.False(_actions.TryExecute(ActionIds.BranchDelete, null));

        Assert.Empty(_effects);
    }

    [Fact]
    public void GivenNoTab_WhenExecuted_ThenNothingIsHandled()
    {
        var empty = new TestShell().Host;
        var actions = new BranchActions(empty, _selection);

        Assert.False(actions.TryExecute(ActionIds.BranchCreate, null));
    }

    private static GitRef RemoteBranch(string name) => new(
        $"refs/remotes/{name}",
        name,
        RefKind.RemoteBranch,
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
        var root = Path.Combine(Path.GetTempPath(), "gitunia-branch-handler-tests", name);
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    private sealed class FixedSelection(BranchSelection value) : IBranchSelectionSource
    {
        public BranchSelection Value { get; set; } = value;

        public BranchSelection Current() => Value;
    }
}
