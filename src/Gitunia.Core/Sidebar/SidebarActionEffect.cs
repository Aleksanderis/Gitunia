using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;

namespace Gitunia.Core.Sidebar;

/// <summary>
/// Find the graph index of a commit in the loaded graph of a tab. The answer is a <see cref="SidebarCommitLocated"/> or
/// a <see cref="SidebarCommitMissing"/>; when the commit is not loaded the rest of the graph is asked for.
/// </summary>
/// <param name="TabId">The tab.</param>
/// <param name="Commit">The commit.</param>
/// <param name="Label">The row's name, for the message.</param>
public sealed record LocateGraphCommitEffect(TabId TabId, ObjectId Commit, string Label) : AppEffect;

/// <summary>Move the graph cursor to a graph index and scroll it into view.</summary>
/// <param name="Index">The graph index.</param>
public sealed record SelectGraphIndexEffect(int Index) : AppEffect;

/// <summary>Show the stash of a sidebar row in the detail pane.</summary>
/// <param name="Row">The stash row.</param>
public sealed record ShowStashEffect(SidebarRow Row) : AppEffect;

/// <summary>End the stash view, if one is shown, and put the graph selection back in the pane.</summary>
public sealed record LeaveStashEffect : AppEffect;

/// <summary>While a stash is shown, the cursor moved onto another stash shows that one.</summary>
public sealed record FollowStashCursorEffect : AppEffect;
