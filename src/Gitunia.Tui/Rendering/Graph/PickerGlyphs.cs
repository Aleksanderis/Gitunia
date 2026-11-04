namespace Gitunia.Tui.Rendering.Graph;

/// <summary>The characters of the repository picker rows and query line, with an ASCII fallback.</summary>
/// <param name="Open">A repository open in a tab.</param>
/// <param name="Pinned">A pinned repository, also after the path of an open or missing pinned one.</param>
/// <param name="Recent">A recently opened repository.</param>
/// <param name="Missing">A repository whose directory is gone.</param>
/// <param name="Found">A discovered repository or a directory.</param>
/// <param name="Prompt">Before the typed query.</param>
/// <param name="More">Before the "N more" line that expands a capped section.</param>
/// <param name="Less">Before the "less" line that collapses an expanded section.</param>
public sealed record PickerGlyphs(
    string Open,
    string Pinned,
    string Recent,
    string Missing,
    string Found,
    string Prompt,
    string More,
    string Less)
{
    public static PickerGlyphs Unicode { get; } = new("●", "★", "◷", "!", "○", "›", "▸", "▴");

    public static PickerGlyphs Ascii { get; } = new("*", "+", "~", "!", "o", ">", ">", "^");
}
