namespace Gitunia.Tui.Rendering;

/// <summary>The characters the graph is drawn with; there is a box-drawing set and an ASCII fallback.</summary>
/// <param name="Node">A commit.</param>
/// <param name="MergeNode">A commit with several parents.</param>
/// <param name="Vertical">A lane passing through.</param>
/// <param name="Horizontal">A connection between lanes.</param>
/// <param name="Cross">A connection crossing a lane.</param>
/// <param name="IncomingFromRight">A lane from above on the right ending at the node.</param>
/// <param name="IncomingFromLeft">A lane from above on the left ending at the node.</param>
/// <param name="OutgoingToRight">A line from the node going down on the right.</param>
/// <param name="OutgoingToLeft">A line from the node going down on the left.</param>
/// <param name="TeeLeft">A lane on the right that both ends at and leaves from the node.</param>
/// <param name="TeeRight">A lane on the left that both ends at and leaves from the node.</param>
/// <param name="Ellipsis">Shown where text was cut.</param>
public sealed record GraphGlyphSet(
    char Node,
    char MergeNode,
    char Vertical,
    char Horizontal,
    char Cross,
    char IncomingFromRight,
    char IncomingFromLeft,
    char OutgoingToRight,
    char OutgoingToLeft,
    char TeeLeft,
    char TeeRight,
    string Ellipsis)
{
    public static GraphGlyphSet Unicode { get; } = new('●', '○', '│', '─', '┼', '╯', '╰', '╮', '╭', '┤', '├', "…");

    public static GraphGlyphSet Ascii { get; } = new('*', 'o', '|', '-', '+', '/', '\\', '\\', '/', '|', '|', "~");

    /// <summary>The set a terminal with the given Unicode support can show.</summary>
    public static GraphGlyphSet For(bool unicode) => unicode ? Unicode : Ascii;
}
