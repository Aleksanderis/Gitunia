using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Overlays;

/// <summary>Builds the entries of an actions menu (#471): a title and the key that runs the action in a view.</summary>
public static class ContextMenuBuilder
{
    /// <summary>A menu with at most this many entries has no subheaders: grouping few rows is noise (#483).</summary>
    public const int UngroupedMaximum = 5;

    /// <summary>
    /// The entries for <paramref name="actions"/>, in that order. The key shown is the first Normal-mode binding of the
    /// action that works in <paramref name="view"/>; an action without one is left out, since the menu runs actions
    /// exactly like the keys do and would otherwise show a row nobody can repeat from the keyboard.
    /// A menu titled by its caller (the <c>view ▾</c> dropdowns) passes <paramref name="grouped"/> false: it is one
    /// list of options, and sub-headers would only repeat its title (#758). Actions above <paramref name="level"/> (#839)
    /// are left out.
    /// </summary>
    public static IReadOnlyList<PaletteEntry> Build(
        KeyBindingTable table,
        string view,
        IReadOnlyList<ActionId> actions,
        bool grouped = true,
        CommandLevel level = CommandLevel.AdvancedPlus)
    {
        var bindings = table.EffectiveBindings(KeyMode.Normal, view);
        var entries = new List<PaletteEntry>(actions.Count);
        foreach (var action in actions)
        {
            var binding = bindings.FirstOrDefault(candidate => candidate.Action == action);
            if (binding is null || !ActionCatalog.IsListedAt(action, level))
            {
                continue;
            }

            entries.Add(new PaletteEntry(
                action,
                ActionCatalog.Describe(action),
                KeyFormatter.Display(binding.Sequence),
                !ActionCatalog.Unimplemented.Contains(action),
                ActionCatalog.GroupOf(action)?.Header));
        }

        return grouped ? Grouped(entries) : [.. entries.Select(entry => entry with { Group = null })];
    }

    // Entries of one group sit together (groups by first appearance); a short or one-group menu has no headers.
    private static List<PaletteEntry> Grouped(List<PaletteEntry> entries)
    {
        if (entries.Count <= UngroupedMaximum || entries.Select(entry => entry.Group).Distinct().Count() < 2)
        {
            return [.. entries.Select(entry => entry with { Group = null })];
        }

        var order = entries.Select(entry => entry.Group).Distinct().ToList();
        return [.. entries.OrderBy(entry => order.IndexOf(entry.Group))];
    }
}
