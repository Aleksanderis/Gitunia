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

    public MainView(Func<ScreenModel> model, Func<int, StyledLine> detailTitleLine, GraphGlyphSet glyphs, SidebarListSource sidebarSource, Func<SidebarChrome> sidebarChrome)
    {
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
            Width = Dim.Func(_ => SidebarWidth),
            Height = Dim.Func(_ => Math.Max(0, DetailLayout.GraphHeight(ScreenHeight()) - 1)),
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
            Y = Pos.Func(_ => ScreenLayout.GraphTop + Math.Max(0, DetailLayout.GraphHeight(ScreenHeight()) - 1)),
            Width = Dim.Func(_ => SidebarWidth),
            Height = 1,
        };
        divider = new DividerView(() => SidebarPane.Divider(sidebarChrome().IsFocused, glyphs))
        {
            X = Pos.Func(_ => SidebarWidth),
            Y = ScreenLayout.GraphTop,
            Width = Dim.Func(_ => SidebarWidth > 0 ? 1 : 0),
            Height = Dim.Func(_ => DetailLayout.GraphHeight(ScreenHeight())),
        };
        Graph = new ListView
        {
            X = Pos.Func(_ => ScreenLayout.GraphLeft(ScreenWidth)),
            Y = ScreenLayout.GraphTop,
            Width = Dim.Fill(),
            Height = Dim.Func(_ => DetailLayout.GraphHeight(ScreenHeight())),
            CanFocus = true,
        };
        detailTitle = new LineView(detailTitleLine)
        {
            X = 0,
            Y = Pos.Func(_ => ScreenLayout.GraphTop + DetailLayout.GraphHeight(ScreenHeight())),
            Width = Dim.Fill(),
            Height = Dim.Func(_ => Math.Min(1, DetailLayout.PaneHeight(ScreenHeight()))),
        };
        Detail = new ListView
        {
            X = 0,
            Y = Pos.Func(_ => ScreenLayout.GraphTop + DetailLayout.GraphHeight(ScreenHeight()) + 1),
            Width = Dim.Fill(),
            Height = Dim.Func(_ => DetailLayout.ContentHeight(ScreenHeight())),
            CanFocus = true,
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
        Window.Add(header, tabs, Sidebar, sidebarFilter, divider, Graph, detailTitle, Detail, picker, status, Overlay);
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

    private int ScreenWidth => Window.Viewport.Width;

    private int SidebarWidth => ScreenLayout.SidebarWidth(ScreenWidth);

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
    }

    public void Dispose() => Window.Dispose();

    private int ScreenHeight() => Window?.Viewport.Height ?? 0;
}
