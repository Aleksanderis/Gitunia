using Gitunia.Core.Keymap;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Tui.App;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Rendering;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.Tests.App;

/// <summary>A loaded tab with a sidebar handler wired to recorders instead of views.</summary>
public sealed class SidebarHarness : IDisposable
{
    private readonly TabSession session;
    private readonly TabSession emptySession;

    private SidebarHarness(WorkspaceHost host, TabSession session, SidebarListSource source, Repo repo)
    {
        Host = host;
        this.session = session;
        emptySession = new TabSession(session.TabId, GraphGlyphSet.Unicode, new CommitDetailsStore(repo, action => action(), TimeSpan.Zero));
        emptySession.Complete();
        Source = source;
        Handler = new SidebarActionHandler(
            host,
            source,
            () => HideGraph ? emptySession : session,
            () => 10,
            () => SidebarVisible,
            () => DetailVisible,
            item => SelectedGraphItem = item,
            mode => Mode = mode);
        host.Changed += () => source.Update(host.Workspace.ActiveTab);
        source.Update(host.Workspace.ActiveTab);
    }

    public WorkspaceHost Host { get; }

    public SidebarListSource Source { get; }

    public SidebarActionHandler Handler { get; }

    public bool SidebarVisible { get; set; } = true;

    public bool DetailVisible { get; set; }

    /// <summary>Makes the handler see a session whose graph has no commits.</summary>
    public bool HideGraph { get; set; }

    public int? SelectedGraphItem { get; private set; }

    public KeyMode Mode { get; private set; } = KeyMode.Normal;

    public RepoState Tab => Host.Workspace.ActiveTab!;

    public static async Task<SidebarHarness> LoadAsync(string repositoryPath)
    {
        var runner = new GitRunner();
        var location = await RepositoryDiscovery.DiscoverAsync(runner, repositoryPath, CancellationToken.None);
        var host = new WorkspaceHost(Workspace.Empty);
        host.Dispatch(new OpenRepositoryCommand(location));
        var tabId = host.Workspace.ActiveTabId!.Value;
        var session = new TabSession(tabId, GraphGlyphSet.Unicode, new CommitDetailsStore(new Repo(runner, location), action => action(), TimeSpan.Zero));

        // Posting runs the action inline (serialized, like the UI thread would).
        await RepoLoader.RunAsync(runner, location, session, host, InlineUi.Post, session.Cancellation.Token);
        return new SidebarHarness(host, session, new SidebarListSource(GraphGlyphSet.Unicode), new Repo(runner, location));
    }

    /// <summary>Focuses the sidebar and puts the cursor on the row with this label.</summary>
    public void FocusRow(string label)
    {
        Send(new FocusPaneCommand(PaneKind.Sidebar));
        Send(new SetSidebarCursorCommand(Source.Rows.ToList().FindIndex(row => row.Label == label)));
    }

    public void Send(RepoCommand command) => Host.Dispatch(new TabCommand(Tab.Id, command));

    public void Dispose()
    {
        session.Dispose();
        emptySession.Dispose();
    }
}
