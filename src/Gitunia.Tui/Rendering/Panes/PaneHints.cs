using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Panes;

namespace Gitunia.Tui.Rendering.Panes;

/// <summary>The key hints the status bar shows for the focused pane and for the repository picker. Help, commands and back come first so a narrow terminal cuts the pane's own hints, not those.</summary>
public static class PaneHints
{
    private static readonly ImmutableArray<KeyHint> Pinned =
    [
        new("?", "help"),
        new(":", "commands"),
        new("q", "back"),
    ];

    private static readonly ImmutableArray<KeyHint> Graph =
    [
        .. Pinned,
        new("j/k", "move"),
        new("gg/G", "top/end"),
        new("Ctrl-d/u", "page"),
        new("@", "HEAD"),
    ];

    private static readonly ImmutableArray<KeyHint> Sidebar =
    [
        .. Pinned,
        new("j/k", "move"),
        new("l/Enter", "expand"),
        new("h", "collapse"),
        new("/", "filter"),
    ];

    private static readonly ImmutableArray<KeyHint> Detail =
    [
        .. Pinned,
        new("j/k", "move"),
        new("]/[", "tabs"),
        new("l", "open diff"),
        new("h", "back"),
        new("r", "retry"),
    ];

    private static readonly ImmutableArray<KeyHint> Diff =
    [
        .. Pinned,
        new("j/k", "scroll"),
        new("]c/[c", "hunk"),
        new("]f/[f", "file"),
        new("/", "search"),
        new("n/N", "next/prev match"),
        new("w", "wrap"),
        new("L", "load all"),
        new("h", "back"),
    ];

    private static readonly ImmutableArray<KeyHint> Picker =
    [
        new("Enter", "open"),
        new("Tab", "accept"),
        new("Alt-p", "pin to top"),
        new("Alt-d", "forget"),
        new("Esc", "close"),
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
