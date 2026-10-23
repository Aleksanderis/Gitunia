namespace Gitunia.Tui.Rendering.Files;

/// <summary>Text shared by the tree style of the Changes tab and of the working-tree lists.</summary>
public static class TreeRowText
{
    private const int IndentColumns = 2;
    private const char Separator = '/';
    private const string RenameArrow = " -> ";

    /// <summary>The blank columns in front of a row <paramref name="depth"/> folders down.</summary>
    public static string Indent(int depth) => new(' ', depth * IndentColumns);

    /// <summary>
    /// The name of a file row: the name alone, or <c>old -&gt; name</c> for a rename, with the old name only when that
    /// sits in the same folder and the whole old path otherwise.
    /// </summary>
    /// <param name="path">The path of the file (the new path of a rename).</param>
    /// <param name="name">The last segment of the path.</param>
    /// <param name="oldPath">The old path of a rename or copy, null for any other change.</param>
    public static string FileName(string path, string name, string? oldPath)
    {
        if (oldPath is null)
        {
            return name;
        }

        var directory = path[..(path.Length - name.Length)];
        var old = oldPath.StartsWith(directory, StringComparison.Ordinal)
            && oldPath.IndexOf(Separator, directory.Length) < 0
            ? oldPath[directory.Length..]
            : oldPath;

        return $"{old}{RenameArrow}{name}";
    }
}
