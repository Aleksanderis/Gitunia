using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Rendering;

namespace Gitunia.Tui.App;

/// <summary>
/// Executes the effects the reducer returns and owns the per-tab sessions they create. New effects (clipboard,
/// file watching, ...) get a case in <see cref="Run"/>; results come back as commands through the host.
/// </summary>
public sealed class EffectRunner
{
    private readonly GitRunner runner;
    private readonly WorkspaceHost host;
    private readonly Action<Action> postToUi;
    private readonly StatusChannel statuses;
    private readonly GraphGlyphSet glyphs;
    private readonly TimeSpan detailDebounce;
    private readonly Dictionary<TabId, TabSession> sessions = [];

    public EffectRunner(GitRunner runner, WorkspaceHost host, Action<Action> postToUi, GraphGlyphSet glyphs, StatusChannel statuses, TimeSpan? detailDebounce = null)
    {
        this.detailDebounce = detailDebounce ?? TimeSpan.FromMilliseconds(80);
        this.runner = runner;
        this.host = host;
        this.postToUi = postToUi;
        this.glyphs = glyphs;
        this.statuses = statuses;
        host.EffectRaised += Run;
    }

    /// <summary>The runtime session of an open tab, or null.</summary>
    public TabSession? SessionOf(TabId tabId) => sessions.GetValueOrDefault(tabId);

    private void Run(WorkspaceEffect effect)
    {
        switch (effect)
        {
            case LoadRepositoryEffect load:
                StartLoad(load);
                break;
            case CancelLoadEffect cancel:
                if (sessions.Remove(cancel.TabId, out var closed))
                {
                    closed.Dispose();
                }

                break;
        }
    }

    private void StartLoad(LoadRepositoryEffect load)
    {
        var tab = host.Workspace.FindTab(load.TabId);
        if (tab is null)
        {
            return;
        }

        if (sessions.Remove(load.TabId, out var previous))
        {
            previous.Dispose();
        }

        var session = new TabSession(load.TabId, glyphs, new CommitDetailsStore(new Repo(runner, tab.Location), postToUi, detailDebounce));
        sessions[load.TabId] = session;
        var token = session.Cancellation.Token;
        _ = Task.Run(() => RepoLoader.RunAsync(runner, tab.Location, session, host, statuses, postToUi, token), CancellationToken.None);
    }
}
