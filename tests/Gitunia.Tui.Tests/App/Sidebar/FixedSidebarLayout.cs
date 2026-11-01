using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.App.Sidebar;

namespace Gitunia.Tui.Tests.App.Sidebar;

/// <summary>A screen layout whose open panes a test sets.</summary>
public sealed class FixedSidebarLayout : ISidebarLayout
{
    public bool SidebarVisible { get; set; } = true;

    public bool GraphVisible { get; set; } = true;

    public bool DetailVisible { get; set; }

    public bool DiffVisible { get; set; }

    public int SidebarPageHeight => 10;

    public ImmutableArray<PaneKind> OpenPanes()
    {
        var panes = ImmutableArray.CreateBuilder<PaneKind>();
        if (SidebarVisible)
        {
            panes.Add(PaneKind.Sidebar);
        }

        if (GraphVisible)
        {
            panes.Add(PaneKind.Graph);
        }

        if (DetailVisible)
        {
            panes.Add(PaneKind.Detail);
        }

        if (DiffVisible)
        {
            panes.Add(PaneKind.Diff);
        }

        return panes.ToImmutable();
    }
}
