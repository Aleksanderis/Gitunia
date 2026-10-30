using Gitunia.Core.Mouse;
using Gitunia.Core.Picker;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.Views;

/// <summary>The repository picker area: draws the composed picker and reports clicks on rows (a click highlights) and the gestures the mouse map decides on.</summary>
public sealed class PickerView(Func<ScreenModel> model, GraphGlyphSet glyphs) : View
{
    /// <summary>True while something modal owns the screen: mouse input is swallowed.</summary>
    public Func<bool> IsSuppressed { get; set; } = () => false;

    /// <summary>Raised with the row index when a row is clicked (a double-click raises it before <see cref="GestureRaised"/>).</summary>
    public event Action<int>? RowClicked;

    /// <summary>Raised with the mouse-map view of a button (pin, remove) clicked on the highlighted row.</summary>
    public event Action<string>? ButtonClicked;

    /// <summary>Raised for a double-click on a row and for the wheel.</summary>
    public event Action<MouseGesture>? GestureRaised;

    protected override bool OnDrawingContent(DrawContext? context)
    {
        if (model().Picker is not { } picker)
        {
            return true;
        }

        var lines = PickerComposer.Compose(
            picker,
            model().HomeDirectory,
            Viewport.Width,
            Viewport.Height,
            glyphs,
            model().Keys);
        var layout = PickerLayout.Compute(picker, Viewport.Width, Viewport.Height);
        for (var row = 0; row < lines.Count; row++)
        {
            LinePainter.Paint(this, lines[row], layout.BoxX, layout.BoxY + row, isFocused: true);
        }

        return true;
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (!MouseFlagTranslator.TryTranslate(mouse.Flags, out var gesture))
        {
            return base.OnMouseEvent(mouse);
        }

        if (IsSuppressed())
        {
            return true;
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

        var layout = PickerLayout.Compute(picker, Viewport.Width, Viewport.Height);
        var line = position.Y - layout.BoxY;
        var column = position.X - layout.BoxX;
        if (!layout.IsFramed || column < 0 || column >= layout.BoxWidth)
        {
            return CompactClick(picker, layout, position.Y, gesture);
        }

        if (line == layout.FooterLine && gesture is MouseGesture.Click or MouseGesture.DoubleClick)
        {
            var buttons = PickerFooterButtons.Layout(
                picker.HighlightedRow,
                layout.InnerWidth,
                picker.LocatingTabId is not null,
                model().Keys);
            if (TitleButton.ViewAt(buttons, column - 1) is { } buttonView)
            {
                ButtonClicked?.Invoke(buttonView);
            }

            return true;
        }

        if (layout.RowIndexAt(line) is { } index)
        {
            ClickRow(index, gesture);
        }

        return true;
    }

    private bool CompactClick(PickerState picker, PickerLayout layout, int line, MouseGesture gesture)
    {
        if (!layout.IsFramed && layout.RowIndexAt(line) is { } index && index < picker.Rows.Length)
        {
            ClickRow(index, gesture);
        }

        return true;
    }

    private void ClickRow(int index, MouseGesture gesture)
    {
        RowClicked?.Invoke(index);
        if (gesture == MouseGesture.DoubleClick)
        {
            GestureRaised?.Invoke(MouseGesture.DoubleClick);
        }
    }
}
