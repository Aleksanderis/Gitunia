using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Keymap;

/// <summary>
/// Visual clusters inside an action group (#838): the actions of one group that share an id prefix (<c>diff.next-hunk</c>
/// and <c>diff.previous-hunk</c>, <c>rebase.onto</c> and <c>rebase.drop</c>) sit together with a blank line between
/// clusters in the help, the which-key popup and the palette. A prefix with a single action in the group forms no
/// cluster: those loose actions follow the clusters, together. Derived from the id, so there is no second list to keep.
/// </summary>
public static class ActionClusters
{
    /// <summary>The cluster of <paramref name="action"/>: the part of its id before the first dot.</summary>
    public static string ClusterOf(ActionId action)
    {
        var dot = action.Value.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? action.Value : action.Value[..dot];
    }

    /// <summary>
    /// <paramref name="items"/> (all of one group) in cluster order: clusters of two or more actions first, in the order
    /// their first member appears, then the loose actions; the order inside a cluster is kept. Each item carries its
    /// cluster, null for a loose action.
    /// </summary>
    public static IReadOnlyList<ClusteredItem<T>> Arrange<T>(IReadOnlyList<T> items, Func<T, ActionId> actionOf)
    {
        var clusters = items.Select(item => ClusterOf(actionOf(item))).ToList();
        var sizes = clusters
            .GroupBy(cluster => cluster, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var firstSeen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var cluster in clusters)
        {
            firstSeen.TryAdd(cluster, firstSeen.Count);
        }

        return [.. items
            .Select((item, index) => (Item: item, Index: index, Cluster: sizes[clusters[index]] > 1 ? clusters[index] : null))
            .OrderBy(entry => entry.Cluster is null ? int.MaxValue : firstSeen[entry.Cluster])
            .ThenBy(entry => entry.Index)
            .Select(entry => new ClusteredItem<T>(entry.Item, entry.Cluster))];
    }
}
