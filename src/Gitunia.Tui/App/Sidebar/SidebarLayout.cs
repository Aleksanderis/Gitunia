using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Sidebar;

/// <summary>What the sidebar keys need of the screen layout.</summary>
/// <param name="panes">The sidebar list.</param>
/// <param name="geometry">Where the panes are.</param>
public sealed class SidebarLayout(PaneViews panes, PaneGeometry geometry) : ISidebarLayout
{
    /// <inheritdoc />
    public int SidebarPageHeight => panes.Sidebar.Viewport.Height;

    /// <inheritdoc />
    public ImmutableArray<PaneKind> OpenPanes()
    {
        var rects = geometry.Rects();
        var shown = ImmutableArray.CreateBuilder<PaneKind>();
        AddIfShown(shown, PaneKind.Sidebar, rects.Sidebar);
        AddIfShown(shown, PaneKind.Graph, rects.Graph);
        AddIfShown(shown, PaneKind.Detail, rects.Detail);
        AddIfShown(shown, PaneKind.Diff, rects.Diff);

        return shown.ToImmutable();
    }

    private static void AddIfShown(ImmutableArray<PaneKind>.Builder shown, PaneKind pane, PaneRect rect)
    {
        if (!rect.IsEmpty)
        {
            shown.Add(pane);
        }
    }
}
