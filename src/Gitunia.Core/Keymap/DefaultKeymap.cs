using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Keymap;

/// <summary>
/// The default bindings, written as data. They mirror the keymap table in <c>docs/ux.md</c> (a test checks
/// that); user config in M4 builds a <see cref="KeyBindingTable"/> from these plus overrides.
/// </summary>
public static class DefaultKeymap
{
    public const string ListsView = "Lists";
    public const string GraphView = "Graph";
    public const string DetailView = "Detail";
    public const string SidebarView = "Sidebar";
    public const string DiffView = "Diff";
    public const string WorkingUnstagedView = "WorkingUnstaged";
    public const string WorkingStagedView = "WorkingStaged";
    public const string PickerView = "Picker";
    public const string PaletteView = "Palette";
    public const string HelpView = "Help";
    public const string MessagesView = "Messages";

    private static readonly KeyMode[] Navigation = [KeyMode.Normal, KeyMode.Visual];
    private static readonly KeyMode[] NormalOnly = [KeyMode.Normal];
    private static readonly KeyMode[] TextEntry = [KeyMode.Normal, KeyMode.Visual, KeyMode.Insert];

    /// <summary>Views that inherit from <c>Lists</c>; the others inherit from <c>Global</c> only.</summary>
    public static IReadOnlyDictionary<string, string> ViewParents { get; } = new Dictionary<string, string>
    {
        [GraphView] = ListsView,
        [DetailView] = ListsView,
        [SidebarView] = ListsView,
        [DiffView] = ListsView,
        [WorkingUnstagedView] = ListsView,
        [WorkingStagedView] = ListsView,
        [ListsView] = KeyBindingTable.GlobalView,
        [PickerView] = KeyBindingTable.GlobalView,
        [PaletteView] = KeyBindingTable.GlobalView,
        [HelpView] = ListsView,
        [MessagesView] = ListsView,
    };

    public static KeyBindingTable Create() => new(CreateBindings(), ViewParents);

    public static IReadOnlyList<KeyBinding> CreateBindings()
    {
        var bindings = new List<KeyBinding>();
        AddGlobal(bindings);
        AddLists(bindings);
        AddViews(bindings);
        AddWorkingTreeLists(bindings);
        AddPicker(bindings);
        AddPalette(bindings);
        AddModals(bindings);
        return bindings;
    }

    private static void AddGlobal(List<KeyBinding> bindings)
    {
        const string global = KeyBindingTable.GlobalView;
        Add(bindings, Navigation, global, "?", ActionIds.HelpShow);
        Add(bindings, NormalOnly, global, ":", ActionIds.PaletteOpen);
        Add(bindings, TextEntry, global, "Ctrl-p", ActionIds.PaletteOpen);
        Add(bindings, Navigation, global, "q", ActionIds.NavigateBack);
        Add(bindings, NormalOnly, global, "Tab", ActionIds.PaneNext);
        Add(bindings, NormalOnly, global, "Shift-Tab", ActionIds.PanePrevious);
        Add(bindings, NormalOnly, global, "gt", ActionIds.TabNext);
        Add(bindings, NormalOnly, global, "gT", ActionIds.TabPrevious);
        for (var number = 1; number <= 9; number++)
        {
            Add(bindings, TextEntry, global, $"Alt-{number}", ActionIds.TabGoTo(number));
        }

        Add(bindings, TextEntry, global, "Ctrl-t", ActionIds.PickerOpen);
        Add(bindings, NormalOnly, global, "<space>o", ActionIds.PickerOpen);
        Add(bindings, NormalOnly, global, "Ctrl-w c", ActionIds.TabClose);
        Add(bindings, NormalOnly, global, "Ctrl-w r", ActionIds.TabRename);
        Add(bindings, NormalOnly, global, "<space>p", ActionIds.RepositoryPin);
        Add(bindings, NormalOnly, global, "Ctrl-w h", ActionIds.PaneFocusLeft);
        Add(bindings, NormalOnly, global, "Ctrl-w j", ActionIds.PaneFocusDown);
        Add(bindings, NormalOnly, global, "Ctrl-w k", ActionIds.PaneFocusUp);
        Add(bindings, NormalOnly, global, "Ctrl-w l", ActionIds.PaneFocusRight);
        Add(bindings, NormalOnly, global, "Ctrl-w w", ActionIds.PaneNext);
        Add(bindings, TextEntry, global, "Ctrl-^", ActionIds.TabLastActive);
        Add(bindings, NormalOnly, global, "<space><Tab>", ActionIds.TabLastActive);
        Add(bindings, NormalOnly, global, "Ctrl-w >", ActionIds.PaneWider);
        Add(bindings, NormalOnly, global, "Ctrl-w <lt>", ActionIds.PaneNarrower);
        Add(bindings, NormalOnly, global, "Ctrl-w +", ActionIds.PaneTaller);
        Add(bindings, NormalOnly, global, "Ctrl-w -", ActionIds.PaneShorter);
        Add(bindings, NormalOnly, global, "Ctrl-w =", ActionIds.PaneResetSizes);
        Add(bindings, NormalOnly, global, "<space>z", ActionIds.PaneZoom);
        Add(bindings, NormalOnly, global, "<space>x", ActionIds.StatusDismiss);
        Add(bindings, NormalOnly, global, "<space>m", ActionIds.StatusHistory);
        Add(bindings, NormalOnly, global, "<space>s", ActionIds.WorkingTreeToggle);
        Add(bindings, [.. Enum.GetValues<KeyMode>()], global, "Esc", ActionIds.Cancel);
    }

    private static void AddLists(List<KeyBinding> bindings)
    {
        Add(bindings, Navigation, ListsView, "j", ActionIds.ListDown);
        Add(bindings, Navigation, ListsView, "k", ActionIds.ListUp);
        Add(bindings, Navigation, ListsView, "gg", ActionIds.ListTop);
        Add(bindings, Navigation, ListsView, "G", ActionIds.ListBottom);
        Add(bindings, NormalOnly, ListsView, "/", ActionIds.SearchOpen);
        Add(bindings, NormalOnly, ListsView, "n", ActionIds.SearchNext);
        Add(bindings, NormalOnly, ListsView, "N", ActionIds.SearchPrevious);
        Add(bindings, Navigation, ListsView, "Ctrl-d", ActionIds.ListHalfPageDown);
        Add(bindings, Navigation, ListsView, "Ctrl-u", ActionIds.ListHalfPageUp);
        Add(bindings, Navigation, ListsView, "Down", ActionIds.ListDown);
        Add(bindings, Navigation, ListsView, "Up", ActionIds.ListUp);
        Add(bindings, Navigation, ListsView, "PageDown", ActionIds.ListHalfPageDown);
        Add(bindings, Navigation, ListsView, "PageUp", ActionIds.ListHalfPageUp);
        Add(bindings, Navigation, ListsView, "v", ActionIds.VisualToggle);
        Add(bindings, [KeyMode.Visual], ListsView, "y", ActionIds.VisualCopy);
    }

    private static void AddViews(List<KeyBinding> bindings)
    {
        Add(bindings, NormalOnly, GraphView, "@", ActionIds.GraphJumpToHead);
        Add(bindings, NormalOnly, GraphView, "gr", ActionIds.GraphJumpToRef);
        Add(bindings, NormalOnly, GraphView, "Enter", ActionIds.GraphOpenDetails);
        Add(bindings, NormalOnly, GraphView, "y", ActionIds.GraphCopyHash);
        Add(bindings, NormalOnly, GraphView, "<space>r", ActionIds.RepositoryRefresh);
        Add(bindings, NormalOnly, GraphView, "<space>td", ActionIds.ToolDifftool);
        Add(bindings, NormalOnly, GraphView, "<space>tm", ActionIds.ToolMergetool);
        Add(bindings, NormalOnly, GraphView, "<space>te", ActionIds.ToolCommitEditor);
        AddBottomTabKeys(bindings, GraphView);

        Add(bindings, NormalOnly, DetailView, "Enter", ActionIds.DetailOpenDiff);
        Add(bindings, NormalOnly, DetailView, "l", ActionIds.DetailOpenDiff);
        Add(bindings, NormalOnly, DetailView, "<space>r", ActionIds.DetailRetry);
        Add(bindings, NormalOnly, DetailView, "y", ActionIds.DetailCopyPath);
        Add(bindings, NormalOnly, DetailView, "h", ActionIds.NavigateBack);
        Add(bindings, NormalOnly, DetailView, "Esc", ActionIds.NavigateBack);
        AddBottomTabKeys(bindings, DetailView);

        Add(bindings, NormalOnly, SidebarView, "l", ActionIds.SidebarExpand);
        Add(bindings, NormalOnly, SidebarView, "Enter", ActionIds.SidebarActivate);
        Add(bindings, NormalOnly, SidebarView, "h", ActionIds.SidebarCollapse);
        Add(bindings, NormalOnly, SidebarView, "/", ActionIds.SidebarFilter);
        Add(bindings, NormalOnly, SidebarView, "y", ActionIds.SidebarCopy);

        Add(bindings, NormalOnly, DiffView, "]c", ActionIds.DiffNextHunk);
        Add(bindings, NormalOnly, DiffView, "[c", ActionIds.DiffPreviousHunk);
        Add(bindings, NormalOnly, DiffView, "]f", ActionIds.DiffNextFile);
        Add(bindings, NormalOnly, DiffView, "[f", ActionIds.DiffPreviousFile);
        Add(bindings, NormalOnly, DiffView, "q", ActionIds.NavigateBack);
        Add(bindings, NormalOnly, DiffView, "Esc", ActionIds.NavigateBack);
        Add(bindings, NormalOnly, DiffView, "h", ActionIds.NavigateBack);
        Add(bindings, NormalOnly, DiffView, "w", ActionIds.DiffToggleWrap);
        Add(bindings, NormalOnly, DiffView, "L", ActionIds.DiffLoadAnyway);
        Add(bindings, NormalOnly, DiffView, "+", ActionIds.DiffMoreContext);
        Add(bindings, NormalOnly, DiffView, "-", ActionIds.DiffLessContext);
        Add(bindings, NormalOnly, DiffView, "F", ActionIds.DiffToggleWholeFile);
        Add(bindings, NormalOnly, DiffView, "W", ActionIds.DiffToggleIgnoreWhitespace);
        Add(bindings, NormalOnly, DiffView, "S", ActionIds.DiffToggleSideBySide);
        Add(bindings, NormalOnly, DiffView, "/", ActionIds.DiffSearchOpen);
        Add(bindings, NormalOnly, DiffView, "n", ActionIds.DiffSearchNext);
        Add(bindings, NormalOnly, DiffView, "N", ActionIds.DiffSearchPrevious);
        Add(bindings, NormalOnly, DiffView, "y", ActionIds.DiffCopyLine);
        Add(bindings, NormalOnly, DiffView, "Y", ActionIds.DiffCopyFile);
    }

    // The two file lists of the working-tree view share their keys; the side-specific ones (stage, unstage, discard) come with their issues.
    private static void AddWorkingTreeLists(List<KeyBinding> bindings)
    {
        foreach (var view in (string[])[WorkingUnstagedView, WorkingStagedView])
        {
            Add(bindings, NormalOnly, view, "<space>r", ActionIds.RepositoryRefresh);
            Add(bindings, NormalOnly, view, "y", ActionIds.WorkingTreeCopyPath);
            Add(bindings, NormalOnly, view, "h", ActionIds.NavigateBack);
            Add(bindings, NormalOnly, view, "Esc", ActionIds.NavigateBack);
        }
    }

    // The graph feeds the bottom pane with its selection, so the tabs switch from there too.
    private static void AddBottomTabKeys(List<KeyBinding> bindings, string view)
    {
        Add(bindings, NormalOnly, view, "]", ActionIds.DetailNextTab);
        Add(bindings, NormalOnly, view, "[", ActionIds.DetailPreviousTab);
        Add(bindings, NormalOnly, view, "gd", ActionIds.DetailTabDetails);
        Add(bindings, NormalOnly, view, "gc", ActionIds.DetailTabChanges);
        Add(bindings, NormalOnly, view, "gf", ActionIds.DetailTabFiles);
    }

    private static void AddPicker(List<KeyBinding> bindings)
    {
        Add(bindings, [KeyMode.Insert], PickerView, "Up", ActionIds.PickerPrevious);
        Add(bindings, [KeyMode.Insert], PickerView, "Down", ActionIds.PickerNext);
        Add(bindings, [KeyMode.Insert], PickerView, "Ctrl-j", ActionIds.PickerNext);
        Add(bindings, [KeyMode.Insert], PickerView, "Ctrl-k", ActionIds.PickerPrevious);
        Add(bindings, [KeyMode.Insert], PickerView, "Enter", ActionIds.PickerAccept);
        Add(bindings, [KeyMode.Insert], PickerView, "Ctrl-l", ActionIds.PickerAccept);
        Add(bindings, [KeyMode.Insert], PickerView, "Tab", ActionIds.PickerCompletePath);
        Add(bindings, [KeyMode.Insert], PickerView, "Right", ActionIds.PickerCompletePath);
        Add(bindings, [KeyMode.Insert], PickerView, "Alt-p", ActionIds.PickerPin);
        Add(bindings, [KeyMode.Insert], PickerView, "Alt-d", ActionIds.PickerRemoveRecent);
    }

    private static void AddPalette(List<KeyBinding> bindings)
    {
        Add(bindings, [KeyMode.Command], PaletteView, "Up", ActionIds.PalettePrevious);
        Add(bindings, [KeyMode.Command], PaletteView, "Down", ActionIds.PaletteNext);
    }

    private static void AddModals(List<KeyBinding> bindings)
    {
        const string global = KeyBindingTable.GlobalView;
        Add(bindings, [KeyMode.Insert], global, "Enter", ActionIds.InputAccept);
        Add(bindings, [KeyMode.Command], global, "Enter", ActionIds.CommandExecute);
        Add(bindings, [KeyMode.Confirm], global, "y", ActionIds.ConfirmYes);
        Add(bindings, [KeyMode.Confirm], global, "n", ActionIds.ConfirmNo);
    }

    private static void Add(List<KeyBinding> bindings, KeyMode[] modes, string view, string keys, ActionId action)
    {
        var sequence = KeySequenceParser.Parse(keys);
        foreach (var mode in modes)
        {
            bindings.Add(new KeyBinding(mode, view, sequence, action));
        }
    }
}
