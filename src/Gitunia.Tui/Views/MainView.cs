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

    public MainView(Func<ScreenModel> model, GraphGlyphSet glyphs)
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
        Graph = new ListView
        {
            X = 0,
            Y = ScreenLayout.GraphTop,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
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
        Window.Add(header, tabs, Graph, picker, status);
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

    /// <summary>Redraws the header, tab row and status bar after the model changed.</summary>
    public void RefreshChrome()
    {
        header.SetNeedsDraw();
        tabs.SetNeedsDraw();
        status.SetNeedsDraw();
        picker.SetNeedsDraw();
    }

    public void Dispose() => Window.Dispose();
}
