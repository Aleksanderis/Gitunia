using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Overlays;

/// <summary>Builds the palette's entries from the key binding table: every action in it, never a hand-written list.</summary>
public static class PaletteEntryBuilder
{
    private const int MaximumShownBindings = 3;

    /// <param name="table">The bindings.</param>
    /// <param name="view">The focused view; its own actions come first, then those it inherits, then the rest.</param>
    public static IReadOnlyList<PaletteEntry> Build(KeyBindingTable table, string view) =>
        Build(table, view, ActionCatalog.Unimplemented);

    /// <summary>Like <see cref="Build(KeyBindingTable, string)"/> with an explicit set of actions that have no behaviour yet (shown greyed out); modal-only actions are left out.</summary>
    /// <param name="table">The bindings.</param>
    /// <param name="view">The focused view.</param>
    /// <param name="unimplemented">The actions to mark as not implemented.</param>
    public static IReadOnlyList<PaletteEntry> Build(KeyBindingTable table, string view, IReadOnlySet<ActionId> unimplemented)
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
        foreach (var group in table.Bindings.GroupBy(binding => binding.Action).Where(group => !ActionCatalog.IsModalOnly(group.Key)))
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
        foreach (var action in ActionCatalog.PaletteOnly.Where(action => !listed.Contains(action)))
        {
            var entry = new PaletteEntry(
                action,
                ActionCatalog.Describe(action),
                string.Empty,
                !unimplemented.Contains(action),
                ActionCatalog.GroupOf(action)?.Header);
            entries.Add(new RankedPaletteEntry(entry, chain.Count, order++));
        }

        // Grouped (#483): the groups in their fixed order; inside a group the focused view's own actions first.
        return [.. entries
            .OrderBy(item => GroupOrder(item.Entry.Action))
            .ThenBy(item => item.Rank)
            .ThenBy(item => item.Order)
            .Select(item => item.Entry)];
    }

    private static int GroupOrder(ActionId action) => ActionCatalog.GroupOf(action)?.Order ?? int.MaxValue;
}
