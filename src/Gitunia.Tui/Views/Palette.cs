using Gitunia.Tui.Rendering;
using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;
using StyledSegment = Gitunia.Tui.Rendering.StyledSegment;

namespace Gitunia.Tui.Views;

/// <summary>Maps the renderer-agnostic <see cref="SegmentRole"/>s to Terminal.Gui colours and text styles.</summary>
public static class Palette
{
    private static readonly Color[] LaneColors = new[] { 75, 114, 215, 177, 203, 80, 222, 141, 150, 209, 117, 183 }
        .Select(Xterm)
        .ToArray();

    private static readonly Color HashGrey = Xterm(244);

    public static Attribute For(StyledSegment segment, bool isSelected, bool isFocused)
    {
        var selection = !isSelected ? TextStyle.None : isFocused ? TextStyle.Reverse : TextStyle.Bold;
        return segment.Role switch
        {
            SegmentRole.Lane => new Attribute(LaneColors[segment.ColorIndex % LaneColors.Length], Color.None, selection),
            SegmentRole.Hash => new Attribute(HashGrey, Color.None, selection | TextStyle.Faint),
            SegmentRole.Punctuation => new Attribute(HashGrey, Color.None, selection),
            SegmentRole.LocalBranch => new Attribute(Xterm(114), Color.None, selection | TextStyle.Bold),
            SegmentRole.CurrentBranch => new Attribute(Xterm(80), Color.None, selection | TextStyle.Bold),
            SegmentRole.RemoteBranch => new Attribute(Xterm(203), Color.None, selection | TextStyle.Bold),
            SegmentRole.Tag => new Attribute(Xterm(222), Color.None, selection | TextStyle.Bold),
            SegmentRole.Header => Style(TextStyle.Bold | TextStyle.Reverse),
            SegmentRole.ActiveTab => Style(TextStyle.Reverse),
            SegmentRole.InactiveTab => Style(TextStyle.Faint),
            SegmentRole.StatusMode => Style(TextStyle.Bold | TextStyle.Reverse),
            SegmentRole.Status => Style(TextStyle.Faint | TextStyle.Reverse),
            SegmentRole.Faint => Style(TextStyle.Faint),
            SegmentRole.Error => new Attribute(Xterm(203), Color.None, TextStyle.Bold),
            SegmentRole.OverlayTitle => Style(TextStyle.Bold | TextStyle.Reverse),
            SegmentRole.OverlayKey => new Attribute(Xterm(222), Color.None, selection | TextStyle.Bold),
            _ => Style(selection),
        };
    }

    // The terminal's default colours with a style (Reverse swaps them: the "filled bar" look).
    private static Attribute Style(TextStyle style) => new(Color.None, Color.None, style);

    // xterm-256 palette index to true colour.
    private static Color Xterm(int index)
    {
        if (index >= 232)
        {
            var grey = 8 + ((index - 232) * 10);
            return new Color(grey, grey, grey);
        }

        int[] levels = [0, 95, 135, 175, 215, 255];
        var offset = index - 16;
        return new Color(levels[offset / 36], levels[(offset / 6) % 6], levels[offset % 6]);
    }
}
