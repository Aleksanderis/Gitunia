using System.Reflection;
using Gitunia.App;
using Gitunia.Core.Input;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Keymap.Repeat;
using Gitunia.Core.Overlays;
using Gitunia.Core.Shell;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Platform;
using Gitunia.Tui.App.Shell;
using Gitunia.Tui.Rendering.Panes;
using Terminal.Gui.Drivers;
using Xunit;

namespace Gitunia.Tui.Tests.App.Startup;

/// <summary>A composed and wired shell on a headless driver reacting to a state change; the order of the state subscribers ShellWiring adds is pinned too.</summary>
public sealed class ShellWiringTests : IDisposable
{
    private const int ScreenWidth = 120;
    private const int ScreenHeight = 30;

    private readonly string _root = Directory.CreateTempSubdirectory("gitunia-wiring-").FullName;
    private readonly ShellComposition _shell;
    private readonly List<string> _wired;

    public ShellWiringTests()
    {
        var capabilities = new ConsoleCapabilities(IsInteractive: true, Color: ColorDepth.TrueColor, Unicode: true);
        _shell = new ShellComposition(
            TestGit.Services,
            new TestPlatform(_root, capabilities),
            capabilities,
            new TuiLaunch(DriverRegistry.Names.ANSI, _root));
        var before = Subscribers();
        new ShellWiring(_shell).Connect();
        _wired = [.. Subscribers().Skip(before.Count)];
    }

    public void Dispose()
    {
        _shell.Dispose();

        // Best effort (#1250): the shell only cancels its background git work on dispose, it does not wait for the
        // processes to exit, and Windows refuses to delete a directory a process is still running in. A leftover
        // temp directory is harmless; failing the test over it is not.
        try
        {
            Directory.Delete(_root, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // See above.
        }
    }

    [Fact]
    public void WhenWired_ThenTheStateSubscribersRunInTheDocumentedOrder()
    {
        // Presenters subscribe in their constructors, the views one by one: the Changes presenter must run before
        // ApplyFocus, which needs the panes it shows. Swapping any two entries changes what the user sees.
        string[] expected =
        [
            "StashViewController.Reconcile",
            "OverlayPresenter.RefreshOnStateChange",
            "SidebarPresenter.Sync",
            "WorkingTreePresenter.Sync",
            "MainView.RefreshChrome",
            "GraphTabBinder.Sync",
            "PaneFocusCoordinator.ApplyFocus",
            "DetailPresenter.Follow",
            "PaneSizeDragger.OnStateChanged",
            "QuickJumpInput.CloseIfStale",
            "PickerPresenter.OnStateChanged",
        ];

        Assert.Equal(expected, _wired);
    }

    private List<string> Subscribers()
    {
        // Reads the backing field of the field-like event; it must stay one (the assert names it if it does not).
        var field = typeof(Dispatcher).GetField("Changed", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var handler = (Delegate?)field.GetValue(_shell.Foundation.Host);
        return handler is null
            ? []
            : [.. handler.GetInvocationList().Select(d => $"{d.Target!.GetType().Name}.{d.Method.Name}")];
    }

    [Fact]
    public void GivenAGraphTab_WhenOpeningTheWorkingTree_ThenTheChangesPaneIsShownAndFocused()
    {
        var host = _shell.Foundation.Host;
        var gitDirectory = Path.Combine(_root, ".git");
        host.Dispatch(new OpenRepositoryCommand(new RepositoryLocation(_root, gitDirectory, gitDirectory, false, false)));
        var tabId = host.Workspace.ActiveTabId!.Value;

        host.Dispatch(new TabCommand(tabId, new OpenWorkingTreeCommand()));

        var unstaged = _shell.Foundation.Panes.WorkingUnstaged;
        Assert.True(unstaged.Visible);
        Assert.True(unstaged.HasFocus);
    }

    [Fact]
    public void GivenTheChangesView_WhenTheResizeKeysRun_ThenTheListsResizeAndTheResetRestoresThem()
    {
        OpenChangesView();
        var automatic = ListColumns();

        PressAction(ActionIds.PaneWider);
        var wider = ListColumns();
        var repeatAfterStep = _shell.Keys.Repeat.Active;
        PressAction(ActionIds.PaneNarrower);
        PressAction(ActionIds.PaneNarrower);
        var narrower = ListColumns();
        PressAction(ActionIds.PaneResetSizes);

        Assert.True(wider > automatic);
        Assert.Equal(RepeatKind.Resize, repeatAfterStep?.Kind);
        Assert.True(narrower < automatic);
        Assert.Equal(automatic, ListColumns());
        Assert.Null(_shell.Foundation.Host.Workspace.ActiveTab!.View.WorkingListWidth);
    }

    [Fact]
    public void GivenTheChangesView_WhenAHeightKeyIsRefused_ThenNoRepeatModeStarts()
    {
        OpenChangesView();

        PressAction(ActionIds.PaneTaller);

        Assert.Null(_shell.Keys.Repeat.Active);
    }

    [Fact]
    public void GivenTheChangesView_WhenTheDividerIsDragged_ThenTheListsFollowThePointer()
    {
        OpenChangesView();
        var divider = _shell.Foundation.Geometry.WorkingDividerRect();

        _shell.Screen.SizeDragger.OnEdgeDragged(PaneEdge.WorkingLists, divider.X + 10, divider.Y);

        Assert.Equal(divider.X - 1 + 10, ListColumns());
        Assert.Equal(divider.X + 10, _shell.Foundation.Geometry.WorkingDividerRect().X);
    }

    [Fact]
    public void GivenTheChangesView_WhenTheDividerIsDraggedPastTheLimits_ThenTheWidthIsClamped()
    {
        OpenChangesView();

        _shell.Screen.SizeDragger.OnEdgeDragged(PaneEdge.WorkingLists, 1, 5);
        var narrowest = ListColumns();
        _shell.Screen.SizeDragger.OnEdgeDragged(PaneEdge.WorkingLists, ScreenWidth - 1, 5);
        var widest = ListColumns();

        Assert.Equal(PaneSizing.MinimumWorkingListWidth, narrowest);
        Assert.Equal(PaneSizing.MinimumDiffWidth, _shell.Foundation.Geometry.WorkingRects().Diff.Width);
        Assert.True(widest > narrowest);
    }

    private void OpenChangesView()
    {
        var host = _shell.Foundation.Host;
        var gitDirectory = Path.Combine(_root, ".git");
        host.Dispatch(new OpenRepositoryCommand(new RepositoryLocation(_root, gitDirectory, gitDirectory, false, false)));
        var window = _shell.Foundation.Chrome.Window;
        window.Width = ScreenWidth;
        window.Height = ScreenHeight;
        window.Layout();
        host.Dispatch(new ScreenResizedCommand(new ScreenExtent(ScreenWidth, ScreenHeight), DateTimeOffset.UnixEpoch));
        host.Dispatch(new TabCommand(host.Workspace.ActiveTabId!.Value, new OpenWorkingTreeCommand()));
    }

    private void PressAction(ActionId action) =>
        _shell.Foundation.Host.Dispatch(new ActionInvoked(action, null, new InputFocus(OverlayKind.None, true)));

    private int ListColumns() => _shell.Foundation.Geometry.WorkingRects().Unstaged.Width;

    private sealed class TestPlatform(string root, ConsoleCapabilities capabilities) : IPlatform
    {
        public string ConfigDirectory => Path.Combine(root, "config");

        public string StateDirectory => Path.Combine(root, "state");

        public ConsoleCapabilities DetectConsole() => capabilities;

        public bool? QueryLightBackground(ConsoleCapabilities detected) => null;
    }
}
