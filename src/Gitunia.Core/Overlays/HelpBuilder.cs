using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Overlays;

/// <summary>Builds the help overlay's content for a view from the key binding table (Normal mode, with inherited views).</summary>
public static class HelpBuilder
{
    public static IReadOnlyList<HelpSection> Build(KeyBindingTable table, string view)
    {
        var effective = table.EffectiveBindings(KeyMode.Normal, view);
        var sections = new List<HelpSection>();
        foreach (var name in table.ViewChain(view))
        {
            var rows = effective
                .Where(binding => binding.View == name)
                .GroupBy(binding => binding.Action)
                .Select(group => new HelpRow(
                    string.Join(" / ", group.Select(binding => KeyFormatter.Format(binding.Sequence))),
                    ActionCatalog.Describe(group.Key)))
                .ToList();
            if (rows.Count > 0)
            {
                sections.Add(new HelpSection(name, rows));
            }
        }

        return sections;
    }
}
