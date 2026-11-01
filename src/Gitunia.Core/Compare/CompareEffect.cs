using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;

namespace Gitunia.Core.Compare;

/// <summary>A step on the details store of a tab, which holds the loaded commits.</summary>
public abstract record CompareEffect : AppEffect
{
    private protected CompareEffect(TabId tabId) => TabId = tabId;

    /// <summary>The tab.</summary>
    public TabId TabId { get; }
}

/// <summary>Load the comparison of two commits; <see cref="CompareStarted"/> follows when the store took it.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Older">The older commit by graph order.</param>
/// <param name="Newer">The newer commit by graph order.</param>
/// <param name="Selected">The commit under the cursor, which is one of the two.</param>
public sealed record StartComparisonEffect(TabId TabId, ObjectId Older, ObjectId Newer, ObjectId Selected)
    : CompareEffect(TabId);

/// <summary>Go back from a comparison to the changes of the selected commit.</summary>
/// <param name="TabId">The tab.</param>
public sealed record ClearComparisonEffect(TabId TabId) : CompareEffect(TabId);
