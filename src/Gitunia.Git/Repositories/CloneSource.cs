namespace Gitunia.Git.Repositories;

/// <summary>Pure helpers for the text a user types as the source of a clone.</summary>
public static class CloneSource
{
    private const string SchemeSeparator = "://";
    private const string GitSuffix = ".git";
    private const string FallbackName = "repository";
    private const char HomeMarker = '~';
    private const char HostSeparator = ':';
    private const char UserSeparator = '@';
    private static readonly char[] PathSeparators = ['/', '\\'];
    private static readonly char[] NameSeparators = ['/', '\\', ':'];

    /// <summary>
    /// A local path becomes absolute against <paramref name="baseDirectory"/> (<c>~</c> is the home directory) so the
    /// process working directory never matters; a URL (<c>scheme://</c> or scp-like <c>host:path</c>) is kept as typed.
    /// </summary>
    public static string Resolve(string text, string baseDirectory, string homeDirectory)
    {
        var trimmed = text.Trim();
        if (!IsLocalPath(trimmed))
        {
            return trimmed;
        }

        if (trimmed == HomeMarker.ToString())
        {
            return homeDirectory;
        }

        var expanded = trimmed.Length > 1 && trimmed[0] == HomeMarker && trimmed[1] is '/' or '\\'
            ? Path.Combine(homeDirectory, trimmed[2..])
            : trimmed;
        return Path.GetFullPath(expanded, baseDirectory);
    }

    /// <summary>
    /// The folder name a clone of <paramref name="source"/> gets by default (<c>app</c> for <c>.../app.git</c>).
    /// </summary>
    public static string RepositoryName(string source)
    {
        var name = source.Trim().TrimEnd(PathSeparators);
        var cut = name.LastIndexOfAny(NameSeparators);
        name = cut >= 0 ? name[(cut + 1)..] : name;
        if (name.EndsWith(GitSuffix, StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^GitSuffix.Length];
        }

        return name.Length == 0 || name is "." or ".." ? FallbackName : name;
    }

    private static bool IsLocalPath(string text)
    {
        if (text.Length == 0 || text.Contains(SchemeSeparator, StringComparison.Ordinal))
        {
            return false;
        }

        if (Path.IsPathRooted(text) || text[0] == HomeMarker || text[0] == '.')
        {
            return true;
        }

        // Scp-like: a colon before any slash (git@host:path).
        var colon = text.IndexOf(HostSeparator, StringComparison.Ordinal);
        var slash = text.IndexOfAny(PathSeparators);
        var isScp = colon > 0 && (slash < 0 || colon < slash);
        return !isScp && !text.Contains(UserSeparator, StringComparison.Ordinal);
    }
}
