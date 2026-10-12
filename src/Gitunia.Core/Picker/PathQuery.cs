namespace Gitunia.Core.Picker;

/// <summary>Recognises typed paths and resolves them without ever looking at the process working directory.</summary>
public static class PathQuery
{
    /// <summary>
    /// Whether the text is a path rather than a fuzzy query: it starts with <c>/</c>, is <c>~</c>, or starts with <c>~/</c>, <c>./</c>, <c>../</c>
    /// or a drive such as <c>C:\</c> or <c>C:/</c> (backslash forms count too).
    /// </summary>
    public static bool IsPath(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        if (text[0] is '/' or '\\' || text is "~" or "." or "..")
        {
            return true;
        }

        if (text.Length >= 2 && text[0] == '~' && text[1] is '/' or '\\')
        {
            return true;
        }

        if (text.StartsWith("./", StringComparison.Ordinal) || text.StartsWith(".\\", StringComparison.Ordinal)
            || text.StartsWith("../", StringComparison.Ordinal) || text.StartsWith("..\\", StringComparison.Ordinal))
        {
            return true;
        }

        return text.Length >= 3 && char.IsAsciiLetter(text[0]) && text[1] == ':' && text[2] is '/' or '\\';
    }

    /// <summary>The absolute path a typed path means: <c>~</c> becomes the home directory, relative paths are taken from <paramref name="baseDirectory"/>.</summary>
    public static string Resolve(string text, string homeDirectory, string baseDirectory)
    {
        var expanded = text;
        if (text == "~")
        {
            expanded = homeDirectory;
        }
        else if (text.Length >= 2 && text[0] == '~' && text[1] is '/' or '\\')
        {
            expanded = Path.Combine(homeDirectory, text[2..]);
        }

        return Path.GetFullPath(expanded, baseDirectory);
    }
}
