using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Keymap.Repeat;

namespace Gitunia.Core.Overlays;

/// <summary>
/// Builds the help overlay's content for a view from the key binding table. The first tabs are contextual (the
/// focused view and what it inherits); then come the all-commands tabs, one per screen, that list every bound action
/// once, a short tab for the palette and mouse-only paths, and a <c>Custom</c> tab when the user changed bindings.
/// </summary>
public static class HelpBuilder
{
    /// <summary>The prefix of the all-commands tab titles.</summary>
    public const string AllPrefix = "All ";

    /// <summary>The title of the tab with the user's own bindings.</summary>
    public const string CustomTitle = "Custom";

    /// <summary>The title of the tab with the palette and the mouse-only paths.</summary>
    public const string CommandsTitle = "Commands";

    private const string MarkersTitle = "Markers";
    private const string UpstreamGoneKeys = "✗ / (x)";
    private const string UpstreamGoneText = "Upstream gone: deleted on the remote, can be pruned";
    private const string OtherTitle = "Other";
    private const string AlsoSeparator = ", ";

    // Not listed in any help page, by design: the keys of the modal prompts, which show their own footer.
    private static readonly HashSet<string> UnlistedViews =
    [
        DefaultKeymap.PickerView,
        DefaultKeymap.PaletteView,
        DefaultKeymap.ContextMenuView,
        DefaultKeymap.HelpView,
    ];

    private static readonly HashSet<ActionId> UnlistedActions =
    [
        ActionIds.InputAccept,
        ActionIds.ConfirmYes,
        ActionIds.ConfirmNo,
    ];

    // The screens of the all-commands tabs: a title and the views whose own bindings it lists (merged by action).
    private static readonly AllTabSpec[] AllTabSpecs =
    [
        new("Global", [KeyBindingTable.GlobalView]),
        new("Lists", [DefaultKeymap.ListsView]),
        new("Graph", [DefaultKeymap.GraphView]),
        new("Sidebar", [DefaultKeymap.SidebarView]),
        new(
            "Detail",
            [DefaultKeymap.DetailView, DefaultKeymap.DetailChangesView, DefaultKeymap.DetailFilesView]),
        new("Diff", [DefaultKeymap.DiffView]),
        new(
            "Changes",
            [
                DefaultKeymap.WorkingUnstagedView,
                DefaultKeymap.WorkingStagedView,
                DefaultKeymap.WorkingUnstagedDiffView,
                DefaultKeymap.WorkingStagedDiffView,
                DefaultKeymap.WorkingMessageView,
            ]),
        new("Reflog", [DefaultKeymap.ReflogView]),
        new("History", [DefaultKeymap.FileHistoryView]),
        new("Blame", [DefaultKeymap.BlameView]),
        new("Rebase todo", [DefaultKeymap.RebaseEditorView]),
        new(
            "Conflicts",
            [DefaultKeymap.ConflictEditorView, DefaultKeymap.ConflictSideView, DefaultKeymap.ConflictResultView]),
        new("Settings", [DefaultKeymap.SettingsView]),
        new("Repeat mode", [RepeatMode.ContextView, RepeatMode.ResizeView]),
    ];

    // The screens that own the keyboard while open (#839): their keys are all listed, whatever the command level.
    private static readonly HashSet<string> ModalScreenViews =
    [
        DefaultKeymap.ReflogView,
        DefaultKeymap.FileHistoryView,
        DefaultKeymap.BlameView,
        DefaultKeymap.RebaseEditorView,
        DefaultKeymap.ConflictEditorView,
        DefaultKeymap.ConflictSideView,
        DefaultKeymap.ConflictResultView,
        DefaultKeymap.SettingsView,
        RepeatMode.ContextView,
        RepeatMode.ResizeView,
    ];

    private static readonly Lazy<KeyBindingTable> Defaults = new(DefaultKeymap.Create);

    /// <summary>
    /// The views and actions that no help page lists (the keys of the picker, the palette, the row menu and the help
    /// overlay itself, and the text-input and Confirm answers): the explicit allow-list of the completeness test.
    /// </summary>
    public static bool IsUnlisted(KeyBinding binding) =>
        UnlistedViews.Contains(binding.View) || UnlistedActions.Contains(binding.Action);

    /// <summary>
    /// True for the views of the screens that own the keyboard while open (reflog, rebase todo, conflict editor,
    /// settings, repeat modes): all their keys are listed whatever the command level (#839).
    /// </summary>
    /// <param name="view">The keymap view.</param>
    public static bool IsModalScreen(string view) => ModalScreenViews.Contains(view);

    /// <summary>
    /// One tab per source in the view's inheritance chain (the view, then <c>Lists</c>, then <c>Global</c>), each split
    /// under the <see cref="ActionCatalog.GroupOf"/> subheaders, plus a <c>Markers</c> tab for the graph and the
    /// sidebar, then the all-commands tabs, <c>Commands</c> and, when bindings were changed, <c>Custom</c>.
    /// </summary>
    /// <param name="table">The bindings.</param>
    /// <param name="view">The focused view.</param>
    /// <param name="level">
    /// The command level (#839): actions above it are left out of the contextual and all-commands tabs, and the
    /// <c>Commands</c> tab closes with the number hidden. Defaults to every command. While the focused view is a modal
    /// screen its own keys are all listed.
    /// </param>
    public static IReadOnlyList<HelpTab> Build(
        KeyBindingTable table,
        string view,
        CommandLevel level = CommandLevel.AdvancedPlus)
    {
        var tabs = Contextual(table, view, level);
        tabs.AddRange(AllCommandsTabs(table, level, view));
        tabs.Add(new HelpTab(CommandsTitle, [.. CommandsSections(), .. HiddenSections(table, level, view)]));
        if (CustomSections(table) is { Count: > 0 } custom)
        {
            tabs.Add(new HelpTab(CustomTitle, custom));
        }

        return tabs;
    }

    /// <summary>The title of the single page the filter (<c>/</c>) shows.</summary>
    public const string FilterTitle = "Search";

    /// <summary>
    /// The rows of every page whose key, title or action id contains <paramref name="query"/> (case-insensitive), as one
    /// page with a section per source page. A row listed on several pages (a contextual tab and its all-commands tab)
    /// appears once, under the first.
    /// </summary>
    /// <param name="tabs">The pages of the help.</param>
    /// <param name="query">The typed filter; whitespace around it is ignored.</param>
    public static HelpTab Filter(IReadOnlyList<HelpTab> tabs, string query)
    {
        var needle = query.Trim();
        var seen = new HashSet<(string?, string, string)>();
        var sections = new List<HelpSection>();
        foreach (var tab in tabs)
        {
            var rows = tab.Rows
                .Where(row => Matches(row, needle) && seen.Add((row.Action?.Value, row.Keys, row.Text)))
                .Select(row => row with { GapBefore = false })
                .ToList();
            if (rows.Count > 0)
            {
                sections.Add(new HelpSection(tab.Title, rows));
            }
        }

        if (sections.Count == 0)
        {
            sections.Add(new HelpSection(FilterTitle, [new HelpRow(string.Empty, "Nothing matches the filter.")]));
        }

        return new HelpTab(FilterTitle, sections);
    }

    private static bool Matches(HelpRow row, string needle) =>
        needle.Length == 0
        || row.Keys.Contains(needle, StringComparison.OrdinalIgnoreCase)
        || row.Text.Contains(needle, StringComparison.OrdinalIgnoreCase)
        || (row.Action?.Value.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false);

    /// <summary>The all-commands tabs: every bound action of every screen once, whatever the focused view.</summary>
    /// <param name="table">The bindings.</param>
    /// <param name="level">The command level (#839); defaults to every command.</param>
    /// <param name="view">The focused view: the tab of a modal screen it belongs to lists all its keys.</param>
    public static IReadOnlyList<HelpTab> AllCommandsTabs(
        KeyBindingTable table,
        CommandLevel level = CommandLevel.AdvancedPlus,
        string? view = null)
    {
        var tabs = new List<HelpTab>();
        foreach (var spec in AllTabSpecs)
        {
            var shown = ShownLevel(spec, view, level);
            var bindings = table.Bindings
                .Where(binding => spec.Views.Contains(binding.View) && !IsUnlisted(binding)
                    && ActionCatalog.IsListedAt(binding.Action, shown))
                .ToList();
            if (bindings.Count > 0)
            {
                var sections = SectionsOf(bindings);
                if (spec.Views.Contains(RepeatMode.ResizeView))
                {
                    sections.Insert(0, RepeatStartSection(table));
                }

                tabs.Add(new HelpTab($"{AllPrefix}{spec.Title}", sections));
            }
        }

        return tabs;
    }

    public static List<HelpTab> Contextual(
        KeyBindingTable table,
        string view,
        CommandLevel level = CommandLevel.AdvancedPlus)
    {
        var shown = ModalScreenViews.Contains(view) ? CommandLevel.AdvancedPlus : level;
        var normal = table.EffectiveBindings(KeyMode.Normal, view)
            .Where(binding => ActionCatalog.IsListedAt(binding.Action, shown))
            .ToList();
        var visual = table.EffectiveBindings(KeyMode.Visual, view)
            .Where(binding => ActionCatalog.IsListedAt(binding.Action, shown))
            .ToList();
        var normalActions = normal.Select(binding => binding.Action).ToHashSet();
        var tabs = new List<HelpTab>();
        foreach (var name in table.ViewChain(view))
        {
            // Visual-only keys (the range actions) follow the Normal ones, so the tab explains the whole screen.
            var bindings = normal
                .Where(binding => binding.View == name)
                .Concat(visual.Where(binding => binding.View == name && !normalActions.Contains(binding.Action)))
                .ToList();
            if (bindings.Count == 0)
            {
                continue;
            }

            tabs.Add(new HelpTab(name, SectionsOf(bindings)));
        }

        if (view is DefaultKeymap.GraphView or DefaultKeymap.SidebarView)
        {
            tabs.Add(new HelpTab(MarkersTitle, [MarkersSection()]));
        }

        return tabs;
    }

    // A modal screen lists all its keys while it is the focused view (#839); every other tab follows the level.
    private static CommandLevel ShownLevel(AllTabSpec spec, string? view, CommandLevel level) =>
        view is not null && spec.Views.Contains(view) && ModalScreenViews.Contains(view)
            ? CommandLevel.AdvancedPlus
            : level;

    // The closing line of the help (#839): how many commands the level keeps out of the all-commands tabs.
    private static List<HelpSection> HiddenSections(KeyBindingTable table, CommandLevel level, string view)
    {
        var hidden = AllTabSpecs
            .SelectMany(spec => table.Bindings
                .Where(binding => spec.Views.Contains(binding.View) && !IsUnlisted(binding))
                .Where(binding => !ActionCatalog.IsListedAt(binding.Action, ShownLevel(spec, view, level))))
            .Select(binding => binding.Action)
            .Distinct()
            .Count();
        if (hidden == 0)
        {
            return [];
        }

        var noun = hidden == 1 ? "advanced command" : "advanced commands";
        var cycleKey = table.Bindings
            .FirstOrDefault(binding => binding.Action == ActionIds.ViewCycleCommandLevel);
        var keys = cycleKey is null ? string.Empty : KeyFormatter.Display(cycleKey.Sequence);

        return
        [
            new HelpSection(
                "Command level",
                [
                    new HelpRow(
                        keys,
                        $"{hidden} {noun} hidden (level: {CommandLevels.LabelOf(level)}); this key shows more",
                        string.Empty,
                        ActionIds.ViewCycleCommandLevel),
                ]),
        ];
    }

    // One row per action: the keys of its Normal mode (else of the first other mode, tagged), main key first.
    private static List<HelpSection> SectionsOf(IReadOnlyList<KeyBinding> bindings)
    {
        var drafts = bindings
            .GroupBy(binding => binding.Action)
            .Select(group => Draft(group.Key, [.. group]))
            .ToList();
        var sections = new List<HelpSection>();
        foreach (var group in drafts
            .GroupBy(draft => ActionCatalog.GroupOf(draft.Action))
            .OrderBy(group => group.Key?.Order ?? int.MaxValue))
        {
            sections.Add(new HelpSection(group.Key?.Header ?? OtherTitle, ClusteredRows([.. group])));
        }

        return sections;
    }

    private static HelpDraft Draft(ActionId action, List<KeyBinding> bindings)
    {
        var mode = bindings.Any(binding => binding.Mode == KeyMode.Normal) ? KeyMode.Normal : bindings[0].Mode;
        var keys = bindings
            .Where(binding => binding.Mode == mode)
            .Select(binding => KeyFormatter.Display(binding.Sequence))
            .Distinct()
            .ToList();
        var title = ActionCatalog.Describe(action);
        var description = mode == KeyMode.Normal ? title : $"({mode} mode) {title}";
        return new HelpDraft(action, keys[0], string.Join(AlsoSeparator, keys.Skip(1)), description);
    }

    private static List<HelpRow> ClusteredRows(IReadOnlyList<HelpDraft> drafts)
    {
        var rows = new List<HelpRow>();
        string? previous = null;
        foreach (var entry in ActionClusters.Arrange(drafts, item => item.Action))
        {
            // A blank line where a cluster of related keys ends; the loose actions after the clusters share one block.
            var gap = rows.Count > 0 && entry.Cluster != previous;
            var draft = entry.Item;
            rows.Add(new HelpRow(draft.Keys, draft.Description, draft.Also, draft.Action, gap));
            previous = entry.Cluster;
        }

        return rows;
    }

    // The bindings that differ from the defaults: added or changed keys and the keys that were unbound.
    private static List<HelpSection> CustomSections(KeyBindingTable table)
    {
        var defaults = Defaults.Value.Bindings.ToDictionary(Slot);
        var current = table.Bindings.ToDictionary(Slot);
        var entries = new List<CustomEntry>();
        foreach (var binding in table.Bindings)
        {
            var slot = Slot(binding);
            if (!defaults.TryGetValue(slot, out var original))
            {
                entries.Add(new CustomEntry(binding, ActionCatalog.Describe(binding.Action)));
            }
            else if (original.Action != binding.Action)
            {
                entries.Add(new CustomEntry(
                    binding,
                    $"{ActionCatalog.Describe(binding.Action)}  (was: {ActionCatalog.Describe(original.Action)})"));
            }
        }

        foreach (var original in Defaults.Value.Bindings.Where(binding => !current.ContainsKey(Slot(binding))))
        {
            entries.Add(new CustomEntry(original, $"Unbound: {ActionCatalog.Describe(original.Action)}", true));
        }

        return [.. entries
            .GroupBy(entry => (entry.Binding.View, entry.Binding.Mode))
            .OrderBy(group => group.Key.View, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Mode)
            .Select(group => new HelpSection(
                group.Key.Mode == KeyMode.Normal ? group.Key.View : $"{group.Key.View} ({group.Key.Mode})",
                [.. group
                    .OrderBy(entry => KeyFormatter.Display(entry.Binding.Sequence), StringComparer.Ordinal)
                    .Select(entry => new HelpRow(
                        KeyFormatter.Display(entry.Binding.Sequence),
                        entry.Text,
                        string.Empty,
                        entry.IsUnbound ? null : entry.Binding.Action))]))];
    }

    private static BindingSlot Slot(KeyBinding binding) => new(binding.Mode, binding.View, binding.Sequence);

    // The palette and the paths only the mouse reaches (their keys are in the tabs above).
    private static List<HelpSection> CommandsSections() =>
    [
        new("Command palette",
        [
            new HelpRow(":", "Type to filter every action by title or id; Enter runs it, Esc closes"),
        ]),
        new("Help",
        [
            new HelpRow("/", "Filter the rows of every section by key, title or action id; Enter keeps the filter, Esc clears it, then closes"),
        ]),
        new("Mouse",
        [
            new HelpRow("Click", "Focus a pane, select a row, switch a tab or a help section; outside a popup it closes"),
            new HelpRow("Double-click", "Graph row: the actions menu; Changes view file: stage or unstage it"),
            new HelpRow("...", "Marker on the selected sidebar row: its actions menu"),
            new HelpRow("Wheel", "Scroll the pane under the pointer"),
            new HelpRow("Drag", "Select a range of rows; drag a divider to resize the panes"),
            new HelpRow("Tab row", "Chips: commands (palette), + (new tab), fetch, pull and push"),
        ]),
    ];

    // The repeat keys only work once a first step started the mode, so the page says how it starts (keys from the keymap).
    private static HelpSection RepeatStartSection(KeyBindingTable table)
    {
        var resizeKeys = StartKeys(
            table,
            KeyBindingTable.GlobalView,
            ActionIds.PaneWider,
            ActionIds.PaneNarrower,
            ActionIds.PaneTaller,
            ActionIds.PaneShorter);
        var contextKeys = StartKeys(table, DefaultKeymap.DiffView, ActionIds.DiffMoreContext, ActionIds.DiffLessContext);

        return new HelpSection(
            "How it starts",
            [
                new HelpRow(
                    resizeKeys.Count > 0 ? resizeKeys[0] : string.Empty,
                    "Resize a pane: this first step turns the mode on, then the keys below repeat it",
                    string.Join(AlsoSeparator, resizeKeys.Skip(1))),
                new HelpRow(
                    contextKeys.Count > 0 ? contextKeys[0] : string.Empty,
                    "In a diff: the first context step turns the mode on the same way",
                    string.Join(AlsoSeparator, contextKeys.Skip(1)),
                    null,
                    true),
            ]);
    }

    private static List<string> StartKeys(KeyBindingTable table, string view, params ActionId[] actions) =>
    [
        .. actions
            .Select(action => table.Bindings.FirstOrDefault(
                binding => binding.View == view && binding.Mode == KeyMode.Normal && binding.Action == action))
            .OfType<KeyBinding>()
            .Select(binding => KeyFormatter.Display(binding.Sequence)),
    ];

    // What the marks after a branch name mean (the Unicode mark, then its ASCII fallback); not tied to a key.
    private static HelpSection MarkersSection() =>
        new(MarkersTitle, [new HelpRow(UpstreamGoneKeys, UpstreamGoneText)]);

    private sealed record AllTabSpec(string Title, string[] Views);

    private sealed record HelpDraft(ActionId Action, string Keys, string Also, string Description);

    private sealed record CustomEntry(KeyBinding Binding, string Text, bool IsUnbound = false);
}
