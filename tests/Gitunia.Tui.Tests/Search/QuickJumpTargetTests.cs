using Gitunia.Tui.Tests.App;
using Gitunia.App.Loading.Commits;
using Gitunia.App.Tabs;
using Gitunia.Core;
using Gitunia.Core.Effects;
using Gitunia.Core.Jump;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Search;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.Search;

/// <summary>The quick jump prompt on lists other than the graph: it only knows the target it is given.</summary>
public sealed class QuickJumpTargetTests : IDisposable
{
    private readonly TerminalTabSession _session;
    private readonly TestShell _shell = new();
    private readonly List<string?> _shown = [];
    private readonly List<int> _jumps = [];
    private readonly List<string> _notices = [];
    private string? _unavailable;
    private bool _hasTarget = true;

    public QuickJumpTargetTests()
    {
        var location = new RepositoryLocation(Path.GetTempPath(), Path.GetTempPath(), Path.GetTempPath(), false, false);
        _shell.Host.Dispatch(new OpenRepositoryCommand(location));
        _session = new TerminalTabSession(
            new TabSessionParts(
                _shell.Host.Workspace.ActiveTabId!.Value,
                new CommitDetailsStore(TestGit.Services.CommitDetails, location, InlineUi.Instance, TimeSpan.Zero)),
            GraphGlyphSet.Unicode);
        Focus(PaneKind.WorkingUnstaged);
        Controller = new QuickJumpInput(_shell.Host, new Host(this));
        _shell.Host.EffectRaised += Run;
        _shell.Modes.Clear();
    }

    private QuickJumpInput Controller { get; }

    private string Notice => _notices.LastOrDefault() ?? string.Empty;

    [Fact]
    public void GivenAFileList_WhenOpeningTheJump_ThenInsertModeIsOnAndTheListShowsLabelsForEmptyText()
    {
        Controller.TryExecute(ActionIds.QuickJump, null);

        Assert.Equal([KeyMode.Insert], _shell.Modes);
        Assert.Equal([string.Empty], _shown);
        Assert.StartsWith("jump _", Controller.StatusLine, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTypedText_WhenPressingALabel_ThenTheListCursorJumpsAndTheLabelsGoAway()
    {
        Controller.TryExecute(ActionIds.QuickJump, null);
        Controller.HandleTextKey(new KeyEvent("t"));
        Controller.HandleTextKey(new KeyEvent("x"));

        // The fake target labels rows 3 and 1 for any text, with the letters 'q' and 'w'.
        Assert.True(Controller.HandleTextKey(new KeyEvent("w")));

        Assert.Equal([1], _jumps);
        Assert.Equal(KeyMode.Normal, _shell.Modes[^1]);
        Assert.Null(_shown[^1]);
        Assert.Equal(string.Empty, Controller.StatusLine);
    }

    [Fact]
    public void GivenTypedText_WhenPressingEnter_ThenTheFirstLabelledRowIsTheJump()
    {
        Controller.TryExecute(ActionIds.QuickJump, null);
        Controller.HandleTextKey(new KeyEvent("t"));

        Controller.TryExecute(ActionIds.InputAccept, null);

        Assert.Equal([3], _jumps);
    }

    [Fact]
    public void GivenAnOpenJump_WhenCancelling_ThenTheLabelsGoAwayWithoutJumping()
    {
        Controller.TryExecute(ActionIds.QuickJump, null);
        Controller.HandleTextKey(new KeyEvent("t"));

        Assert.True(Controller.TryExecute(ActionIds.Cancel, null));

        Assert.Empty(_jumps);
        Assert.Null(_shown[^1]);
        Assert.Equal(KeyMode.Normal, _shell.Modes[^1]);
    }

    [Fact]
    public void GivenAListThatCannotBeJumpedIn_WhenOpeningTheJump_ThenTheReasonIsTheNoticeAndNothingOpens()
    {
        _unavailable = "Not here";

        Assert.True(Controller.TryExecute(ActionIds.QuickJump, null));

        Assert.Equal("Not here", Notice);
        Assert.Empty(_shell.Modes);
        Assert.Empty(_shown);
    }

    [Fact]
    public void GivenAPaneWithoutAJump_WhenOpeningTheJump_ThenItSaysSo()
    {
        _hasTarget = false;

        Assert.True(Controller.TryExecute(ActionIds.QuickJump, null));

        Assert.Equal("Quick jump is not available here", Notice);
        Assert.Empty(_shell.Modes);
    }

    [Fact]
    public void GivenAnOpenJump_WhenFocusMovesToAnotherPane_ThenTheJumpClosesAndTheKeyIsNotTaken()
    {
        Controller.TryExecute(ActionIds.QuickJump, null);
        Focus(PaneKind.WorkingStaged);

        Assert.False(Controller.HandleTextKey(new KeyEvent("a")));

        Assert.Null(_shown[^1]);
        Assert.Equal(KeyMode.Normal, _shell.Modes[^1]);
        Assert.Equal(string.Empty, Controller.StatusLine);
    }

    [Fact]
    public void GivenAnOpenJump_WhenAClickMovesFocus_ThenCloseIfStaleEndsIt()
    {
        Controller.TryExecute(ActionIds.QuickJump, null);
        Focus(PaneKind.Detail);

        Controller.CloseIfStale();

        Assert.Equal(KeyMode.Normal, _shell.Modes[^1]);
        Assert.Null(_shown[^1]);
    }

    [Fact]
    public void GivenNoOpenJump_WhenAskedToCloseIfStale_ThenNothingHappens()
    {
        Controller.CloseIfStale();

        Assert.Empty(_shell.Modes);
    }

    public void Dispose() => _session.Dispose();

    private void Run(AppEffect effect)
    {
        if (effect is JumpToRowEffect jump)
        {
            Controller.JumpTo(jump.Row);
        }
        else if (effect is PostStatusEffect post)
        {
            _notices.Add(post.Text);
        }
    }

    private void Focus(PaneKind pane) =>
        _shell.Host.Dispatch(new TabCommand(_session.TabId, new FocusPaneCommand(pane)));

    private ListJumpTarget? OpenListTarget()
    {
        if (!_hasTarget)
        {
            return null;
        }

        return new ListJumpTarget(
            PaneKind.WorkingUnstaged,
            _ =>
            [
                new JumpLabel(3, 0, 1, 'q'),
                new JumpLabel(1, 0, 1, 'w'),
            ],
            _shown.Add,
            _jumps.Add,
            () => _unavailable);
    }

    private sealed class Host(QuickJumpTargetTests test) : IQuickJumpHost
    {
        public IJumpTarget? OpenTarget() => test.OpenListTarget();

        public void Redraw()
        {
        }
    }
}
