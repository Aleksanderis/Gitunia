using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Views.Lists;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Window = Terminal.Gui.Views.Window;

namespace Gitunia.Tui.Views;

/// <summary>
/// The list widgets of the panes (history and Changes view) and the overlay box, with their places on the screen. They
/// need no content from the application: the controllers take them from here, and the main view puts the chrome around
/// them afterwards.
/// </summary>
public sealed class PaneViews : IPaneLists
{
    /// <param name="chrome">The window the widgets go in.</param>
    /// <param name="geometry">Where each pane is.</param>
    /// <param name="gate">Tells when an overlay owns the mouse.</param>
    /// <param name="content">What the list sources and the message box read.</param>
    public PaneViews(PaneChrome chrome, PaneGeometry geometry, IMouseGate gate, PaneViewContent content)
    {
        Chrome = chrome;
        Content = content;
        Sidebar = new PaneListView
        {
            OpensRowMenu = true,
            X = 1,
            Y = ScreenLayout.GraphTop,
            Width = Dim.Func(_ => geometry.Rects().Sidebar.Width),
            Height = Dim.Func(_ => geometry.Rects().Sidebar.Height),
            CanFocus = true,
            Source = content.SidebarSource,
            Gate = gate,
        };
        Graph = new PaneListView
        {
            X = Pos.Func(_ => geometry.Rects().Graph.X),
            Y = ScreenLayout.GraphTop,
            Width = Dim.Func(_ => geometry.Rects().Graph.Width),
            Height = Dim.Func(_ => geometry.Rects().Graph.Height),
            CanFocus = true,
            IgnoresBlankDoubleClick = true,
            Gate = gate,
        };
        Detail = new PaneListView
        {
            X = Pos.Func(_ => geometry.Rects().Detail.X),
            Y = Pos.Func(_ => geometry.Rects().Detail.Y),
            Width = Dim.Func(_ => geometry.Rects().Detail.Width),
            Height = Dim.Func(_ => geometry.Rects().Detail.Height),
            CanFocus = true,
            Gate = gate,
        };
        Diff = new PaneListView
        {
            X = Pos.Func(_ => geometry.Rects().Diff.X),
            Y = Pos.Func(_ => geometry.Rects().Diff.Y),
            Width = Dim.Func(_ => geometry.Rects().Diff.Width),
            Height = Dim.Func(_ => geometry.Rects().Diff.Height),
            CanFocus = true,
            Source = new DiffListSource(content.Glyphs),
            Gate = gate,
        };
        UnstagedSource = new WorkingTreeListSource(WorkingTreeSection.Unstaged, content.Glyphs);
        StagedSource = new WorkingTreeListSource(WorkingTreeSection.Staged, content.Glyphs);
        WorkingUnstaged = CreateWorkingList(UnstagedSource, gate, () => geometry.WorkingRects().Unstaged);
        WorkingStaged = CreateWorkingList(StagedSource, gate, () => geometry.WorkingRects().Staged);
        WorkingDiff = new PaneListView
        {
            X = Pos.Func(_ => geometry.WorkingRects().Diff.X),
            Y = Pos.Func(_ => geometry.WorkingRects().Diff.Y),
            Width = Dim.Func(_ => geometry.WorkingRects().Diff.Width),
            Height = Dim.Func(_ => geometry.WorkingRects().Diff.Height),
            CanFocus = true,
            Source = new DiffListSource(content.Glyphs) { GroupKeys = () => content.Keys },
            Visible = false,
            Gate = gate,
        };
        WorkingDiff.ButtonAt = (column, row) => (WorkingDiff.Source as DiffListSource)?.ButtonAt(WorkingDiff, column, row);
        WorkingMessage = new PaneListView
        {
            X = Pos.Func(_ => geometry.WorkingRects().MessageEditor.X),
            Y = Pos.Func(_ => geometry.WorkingRects().MessageEditor.Y),
            Width = Dim.Func(_ => geometry.WorkingRects().MessageEditor.Width),
            Height = Dim.Func(_ => geometry.WorkingRects().MessageEditor.Height),
            CanFocus = true,
            Source = new MessageBoxListSource(content.MessageBox, content.Glyphs),
            Visible = false,
            Gate = gate,
        };
        Overlay = new OverlayView();
        ApplyPaneSchemes();
    }

    /// <summary>The window that holds every view.</summary>
    public Window Window => Chrome.Window;

    /// <summary>The screen the panes sit in.</summary>
    public PaneChrome Chrome { get; }

    /// <summary>What the list sources and the message box read.</summary>
    public PaneViewContent Content { get; }

    /// <summary>The rows of the Unstaged list of the Changes view.</summary>
    public WorkingTreeListSource UnstagedSource { get; }

    /// <summary>The rows of the Staged list of the Changes view.</summary>
    public WorkingTreeListSource StagedSource { get; }

    /// <summary>The Unstaged file list of the Changes view.</summary>
    public PaneListView WorkingUnstaged { get; }

    /// <summary>The Staged file list of the Changes view.</summary>
    public PaneListView WorkingStaged { get; }

    /// <summary>The diff box of the Changes view.</summary>
    public PaneListView WorkingDiff { get; }

    /// <summary>The editor rows of the commit message box of the Changes view (the option row below them is a separate row).</summary>
    public PaneListView WorkingMessage { get; }

    /// <summary>The commit graph.</summary>
    public PaneListView Graph { get; }

    /// <summary>The sidebar.</summary>
    public PaneListView Sidebar { get; }

    /// <summary>The commit detail pane below the graph.</summary>
    public PaneListView Detail { get; }

    /// <summary>The diff pane, beside or in place of the detail pane.</summary>
    public PaneListView Diff { get; }

    /// <summary>The floating box for the help, the palette and the which-key popup; added last so it draws on top.</summary>
    public OverlayView Overlay { get; }

    /// <summary>The list panes, history and Changes view.</summary>
    public IEnumerable<PaneListView> Lists =>
        [Graph, Sidebar, Detail, Diff, WorkingUnstaged, WorkingStaged, WorkingDiff, WorkingMessage];

    /// <summary>The list that shows a pane; the graph's for a pane without a list of its own.</summary>
    public ListView ListOf(PaneKind pane) => PaneOf(pane);

    /// <summary>The list widget that shows a pane; the graph's for a pane without a list of its own.</summary>
    public PaneListView PaneOf(PaneKind pane) => pane switch
    {
        PaneKind.Sidebar => Sidebar,
        PaneKind.Detail => Detail,
        PaneKind.Diff => Diff,
        PaneKind.WorkingUnstaged => WorkingUnstaged,
        PaneKind.WorkingStaged => WorkingStaged,
        PaneKind.WorkingUnstagedDiff or PaneKind.WorkingStagedDiff => WorkingDiff,
        PaneKind.WorkingMessage => WorkingMessage,
        _ => Graph,
    };

    /// <summary>Gives every list the palette scheme again (after a theme change).</summary>
    public void ApplyPaneSchemes()
    {
        foreach (var pane in Lists)
        {
            pane.SetScheme(Palette.BaseScheme());
        }
    }

    private static PaneListView CreateWorkingList(WorkingTreeListSource source, IMouseGate gate, Func<PaneRect> rect) => new()
    {
        X = Pos.Func(_ => rect().X),
        Y = Pos.Func(_ => rect().Y),
        Width = Dim.Func(_ => rect().Width),
        Height = Dim.Func(_ => rect().Height),
        CanFocus = true,
        Source = source,
        Visible = false,
        Gate = gate,
    };
}
