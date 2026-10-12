using Gitunia.Core.Keymap;

namespace Gitunia.Core.Overlays;

/// <summary>Builds the palette's entries from the key binding table: every action in it, never a hand-written list.</summary>
public static class PaletteEntryBuilder
{
    private const int MaximumShownBindings = 3;

    /// <param name="table">The bindings.</param>
    /// <param name="view">The focused view; its own actions come first, then those it inherits, then the rest.</param>
    public static IReadOnlyList<PaletteEntry> Build(KeyBindingTable table, string view)
    {
        var chain = table.ViewChain(view).ToList();
        var rankByAction = new Dictionary<ActionId, int>();
        foreach (var binding in table.EffectiveBindings(KeyMode.Normal, view))
        {
            var rank = chain.IndexOf(binding.View);
            rankByAction[binding.Action] = Math.Min(rank, rankByAction.GetValueOrDefault(binding.Action, rank));
        }

        var entries = new List<(int Rank, int Order, PaletteEntry Entry)>();
        var order = 0;
        foreach (var group in table.Bindings.GroupBy(binding => binding.Action))
        {
            var keys = group
                .OrderBy(binding => binding.Mode == KeyMode.Normal ? 0 : 1)
                .Select(binding => KeyFormatter.Format(binding.Sequence))
                .Distinct()
                .Take(MaximumShownBindings);
            entries.Add((rankByAction.GetValueOrDefault(group.Key, chain.Count), order++, new PaletteEntry(group.Key, ActionCatalog.Describe(group.Key), string.Join(" / ", keys))));
        }

        return [.. entries.OrderBy(item => item.Rank).ThenBy(item => item.Order).Select(item => item.Entry)];
    }
}
