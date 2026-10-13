using Gitunia.Core.Workspaces;

namespace Gitunia.Tui.Rendering;

/// <summary>The key hints the status bar shows for the focused pane.</summary>
public static class PaneHints
{
    private const string Tail = " ? : q";

    public static string For(PaneKind pane) => pane switch
    {
        PaneKind.Sidebar => "j/k l/Enter h / filter" + Tail,
        PaneKind.Detail => "j/k [ ] tab Enter diff h back r retry" + Tail,
        PaneKind.Diff => "j/k ]c [c ]f [f / n N w wrap L all h back" + Tail,
        _ => "j/k gg G ^d ^u @ ? : q",
    };
}
