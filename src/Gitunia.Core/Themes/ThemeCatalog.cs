using Gitunia.Core.Config.Models;
using Gitunia.Core.Themes.Models;

namespace Gitunia.Core.Themes;

/// <summary>
/// The themes that can be chosen: the built-in ones and the user's files in the <c>themes</c> folder of the config
/// directory (<c>&lt;name&gt;.json</c>; a user file wins over a built-in theme of the same name). The folder is read
/// and every file parsed once, on first use; a file that cannot be used is not offered and its reasons are in
/// <see cref="Problems"/>. Edits to a file show after a restart.
/// </summary>
/// <param name="themesDirectory">The user's theme folder; it need not exist.</param>
public sealed class ThemeCatalog(string themesDirectory)
{
    private const string FileFilter = "*.json";
    private const string FileExtension = ".json";

    private readonly Dictionary<string, ThemeDefinition?> _builtIn = new(StringComparer.Ordinal);
    private readonly List<ConfigProblem> _problems = [];
    private Dictionary<string, ThemeDefinition>? _user;

    /// <summary>The theme names on offer: the built-in ones, then the user's, each once, lower case.</summary>
    public IReadOnlyList<string> Names =>
    [
        .. BuiltInThemes.Names,
        .. User().Keys.Where(name => !BuiltInThemes.Names.Contains(name)).Order(StringComparer.Ordinal),
    ];

    /// <summary>Why user theme files were not offered (unreadable or invalid).</summary>
    public IReadOnlyList<ConfigProblem> Problems
    {
        get
        {
            User();

            return _problems;
        }
    }

    /// <summary>The theme with a name (any case), null when there is none.</summary>
    /// <param name="name">The name from the config file.</param>
    /// <param name="problems">Receives why a built-in file is invalid (a bug; nothing is added on a repeated call).</param>
    public ThemeDefinition? Find(string name, ICollection<ConfigProblem> problems)
    {
        var key = name.Trim().ToLowerInvariant();
        if (User().TryGetValue(key, out var user))
        {
            return user;
        }

        if (!_builtIn.TryGetValue(key, out var builtIn))
        {
            builtIn = BuiltInThemes.Find(key, problems);
            _builtIn[key] = builtIn;
        }

        return builtIn;
    }

    private Dictionary<string, ThemeDefinition> User()
    {
        if (_user is not null)
        {
            return _user;
        }

        var themes = new Dictionary<string, ThemeDefinition>(StringComparer.Ordinal);
        foreach (var path in ListFiles())
        {
            var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (!IsReserved(name) && !themes.ContainsKey(name) && ReadFile(name, path) is { } theme)
            {
                themes[name] = theme;
            }
        }

        _user = themes;

        return themes;
    }

    private List<string> ListFiles()
    {
        var paths = new List<string>();
        try
        {
            if (Directory.Exists(themesDirectory))
            {
                paths.AddRange(Directory.EnumerateFiles(themesDirectory, FileFilter)
                    .Where(path => path.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase)));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _problems.Add(new ConfigProblem("themes", $"theme folder cannot be read: {exception.Message}"));
        }

        paths.Sort(StringComparer.Ordinal);

        return paths;
    }

    private ThemeDefinition? ReadFile(string name, string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _problems.Add(new ConfigProblem(Path.GetFileName(path), $"theme file cannot be read: {exception.Message}"));
            return null;
        }

        return ThemeFileParser.Parse(name, text, _problems);
    }

    private static bool IsReserved(string name) =>
        name.Length == 0 || name.Trim() != name || !ThemeChoice.FromText(name).IsNamedTheme;
}
