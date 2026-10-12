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
    private readonly Dictionary<TabId, Action> pendingLoads = [];

    public EffectRunner(GitRunner runner, WorkspaceHost host, Action<Action> postToUi, GraphGlyphSet glyphs, StatusChannel statuses, TimeSpan? detailDebounce = null)
    {
        this.detailDebounce = detailDebounce ?? TimeSpan.FromMilliseconds(80);
        this.runner = runner;
        this.host = host;
        this.postToUi = postToUi;
        this.glyphs = glyphs;
        this.statuses = statuses;
        host.EffectRaised += Run;
        host.Changed += StartActiveTabLoad;
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
                pendingLoads.Remove(cancel.TabId);
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

        pendingLoads.Remove(load.TabId);
        if (sessions.Remove(load.TabId, out var previous))
        {
            previous.Dispose();
        }

        var repo = new Repo(runner, tab.Location);
        var session = new TabSession(load.TabId, glyphs, new CommitDetailsStore(repo, postToUi, detailDebounce), new DiffStore(repo, postToUi));
        sessions[load.TabId] = session;
        var token = session.Cancellation.Token;
        var location = tab.Location;
        void Start()
        {
            session.MarkStarted();
            _ = Task.Run(() => RepoLoader.RunAsync(runner, location, session, host, statuses, postToUi, token), CancellationToken.None);
        }

        // Only the active tab loads now; background tabs keep an empty session until first activated (docs/architecture.md, "Many tabs").
        if (host.Workspace.ActiveTabId == load.TabId)
        {
            Start();
        }
        else
        {
            pendingLoads[load.TabId] = Start;
        }
    }

    private void StartActiveTabLoad()
    {
        if (pendingLoads.Count > 0 && host.Workspace.ActiveTabId is { } active && pendingLoads.Remove(active, out var start))
        {
            start();
        }
    }
}
