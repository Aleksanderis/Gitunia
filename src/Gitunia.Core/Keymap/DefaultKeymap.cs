using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Keymap.Repeat;

namespace Gitunia.Core.Keymap;

/// <summary>
/// The default bindings. They are read from the embedded <c>keymap.default.json</c> (one row per action, view and
/// mode set; see <see cref="KeymapRowReader"/>), and <c>docs/keymap.md</c> is generated from that file (a test checks it is current);
/// user config builds a <see cref="KeyBindingTable"/> from these plus overrides.
/// </summary>
public static class DefaultKeymap
{
    public const string ListsView = "Lists";
    public const string GraphView = "Graph";
    public const string DetailView = "Detail";

    /// <summary>The Detail pane while its Changes tab is selected: adds the keys that only act on a changed file list.</summary>
    public const string DetailChangesView = "DetailChanges";
    /// <summary>The detail pane while its Files tab is selected.</summary>
    public const string DetailFilesView = "DetailFiles";

    public const string SidebarView = "Sidebar";
    public const string DiffView = "Diff";
    public const string WorkingUnstagedView = "WorkingUnstaged";
    public const string WorkingStagedView = "WorkingStaged";
    public const string WorkingUnstagedDiffView = "WorkingUnstagedDiff";
    public const string WorkingStagedDiffView = "WorkingStagedDiff";
    public const string WorkingMessageView = "WorkingMessage";
    public const string PickerView = "Picker";
    public const string PaletteView = "Palette";
    public const string ContextMenuView = "ContextMenu";
    public const string HelpView = "Help";
    public const string MessagesView = "Messages";
    public const string ConfirmView = "Confirm";
    public const string ReflogView = "Reflog";
    public const string BlameView = "Blame";
    public const string RebaseEditorView = "RebaseEditor";
    public const string ConflictEditorView = "ConflictEditor";
    public const string ConflictSideView = "ConflictSide";
    public const string ConflictResultView = "ConflictResult";
    public const string SettingsView = "Settings";

    private const string ResourceName = "keymap.default.json";

    private static readonly Lazy<IReadOnlyList<KeymapRow>> Rows = new(LoadRows);

    /// <summary>Views that inherit from <c>Lists</c>; the others inherit from <c>Global</c> only.</summary>
    public static IReadOnlyDictionary<string, string> ViewParents { get; } = new Dictionary<string, string>
    {
        [GraphView] = ListsView,
        [DetailView] = ListsView,
        [DetailChangesView] = DetailView,
        [DetailFilesView] = DetailView,
        [SidebarView] = ListsView,
        [DiffView] = ListsView,
        [WorkingUnstagedView] = ListsView,
        [WorkingStagedView] = ListsView,
        [WorkingUnstagedDiffView] = ListsView,
        [WorkingStagedDiffView] = ListsView,
        [ListsView] = KeyBindingTable.GlobalView,
        [PickerView] = KeyBindingTable.GlobalView,
        [PaletteView] = KeyBindingTable.GlobalView,
        [ContextMenuView] = KeyBindingTable.GlobalView,
        [HelpView] = ListsView,
        [MessagesView] = ListsView,
        [ConfirmView] = KeyBindingTable.GlobalView,
        [ReflogView] = ListsView,
        [BlameView] = ListsView,
        [RebaseEditorView] = ListsView,
        [ConflictEditorView] = ListsView,
        [ConflictSideView] = ConflictEditorView,
        [ConflictResultView] = ConflictEditorView,
        [SettingsView] = ListsView,
        [RepeatMode.ContextView] = KeyBindingTable.GlobalView,
        [RepeatMode.ResizeView] = KeyBindingTable.GlobalView,
    };

    public static KeyBindingTable Create() => new(CreateBindings(), ViewParents);

    public static IReadOnlyList<KeyBinding> CreateBindings()
    {
        var bindings = new List<KeyBinding>();
        foreach (var row in Rows.Value)
        {
            foreach (var key in row.Keys)
            {
                var sequence = KeySequenceParser.Parse(key);
                foreach (var mode in row.Modes)
                {
                    bindings.Add(new KeyBinding(mode, row.View, sequence, row.Action));
                }
            }
        }

        return bindings;
    }

    private static IReadOnlyList<KeymapRow> LoadRows()
    {
        using var stream = typeof(DefaultKeymap).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return KeymapRowReader.Read(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    }
}
