using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Gitunia.Tui;

/// <summary>Colours/styles shared by the polished sample; mirrors the custom renderer demo's look.</summary>
public static class Look
{
    /// <summary>xterm-256 palette index to true colour, so we can reuse the custom demo's exact colours.</summary>
    public static Color Xterm(int index)
    {
        if (index >= 232)
        {
            var grey = 8 + ((index - 232) * 10);
            return new Color(grey, grey, grey);
        }

        int[] levels = [0, 95, 135, 175, 215, 255];
        var offset = index - 16;
        return new Color(levels[offset / 36], levels[offset / 6 % 6], levels[offset % 6]);
    }

    public static readonly Color[] Lanes = new[] { 75, 114, 215, 177, 203, 80, 222, 141, 150, 209, 117, 183 }.Select(Xterm).ToArray();

    public static readonly Color HashGrey = Xterm(244);

    public static readonly Color SectionBlue = Xterm(110);

    /// <summary>Terminal default colours with the given style (Reverse swaps them: the "filled bar" look).</summary>
    public static Attribute Style(TextStyle style) => new(Color.None, Color.None, style);
}

/// <summary>One-line view drawn from coloured segments and padded to the full width (header, tab row, status line).</summary>
public sealed class SegmentView : View
{
    public Func<IEnumerable<(string Text, Attribute Appearance)>>? Build { get; set; }

    public Attribute Fill { get; set; } = Look.Style(TextStyle.None);

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var width = Viewport.Width;
        Move(0, 0);
        var used = 0;
        foreach (var (text, appearance) in Build?.Invoke() ?? Enumerable.Empty<(string, Attribute)>())
        {
            if (used >= width)
            {
                break;
            }

            var visible = text.Length > width - used ? text[..(width - used)] : text;
            SetAttribute(appearance);
            AddStr(visible);
            used += visible.Length;
        }

        SetAttribute(Fill);
        AddStr(new string(' ', Math.Max(0, width - used)));
        return true;
    }
}
