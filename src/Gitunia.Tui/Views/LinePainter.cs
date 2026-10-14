using Gitunia.Tui.Rendering;
using Gitunia.Tui.Rendering.Styling;
using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.Views;

/// <summary>Draws a <see cref="StyledLine"/> into a view at a position.</summary>
public static class LinePainter
{
    public static void Paint(View view, StyledLine line, int column, int row, bool isFocused)
    {
        view.Move(column, row);
        foreach (var segment in line.Segments)
        {
            view.SetAttribute(Palette.For(segment, line.IsSelected, isFocused));
            view.AddStr(segment.Text);
        }
    }
}
