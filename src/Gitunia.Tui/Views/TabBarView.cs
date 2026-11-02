using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Tabs;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.Views;

/// <summary>The tab row: draws the tab labels and reports which tab was clicked.</summary>
public sealed class TabBarView(IScreenContent content, GraphGlyphSet glyphs) : View
{
    /// <summary>Raised with the one-based tab number when a tab is clicked.</summary>
    public event Action<int>? TabClicked;

    /// <summary>Raised with the one-based tab number when the dirty marker of a tab is clicked.</summary>
    public event Action<int>? DirtyMarkerClicked;

    /// <summary>Raised when the commands button at the right end of the row is clicked.</summary>
    public event Action? CommandsClicked;

    /// <summary>Raised with the mouse view name of a fetch, pull, push or cancel chip that was clicked.</summary>
    public event Action<string>? RemoteChipClicked;

    /// <summary>Raised when the "+" button after the last tab is clicked.</summary>
    public event Action? NewTabClicked;

    protected override bool OnDrawingContent(DrawContext? context)
    {
        LinePainter.Paint(this, ChromeBuilder.Tabs(content.Model(), Viewport.Width, glyphs), 0, 0, isFocused: false);
        return true;
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (!MouseFlagTranslator.IsLeftClick(mouse.Flags) || mouse.Position is not { } position)
        {
            return base.OnMouseEvent(mouse);
        }

        // The button only exists when the row shows tabs.
        if (content.Model().Tabs is not { Count: > 0 } labels)
        {
            return base.OnMouseEvent(mouse);
        }

        if (TabBarLayout.IsOnCommands(Viewport.Width, position.X, content.Model().Keys))
        {
            CommandsClicked?.Invoke();
            return true;
        }

        if (content.Model().Picker is null
            && RemoteChips.ViewAt(Viewport.Width, content.Model().RemoteBusy, position.X, content.Model().Keys) is { } chipView)
        {
            RemoteChipClicked?.Invoke(chipView);
            return true;
        }

        var cells = TabBarLayout.Layout(labels, Viewport.Width, glyphs, content.Model().Keys);
        if (TabBarLayout.IsOnNewTab(cells, position.X))
        {
            NewTabClicked?.Invoke();
            return true;
        }

        if (TabBarLayout.DirtyMarkerAt(cells, position.X) is { } dirtyNumber)
        {
            DirtyMarkerClicked?.Invoke(dirtyNumber);
            return true;
        }

        // The row starts with "< " when tabs are cut off on the left; the layout already counts it in the cell columns.
        if (TabBarLayout.NumberAt(cells, position.X) is { } number)
        {
            TabClicked?.Invoke(number);
        }

        return true;
    }
}
