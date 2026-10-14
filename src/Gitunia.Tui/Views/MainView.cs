using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Rendering.Styling;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Window = Terminal.Gui.Views.Window;

namespace Gitunia.Tui.Views;

/// <summary>The first screen: tab row, the framed panes and the status bar, laid out like <see cref="ScreenLayout"/>.</summary>
public sealed class MainView : IDisposable
{
    private readonly TabBarView _tabs;
    private readonly PickerView _picker;
    private readonly LineView _status;
    private readonly LineView _sidebarFilter;
    private readonly FrameStripView _divider;
    private readonly FrameStripView[] _edges;
    private readonly LineView _detailTitle;
    private readonly LineView _diffTitle;
    private readonly FrameStripView _bottomDividerView;
    /// <summary>The view name reported for the wheel over an overlay; the application maps it to the open overlay's own view.</summary>
    public const string OverlayGestureView = "Overlay";

    private static readonly PaneRect Empty = new(0, 0, 0, 0);

    private readonly Func<ScreenModel> _model;
    private readonly GraphGlyphSet _glyphs;
    private readonly Func<PaneArrangement> _arrangement;
    private Func<bool> _suppressed = () => false;
    private GridKey? _gridKey;
    private FrameGrid? _grid;

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
        _arrangement = arrangement;
        _model = model;
        _glyphs = glyphs;
        _tabs = new TabBarView(model, glyphs)
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
        _sidebarFilter = new LineView(width =>
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
        _edges =
        [
            new FrameStripView(CurrentGrid) { X = 0, Y = ScreenLayout.FrameTop, Width = Dim.Fill(), Height = 1 },
            new FrameStripView(CurrentGrid) { X = 0, Y = Pos.Func(_ => ScreenLayout.FrameBottom(ScreenHeight())), Width = Dim.Fill(), Height = 1 },
            new FrameStripView(CurrentGrid) { X = 0, Y = ScreenLayout.GraphTop, Width = 1, Height = Dim.Func(_ => ScreenLayout.GraphHeight(ScreenHeight())) },
            new FrameStripView(CurrentGrid) { X = Pos.Func(_ => Math.Max(0, ScreenWidth - 1)), Y = ScreenLayout.GraphTop, Width = 1, Height = Dim.Func(_ => ScreenLayout.GraphHeight(ScreenHeight())) },
        ];
        _divider = new FrameStripView(CurrentGrid)
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
        _detailTitle = new LineView(width => CurrentGrid().ResolveRules(detailTitleLine(width), Rects().DetailTitle.Y, Rects().DetailTitle.X))
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
        Detail.SetScheme(Palette.PanelScheme());
        _bottomDividerView = new FrameStripView(CurrentGrid)
        {
            X = Pos.Func(_ => Rects().BottomDivider.X),
            Y = Pos.Func(_ => Rects().BottomDivider.Y),
            Width = Dim.Func(_ => Rects().BottomDivider.Width),
            Height = Dim.Func(_ => Rects().BottomDivider.Height),
        };
        _diffTitle = new LineView(width => CurrentGrid().ResolveRules(diffTitleLine(width), Rects().DiffTitle.Y, Rects().DiffTitle.X))
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
        _picker = new PickerView(model, glyphs)
        {
            X = 1,
            Y = ScreenLayout.GraphTop,
            Width = Dim.Func(_ => ScreenLayout.InnerWidth(ScreenWidth)),
            Height = Dim.Func(_ => ScreenLayout.GraphHeight(ScreenHeight())),
            Visible = false,
        };
        _status = new LineView(width => ChromeBuilder.Status(model(), width, glyphs))
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1,
        };
        Window = new Window { Title = string.Empty, BorderStyle = LineStyle.None, Width = Dim.Fill(), Height = Dim.Fill() };
        Overlay = new OverlayView();
        _tabs.TabClicked += number => Gestured?.Invoke(DefaultMouseMap.TabsView, MouseGesture.Click, number);
        _tabs.NewTabClicked += () => Gestured?.Invoke(DefaultMouseMap.NewTabButton, MouseGesture.Click, null);
        _tabs.CommandsClicked += () => Gestured?.Invoke(DefaultMouseMap.CommandsButton, MouseGesture.Click, null);
        _picker.GestureRaised += gesture => Gestured?.Invoke(DefaultKeymap.PickerView, gesture, null);
        _picker.ButtonClicked += buttonView => Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        Graph.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.GraphView, gesture, target);
        Sidebar.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.SidebarView, gesture, target);
        Detail.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.DetailView, gesture, target);
        Diff.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.DiffView, gesture, target);
        _divider.Dragged += (column, row) => EdgeDragged?.Invoke(PaneEdge.Sidebar, column, row);
        _bottomDividerView.Dragged += (column, row) => EdgeDragged?.Invoke(PaneEdge.Detail, column, row);
        _detailTitle.Dragged += (column, row) => EdgeDragged?.Invoke(PaneEdge.Bottom, column, row);
        _diffTitle.Dragged += (column, row) => EdgeDragged?.Invoke(PaneEdge.Bottom, column, row);
        _detailTitle.Clicked += OnDetailTitleClicked;
        _diffTitle.Clicked += OnDiffTitleClicked;
        Overlay.LineClicked += line => OverlayLineClicked?.Invoke(line);
        Overlay.GestureRaised += gesture => Gestured?.Invoke(OverlayGestureView, gesture, null);
        Window.Add(_tabs, _edges[0], _edges[1], _edges[2], _edges[3], Sidebar, _sidebarFilter, _divider, Graph, _detailTitle, Detail, _bottomDividerView, _diffTitle, Diff, _picker, _status, Overlay);
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
        add => _picker.RowClicked += value;
        remove => _picker.RowClicked -= value;
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
    public ScreenSize ScreenSize => new(Window.Viewport.Width, Window.Viewport.Height);

    /// <summary>Makes every pane and the picker swallow mouse input while <paramref name="isSuppressed"/> returns true (an overlay is open).</summary>
    public void SuppressMouseWhen(Func<bool> isSuppressed)
    {
        _suppressed = isSuppressed;
        Graph.IsSuppressed = isSuppressed;
        Sidebar.IsSuppressed = isSuppressed;
        Detail.IsSuppressed = isSuppressed;
        Diff.IsSuppressed = isSuppressed;
        _picker.IsSuppressed = isSuppressed;
    }

    /// <summary>Shows the repository picker in place of the graph, or the graph again.</summary>
    public void ShowPicker(bool isOpen)
    {
        if (_picker.Visible == isOpen)
        {
            return;
        }

        _picker.Visible = isOpen;
        Graph.Visible = !isOpen;
        if (!isOpen)
        {
            Graph.SetFocus();
        }

        _picker.SetNeedsDraw();
        Graph.SetNeedsDraw();
    }

    private int ScreenWidth => Window?.Viewport.Width ?? 0;

    /// <summary>Redraws the tab row, frame and status bar after the model changed.</summary>
    public void RefreshChrome()
    {
        _tabs.SetNeedsDraw();
        _status.SetNeedsDraw();
        _picker.SetNeedsDraw();
        _sidebarFilter.SetNeedsDraw();
        _divider.SetNeedsDraw();
        foreach (var edge in _edges)
        {
            edge.SetNeedsDraw();
        }

        Sidebar.SetNeedsDraw();
        _detailTitle.SetNeedsDraw();
        _diffTitle.SetNeedsDraw();
        _bottomDividerView.SetNeedsDraw();
    }

    public void Dispose() => Window.Dispose();

    private int ScreenHeight() => Window?.Viewport.Height ?? 0;

    // A click on a pane's title row focuses the pane, like a click in its list.
    private void FocusFromTitle(PaneListView pane)
    {
        if (!_suppressed() && !pane.HasFocus)
        {
            pane.SetFocus();
        }
    }

    private void OnDetailTitleClicked(int column)
    {
        if (_suppressed())
        {
            return;
        }

        FocusFromTitle(Detail);
        var selectedTab = (Detail.Source as DetailListSource)?.Content.Tab ?? BottomTab.Changes;
        if (TitleButton.ViewAt(DetailTabStrip.Layout(_detailTitle.Viewport.Width, selectedTab), column) is { } tabView)
        {
            Gestured?.Invoke(tabView, MouseGesture.Click, null);
        }
    }

    private void OnDiffTitleClicked(int column)
    {
        if (_suppressed())
        {
            return;
        }

        FocusFromTitle(Diff);
        var buttons = DiffTitleButtons.Layout(_diffTitle.Viewport.Width);
        if (TitleButton.ViewAt(buttons, column) is { } buttonView)
        {
            Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        }
    }

    private FrameGrid CurrentGrid()
    {
        var isPicker = _picker.Visible;
        var rects = isPicker
            ? new PaneRects(Empty, Empty, Empty, new PaneRect(1, ScreenLayout.GraphTop, ScreenLayout.InnerWidth(ScreenWidth), ScreenLayout.GraphHeight(ScreenHeight())), Empty, Empty, Empty, Empty, Empty)
            : Rects();
        PaneKind? focused = isPicker ? PaneKind.Graph : Sidebar.HasFocus ? PaneKind.Sidebar : Detail.HasFocus ? PaneKind.Detail : Diff.HasFocus ? PaneKind.Diff : PaneKind.Graph;
        var title = _model().RepositoryName.Length > 0 ? _model().RepositoryName : "Repo";
        var key = new GridKey(ScreenWidth, ScreenHeight(), rects, focused, title, isPicker, _glyphs.IsAscii);
        if (_grid is null || _gridKey != key)
        {
            _grid = FrameGrid.Build(ScreenWidth, ScreenHeight(), rects, focused, _glyphs, isPicker ? string.Empty : title, isPicker ? string.Empty : "History");
            _gridKey = key;
        }

        return _grid;
    }

    private PaneRects Rects()
    {
        var current = _arrangement();
        return PaneLayout.Compute(ScreenWidth, ScreenHeight(), current.IsDiffOpen, current.Zoomed, current.Sizes);
    }
}
