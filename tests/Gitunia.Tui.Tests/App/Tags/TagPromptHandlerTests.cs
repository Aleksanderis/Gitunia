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
using Gitunia.Tui.App.Tags;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Tags;

// The handler only translates keys into commands and draws the popup from state; the decisions are in TagReducerTests.
public class TagPromptHandlerTests
{
    private const string Commit = "1111111111111111111111111111111111111111";

    private readonly Dispatcher _host = new(AppState.Empty);
    private readonly FixedSelection _selection = new(new BranchSelection(null, null, Commit));
    private readonly List<AppEffect> _effects = [];
    private readonly TagPromptHandler _handler;

    public TagPromptHandlerTests()
    {
        _host.Dispatch(new OpenRepositoryCommand(Location("alpha")));
        _host.EffectRaised += _effects.Add;
        _handler = new TagPromptHandler(_host, _selection);
    }

    [Fact]
    public void GivenACommitSelected_WhenCreating_ThenTheNamePopupOpensFromState()
    {
        Assert.True(_handler.TryExecute(ActionIds.TagCreate, null));

        Assert.Equal(TagPromptKind.CreateName, _host.Workspace.ActiveTab!.Tags.Prompt!.Kind);
        Assert.Contains("New tag", PopupText.Of(_handler), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheNamePopup_WhenTyping_ThenTheTypedTextIsInStateAndOnScreen()
    {
        _handler.TryExecute(ActionIds.TagCreate, null);

        Type("v1");

        Assert.Equal("v1", _host.Workspace.ActiveTab!.Tags.Prompt!.Text);
        Assert.Contains("> v1_", PopupText.Of(_handler), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheNamePopup_WhenAcceptingAName_ThenTheMessagePopupFollows()
    {
        _handler.TryExecute(ActionIds.TagCreate, null);
        Type("v1");

        Assert.True(_handler.TryExecute(ActionIds.InputAccept, null));

        Assert.Contains("v1: empty makes a lightweight tag", PopupText.Of(_handler), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnOpenPopup_WhenCancelling_ThenItClosesAndTheModeGoesBack()
    {
        _handler.TryExecute(ActionIds.TagCreate, null);

        Assert.True(_handler.TryExecute(ActionIds.Cancel, null));

        Assert.Null(_host.Workspace.ActiveTab!.Tags.Prompt);
        Assert.Equal(string.Empty, PopupText.Of(_handler));
    }

    [Fact]
    public void GivenAnOpenPopup_WhenAnotherActionComes_ThenItIsNotHandled()
    {
        _handler.TryExecute(ActionIds.TagCreate, null);

        Assert.False(_handler.TryExecute(ActionIds.StatusDismiss, null));
    }

    [Fact]
    public void GivenNoPopup_WhenAnotherActionComes_ThenItIsNotHandled()
    {
        Assert.False(_handler.TryExecute(ActionIds.StatusDismiss, null));
        Assert.False(_handler.HandleTextKey(new KeyEvent("a")));
    }

    [Fact]
    public void GivenARowSelectedInTheSidebar_WhenDeleting_ThenTheSidebarRowIsTheTarget()
    {
        _selection.Value = new BranchSelection(SidebarRowKind.LocalBranch, "main", Commit);

        _handler.TryExecute(ActionIds.TagDelete, null);

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(_effects));
        Assert.Equal("Select a tag first", post.Text);
        Assert.Null(_host.Workspace.ActiveTab!.Tags.Prompt);
    }

    [Fact]
    public void GivenTheRemotesLoaded_WhenMovingDownAndAccepting_ThenTheSecondRemoteIsChosen()
    {
        var tabId = _host.Workspace.ActiveTabId!.Value;
        _host.Dispatch(new TagRemotesLoaded(tabId, "v1", true, ["origin", "backup"]));
        Assert.Contains("> origin", PopupText.Of(_handler), StringComparison.Ordinal);

        Assert.True(_handler.HandleTextKey(new KeyEvent(KeyNames.Down)));

        Assert.Equal(1, _host.Workspace.ActiveTab!.Tags.Prompt!.Index);
        Assert.Contains("> backup", PopupText.Of(_handler), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAPopupInATab_WhenAnotherTabBecomesActive_ThenThePopupOfTheLeftTabIsCancelled()
    {
        var first = _host.Workspace.ActiveTabId!.Value;
        _handler.TryExecute(ActionIds.TagCreate, null);
        _host.Dispatch(new OpenRepositoryCommand(Location("beta")));

        Assert.False(_handler.TryExecute(ActionIds.StatusDismiss, null));

        Assert.Null(_host.Workspace.FindTab(first)!.Tags.Prompt);
        Assert.Equal(string.Empty, PopupText.Of(_handler));
    }

    private void Type(string text)
    {
        foreach (var character in text)
        {
            Assert.True(_handler.HandleTextKey(new KeyEvent(character.ToString())));
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
