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
    private readonly Func<PaneArrangement> arrangement;

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
        Sidebar = new ListView
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
        Graph = new ListView
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
        Detail = new ListView
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
        Diff = new ListView
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
        Window.Add(header, tabs, Sidebar, sidebarFilter, divider, Graph, detailTitle, Detail, bottomDividerView, diffTitle, Diff, picker, status, Overlay);
    }

    /// <summary>Raised with the one-based tab number when a tab in the tab row is clicked.</summary>
    public event Action<int>? TabClicked
    {
        add => tabs.TabClicked += value;
        remove => tabs.TabClicked -= value;
    }

    /// <summary>Raised with the row index and whether it was a double-click when a picker row is clicked.</summary>
    public event Action<int, bool>? PickerRowClicked
    {
        add => picker.RowClicked += value;
        remove => picker.RowClicked -= value;
    }

    public Window Window { get; }

    public ListView Graph { get; }

    public ListView Sidebar { get; }

    /// <summary>The commit detail pane below the graph.</summary>
    public ListView Detail { get; }

    /// <summary>The diff pane, beside or in place of the detail pane.</summary>
    public ListView Diff { get; }

    /// <summary>The floating box for the help, the palette and the which-key popup; added last so it draws on top.</summary>
    public OverlayView Overlay { get; }

    /// <summary>The terminal size in columns and rows.</summary>
    public (int Width, int Height) ScreenSize => (Window.Viewport.Width, Window.Viewport.Height);

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

    private PaneRects Rects()
    {
        var current = arrangement();
        return PaneLayout.Compute(ScreenWidth, ScreenHeight(), current.IsDiffOpen, current.Zoomed);
    }
}
