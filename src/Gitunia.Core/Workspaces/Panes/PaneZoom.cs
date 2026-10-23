namespace Gitunia.Core.Workspaces.Panes;

/// <summary>Pure rule for which panes can be zoomed to the full screen.</summary>
public static class PaneZoom
{
    /// <summary>The graph, the commit details and the diff can be zoomed; the sidebar cannot.</summary>
    public static bool CanZoom(PaneKind pane) => pane is PaneKind.Graph or PaneKind.Detail or PaneKind.Diff;

    /// <summary>The detail and diff panes sit side by side and zoom as one view when both are shown (#632).</summary>
    public static bool IsZoomPair(PaneKind first, PaneKind second) =>
        first is PaneKind.Detail or PaneKind.Diff && second is PaneKind.Detail or PaneKind.Diff;

    /// <summary>The zoomed pane after <c>&lt;space&gt;z</c> on <paramref name="focused"/>: unzoomed when it was zoomed already, null when it cannot be zoomed.</summary>
    public static PaneKind? Toggle(PaneKind? zoomed, PaneKind focused) =>
        zoomed == focused ? null : CanZoom(focused) ? focused : zoomed;
}
