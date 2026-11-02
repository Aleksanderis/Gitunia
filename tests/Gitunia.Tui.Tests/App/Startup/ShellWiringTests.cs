using System.Reflection;
using Gitunia.App;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Platform;
using Gitunia.Tui.App.Shell;
using Terminal.Gui.Drivers;
using Xunit;

namespace Gitunia.Tui.Tests.App.Startup;

/// <summary>A composed and wired shell on a headless driver reacting to a state change; the order the state subscribers run in is pinned too.</summary>
public sealed class ShellWiringTests : IDisposable
{
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
        Directory.Delete(_root, true);
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
        var field = typeof(Dispatcher).GetField("Changed", BindingFlags.Instance | BindingFlags.NonPublic)!;
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

    private sealed class TestPlatform(string root, ConsoleCapabilities capabilities) : IPlatform
    {
        public string ConfigDirectory => Path.Combine(root, "config");

        public string StateDirectory => Path.Combine(root, "state");

        public ConsoleCapabilities DetectConsole() => capabilities;

        public bool? QueryLightBackground(ConsoleCapabilities detected) => null;
    }
}
