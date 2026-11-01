using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Pick;
using Gitunia.Core.Running;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Pick;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Pick;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Pick;

// The handler only translates keys into commands and shows the question from state; the decisions are in
// PickReducerTests and PickAppReducerTests.
public class PickActionsTests
{
    private const string First = "1111111111111111111111111111111111111111";
    private const string Second = "2222222222222222222222222222222222222222";

    private readonly Dispatcher _host;
    private readonly FixedSelection _selection = new(new PickSelection(null, [First]));
    private readonly List<AppEffect> _effects = [];
    private readonly PickActions _actions;

    public PickActionsTests()
    {
        _host = new TestShell().Host;
        _host.Dispatch(new OpenRepositoryCommand(Location("alpha")));
        _host.EffectRaised += _effects.Add;
        _actions = new PickActions(_host, _selection);
    }

    [Fact]
    public void GivenAGraphCommit_WhenCherryPicking_ThenTheCommitsAreReadAndTheRangeEnds()
    {
        Assert.True(_actions.TryExecute(ActionIds.PickCherry, null));

        var read = Assert.IsType<ReadPickCommitsEffect>(Assert.Single(_effects));
        Assert.Equal([First], read.Hashes);
        Assert.Equal(PickKind.CherryPick, read.PickKind);
        Assert.Equal(1, _selection.RangeEnds);
    }

    [Fact]
    public void GivenARange_WhenReverting_ThenTheHashesKeepTheGraphOrder()
    {
        _selection.Value = new PickSelection(null, [First, Second]);

        Assert.True(_actions.TryExecute(ActionIds.PickRevert, null));

        var read = Assert.IsType<ReadPickCommitsEffect>(Assert.Single(_effects));
        Assert.Equal([First, Second], read.Hashes);
        Assert.Equal(PickKind.Revert, read.PickKind);
    }

    [Fact]
    public void GivenARefusal_WhenPicking_ThenTheRangeStaysAndNothingIsRead()
    {
        _selection.Value = new PickSelection(SidebarRowKind.Stash, [First]);

        Assert.True(_actions.TryExecute(ActionIds.PickCherry, null));

        Assert.DoesNotContain(_effects, effect => effect is ReadPickCommitsEffect);
        Assert.Equal(0, _selection.RangeEnds);
        Assert.Equal(PickReducer.SelectNotice, Assert.Single(_effects.OfType<PostStatusEffect>()).Text);
    }

    [Fact]
    public void GivenAnUnrelatedAction_WhenExecuting_ThenItIsNotHandled()
    {
        Assert.False(_actions.TryExecute(ActionIds.Cancel, null));
        Assert.Empty(_effects);
    }

    [Fact]
    public void GivenNoQuestion_WhenATextKeyArrives_ThenItIsNotHandled()
    {
        Assert.False(_actions.HandleTextKey(new KeyEvent("1")));
        Assert.Null(_actions.Prompt);
        Assert.Equal(string.Empty, _actions.StatusLine);
    }

    [Fact]
    public void GivenTheQuestion_WhenItIsShown_ThenTheOverlayAndTheStatusLineListTheParents()
    {
        OpenQuestion();

        var prompt = _actions.Prompt!;
        Assert.Equal("revert merge commit 1111111: keep the mainline of", prompt.Title);
        Assert.Equal(2, prompt.Parents.Count);
        Assert.Contains("topic two", prompt.Parents[1], StringComparison.Ordinal);
        Assert.Equal(2, prompt.UsableParents);
        Assert.Contains("[1]", _actions.StatusLine, StringComparison.Ordinal);
        Assert.Contains("[2]", _actions.StatusLine, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheQuestion_WhenADigitIsPressed_ThenThatParentIsChosen()
    {
        OpenQuestion();
        _effects.Clear();

        Assert.True(_actions.HandleTextKey(new KeyEvent("2")));

        var run = Assert.IsType<RunPickEffect>(_effects.OfType<RunPickEffect>().Single());
        Assert.Equal(2, run.Mainline);
        Assert.Null(_actions.Prompt);
    }

    [Fact]
    public void GivenTheQuestion_WhenALetterIsPressed_ThenItIsSwallowedAndTheQuestionStays()
    {
        OpenQuestion();
        _effects.Clear();

        Assert.True(_actions.HandleTextKey(new KeyEvent("x")));

        Assert.Empty(_effects);
        Assert.NotNull(_actions.Prompt);
    }

    [Fact]
    public void GivenTheQuestion_WhenAParentRowIsClicked_ThenItRunsWithThatParentLikeTheKey()
    {
        OpenQuestion();
        _effects.Clear();

        Assert.True(_actions.TryExecute(ActionIds.PickMainline, 1));

        Assert.Equal(1, Assert.Single(_effects.OfType<RunPickEffect>()).Mainline);
        Assert.Null(_actions.Prompt);
    }

    [Fact]
    public void GivenTheQuestion_WhenAnOutOfRangeRowIsClicked_ThenItStaysOpenAndNothingRuns()
    {
        OpenQuestion();
        _effects.Clear();

        Assert.True(_actions.TryExecute(ActionIds.PickMainline, 3));

        Assert.NotNull(_actions.Prompt);
        Assert.DoesNotContain(_effects, effect => effect is OperationEffect);
        Assert.Contains("Choose a parent from 1 to 2", Assert.Single(_effects.OfType<PostStatusEffect>()).Text);
    }

    [Fact]
    public void GivenTheQuestion_WhenEscapeIsPressed_ThenItClosesAndNothingRuns()
    {
        OpenQuestion();
        _effects.Clear();

        Assert.True(_actions.TryExecute(ActionIds.Cancel, null));

        Assert.Null(_actions.Prompt);
        Assert.DoesNotContain(_effects, effect => effect is OperationEffect);
    }

    [Fact]
    public void GivenTheQuestion_WhenEnterIsPressed_ThenItIsConsumedAndTheQuestionStays()
    {
        OpenQuestion();

        Assert.True(_actions.TryExecute(ActionIds.InputAccept, null));

        Assert.NotNull(_actions.Prompt);
    }

    [Fact]
    public void GivenTheQuestion_WhenAnotherTabBecomesActive_ThenItIsGone()
    {
        OpenQuestion();

        _host.Dispatch(new OpenRepositoryCommand(Location("beta")));

        Assert.Null(_actions.Prompt);
        Assert.False(_actions.HandleTextKey(new KeyEvent("1")));
    }

    // The question is what the reducer opens once the commits are read, so the state carries it as in the app.
    private void OpenQuestion()
    {
        var tabId = _host.Workspace.ActiveTabId!.Value;
        var parents = new[] { new PickParent(First, "main two"), new PickParent(Second, "topic two") };
        _host.Dispatch(new PickCommitsRead(tabId, PickKind.Revert, [new PickCommit(First, "merge", parents)]));
    }

    private static RepositoryLocation Location(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-pick-view-tests", name);
        var gitDirectory = Path.Combine(root, ".git");

        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    private sealed class FixedSelection(PickSelection value) : IPickSelectionSource
    {
        public PickSelection Value { get; set; } = value;

        public int RangeEnds { get; private set; }

        public PickSelection Current() => Value;

        public void EndRange() => RangeEnds++;
    }
}
