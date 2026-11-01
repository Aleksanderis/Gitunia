using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;

namespace Gitunia.Core.Compare;

/// <summary>A command of the comparison of two commits, for one tab. Dropped when the tab is no longer open.</summary>
public abstract record CompareCommand : AppCommand
{
    private protected CompareCommand(TabId tabId) => TabId = tabId;

    /// <summary>The tab the command is for.</summary>
    public TabId TabId { get; }
}

/// <summary>The compare key was pressed on the commit under the graph cursor.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Selected">The commit under the cursor, null when none.</param>
public sealed record CompareMarkPressed(TabId TabId, ObjectId? Selected) : CompareCommand(TabId);

/// <summary>Esc was pressed in the graph: ends the mark and the comparison when there is one.</summary>
/// <param name="TabId">The tab.</param>
public sealed record CompareEscapePressed(TabId TabId) : CompareCommand(TabId);

/// <summary>
/// The details of the tab changed. The facts are read from the details store and the graph, which hold the big data.
/// </summary>
/// <param name="TabId">The tab.</param>
/// <param name="Facts">What the details store shows now.</param>
public sealed record CompareDetailsChanged(TabId TabId, CompareFacts Facts) : CompareCommand(TabId);

/// <summary>The details store started the comparison of the commit.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Selected">The commit that is compared with the mark.</param>
public sealed record CompareStarted(TabId TabId, ObjectId Selected) : CompareCommand(TabId);
