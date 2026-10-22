using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Overlays;

/// <summary>Builds the help overlay's content for a view from the key binding table (Normal mode, with inherited views).</summary>
public static class HelpBuilder
{
    private const string MarkersTitle = "Markers";
    private const string UpstreamGoneKeys = "✗ / (x)";
    private const string UpstreamGoneText = "Upstream gone: deleted on the remote, can be pruned";

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

        if (view is DefaultKeymap.GraphView or DefaultKeymap.SidebarView)
        {
            sections.Add(MarkersSection());
        }

        return sections;
    }

    // What the marks after a branch name mean (the Unicode mark, then its ASCII fallback); not tied to a key.
    private static HelpSection MarkersSection() =>
        new(MarkersTitle, [new HelpRow(UpstreamGoneKeys, UpstreamGoneText)]);
}
