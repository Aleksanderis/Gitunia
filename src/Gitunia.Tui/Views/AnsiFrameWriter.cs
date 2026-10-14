using System.Globalization;
using System.Text;
using Gitunia.Platform;
using Gitunia.Tui.Rendering.Styling;
using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;
using StyledSegment = Gitunia.Tui.Rendering.Styling.StyledSegment;

namespace Gitunia.Tui.Views;

/// <summary>
/// Writes a composed frame with the colours the <see cref="Palette"/> would give it, as ANSI escape sequences
/// (<c>--print-frame --colors</c>): a way to look at the colours of any scheme without starting the UI.
/// </summary>
public static class AnsiFrameWriter
{
    private const string Escape = "\u001b[";

    // The named colours in the order of the SGR codes 30-37 and 90-97.
    private static readonly ColorName16[] AnsiOrder =
    [
        ColorName16.Black, ColorName16.Red, ColorName16.Green, ColorName16.Yellow, ColorName16.Blue, ColorName16.Magenta, ColorName16.Cyan, ColorName16.Gray,
        ColorName16.DarkGray, ColorName16.BrightRed, ColorName16.BrightGreen, ColorName16.BrightYellow, ColorName16.BrightBlue, ColorName16.BrightMagenta, ColorName16.BrightCyan, ColorName16.White,
    ];

    public static string Render(IReadOnlyList<StyledLine> lines, PaletteSettings settings)
    {
        var colors = PaletteTable.For(settings);
        var builder = new StringBuilder();
        foreach (var line in lines)
        {
            foreach (var segment in line.Segments)
            {
                var selection = SelectionOf(segment, line);
                builder.Append(Escape).Append('0').Append(Sequence(Palette.For(segment, selection.IsSelected, selection.IsFocused, colors), settings.Depth)).Append('m').Append(segment.Text);
            }

            builder.Append(Escape).Append("0m").Append('\n');
        }

        return builder.ToString();
    }

    private static SegmentHighlight SelectionOf(StyledSegment segment, StyledLine line) => segment.Selection switch
    {
        SegmentSelection.None => new SegmentHighlight(false, false),
        SegmentSelection.Focused => new SegmentHighlight(true, true),
        SegmentSelection.Dim => new SegmentHighlight(true, false),
        _ => new SegmentHighlight(line.IsSelected, true),
    };

    // The parameters that follow "0" (reset) for an attribute, each starting with ';'.
    private static string Sequence(Attribute attribute, ColorDepth depth)
    {
        var builder = new StringBuilder();
        AppendStyle(builder, attribute.Style, TextStyle.Bold, 1);
        AppendStyle(builder, attribute.Style, TextStyle.Faint, 2);
        AppendStyle(builder, attribute.Style, TextStyle.Underline, 4);
        AppendStyle(builder, attribute.Style, TextStyle.Reverse, 7);
        AppendColor(builder, attribute.Foreground, isBackground: false, depth);
        AppendColor(builder, attribute.Background, isBackground: true, depth);
        return builder.ToString();
    }

    private static void AppendStyle(StringBuilder builder, TextStyle style, TextStyle flag, int code)
    {
        if (style.HasFlag(flag))
        {
            builder.Append(';').Append(code.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void AppendColor(StringBuilder builder, Color color, bool isBackground, ColorDepth depth)
    {
        if (color == Color.None)
        {
            return;
        }

        if (depth == ColorDepth.Ansi16)
        {
            for (var index = 0; index < AnsiOrder.Length; index++)
            {
                if (new Color(AnsiOrder[index]) == color)
                {
                    var code = (index < 8 ? 30 : 90 - 8) + index + (isBackground ? 10 : 0);
                    builder.Append(';').Append(code.ToString(CultureInfo.InvariantCulture));
                    return;
                }
            }

            return;
        }

        builder.Append(isBackground ? ";48;2;" : ";38;2;")
            .Append(color.R.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(color.G.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(color.B.ToString(CultureInfo.InvariantCulture));
    }
}
