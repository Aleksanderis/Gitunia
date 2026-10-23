using Gitunia.Core.Config.Models;
using Gitunia.Core.Themes.Models;


namespace Gitunia.Core.Themes;

/// <summary>
/// The themes that can be chosen: the built-in ones and the user's files in the <c>themes</c> folder of the config
/// directory (<c>&lt;name&gt;.json</c>; a user file wins over a built-in theme of the same name). Files are read when a
/// theme is asked for and the result is kept.
/// </summary>
/// <param name="themesDirectory">The user's theme folder; it need not exist.</param>
public sealed class ThemeCatalog(string themesDirectory)
{
    private const string FileFilter = "*.json";
    private const string FileExtension = ".json";

    private readonly Dictionary<string, ThemeDefinition?> _loaded = new(StringComparer.Ordinal);
    private IReadOnlyDictionary<string, string>? _userFiles;

    /// <summary>The names of the choices that are themes: the built-in ones, then the user's, each once, lower case.</summary>
    public IReadOnlyList<string> Names =>
    [
        .. BuiltInThemes.Names,
        .. UserFiles().Keys.Where(name => !BuiltInThemes.Names.Contains(name)).Order(StringComparer.Ordinal),
    ];

    /// <summary>The theme with a name (any case), null when there is none or its file is unusable.</summary>
    /// <param name="name">The name from the config file.</param>
    /// <param name="problems">Receives why a file was unusable; nothing is added on a repeated call.</param>
    public ThemeDefinition? Find(string name, ICollection<ConfigProblem> problems)
    {
        var key = name.Trim().ToLowerInvariant();
        if (_loaded.TryGetValue(key, out var known))
        {
            return known;
        }

        var found = UserFiles().TryGetValue(key, out var path) ? ReadUserFile(key, path, problems) : null;
        found ??= BuiltInThemes.Find(key, problems);
        _loaded[key] = found;

        return found;
    }

    private static ThemeDefinition? ReadUserFile(string key, string path, ICollection<ConfigProblem> problems)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            problems.Add(new ConfigProblem(Path.GetFileName(path), $"theme file cannot be read: {exception.Message}"));
            return null;
        }

        return ThemeFileParser.Parse(key, text, problems);
    }

    private IReadOnlyDictionary<string, string> UserFiles()
    {
        if (_userFiles is not null)
        {
            return _userFiles;
        }

        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            if (Directory.Exists(themesDirectory))
            {
                foreach (var path in Directory.EnumerateFiles(themesDirectory, FileFilter))
                {
                    var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                    if (path.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase) && !IsReserved(name))
                    {
                        files.TryAdd(name, path);
                    }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // An unreadable folder offers no user themes; the built-in ones still work.
        }

        _userFiles = files;

        return files;
    }

    private static bool IsReserved(string name) =>
        name.Length == 0 || name.Trim() != name || !ThemeChoice.FromText(name).IsNamedTheme;
}
