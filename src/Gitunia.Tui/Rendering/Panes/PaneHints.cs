using Gitunia.Core.Workspaces.Panes;

namespace Gitunia.Tui.Rendering.Panes;

/// <summary>The key hints the status bar shows for the focused pane and for the repository picker.</summary>
public static class PaneHints
{
    private static readonly KeyHint[] Tail =
    [
        new("?", "help"),
        new(":", "commands"),
        new("q", "back"),
    ];

    private static readonly KeyHint[] Graph =
    [
        new("j/k", "move"),
        new("gg/G", "top/end"),
        new("Ctrl-d/u", "page"),
        new("@", "HEAD"),
        .. Tail,
    ];

    private static readonly KeyHint[] Sidebar =
    [
        new("j/k", "move"),
        new("l/Enter", "expand"),
        new("h", "collapse"),
        new("/", "filter"),
        .. Tail,
    ];

    private static readonly KeyHint[] Detail =
    [
        new("j/k", "move"),
        new("]/[", "tabs"),
        new("l", "open diff"),
        new("h", "back"),
        new("r", "retry"),
        .. Tail,
    ];

    private static readonly KeyHint[] Diff =
    [
        new("j/k", "scroll"),
        new("]c/[c", "hunk"),
        new("]f/[f", "file"),
        new("/", "search"),
        new("n/N", "next/prev match"),
        new("w", "wrap"),
        new("L", "load all"),
        new("h", "back"),
        .. Tail,
    ];

    private static readonly KeyHint[] Picker =
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
