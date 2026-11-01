namespace Gitunia.Core.Workspaces.Tabs;

/// <summary>
/// Renaming a tab (<c>Ctrl-w r</c>): the request that opens the prompt and its answer. Dropped when the tab is no
/// longer open.
/// </summary>
public abstract record TabRenameCommand : AppCommand
{
    private protected TabRenameCommand(TabId tabId) => TabId = tabId;

    /// <summary>The tab the command is for.</summary>
    public TabId TabId { get; }
}

/// <summary>Open the rename prompt, starting with the tab's current name.</summary>
/// <param name="TabId">The tab.</param>
public sealed record TabRenameRequested(TabId TabId) : TabRenameCommand(TabId);

/// <summary>The prompt was accepted: store the name (a blank one goes back to the repository name).</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Name">The typed name.</param>
public sealed record TabRenameAnswered(TabId TabId, string Name) : TabRenameCommand(TabId);
