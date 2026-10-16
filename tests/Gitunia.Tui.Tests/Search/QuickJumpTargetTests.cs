using Gitunia.Core.Jump.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Search;
using Xunit;

namespace Gitunia.Tui.Tests.Search;

/// <summary>The quick jump prompt on lists other than the graph: it only knows the target it is given.</summary>
public sealed class QuickJumpTargetTests : IDisposable
{
    private readonly TabSession _session;
    private readonly List<KeyMode> _modes = [];
    private readonly List<string?> _shown = [];
    private readonly List<int> _jumps = [];
    private PaneKind _focused = PaneKind.WorkingUnstaged;
    private string? _unavailable;
    private bool _hasTarget = true;

    public QuickJumpTargetTests()
    {
        var location = new RepositoryLocation(Path.GetTempPath(), Path.GetTempPath(), Path.GetTempPath(), false, false);
        _session = new TabSession(new TabId(1), GraphGlyphSet.Unicode, new CommitDetailsStore(TestGit.Services.CommitDetails, location, action => action(), TimeSpan.Zero));
        Controller = new QuickJumpController(new QuickJumpHost(
            () => _session,
            () => _focused,
            OpenTarget,
            _modes.Add,
            () => { }));
    }

    private QuickJumpController Controller { get; }

    [Fact]
    public void GivenAFileList_WhenOpeningTheJump_ThenInsertModeIsOnAndTheListShowsLabelsForEmptyText()
    {
        Controller.TryExecute(ActionIds.QuickJump, null);

        Assert.Equal([KeyMode.Insert], _modes);
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
        Assert.Equal(KeyMode.Normal, _modes[^1]);
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
        Assert.Equal(KeyMode.Normal, _modes[^1]);
    }

    [Fact]
    public void GivenAListThatCannotBeJumpedIn_WhenOpeningTheJump_ThenTheReasonIsTheNoticeAndNothingOpens()
    {
        _unavailable = "Not here";

        Assert.True(Controller.TryExecute(ActionIds.QuickJump, null));

        Assert.Equal("Not here", Controller.Notice);
        Assert.Empty(_modes);
        Assert.Empty(_shown);
    }

    [Fact]
    public void GivenAPaneWithoutAJump_WhenOpeningTheJump_ThenItSaysSo()
    {
        _hasTarget = false;

        Assert.True(Controller.TryExecute(ActionIds.QuickJump, null));

        Assert.Equal("Quick jump is not available here", Controller.Notice);
        Assert.Empty(_modes);
    }

    [Fact]
    public void GivenAnOpenJump_WhenFocusMovesToAnotherPane_ThenTheJumpClosesAndTheKeyIsNotTaken()
    {
        Controller.TryExecute(ActionIds.QuickJump, null);
        _focused = PaneKind.WorkingStaged;

        Assert.False(Controller.HandleTextKey(new KeyEvent("a")));

        Assert.Null(_shown[^1]);
        Assert.Equal(KeyMode.Normal, _modes[^1]);
        Assert.Equal(string.Empty, Controller.StatusLine);
    }

    [Fact]
    public void GivenAnOpenJump_WhenAClickMovesFocus_ThenCloseIfStaleEndsIt()
    {
        Controller.TryExecute(ActionIds.QuickJump, null);
        _focused = PaneKind.WorkingUnstagedDiff;

        Controller.CloseIfStale();

        Assert.Equal(KeyMode.Normal, _modes[^1]);
        Assert.Null(_shown[^1]);
    }

    [Fact]
    public void GivenNoOpenJump_WhenAskedToCloseIfStale_ThenNothingHappens()
    {
        Controller.CloseIfStale();

        Assert.Empty(_modes);
    }

    public void Dispose() => _session.Dispose();

    private ListJumpTarget? OpenTarget() => !_hasTarget
        ? null
        : new ListJumpTarget(
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
