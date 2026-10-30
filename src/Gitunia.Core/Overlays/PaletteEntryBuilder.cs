using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Overlays;

/// <summary>
/// Builds the palette's entries from the key binding table: every action in it, never a hand-written list.
/// </summary>
public static class PaletteEntryBuilder
{
    private const int MaximumShownBindings = 3;

    /// <param name="table">The bindings.</param>
    /// <param name="view">The focused view; its own actions come first, then those it inherits, then the rest.</param>
    /// <param name="level">The command level (#839): actions above it are left out. Defaults to every command.</param>
    public static IReadOnlyList<PaletteEntry> Build(
        KeyBindingTable table,
        string view,
        CommandLevel level = CommandLevel.AdvancedPlus) =>
        Build(table, view, ActionCatalog.Unimplemented, level);

    /// <summary>
    /// Like <see cref="Build(KeyBindingTable, string, CommandLevel)"/> with an explicit set of actions that have no
    /// behaviour yet (shown greyed out); modal-only actions are left out.
    /// </summary>
    /// <param name="table">The bindings.</param>
    /// <param name="view">The focused view.</param>
    /// <param name="unimplemented">The actions to mark as not implemented.</param>
    /// <param name="level">The command level (#839): actions above it are left out. Defaults to every command.</param>
    public static IReadOnlyList<PaletteEntry> Build(
        KeyBindingTable table,
        string view,
        IReadOnlySet<ActionId> unimplemented,
        CommandLevel level = CommandLevel.AdvancedPlus)
    {
        var chain = table.ViewChain(view).ToList();
        var rankByAction = new Dictionary<ActionId, int>();
        foreach (var binding in table.EffectiveBindings(KeyMode.Normal, view))
        {
            var rank = chain.IndexOf(binding.View);
            rankByAction[binding.Action] = Math.Min(rank, rankByAction.GetValueOrDefault(binding.Action, rank));
        }

        var entries = new List<RankedPaletteEntry>();
        var order = 0;
        var listedGroups = table.Bindings
            .GroupBy(binding => binding.Action)
            .Where(group => !ActionCatalog.IsModalOnly(group.Key) && ActionCatalog.IsListedAt(group.Key, level));
        foreach (var group in listedGroups)
        {
            var keys = group
                .OrderBy(binding => binding.Mode == KeyMode.Normal ? 0 : 1)
                .Select(binding => KeyFormatter.Display(binding.Sequence))
                .Distinct()
                .Take(MaximumShownBindings);
            var entry = new PaletteEntry(
                group.Key,
                ActionCatalog.Describe(group.Key),
                string.Join(" / ", keys),
                !unimplemented.Contains(group.Key),
                ActionCatalog.GroupOf(group.Key)?.Header);
            entries.Add(new RankedPaletteEntry(entry, rankByAction.GetValueOrDefault(group.Key, chain.Count), order++));
        }

        var listed = entries.Select(item => item.Entry.Action).ToHashSet();
        foreach (var action in ActionCatalog.PaletteOnly
            .Where(action => !listed.Contains(action) && ActionCatalog.IsListedAt(action, level)))
        {
            var entry = new PaletteEntry(
                action,
                ActionCatalog.Describe(action),
                string.Empty,
                !unimplemented.Contains(action),
                ActionCatalog.GroupOf(action)?.Header);
            entries.Add(new RankedPaletteEntry(entry, chain.Count, order++));
        }

        // Grouped (#483): the groups in their fixed order; inside a group the clusters of related actions (#838), and
        // inside a cluster the focused view's own actions first.
        var ordered = new List<PaletteEntry>(entries.Count);
        foreach (var group in entries.OrderBy(item => GroupOrder(item.Entry.Action)).GroupBy(item => GroupOrder(item.Entry.Action)))
        {
            var members = group.OrderBy(item => item.Rank).ThenBy(item => item.Order).ToList();
            ordered.AddRange(ActionClusters
                .Arrange(members, item => item.Entry.Action)
                .Select(entry => entry.Item.Entry with { Cluster = entry.Cluster }));
        }

        return ordered;
    }

    private static int GroupOrder(ActionId action) => ActionCatalog.GroupOf(action)?.Order ?? int.MaxValue;
}
