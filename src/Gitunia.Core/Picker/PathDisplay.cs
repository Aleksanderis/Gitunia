using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Picker;

/// <summary>How paths are shown to the user: native on Windows (no <c>~</c> shortcut), home-abbreviated on Linux and macOS.</summary>
public static class PathDisplay
{
    /// <summary>The path for display. On Windows it is returned as is; elsewhere the home directory becomes <c>~</c>.</summary>
    public static string Abbreviate(string path, string homeDirectory, bool windows)
    {
        if (windows || homeDirectory.Length == 0)
        {
            return path;
        }

        var home = Path.TrimEndingDirectorySeparator(homeDirectory);
        if (RepositoryPathComparer.Instance.Equals(path, home))
        {
            return "~";
        }

        var prefix = home + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return path.StartsWith(prefix, comparison) ? $"~{Path.DirectorySeparatorChar}{path[prefix.Length..]}" : path;
    }

    /// <summary>The path starts the picker hint names.</summary>
    public static string PathStartsHint(bool windows) => windows ? @"C:\ .\ ..\" : "/ ~/ ./ ../";

    /// <summary>An example path for messages.</summary>
    public static string ExamplePath(bool windows) => windows ? @"C:\projects\app" : "~/projects/app";
}
