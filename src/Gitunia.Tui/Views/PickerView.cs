using Gitunia.Tui.Rendering;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.Views;

/// <summary>The repo picker area: draws the composed picker and reports clicks on rows (a click highlights, a double-click opens).</summary>
public sealed class PickerView(Func<ScreenModel> model, GraphGlyphSet glyphs) : View
{
    /// <summary>Raised with the row index and whether it was a double-click.</summary>
    public event Action<int, bool>? RowClicked;

    protected override bool OnDrawingContent(Terminal.Gui.ViewBase.DrawContext? context)
    {
        if (model().Picker is not { } picker)
        {
            return true;
        }

        var lines = PickerComposer.Compose(picker, model().HomeDirectory, Viewport.Width, Viewport.Height, glyphs);
        for (var row = 0; row < lines.Count; row++)
        {
            LinePainter.Paint(this, lines[row], 0, row, isFocused: true);
        }

        return true;
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        var isClick = mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked);
        var isDoubleClick = mouse.Flags.HasFlag(MouseFlags.LeftButtonDoubleClicked);
        if ((!isClick && !isDoubleClick) || mouse.Position is not { } position || model().Picker is not { } picker)
        {
            return base.OnMouseEvent(mouse);
        }

        var listHeight = Viewport.Height - PickerComposer.HeaderRows - (picker.Message.Length > 0 ? 1 : 0);
        var listRow = position.Y - PickerComposer.HeaderRows;
        if (listRow >= 0 && listRow < listHeight)
        {
            var index = PickerComposer.TopRow(picker.Highlight, listHeight) + listRow;
            if (index < picker.Rows.Length)
            {
                RowClicked?.Invoke(index, isDoubleClick);
            }
        }

        return true;
    }
}
