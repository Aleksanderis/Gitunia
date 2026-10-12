using Gitunia.Core.Mouse;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.Views;

/// <summary>The repo picker area: draws the composed picker and reports clicks on rows (a click highlights) and the gestures the mouse map decides on.</summary>
public sealed class PickerView(Func<ScreenModel> model, GraphGlyphSet glyphs) : View
{
    /// <summary>Raised with the row index when a row is clicked (a double-click raises it before <see cref="GestureRaised"/>).</summary>
    public event Action<int>? RowClicked;

    /// <summary>Raised for a double-click on a row and for the wheel.</summary>
    public event Action<MouseGesture>? GestureRaised;

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
        if (!MouseFlagTranslator.TryTranslate(mouse.Flags, out var gesture))
        {
            return base.OnMouseEvent(mouse);
        }

        if (gesture is MouseGesture.WheelUp or MouseGesture.WheelDown)
        {
            GestureRaised?.Invoke(gesture);
            return true;
        }

        if (mouse.Position is not { } position || model().Picker is not { } picker)
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
                RowClicked?.Invoke(index);
                if (gesture == MouseGesture.DoubleClick)
                {
                    GestureRaised?.Invoke(MouseGesture.DoubleClick);
                }
            }
        }

        return true;
    }
}
