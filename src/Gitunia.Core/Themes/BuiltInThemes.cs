using Gitunia.Core.Config.Models;
using Gitunia.Core.Themes.Models;

namespace Gitunia.Core.Themes;

/// <summary>
/// The theme files shipped inside the binary (embedded resources of <c>Themes/Files</c>, read without reflection). A
/// theme is added by dropping its file there and listing its name in <see cref="Names"/>; a test checks both agree.
/// </summary>
public static class BuiltInThemes
{
    private const string ResourcePrefix = "themes/";
    private const string ResourceSuffix = ".json";

    /// <summary>The names (file names without extension) of the built-in themes, in the order they are listed.</summary>
    public static IReadOnlyList<string> Names { get; } =
    [
        "catppuccin-mocha",
        "catppuccin-macchiato",
        "catppuccin-frappe",
        "catppuccin-latte",
        "tokyo-night",
        "tokyo-night-storm",
        "tokyo-night-moon",
        "tokyo-night-day",
    ];

    /// <summary>The names of all embedded theme files, whether listed in <see cref="Names"/> or not.</summary>
    public static IReadOnlyList<string> EmbeddedNames { get; } = [.. typeof(BuiltInThemes).Assembly
        .GetManifestResourceNames()
        .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
        .Select(name => name[ResourcePrefix.Length..^ResourceSuffix.Length])
        .Order(StringComparer.Ordinal)];

    /// <summary>The theme of that name, null when there is none or its file is invalid (a bug, reported as a problem).</summary>
    /// <param name="name">The lower-case name.</param>
    /// <param name="problems">Receives the reasons a built-in file is invalid.</param>
    public static ThemeDefinition? Find(string name, ICollection<ConfigProblem> problems)
    {
        using var stream = typeof(BuiltInThemes).Assembly.GetManifestResourceStream($"{ResourcePrefix}{name}{ResourceSuffix}");
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);

        return ThemeFileParser.Parse(name, reader.ReadToEnd(), problems);
    }
}
