using Gitunia.App;
using Gitunia.App.Loading.Commits;
using Gitunia.App.Tabs;
using Gitunia.Core;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public sealed class RefreshActionsTests : IDisposable
{
    private readonly Dispatcher _host = new(AppState.Empty);
    private readonly List<AppEffect> _effects = [];
    private readonly TerminalTabSession _session;
    private readonly RefreshActions _actions;

    public RefreshActionsTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-refresh-actions");
        var gitDirectory = Path.Combine(root, ".git");
        var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
        _host.Dispatch(new OpenRepositoryCommand(location));
        _session = new TerminalTabSession(
            new TabSessionParts(
                _host.Workspace.ActiveTabId!.Value,
                new CommitDetailsStore(TestGit.Services.CommitDetails, location, InlineUi.Instance, TimeSpan.Zero)),
            GraphGlyphSet.Unicode);
        _host.EffectRaised += _effects.Add;
        _actions = new RefreshActions(_host, new FakeActiveSessions(() => _session));
    }

    public void Dispose() => _session.Dispose();

    [Fact]
    public void GivenALoadedTab_WhenRefreshing_ThenAnAnnouncedRefreshOfTheTabIsRequested()
    {
        _session.MarkStarted();

        var handled = _actions.TryExecute(ActionIds.RepositoryRefresh, null);

        Assert.True(handled);
        Assert.Equal("refreshing...", _actions.Notice);
        Assert.Equal(new RefreshTabEffect(_host.Workspace.ActiveTabId!.Value, true), Assert.Single(_effects));
    }

    [Fact]
    public void GivenAPendingTab_WhenRefreshing_ThenNothingIsRequestedAndTheNoticeSaysNotLoaded()
    {
        var handled = _actions.TryExecute(ActionIds.RepositoryRefresh, null);

        Assert.True(handled);
        Assert.Equal("not loaded yet", _actions.Notice);
        Assert.Empty(_effects);
    }

    [Fact]
    public void GivenAnotherAction_WhenExecuting_ThenItIsLeftToOthers()
    {
        Assert.False(_actions.TryExecute(ActionIds.Cancel, null));
        Assert.Empty(_effects);
    }
}
