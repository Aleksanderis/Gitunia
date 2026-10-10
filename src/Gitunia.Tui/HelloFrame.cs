using Gitunia.Platform;

namespace Gitunia.Tui;

/// <summary>Placeholder frame so the AOT binary has something to print.</summary>
public static class HelloFrame
{
    public static string Render(string title, ConsoleCapabilities caps)
    {
        var line = caps.Unicode ? new string('─', title.Length + 2) : new string('-', title.Length + 2);
        var (tl, tr, bl, br, v) = caps.Unicode ? ('┌', '┐', '└', '┘', '│') : ('+', '+', '+', '+', '|');
        return $"{tl}{line}{tr}\n{v} {title} {v}\n{bl}{line}{br}\n";
    }
}
