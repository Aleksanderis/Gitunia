using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Keymap.Repeat;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.App.Panes;

[Trait("Os", "Independent")]
public class RepeatControllerTests
{
    private readonly List<ActionId> _executed = [];
    private readonly RepeatController _repeat;
    private readonly KeyController _keys;
    private TabId? _tab = new(1);
    private PaneKind _focused = PaneKind.Graph;
    private bool _blocked;

    public RepeatControllerTests()
    {
        var table = DefaultKeymap.Create();
        _keys = new KeyController(new KeymapEngine(table), RouteAction);
        _repeat = new RepeatController(table, () => _tab, () => _focused, () => _blocked, RouteAction);
        _keys.Interceptor = _repeat.TryHandle;
    }

    [Fact]
    public void GivenAResizeStep_WhenItRuns_ThenTheResizeModeStartsOnThatPane()
    {
        Press("Ctrl-w", ">");

        Assert.Equal(new RepeatMode(RepeatKind.Resize, new TabId(1), PaneKind.Graph), _repeat.Active);
    }

    [Fact]
    public void GivenTheResizeMode_WhenPressingTheBareStepKeys_ThenEachRunsItsResizeAndTheModeStays()
    {
        Press("Ctrl-w", ">");

        Press(">", "<", "+", "-", "=");

        Assert.Equal(
            [
                ActionIds.PaneWider,
                ActionIds.PaneWider,
                ActionIds.PaneNarrower,
                ActionIds.PaneTaller,
                ActionIds.PaneShorter,
                ActionIds.PaneResetSizes,
            ],
            _executed);
        Assert.NotNull(_repeat.Active);
    }

    [Fact]
    public void GivenTheResizeModeInTheDiff_WhenPressingPlus_ThenThePaneGrowsTallerInsteadOfAddingContext()
    {
        _focused = PaneKind.Diff;
        Press("Ctrl-w", ">");

        Press("+");

        Assert.Equal(ActionIds.PaneTaller, _executed[^1]);
    }

    [Fact]
    public void GivenTheContextMode_WhenPressingPlusAndMinus_ThenTheContextStepsRepeat()
    {
        _focused = PaneKind.Diff;
        Press("+");

        Press("+", "-");

        Assert.Equal([ActionIds.DiffMoreContext, ActionIds.DiffMoreContext, ActionIds.DiffLessContext], _executed);
        Assert.Equal(RepeatKind.Context, _repeat.Active?.Kind);
    }

    [Fact]
    public void GivenTheMode_WhenPressingEscape_ThenItEndsAndNothingElseRuns()
    {
        Press("Ctrl-w", ">");
        _executed.Clear();

        Press("Esc");

        Assert.Null(_repeat.Active);
        Assert.Empty(_executed);
    }

    [Fact]
    public void GivenTheMode_WhenPressingAnotherKey_ThenTheModeEndsAndTheKeyActsNormally()
    {
        Press("Ctrl-w", ">");
        _executed.Clear();

        Press("j");

        Assert.Null(_repeat.Active);
        Assert.Equal([ActionIds.ListDown], _executed);
    }

    [Fact]
    public void GivenTheEndedMode_WhenPressingABareStepKey_ThenItIsNotAStep()
    {
        Press("Ctrl-w", ">");
        Press("Esc");
        _executed.Clear();

        Press(">");

        Assert.DoesNotContain(ActionIds.PaneWider, _executed);
    }

    [Fact]
    public void GivenTheMode_WhenFocusMoves_ThenItEnds()
    {
        Press("Ctrl-w", ">");

        _focused = PaneKind.Sidebar;

        Assert.Null(_repeat.Active);
    }

    [Fact]
    public void GivenTheMode_WhenAnotherTabBecomesActive_ThenItEnds()
    {
        Press("Ctrl-w", ">");

        _tab = new TabId(2);

        Assert.Null(_repeat.Active);
    }

    [Fact]
    public void GivenTheMode_WhenAnOverlayOpens_ThenItEndsAndTheStepKeysAreLeftToTheOverlay()
    {
        Press("Ctrl-w", ">");
        _executed.Clear();

        _blocked = true;
        var taken = _repeat.TryHandle(new KeyEvent(">"), KeymapState.Initial);

        Assert.False(taken);
        Assert.Empty(_executed);
    }

    [Fact]
    public void GivenAToggleAction_WhenItRuns_ThenNoModeStarts()
    {
        _focused = PaneKind.Diff;

        Press(" ", "v", "w");

        Assert.Null(_repeat.Active);
    }

    [Fact]
    public void GivenAPaneThatHasNoDiffOptions_WhenAddingContext_ThenNoModeStarts()
    {
        _focused = PaneKind.Graph;

        _repeat.Started(ActionIds.DiffMoreContext);

        Assert.Null(_repeat.Active);
    }

    [Fact]
    public void GivenAStepThatTheModeLeftAlone_WhenAnUnrelatedActionRuns_ThenTheModeStays()
    {
        Press("Ctrl-w", ">");

        _repeat.Started(ActionIds.ListDown);

        Assert.NotNull(_repeat.Active);
    }

    private void RouteAction(ActionId action, int? count)
    {
        _executed.Add(action);
        _repeat.Started(action);
    }

    private void Press(params string[] keys)
    {
        foreach (var key in keys)
        {
            var isCtrl = key.StartsWith("Ctrl-", StringComparison.Ordinal);
            var name = isCtrl ? key["Ctrl-".Length..] : key;
            var modifiers = isCtrl ? KeyModifiers.Ctrl : KeyModifiers.None;
            _keys.Handle(new KeyEvent(name, modifiers), ViewOfFocus());
        }
    }

    private string ViewOfFocus() => _focused switch
    {
        PaneKind.Diff => DefaultKeymap.DiffView,
        PaneKind.Sidebar => DefaultKeymap.SidebarView,
        _ => DefaultKeymap.GraphView,
    };
}
