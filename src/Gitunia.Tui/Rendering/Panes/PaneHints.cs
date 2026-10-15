using System.Collections.Immutable;
using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces.Panes;

namespace Gitunia.Tui.Rendering.Panes;

/// <summary>The key hints the status bar shows for the focused pane and for the repository picker. Help, commands and back come first so a narrow terminal cuts the pane's own hints, not those.</summary>
public static class PaneHints
{
    private static readonly ImmutableArray<KeyHint> Pinned =
    [
        new("?", "help", [ActionIds.HelpShow]),
        new(":", "commands", [ActionIds.PaletteOpen]),
        new("q", "back", [ActionIds.NavigateBack]),
    ];

    private static readonly ImmutableArray<KeyHint> Graph =
    [
        .. Pinned,
        new("j/k", "move", [ActionIds.ListDown, ActionIds.ListUp]),
        new("gg/G", "top/end", [ActionIds.ListTop, ActionIds.ListBottom]),
        new("Ctrl-d/u", "page", [ActionIds.ListHalfPageDown, ActionIds.ListHalfPageUp]),
        new("@", "HEAD", [ActionIds.GraphJumpToHead]),
    ];

    private static readonly ImmutableArray<KeyHint> Sidebar =
    [
        .. Pinned,
        new("j/k", "move", [ActionIds.ListDown, ActionIds.ListUp]),
        new("l/Enter", "expand", [ActionIds.SidebarExpand, ActionIds.SidebarActivate]),
        new("h", "collapse", [ActionIds.SidebarCollapse]),
        new("/", "filter", [ActionIds.SidebarFilter]),
    ];

    private static readonly ImmutableArray<KeyHint> Detail =
    [
        .. Pinned,
        new("j/k", "move", [ActionIds.ListDown, ActionIds.ListUp]),
        new("]/[", "tabs", [ActionIds.DetailNextTab, ActionIds.DetailPreviousTab]),
        new("l", "open diff", [ActionIds.DetailOpenDiff]),
        new("h", "back", [ActionIds.NavigateBack]),
        new("r", "retry", [ActionIds.DetailRetry]),
    ];

    private static readonly ImmutableArray<KeyHint> Diff =
    [
        .. Pinned,
        new("j/k", "scroll", [ActionIds.ListDown, ActionIds.ListUp]),
        new("]c/[c", "hunk", [ActionIds.DiffNextHunk, ActionIds.DiffPreviousHunk]),
        new("]f/[f", "file", [ActionIds.DiffNextFile, ActionIds.DiffPreviousFile]),
        new("/", "search", [ActionIds.DiffSearchOpen]),
        new("n/N", "next/prev match", [ActionIds.DiffSearchNext, ActionIds.DiffSearchPrevious]),
        new("w", "wrap", [ActionIds.DiffToggleWrap]),
        new("+/-", "context", [ActionIds.DiffMoreContext, ActionIds.DiffLessContext]),
        new("F", "whole file", [ActionIds.DiffToggleWholeFile]),
        new("W", "ignore space", [ActionIds.DiffToggleIgnoreWhitespace]),
        new("L", "load all", [ActionIds.DiffLoadAnyway]),
        new("h", "back", [ActionIds.NavigateBack]),
    ];

    private static readonly ImmutableArray<KeyHint> Picker =
    [
        new("Enter", "open", [ActionIds.PickerAccept]),
        new("Tab", "accept", [ActionIds.PickerCompletePath]),
        new("Alt-p", "pin to top", [ActionIds.PickerPin]),
        new("Alt-d", "forget", [ActionIds.PickerRemoveRecent]),
        new("Esc", "close", [ActionIds.Cancel]),
    ];

    public static IReadOnlyList<KeyHint> For(PaneKind pane) => pane switch
    {
        PaneKind.Sidebar => Sidebar,
        PaneKind.Detail => Detail,
        PaneKind.Diff => Diff,
        _ => Graph,
    };

    /// <summary>The hints while the repository picker is open.</summary>
    public static IReadOnlyList<KeyHint> ForPicker() => Picker;
}
