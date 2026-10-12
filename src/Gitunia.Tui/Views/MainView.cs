using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Tui.Rendering;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Window = Terminal.Gui.Views.Window;

namespace Gitunia.Tui.Views;

/// <summary>The first screen: header, tab row, the graph list and the status bar, laid out like <see cref="ScreenLayout"/>.</summary>
public sealed class MainView : IDisposable
{
    private readonly LineView header;
    private readonly TabBarView tabs;
    private readonly PickerView picker;
    private readonly LineView status;
    private readonly LineView sidebarFilter;
    private readonly DividerView divider;
    private readonly LineView detailTitle;
    private readonly LineView diffTitle;
    private readonly DividerView bottomDividerView;
    /// <summary>The view name reported for the wheel over an overlay; the application maps it to the open overlay's own view.</summary>
    public const string OverlayGestureView = "Overlay";

    private readonly Func<PaneArrangement> arrangement;
    private Func<bool> suppressed = () => false;

    public MainView(
        Func<ScreenModel> model,
        Func<int, StyledLine> detailTitleLine,
        GraphGlyphSet glyphs,
        SidebarListSource sidebarSource,
        Func<SidebarChrome> sidebarChrome,
        Func<PaneArrangement> arrangement,
        Func<int, StyledLine> diffTitleLine,
        DiffListSource diffSource,
        Func<StyledSegment> bottomDivider)
    {
        this.arrangement = arrangement;
        header = new LineView(width => ChromeBuilder.Header(model(), width, glyphs))
        {
            X = 0,
            Y = ScreenLayout.HeaderRow,
            Width = Dim.Fill(),
            Height = 1,
        };
        tabs = new TabBarView(model, glyphs)
        {
            X = 0,
            Y = ScreenLayout.TabRow,
            Width = Dim.Fill(),
            Height = 1,
        };
        Sidebar = new PaneListView
        {
            X = 0,
            Y = ScreenLayout.GraphTop,
            Width = Dim.Func(_ => Rects().Sidebar.Width),
            Height = Dim.Func(_ => Rects().Sidebar.Height),
            CanFocus = true,
            Source = sidebarSource,
        };
        sidebarFilter = new LineView(width =>
        {
            var chrome = sidebarChrome();
            return SidebarRowRenderer.FilterLine(chrome.FilterText, chrome.IsEditingFilter, width, glyphs);
        })
        {
            X = 0,
            Y = Pos.Func(_ => Rects().SidebarFilter.Y),
            Width = Dim.Func(_ => Rects().SidebarFilter.Width),
            Height = Dim.Func(_ => Rects().SidebarFilter.Height),
        };
        divider = new DividerView(() => SidebarPane.Divider(sidebarChrome().IsFocused, glyphs))
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
        detailTitle = new LineView(detailTitleLine)
        {
            X = 0,
            Y = Pos.Func(_ => Rects().DetailTitle.Y),
            Width = Dim.Func(_ => Rects().DetailTitle.Width),
            Height = Dim.Func(_ => Rects().DetailTitle.Height),
        };
        Detail = new PaneListView
        {
            X = 0,
            Y = Pos.Func(_ => Rects().Detail.Y),
            Width = Dim.Func(_ => Rects().Detail.Width),
            Height = Dim.Func(_ => Rects().Detail.Height),
            CanFocus = true,
        };
        bottomDividerView = new DividerView(bottomDivider)
        {
            X = Pos.Func(_ => Rects().BottomDivider.X),
            Y = Pos.Func(_ => Rects().BottomDivider.Y),
            Width = Dim.Func(_ => Rects().BottomDivider.Width),
            Height = Dim.Func(_ => Rects().BottomDivider.Height),
        };
        diffTitle = new LineView(diffTitleLine)
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
            X = 0,
            Y = ScreenLayout.GraphTop,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
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
        header.Clicked += _ => Gestured?.Invoke(DefaultMouseMap.HeaderView, MouseGesture.Click, null);
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
        detailTitle.Clicked += _ => FocusFromTitle(Detail);
        diffTitle.Clicked += OnDiffTitleClicked;
        Overlay.LineClicked += line => OverlayLineClicked?.Invoke(line);
        Overlay.GestureRaised += gesture => Gestured?.Invoke(OverlayGestureView, gesture, null);
        Window.Add(header, tabs, Sidebar, sidebarFilter, divider, Graph, detailTitle, Detail, bottomDividerView, diffTitle, Diff, picker, status, Overlay);
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

    /// <summary>Redraws the header, tab row and status bar after the model changed.</summary>
    public void RefreshChrome()
    {
        header.SetNeedsDraw();
        tabs.SetNeedsDraw();
        status.SetNeedsDraw();
        picker.SetNeedsDraw();
        sidebarFilter.SetNeedsDraw();
        divider.SetNeedsDraw();
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

    private PaneRects Rects()
    {
        var current = arrangement();
        return PaneLayout.Compute(ScreenWidth, ScreenHeight(), current.IsDiffOpen, current.Zoomed, current.Sizes);
    }
}
