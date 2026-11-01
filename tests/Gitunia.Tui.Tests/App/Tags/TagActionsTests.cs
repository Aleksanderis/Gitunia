using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Tags;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Prompts;
using Gitunia.Tui.App.Tags;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Tags;

// The handlers only translate keys into commands and draw the popup from state; the decisions are in TagReducerTests
// and PromptReducerTests.
public class TagActionsTests
{
    private const string Commit = "1111111111111111111111111111111111111111";

    private readonly Dispatcher _host;
    private readonly FixedSelection _selection = new(new BranchSelection(null, null, Commit));
    private readonly List<AppEffect> _effects = [];
    private readonly TagActions _actions;
    private readonly PromptInputHandler _prompt;

    public TagActionsTests()
    {
        _host = new TestShell().Host;
        _host.Dispatch(new OpenRepositoryCommand(Location("alpha")));
        _host.EffectRaised += _effects.Add;
        _actions = new TagActions(_host, _selection);
        _prompt = new PromptInputHandler(_host);
    }

    [Fact]
    public void GivenACommitSelected_WhenCreating_ThenTheNamePopupOpensFromState()
    {
        Assert.True(_actions.TryExecute(ActionIds.TagCreate, null));

        Assert.Equal("New tag", _host.State.Shell.Prompt!.Title);
        Assert.Equal(KeyMode.Insert, _host.State.Shell.Mode);
        Assert.Contains("New tag", PopupText.Of(_prompt), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheNamePopup_WhenTyping_ThenTheTypedTextIsInStateAndOnScreen()
    {
        _actions.TryExecute(ActionIds.TagCreate, null);

        Type("v1");

        Assert.Equal("v1", _host.State.Shell.Prompt!.Text);
        Assert.Contains("> v1_", PopupText.Of(_prompt), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheNamePopup_WhenAcceptingAName_ThenTheMessagePopupFollows()
    {
        _actions.TryExecute(ActionIds.TagCreate, null);
        Type("v1");

        Assert.True(_prompt.TryExecute(ActionIds.InputAccept, null));

        Assert.Contains("v1: empty makes a lightweight tag", PopupText.Of(_prompt), StringComparison.Ordinal);
        Assert.Equal(KeyMode.Insert, _host.State.Shell.Mode);
    }

    [Fact]
    public void GivenAnOpenPopup_WhenCancelling_ThenItClosesAndTheModeGoesBack()
    {
        _actions.TryExecute(ActionIds.TagCreate, null);

        Assert.True(_prompt.TryExecute(ActionIds.Cancel, null));

        Assert.Null(_host.State.Shell.Prompt);
        Assert.Equal(KeyMode.Normal, _host.State.Shell.Mode);
        Assert.Equal(string.Empty, PopupText.Of(_prompt));
    }

    [Fact]
    public void GivenAnOpenPopup_WhenAnotherActionComes_ThenItIsNotHandled()
    {
        _actions.TryExecute(ActionIds.TagCreate, null);

        Assert.False(_prompt.TryExecute(ActionIds.StatusDismiss, null));
    }

    [Fact]
    public void GivenNoPopup_WhenAnActionOrKeyComes_ThenTheHandlerLeavesItAlone()
    {
        Assert.False(_prompt.TryExecute(ActionIds.InputAccept, null));
        Assert.False(_prompt.TryExecute(ActionIds.Cancel, null));
        Assert.False(_prompt.HandleTextKey(new KeyEvent("a")));
        Assert.False(_actions.TryExecute(ActionIds.StatusDismiss, null));
    }

    [Fact]
    public void GivenARowSelectedInTheSidebar_WhenDeleting_ThenTheSidebarRowIsTheTarget()
    {
        _selection.Value = new BranchSelection(SidebarRowKind.LocalBranch, "main", Commit);

        _actions.TryExecute(ActionIds.TagDelete, null);

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(_effects));
        Assert.Equal("Select a tag first", post.Text);
        Assert.Null(_host.State.Shell.Prompt);
    }

    [Fact]
    public void GivenTheRemotesLoaded_WhenMovingDownAndAccepting_ThenTheSecondRemoteIsChosen()
    {
        var tabId = _host.Workspace.ActiveTabId!.Value;
        _host.Dispatch(new TagRemotesLoaded(tabId, "v1", true, ["origin", "backup"]));
        Assert.Contains("> origin", PopupText.Of(_prompt), StringComparison.Ordinal);

        Assert.True(_prompt.HandleTextKey(new KeyEvent(KeyNames.Down)));

        Assert.Equal(1, _host.State.Shell.Prompt!.Picker!.Index);
        Assert.Contains("> backup", PopupText.Of(_prompt), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAPopupInATab_WhenAnotherTabBecomesActive_ThenThePopupOfTheLeftTabIsCancelled()
    {
        _actions.TryExecute(ActionIds.TagCreate, null);
        _host.Dispatch(new OpenRepositoryCommand(Location("beta")));

        Assert.False(_prompt.TryExecute(ActionIds.Cancel, null));

        Assert.Null(_host.State.Shell.Prompt);
        Assert.Equal(KeyMode.Normal, _host.State.Shell.Mode);
        Assert.Equal(string.Empty, PopupText.Of(_prompt));
    }

    private void Type(string text)
    {
        foreach (var character in text)
        {
            Assert.True(_prompt.HandleTextKey(new KeyEvent(character.ToString())));
        }
    }

    private static RepositoryLocation Location(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-tag-handler-tests", name);
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    private sealed class FixedSelection(BranchSelection value) : IBranchSelectionSource
    {
        public BranchSelection Value { get; set; } = value;

        public BranchSelection Current() => Value;
    }
}
