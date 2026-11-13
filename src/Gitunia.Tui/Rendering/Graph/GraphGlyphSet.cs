
using Gitunia.Core.Config.Models;

namespace Gitunia.Tui.Rendering.Graph;

/// <summary>The characters the graph is drawn with; there is a box-drawing set and an ASCII fallback.</summary>
/// <param name="Node">A commit.</param>
/// <param name="MergeNode">A commit with several parents.</param>
/// <param name="LineNode">A commit in the middle of a branch: one line in and one out on its own lane, drawn as part of the line.</param>
/// <param name="Lines">Line characters indexed by <see cref="LineDirections"/> flags (16 entries).</param>
/// <param name="DepartRight">In the cell of a new lane right of the node: the line falls from the node's side to the lane's centre (see <see cref="GraphDiagonals"/>).</param>
/// <param name="DepartLeft">The same for a new lane left of the node.</param>
/// <param name="Ellipsis">Shown where text was cut.</param>
/// <param name="CurrentBranch">Before the name of the branch HEAD is on, inside its label box: the local glyph with a `◉` directly in front, no space (ASCII: `HEAD -> `).</param>
/// <param name="LocalBranch">
/// Before the name of every other local branch, inside its label box: the branch exists locally, checked out or not (#826).
/// Empty in the ASCII set, where an unmarked name is the local one.
/// </param>
/// <param name="Remote">After the name, inside the label box (the box has one space of padding on each side, so a wide icon does not touch its edge, #827), of a branch that also stands for its remote-tracking branch.</param>
/// <param name="UpstreamGone">After the label box, separated by a space, of a local branch whose upstream no longer exists.</param>
/// <param name="Picker">The marks of the repository picker (#1203).</param>
/// <param name="LabelCapLeft">Drawn before a branch label box, in the colour of the box over the row background (#851); empty for a plain box.</param>
/// <param name="LabelCapRight">Drawn after a branch label box, like <paramref name="LabelCapLeft"/>.</param>
/// <param name="LaneLines">
/// Line characters of the graph lanes, laid out like <paramref name="Lines"/>, when the lane style draws them
/// differently from the frames and rules (<see cref="WithLaneStyle"/>); empty for the same ones.
/// </param>
public sealed record GraphGlyphSet(
    char Node,
    char MergeNode,
    char LineNode,
    string Lines,
    string DepartRight,
    string DepartLeft,
    string Ellipsis,
    string CurrentBranch,
    string LocalBranch,
    string Remote,
    string UpstreamGone,
    PickerGlyphs Picker,
    string LabelCapLeft = "",
    string LabelCapRight = "",
    string LaneLines = "")
{
    private const string HeavyLines = " ┃┃┃━┛┓┫━┗┏┣━┻┳╋";
    private const char HeavyLineNode = '┣';

    // Index: Up=1, Down=2, Left=4, Right=8. Lone strokes borrow the straight line of their axis.
    public static GraphGlyphSet Unicode { get; } =
        new('●', '○', '├', " │││─╯╮┤─╰╭├─┴┬┼", "\\", "/", "…", "◉ ", "⎇ ", " ", " ✗", PickerGlyphs.Unicode);

    public static GraphGlyphSet Ascii { get; } =
        new('*', 'o', '|', " |||-/\\|-\\/|---+", "\\", "/", "~", "HEAD -> ", "", " (r)", " (x)", PickerGlyphs.Ascii);

    /// <summary>
    /// The set with Powerline caps on the branch labels (#851). Only the Unicode set takes them, and only where the
    /// terminal has colours: a cap is drawn in the box colour on the row background, which monochrome (inverse video)
    /// cannot do on a selected row. The ASCII set, no colours and <see cref="LabelCapStyle.None"/> keep the plain box.
    /// </summary>
    /// <param name="style">The configured style.</param>
    /// <param name="hasColors">The terminal shows colours.</param>
    public GraphGlyphSet WithLabelCaps(LabelCapStyle style, bool hasColors)
    {
        if (IsAscii || !hasColors)
        {
            return this;
        }

        return style switch
        {
            LabelCapStyle.Rounded => this with { LabelCapLeft = "\uE0B6", LabelCapRight = "\uE0B4" },
            LabelCapStyle.Angled => this with { LabelCapLeft = "\uE0B2", LabelCapRight = "\uE0B0" },
            _ => this,
        };
    }

    /// <summary>
    /// The set with the lines of the graph lanes drawn in the given style (<c>laneStyle</c>). Only the lanes change: frames,
    /// rules and overlays keep the light lines. The ASCII set has one weight.
    /// </summary>
    /// <param name="style">The configured style.</param>
    public GraphGlyphSet WithLaneStyle(LaneStyle style)
    {
        if (IsAscii)
        {
            return this;
        }

        return style switch
        {
            LaneStyle.Heavy => this with { LineNode = HeavyLineNode, LaneLines = HeavyLines },
            _ => this with { LineNode = Unicode.LineNode, LaneLines = string.Empty },
        };
    }

    /// <summary>Branch label boxes are closed by caps (no padding inside the box then, #854).</summary>
    public bool HasLabelCaps => LabelCapLeft.Length > 0;

    /// <summary>This is the ASCII fallback set.</summary>
    public bool IsAscii => Lines == Ascii.Lines;

    /// <summary>Drawn in place of a tab's first column when whitespace is shown.</summary>
    public char TabMark => IsAscii ? '>' : '→';

    /// <summary>Drawn in place of a trailing space (a heavier mark than inner ones) when whitespace is shown.</summary>
    public char SpaceMark => IsAscii ? '.' : '•';

    /// <summary>Drawn in place of a leading or inner space when whitespace is shown.</summary>
    public char InnerSpaceMark => IsAscii ? '.' : '·';

    /// <summary>Drawn in place of a carriage return when whitespace is shown.</summary>
    public char CarriageReturnMark => IsAscii ? '<' : '¶';

    /// <summary>A horizontal connection.</summary>
    public char Horizontal => Lines[(int)(LineDirections.Left | LineDirections.Right)];

    /// <summary>The character for a cell whose lines go in the given directions.</summary>
    public char For(LineDirections directions) => Lines[(int)directions];

    /// <summary>The character for a cell of the graph lanes whose lines go in the given directions.</summary>
    /// <param name="directions">The sides the lines leave through.</param>
    public char ForLane(LineDirections directions) => (LaneLines.Length == 0 ? Lines : LaneLines)[(int)directions];

    /// <summary>The set a terminal with the given Unicode support can show.</summary>
    public static GraphGlyphSet For(bool unicode) => unicode ? Unicode : Ascii;
}
