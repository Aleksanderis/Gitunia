using Gitunia.Tui.Rendering;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Window = Terminal.Gui.Views.Window;

namespace Gitunia.Tui.Views;

/// <summary>The first screen: header, tab row, the graph list and the status bar, laid out like <see cref="ScreenLayout"/>.</summary>
public sealed class MainView : IDisposable
{
    private readonly LineView header;
    private readonly LineView tabs;
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
        tabs = new LineView(width => ChromeBuilder.Tabs(model(), width, glyphs))
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
        status = new LineView(width => ChromeBuilder.Status(model(), width, glyphs))
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1,
        };
        Window = new Window { Title = string.Empty, BorderStyle = Terminal.Gui.Drawing.LineStyle.None, Width = Dim.Fill(), Height = Dim.Fill() };
        Window.Add(header, tabs, Graph, status);
    }

    public Window Window { get; }

    public ListView Graph { get; }

    /// <summary>Redraws the header, tab row and status bar after the model changed.</summary>
    public void RefreshChrome()
    {
        header.SetNeedsDraw();
        tabs.SetNeedsDraw();
        status.SetNeedsDraw();
    }

    public void Dispose() => Window.Dispose();
}
