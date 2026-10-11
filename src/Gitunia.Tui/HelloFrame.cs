using Gitunia.Platform;

namespace Gitunia.Tui;

/// <summary>Placeholder frame so the AOT binary has something to print.</summary>
public static class HelloFrame
{
    public static string Render(string title, ConsoleCapabilities capabilities)
    {
        var line = capabilities.Unicode ? new string('─', title.Length + 2) : new string('-', title.Length + 2);
        var (topLeft, topRight, bottomLeft, bottomRight, vertical) = capabilities.Unicode ? ('┌', '┐', '└', '┘', '│') : ('+', '+', '+', '+', '|');
        return $"{topLeft}{line}{topRight}\n{vertical} {title} {vertical}\n{bottomLeft}{line}{bottomRight}\n";
    }
}
