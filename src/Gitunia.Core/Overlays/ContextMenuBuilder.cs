using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Overlays;

/// <summary>Builds the entries of an actions menu (#471): a title and the key that runs the action in a view.</summary>
public static class ContextMenuBuilder
{
    /// <summary>
    /// The entries for <paramref name="actions"/>, in that order. The key shown is the first Normal-mode binding of the
    /// action that works in <paramref name="view"/>; an action without one is left out, since the menu runs actions
    /// exactly like the keys do and would otherwise show a row nobody can repeat from the keyboard.
    /// </summary>
    public static IReadOnlyList<PaletteEntry> Build(KeyBindingTable table, string view, IReadOnlyList<ActionId> actions)
    {
        var bindings = table.EffectiveBindings(KeyMode.Normal, view);
        var entries = new List<PaletteEntry>(actions.Count);
        foreach (var action in actions)
        {
            var binding = bindings.FirstOrDefault(candidate => candidate.Action == action);
            if (binding is null)
            {
                continue;
            }

            entries.Add(new PaletteEntry(
                action,
                ActionCatalog.Describe(action),
                KeyFormatter.Format(binding.Sequence),
                !ActionCatalog.Unimplemented.Contains(action)));
        }

        return entries;
    }
}
