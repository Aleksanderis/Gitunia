namespace Gitunia.Core.Themes.Models;

/// <summary>The names of the 16 terminal colours a theme file can use in its <c>ansi</c> object (any case).</summary>
public static class AnsiColorNames
{
    /// <summary>All 16 names.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        "black", "red", "green", "yellow", "blue", "magenta", "cyan", "gray",
        "darkGray", "brightRed", "brightGreen", "brightYellow", "brightBlue", "brightMagenta", "brightCyan", "white",
    ];

    /// <summary>The name in <see cref="All"/> that matches a text ignoring case, null when there is none.</summary>
    /// <param name="text">The text from the file.</param>
    public static string? Find(string text) =>
        All.FirstOrDefault(name => string.Equals(name, text.Trim(), StringComparison.OrdinalIgnoreCase));
}
