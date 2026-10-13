using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Core.Workspaces;
using Gitunia.Tui.Rendering;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Window = Terminal.Gui.Views.Window;

namespace Gitunia.Tui.Views;

/// <summary>The first screen: tab row, the framed panes and the status bar, laid out like <see cref="ScreenLayout"/>.</summary>
public sealed class MainView : IDisposable
{
    private readonly TabBarView tabs;
    private readonly PickerView picker;
    private readonly LineView status;
    private readonly LineView sidebarFilter;
    private readonly FrameStripView divider;
    private readonly FrameStripView[] edges;
    private readonly LineView detailTitle;
    private readonly LineView diffTitle;
    private readonly FrameStripView bottomDividerView;
    /// <summary>The view name reported for the wheel over an overlay; the application maps it to the open overlay's own view.</summary>
    public const string OverlayGestureView = "Overlay";

    private static readonly PaneRect Empty = new(0, 0, 0, 0);

    private readonly Func<ScreenModel> model;
    private readonly GraphGlyphSet glyphs;
    private readonly Func<PaneArrangement> arrangement;
    private Func<bool> suppressed = () => false;
    private (int Width, int Height, PaneRects Rects, PaneKind? Focused, string Title, bool IsPicker, bool IsAscii)? gridKey;
    private FrameGrid? grid;

    public MainView(
        Func<ScreenModel> model,
        Func<int, StyledLine> detailTitleLine,
        GraphGlyphSet glyphs,
        SidebarListSource sidebarSource,
        Func<SidebarChrome> sidebarChrome,
        Func<PaneArrangement> arrangement,
        Func<int, StyledLine> diffTitleLine,
        DiffListSource diffSource)
    {
        this.arrangement = arrangement;
        this.model = model;
        this.glyphs = glyphs;
        tabs = new TabBarView(model, glyphs)
        {
            X = 0,
            Y = ScreenLayout.TabRow,
            Width = Dim.Fill(),
            Height = 1,
        };
        Sidebar = new PaneListView
        {
            X = 1,
            Y = ScreenLayout.GraphTop,
            Width = Dim.Func(_ => Rects().Sidebar.Width),
            Height = Dim.Func(_ => Rects().Sidebar.Height),
            CanFocus = true,
            Source = sidebarSource,
        };
        sidebarFilter = new LineView(width =>
        {
            var chrome = sidebarChrome();
            return PanePadding.Wrap(SidebarRowRenderer.FilterLine(chrome.FilterText, chrome.IsEditingFilter, PanePadding.TextWidth(width), glyphs));
        })
        {
            X = 1,
            Y = Pos.Func(_ => Rects().SidebarFilter.Y),
            Width = Dim.Func(_ => Rects().SidebarFilter.Width),
            Height = Dim.Func(_ => Rects().SidebarFilter.Height),
        };
        edges =
        [
            new FrameStripView(CurrentGrid) { X = 0, Y = ScreenLayout.FrameTop, Width = Dim.Fill(), Height = 1 },
            new FrameStripView(CurrentGrid) { X = 0, Y = Pos.Func(_ => ScreenLayout.FrameBottom(ScreenHeight())), Width = Dim.Fill(), Height = 1 },
            new FrameStripView(CurrentGrid) { X = 0, Y = ScreenLayout.GraphTop, Width = 1, Height = Dim.Func(_ => ScreenLayout.GraphHeight(ScreenHeight())) },
            new FrameStripView(CurrentGrid) { X = Pos.Func(_ => Math.Max(0, ScreenWidth - 1)), Y = ScreenLayout.GraphTop, Width = 1, Height = Dim.Func(_ => ScreenLayout.GraphHeight(ScreenHeight())) },
        ];
        divider = new FrameStripView(CurrentGrid)
        {
            X = Pos.Func(_ => Rects().SidebarDivider.X),
            Y = ScreenLayout.GraphTop,
            Width = Dim.Func(_ => Rects().SidebarDivider.Width),
            Height = Dim.Func(_ => Rects().SidebarDivider.Height),
        };
        Graph = new PaneListView
        {
            X = Pos.Func(_ => Rects().Graph.X),
            Y = ScreenLayout.GraphTop,
            Width = Dim.Func(_ => Rects().Graph.Width),
            Height = Dim.Func(_ => Rects().Graph.Height),
            CanFocus = true,
        };
        detailTitle = new LineView(width => CurrentGrid().ResolveRules(detailTitleLine(width), Rects().DetailTitle.Y, Rects().DetailTitle.X))
        {
            X = Pos.Func(_ => Rects().DetailTitle.X),
            Y = Pos.Func(_ => Rects().DetailTitle.Y),
            Width = Dim.Func(_ => Rects().DetailTitle.Width),
            Height = Dim.Func(_ => Rects().DetailTitle.Height),
        };
        Detail = new PaneListView
        {
            X = Pos.Func(_ => Rects().Detail.X),
            Y = Pos.Func(_ => Rects().Detail.Y),
            Width = Dim.Func(_ => Rects().Detail.Width),
            Height = Dim.Func(_ => Rects().Detail.Height),
            CanFocus = true,
        };
        bottomDividerView = new FrameStripView(CurrentGrid)
        {
            X = Pos.Func(_ => Rects().BottomDivider.X),
            Y = Pos.Func(_ => Rects().BottomDivider.Y),
            Width = Dim.Func(_ => Rects().BottomDivider.Width),
            Height = Dim.Func(_ => Rects().BottomDivider.Height),
        };
        diffTitle = new LineView(width => CurrentGrid().ResolveRules(diffTitleLine(width), Rects().DiffTitle.Y, Rects().DiffTitle.X))
        {
            X = Pos.Func(_ => Rects().DiffTitle.X),
            Y = Pos.Func(_ => Rects().DiffTitle.Y),
            Width = Dim.Func(_ => Rects().DiffTitle.Width),
            Height = Dim.Func(_ => Rects().DiffTitle.Height),
        };
        Diff = new PaneListView
        {
            X = Pos.Func(_ => Rects().Diff.X),
            Y = Pos.Func(_ => Rects().Diff.Y),
            Width = Dim.Func(_ => Rects().Diff.Width),
            Height = Dim.Func(_ => Rects().Diff.Height),
            CanFocus = true,
            Source = diffSource,
        };
        picker = new PickerView(model, glyphs)
        {
            X = 1,
            Y = ScreenLayout.GraphTop,
            Width = Dim.Func(_ => ScreenLayout.InnerWidth(ScreenWidth)),
            Height = Dim.Func(_ => ScreenLayout.GraphHeight(ScreenHeight())),
            Visible = false,
        };
        status = new LineView(width => ChromeBuilder.Status(model(), width, glyphs))
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1,
        };
        Window = new Window { Title = string.Empty, BorderStyle = Terminal.Gui.Drawing.LineStyle.None, Width = Dim.Fill(), Height = Dim.Fill() };
        Overlay = new OverlayView();
        tabs.TabClicked += number => Gestured?.Invoke(DefaultMouseMap.TabsView, MouseGesture.Click, number);
        tabs.CommandsClicked += () => Gestured?.Invoke(DefaultMouseMap.CommandsButton, MouseGesture.Click, null);
        picker.GestureRaised += gesture => Gestured?.Invoke(DefaultKeymap.PickerView, gesture, null);
        picker.ButtonClicked += buttonView => Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        Graph.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.GraphView, gesture, target);
        Sidebar.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.SidebarView, gesture, target);
        Detail.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.DetailView, gesture, target);
        Diff.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.DiffView, gesture, target);
        divider.Dragged += (column, row) => EdgeDragged?.Invoke(PaneEdge.Sidebar, column, row);
        bottomDividerView.Dragged += (column, row) => EdgeDragged?.Invoke(PaneEdge.Detail, column, row);
        detailTitle.Dragged += (column, row) => EdgeDragged?.Invoke(PaneEdge.Bottom, column, row);
        diffTitle.Dragged += (column, row) => EdgeDragged?.Invoke(PaneEdge.Bottom, column, row);
        detailTitle.Clicked += OnDetailTitleClicked;
        diffTitle.Clicked += OnDiffTitleClicked;
        Overlay.LineClicked += line => OverlayLineClicked?.Invoke(line);
        Overlay.GestureRaised += gesture => Gestured?.Invoke(OverlayGestureView, gesture, null);
        Window.Add(tabs, edges[0], edges[1], edges[2], edges[3], Sidebar, sidebarFilter, divider, Graph, detailTitle, Detail, bottomDividerView, diffTitle, Diff, picker, status, Overlay);
    }

    /// <summary>
    /// Raised for a mouse gesture the mouse map decides on: the keymap view name of the pane (or <c>Tabs</c>, <c>Picker</c>),
    /// the gesture and, for the tab row, the one-based number of the clicked tab.
    /// </summary>
    public event Action<string, MouseGesture, int?>? Gestured;

    /// <summary>Raised while a divider or a pane title row is dragged: which edge and the pointer's screen column and row.</summary>
    public event Action<PaneEdge, int, int>? EdgeDragged;

    /// <summary>Raised with the frame line when the overlay box is clicked.</summary>
    public event Action<int>? OverlayLineClicked;

    /// <summary>Raised with the row index when a picker row is clicked (the highlight follows the pointer).</summary>
    public event Action<int>? PickerRowClicked
    {
        add => picker.RowClicked += value;
        remove => picker.RowClicked -= value;
    }

    public Window Window { get; }

    public PaneListView Graph { get; }

    public PaneListView Sidebar { get; }

    /// <summary>The commit detail pane below the graph.</summary>
    public PaneListView Detail { get; }

    /// <summary>The diff pane, beside or in place of the detail pane.</summary>
    public PaneListView Diff { get; }

    /// <summary>The floating box for the help, the palette and the which-key popup; added last so it draws on top.</summary>
    public OverlayView Overlay { get; }

    /// <summary>The terminal size in columns and rows.</summary>
    public (int Width, int Height) ScreenSize => (Window.Viewport.Width, Window.Viewport.Height);

    /// <summary>Makes every pane and the picker swallow mouse input while <paramref name="isSuppressed"/> returns true (an overlay is open).</summary>
    public void SuppressMouseWhen(Func<bool> isSuppressed)
    {
        suppressed = isSuppressed;
        Graph.IsSuppressed = isSuppressed;
        Sidebar.IsSuppressed = isSuppressed;
        Detail.IsSuppressed = isSuppressed;
        Diff.IsSuppressed = isSuppressed;
        picker.IsSuppressed = isSuppressed;
    }

    /// <summary>Shows the repo picker in place of the graph, or the graph again.</summary>
    public void ShowPicker(bool isOpen)
    {
        if (picker.Visible == isOpen)
        {
            return;
        }

        picker.Visible = isOpen;
        Graph.Visible = !isOpen;
        if (!isOpen)
        {
            Graph.SetFocus();
        }

        picker.SetNeedsDraw();
        Graph.SetNeedsDraw();
    }

    private int ScreenWidth => Window?.Viewport.Width ?? 0;

    /// <summary>Redraws the tab row, frame and status bar after the model changed.</summary>
    public void RefreshChrome()
    {
        tabs.SetNeedsDraw();
        status.SetNeedsDraw();
        picker.SetNeedsDraw();
        sidebarFilter.SetNeedsDraw();
        divider.SetNeedsDraw();
        foreach (var edge in edges)
        {
            edge.SetNeedsDraw();
        }

        Sidebar.SetNeedsDraw();
        detailTitle.SetNeedsDraw();
        diffTitle.SetNeedsDraw();
        bottomDividerView.SetNeedsDraw();
    }

    public void Dispose() => Window.Dispose();

    private int ScreenHeight() => Window?.Viewport.Height ?? 0;

    // A click on a pane's title row focuses the pane, like a click in its list.
    private void FocusFromTitle(PaneListView pane)
    {
        if (!suppressed() && !pane.HasFocus)
        {
            pane.SetFocus();
        }
    }

    private void OnDetailTitleClicked(int column)
    {
        if (suppressed())
        {
            return;
        }

        FocusFromTitle(Detail);
        if (TitleButton.ViewAt(DetailTabStrip.Layout(detailTitle.Viewport.Width), column) is { } tabView)
        {
            Gestured?.Invoke(tabView, MouseGesture.Click, null);
        }
    }

    private void OnDiffTitleClicked(int column)
    {
        if (suppressed())
        {
            return;
        }

        FocusFromTitle(Diff);
        var buttons = DiffTitleButtons.Layout(diffTitle.Viewport.Width);
        if (TitleButton.ViewAt(buttons, column) is { } buttonView)
        {
            Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        }
    }

    private FrameGrid CurrentGrid()
    {
        var isPicker = picker.Visible;
        var rects = isPicker
            ? new PaneRects(Empty, Empty, Empty, new PaneRect(1, ScreenLayout.GraphTop, ScreenLayout.InnerWidth(ScreenWidth), ScreenLayout.GraphHeight(ScreenHeight())), Empty, Empty, Empty, Empty, Empty)
            : Rects();
        PaneKind? focused = isPicker ? PaneKind.Graph : Sidebar.HasFocus ? PaneKind.Sidebar : Detail.HasFocus ? PaneKind.Detail : Diff.HasFocus ? PaneKind.Diff : PaneKind.Graph;
        var title = model().RepositoryName.Length > 0 ? model().RepositoryName : "Repo";
        var key = (ScreenWidth, ScreenHeight(), rects, focused, title, isPicker, glyphs.IsAscii);
        if (grid is null || gridKey != key)
        {
            grid = FrameGrid.Build(ScreenWidth, ScreenHeight(), rects, focused, glyphs, isPicker ? string.Empty : title, isPicker ? string.Empty : "History");
            gridKey = key;
        }

        return grid;
    }

    private PaneRects Rects()
    {
        var current = arrangement();
        return PaneLayout.Compute(ScreenWidth, ScreenHeight(), current.IsDiffOpen, current.Zoomed, current.Sizes);
    }
}
