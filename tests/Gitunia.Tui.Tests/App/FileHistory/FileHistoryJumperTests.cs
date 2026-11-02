using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.Effects;
using Gitunia.Core.FileHistory;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.FileHistory;
using Gitunia.Tui.App.Reflog;
using Xunit;

namespace Gitunia.Tui.Tests.App.FileHistory;

public class FileHistoryJumperTests
{
    private readonly Dispatcher _host = new(AppState.Empty);

    public FileHistoryJumperTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-file-history-jumper");
        var gitDirectory = Path.Combine(root, ".git");
        _host.Dispatch(new OpenRepositoryCommand(new RepositoryLocation(root, gitDirectory, gitDirectory, false, false)));
    }

    [Fact]
    public void GivenTheGraphSelectsTheCommit_WhenJumping_ThenTheHistoryClosesAndTheGraphWasAskedForThatCommit()
    {
        var graph = new RecordingSelector(string.Empty);
        var tab = OpenHistory();

        new FileHistoryJumper(_host, graph).Jump(new FileHistoryJumpEffect(tab, "abc"));

        Assert.Equal([(tab, "abc")], graph.Asked);
        Assert.False(_host.State.Shell.FileHistory.IsOpen);
    }

    [Fact]
    public void GivenTheGraphCannotSelectTheCommit_WhenJumping_ThenTheHistoryStaysOpenAndTheStatusSaysWhy()
    {
        var graph = new RecordingSelector("Commit not loaded yet");
        var tab = OpenHistory();
        List<AppEffect> effects = [];
        _host.EffectRaised += effects.Add;

        new FileHistoryJumper(_host, graph).Jump(new FileHistoryJumpEffect(tab, "abc"));

        Assert.True(_host.State.Shell.FileHistory.IsOpen);
        Assert.Equal("Commit not loaded yet", Assert.IsType<PostStatusEffect>(Assert.Single(effects)).Text);
    }

    private TabId OpenHistory()
    {
        var tab = _host.Workspace.ActiveTabId!.Value;
        _host.Dispatch(new FileHistoryOpenRequested(tab, "a.txt", "HEAD"));
        _host.Dispatch(new FileHistoryLoaded(tab, _host.State.Shell.FileHistory.Generation, "a.txt", "HEAD", []));

        return tab;
    }

    private sealed class RecordingSelector(string refusal) : IGraphCommitSelector
    {
        public List<(TabId Tab, string Hash)> Asked { get; } = [];

        public string SelectCommit(TabId tabId, string hash)
        {
            Asked.Add((tabId, hash));

            return refusal;
        }
    }
}
