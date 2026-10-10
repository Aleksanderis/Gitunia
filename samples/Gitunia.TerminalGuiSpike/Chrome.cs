using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Gitunia.Tui;

/// <summary>Colours/styles shared by the polished sample; mirrors the custom renderer demo's look.</summary>
public static class Look
{
    /// <summary>xterm-256 palette index to true colour, so we can reuse the custom demo's exact colours.</summary>
    public static Color Xterm(int i)
    {
        if (i >= 232)
        {
            var g = 8 + ((i - 232) * 10);
            return new Color(g, g, g);
        }

        int[] lv = [0, 95, 135, 175, 215, 255];
        var c = i - 16;
        return new Color(lv[c / 36], lv[c / 6 % 6], lv[c % 6]);
    }

    public static readonly Color[] Lanes = new[] { 75, 114, 215, 177, 203, 80, 222, 141, 150, 209, 117, 183 }.Select(Xterm).ToArray();

    public static readonly Color HashGrey = Xterm(244);

    public static readonly Color SectionBlue = Xterm(110);

    /// <summary>Terminal default colours with the given style (Reverse swaps them: the "filled bar" look).</summary>
    public static Attribute Style(TextStyle s) => new(Color.None, Color.None, s);
}

/// <summary>One-line view drawn from coloured segments and padded to the full width (header, tab row, status line).</summary>
public sealed class SegView : View
{
    public Func<IEnumerable<(string Text, Attribute Attr)>>? Build { get; set; }

    public Attribute Fill { get; set; } = Look.Style(TextStyle.None);

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var width = Viewport.Width;
        Move(0, 0);
        var used = 0;
        foreach (var (text, attr) in Build?.Invoke() ?? Enumerable.Empty<(string, Attribute)>())
        {
            if (used >= width)
            {
                break;
            }

            var t = text.Length > width - used ? text[..(width - used)] : text;
            SetAttribute(attr);
            AddStr(t);
            used += t.Length;
        }

        SetAttribute(Fill);
        AddStr(new string(' ', Math.Max(0, width - used)));
        return true;
    }
}
