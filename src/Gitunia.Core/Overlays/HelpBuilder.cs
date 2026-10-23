using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Overlays;

/// <summary>Builds the help overlay's content for a view from the key binding table (Normal mode, with inherited views).</summary>
public static class HelpBuilder
{
    private const string MarkersTitle = "Markers";
    private const string UpstreamGoneKeys = "✗ / (x)";
    private const string UpstreamGoneText = "Upstream gone: deleted on the remote, can be pruned";

    private const string OtherTitle = "Other";

    /// <summary>
    /// One tab per source in the view's inheritance chain (the view, then <c>Lists</c>, then <c>Global</c>), each split under
    /// the <see cref="ActionCatalog.GroupOf"/> subheaders, plus a <c>Markers</c> tab for the graph and the sidebar.
    /// </summary>
    public static IReadOnlyList<HelpTab> Build(KeyBindingTable table, string view)
    {
        var effective = table.EffectiveBindings(KeyMode.Normal, view);
        var tabs = new List<HelpTab>();
        foreach (var name in table.ViewChain(view))
        {
            var rows = effective
                .Where(binding => binding.View == name)
                .GroupBy(binding => binding.Action)
                .Select(group => (
                    Group: ActionCatalog.GroupOf(group.Key),
                    Row: new HelpRow(
                        string.Join(" / ", group.Select(binding => KeyFormatter.Format(binding.Sequence))),
                        ActionCatalog.Describe(group.Key))))
                .ToList();
            if (rows.Count == 0)
            {
                continue;
            }

            var sections = rows
                .GroupBy(entry => entry.Group?.Header ?? OtherTitle)
                .OrderBy(group => rows.First(entry => (entry.Group?.Header ?? OtherTitle) == group.Key).Group?.Order ?? int.MaxValue)
                .Select(group => new HelpSection(group.Key, [.. group.Select(entry => entry.Row)]))
                .ToList();
            tabs.Add(new HelpTab(name, sections));
        }

        if (view is DefaultKeymap.GraphView or DefaultKeymap.SidebarView)
        {
            tabs.Add(new HelpTab(MarkersTitle, [MarkersSection()]));
        }

        return tabs;
    }

    // What the marks after a branch name mean (the Unicode mark, then its ASCII fallback); not tied to a key.
    private static HelpSection MarkersSection() =>
        new(MarkersTitle, [new HelpRow(UpstreamGoneKeys, UpstreamGoneText)]);
}
