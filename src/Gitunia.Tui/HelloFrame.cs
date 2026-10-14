using Gitunia.Platform;

namespace Gitunia.Tui;

/// <summary>Placeholder frame so the AOT binary has something to print.</summary>
public static class HelloFrame
{
    public static string Render(string title, ConsoleCapabilities capabilities)
    {
        var line = capabilities.Unicode ? new string('─', title.Length + 2) : new string('-', title.Length + 2);
        var glyphs = capabilities.Unicode ? BoxGlyphs.Unicode : BoxGlyphs.Ascii;
        return $"{glyphs.TopLeft}{line}{glyphs.TopRight}\n{glyphs.Vertical} {title} {glyphs.Vertical}\n{glyphs.BottomLeft}{line}{glyphs.BottomRight}\n";
    }
}
