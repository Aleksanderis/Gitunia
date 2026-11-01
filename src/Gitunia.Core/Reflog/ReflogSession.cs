using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Reflog;

/// <summary>
/// The reflog overlay as the shell state holds it: the open list (null while closed) and the bookkeeping of the read
/// that fills it. Changed only by <see cref="ReflogReducer"/>.
/// </summary>
/// <param name="List">The open list, null while it is closed.</param>
/// <param name="Generation">Counts reads and closes; a read result with another number is stale and dropped.</param>
/// <param name="IsReading">True from the read effect until its result arrives; a second read is refused meanwhile.</param>
public sealed record ReflogSession(ReflogState? List, long Generation, bool IsReading)
{
    public static ReflogSession Empty { get; } = new(null, 0, false);

    public bool IsOpen => List is not null;

    /// <summary>True when typed keys go to the search box or the ref picker of the list in the active tab.</summary>
    /// <param name="activeTab">The active tab.</param>
    public bool TakesText(TabId? activeTab) =>
        List is { } list && (list.Picker is not null || list.IsEditingSearch) && activeTab == list.TabId;
}
