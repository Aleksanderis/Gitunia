using Gitunia.Core.Clipboard;
using Gitunia.Core.Keymap;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Sidebar;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.Tests.App.Sidebar;

/// <summary>A loaded tab with a sidebar handler wired to recorders instead of views.</summary>
public sealed class SidebarHarness : IDisposable
{
    private readonly TabSession _session;
    private readonly TabSession _emptySession;

    private SidebarHarness(WorkspaceHost host, TabSession session, SidebarListSource source, RepositoryLocation location)
    {
        Host = host;
        _session = session;
        _emptySession = new TabSession(session.TabId, GraphGlyphSet.Unicode, new CommitDetailsStore(TestGit.Services.CommitDetails, location, action => action(), TimeSpan.Zero));
        _emptySession.Complete();
        Source = source;
        Handler = new SidebarActionHandler(
            host,
            source,
            () => HideGraph ? _emptySession : session,
            () => 10,
            () => SidebarVisible,
            () => DetailVisible,
            item => SelectedGraphItem = item,
            mode => Mode = mode,
            new ClipboardCopier(new ClipboardWriter(Clipboard, new Osc52Options(Osc52Mode.Direct, 100_000)), new StatusChannel()));
        host.Changed += () => source.Update(host.Workspace.ActiveTab);
        source.Update(host.Workspace.ActiveTab);
    }

    public WorkspaceHost Host { get; }

    /// <summary>What the handler wrote to the terminal (OSC 52 sequences).</summary>
    public StringWriter Clipboard { get; } = new();

    public SidebarListSource Source { get; }

    public SidebarActionHandler Handler { get; }

    public bool SidebarVisible { get; set; } = true;

    public bool DetailVisible { get; set; }

    /// <summary>Makes the handler see a session whose graph has no commits.</summary>
    public bool HideGraph { get; set; }

    public int? SelectedGraphItem { get; private set; }

    public KeyMode Mode { get; private set; } = KeyMode.Normal;

    public RepositoryState Tab => Host.Workspace.ActiveTab!;

    public static async Task<SidebarHarness> LoadAsync(string repositoryPath)
    {
        var location = await TestGit.Services.Discovery.DiscoverAsync(repositoryPath, CancellationToken.None);
        var host = new WorkspaceHost(Workspace.Empty);
        host.Dispatch(new OpenRepositoryCommand(location));
        var tabId = host.Workspace.ActiveTabId!.Value;
        var session = new TabSession(tabId, GraphGlyphSet.Unicode, new CommitDetailsStore(TestGit.Services.CommitDetails, location, action => action(), TimeSpan.Zero));

        // Posting runs the action inline (serialized, like the UI thread would).
        await TestGit.Services.RepositoryLoader.RunAsync(location, session, host, new StatusChannel(), InlineUi.Post, session.Cancellation.Token);
        return new SidebarHarness(host, session, new SidebarListSource(GraphGlyphSet.Unicode), location);
    }

    /// <summary>Focuses the sidebar and puts the cursor on the row with this label.</summary>
    public void FocusRow(string label)
    {
        Send(new FocusPaneCommand(PaneKind.Sidebar));
        Send(new SetSidebarCursorCommand(Source.Rows.ToList().FindIndex(row => row.Label == label)));
    }

    public void Send(RepositoryCommand command) => Host.Dispatch(new TabCommand(Tab.Id, command));

    public void Dispose()
    {
        _session.Dispose();
        _emptySession.Dispose();
    }
}
