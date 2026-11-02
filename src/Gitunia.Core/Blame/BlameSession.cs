using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Blame;

/// <summary>
/// The blame view as the shell state holds it: the open view (null while closed) and the number of the read that fills
/// it. Changed only by <see cref="BlameReducer"/>.
/// </summary>
/// <param name="View">The open view, null while it is closed.</param>
/// <param name="Generation">Counts opens and closes; a result with another number is stale and dropped. The blame
/// store carries the same number for the data it holds.</param>
public sealed record BlameSession(BlameState? View, long Generation)
{
    public static BlameSession Empty { get; } = new(null, 0);

    public bool IsOpen => View is not null;

    /// <summary>True when typed keys go to the search box of the view in the active tab.</summary>
    /// <param name="activeTab">The active tab.</param>
    public bool TakesText(TabId? activeTab) => View is { IsEditingSearch: true } view && activeTab == view.TabId;
}
