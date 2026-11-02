using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Window = Terminal.Gui.Views.Window;

namespace Gitunia.Tui.Views;

/// <summary>
/// The screen the panes sit in: the window, its size, and the two requests the pane controllers make of it (lay out
/// again, redraw the frame titles and the status bar). The main view does the redrawing; this object is built before it
/// so the controllers need no reference to the view.
/// </summary>
public sealed class PaneChrome : IPaneChrome
{
    /// <summary>The window that holds every view.</summary>
    public Window Window { get; } = new()
    {
        Title = string.Empty,
        BorderStyle = LineStyle.None,
        Width = Dim.Fill(),
        Height = Dim.Fill(),
    };

    /// <inheritdoc />
    public ScreenSize ScreenSize => new(Window.Viewport.Width, Window.Viewport.Height);

    /// <summary>Raised when the chrome (titles, tab row, status bar) is to be redrawn.</summary>
    public event Action? RefreshRequested;

    /// <inheritdoc />
    public void Relayout()
    {
        Window.SetNeedsLayout();
        RefreshChrome();
    }

    /// <inheritdoc />
    public void RefreshChrome() => RefreshRequested?.Invoke();
}
