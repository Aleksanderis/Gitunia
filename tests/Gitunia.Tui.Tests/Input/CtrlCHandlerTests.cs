using Gitunia.App;
using Gitunia.App.Blame;
using Gitunia.App.Shell;
using Gitunia.Core;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Blame;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Tests.App.Blame;
using Gitunia.Tui.Tests.App.Panes;
using Gitunia.Tui.Tests.Fakes;
using Gitunia.Tui.Tests.Loading;
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.Input;

/// <summary>Ctrl-c copies an active selection first and only a second press within the window quits.</summary>
public sealed class CtrlCHandlerTests : IDisposable
{
    private readonly ManualTimeProvider _clock = new();
    private readonly CountingQuit _quit = new();
    private readonly Dispatcher _host = new(AppState.Empty);
    private readonly List<AppEffect> _effects = [];
    private readonly ListView _list;
    private readonly RangeSource _source = new(20);
    private readonly VisualRangeInput _visual;
    private readonly CtrlCHandler _handler;

    public CtrlCHandlerTests()
    {
        _list = new ListView { Source = _source };
        var root = Path.Combine(Path.GetTempPath(), "gitunia-ctrl-c");
        var gitDirectory = Path.Combine(root, ".git");
        var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
        _host.Dispatch(new OpenRepositoryCommand(location));
        _host.Dispatch(new TabCommand(_host.Workspace.ActiveTabId!.Value, new FocusPaneCommand(PaneKind.Graph)));
        _host.EffectRaised += _effects.Add;
        _visual = new VisualRangeInput(_host, new FakePaneLists(_list));
        var blame = new BlameInput(_host, new BlameStore(), new NoBlameTargets(), GraphGlyphSet.Unicode);
        _handler = new CtrlCHandler(_host, _visual, blame, _quit, _clock);
    }

    public void Dispose()
    {
        _list.Dispose();
        _source.Dispose();
    }

    [Fact]
    public void GivenNoSelection_WhenCtrlCIsPressed_ThenItQuitsAtOnce()
    {
        _handler.Press();

        Assert.Equal(1, _quit.Forced);
        Assert.Empty(_effects.OfType<CopyVisualRangeEffect>());
    }

    [Fact]
    public void GivenASelection_WhenCtrlCIsPressedOnce_ThenItCopiesWithoutQuitting()
    {
        StartSelection();

        _handler.Press();

        Assert.Equal(0, _quit.Forced);
        Assert.Equal(VisualCopyMode.CtrlC, Assert.Single(_effects.OfType<CopyVisualRangeEffect>()).Mode);
    }

    [Fact]
    public void GivenASelection_WhenCtrlCIsPressedTwiceWithinTheWindow_ThenTheSecondQuits()
    {
        StartSelection();
        _handler.Press();
        _clock.Advance(CtrlCHandler.QuitWindow);

        _handler.Press();

        Assert.Equal(1, _quit.Forced);
        Assert.Single(_effects.OfType<CopyVisualRangeEffect>());
    }

    [Fact]
    public void GivenASelection_WhenTheSecondCtrlCComesAfterTheWindow_ThenItCopiesAgainInsteadOfQuitting()
    {
        StartSelection();
        _handler.Press();
        _clock.Advance(CtrlCHandler.QuitWindow + TimeSpan.FromMilliseconds(1));

        _handler.Press();

        Assert.Equal(0, _quit.Forced);
        Assert.Equal(2, _effects.OfType<CopyVisualRangeEffect>().Count());
    }

    [Fact]
    public void GivenACopyThenTheSelectionEnds_WhenCtrlCIsPressed_ThenItQuitsBecauseThereIsNothingToCopy()
    {
        StartSelection();
        _handler.Press();
        _visual.End();

        _handler.Press();

        Assert.Equal(1, _quit.Forced);
    }

    [Fact]
    public void GivenACopyThenANewSelection_WhenCtrlCIsPressedWithinTheWindow_ThenItCopiesTheNewOneInsteadOfQuitting()
    {
        StartSelection();
        _handler.Press();
        _visual.End();
        StartSelection();

        _handler.Press();

        Assert.Equal(0, _quit.Forced);
        Assert.Equal(2, _effects.OfType<CopyVisualRangeEffect>().Count());
    }

    [Fact]
    public void GivenASelectionHeldWhileAnotherModeIsOpen_WhenCtrlCIsPressed_ThenItQuitsWithoutCopying()
    {
        StartSelection();
        _host.SetKeyMode(KeyMode.Insert);

        _handler.Press();

        Assert.Equal(1, _quit.Forced);
        Assert.Empty(_effects.OfType<CopyVisualRangeEffect>());
    }

    private void StartSelection()
    {
        _list.SelectedItem = 4;
        _visual.TryExecute(ActionIds.VisualToggle, null);
    }
}
