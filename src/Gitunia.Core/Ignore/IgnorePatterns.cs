using System.Collections.Immutable;
using System.Text;
using Gitunia.Core.Prompts;

namespace Gitunia.Core.Ignore;

/// <summary>
/// The patterns offered for an untracked file or folder, as one-key choices whose value is the exact line written to
/// <c>.gitignore</c>. Pure: the same path gives the same options. Patterns are anchored to the root (a leading slash)
/// so they match this path only, and glob characters in names are escaped so they match literally.
/// </summary>
public static class IgnorePatterns
{
    /// <summary>The key of the option that ignores exactly this file or folder.</summary>
    public const string PathKey = "p";

    /// <summary>The key of the option that ignores every file with this extension.</summary>
    public const string ExtensionKey = "e";

    /// <summary>The key of the option that ignores the folder the path is in.</summary>
    public const string FolderKey = "d";

    private const char Separator = '/';
    private const char Dot = '.';
    private const char Escape = '\\';
    private const char Space = ' ';
    private const string Glob = "*";
    private const string GlobSpecials = "\\*?[";

    /// <summary>The options for <paramref name="path"/>, relative to the repository root as git reports it.</summary>
    /// <param name="path">The file, or the folder (a trailing slash is ignored).</param>
    /// <param name="isFolder">The path is a folder (a folder row or an untracked directory).</param>
    public static ImmutableArray<PromptKeyChoice> Options(string path, bool isFolder)
    {
        var trimmed = path.TrimEnd(Separator);
        var slash = trimmed.LastIndexOf(Separator);
        var name = trimmed[(slash + 1)..];
        var suffix = isFolder ? Separator.ToString() : string.Empty;
        var own = $"{Separator}{Escaped(trimmed)}{suffix}";
        var kind = isFolder ? "this folder" : "this file";
        List<PromptKeyChoice> options = [new(PathKey, $"{kind}: {own}", own)];
        if (!isFolder && ExtensionOf(name) is { } extension)
        {
            var pattern = $"{Glob}{Escaped(extension)}";
            options.Add(new PromptKeyChoice(ExtensionKey, $"every file with this extension: {pattern}", pattern));
        }

        if (slash > 0)
        {
            var parent = $"{Separator}{Escaped(trimmed[..slash])}{Separator}";
            options.Add(new PromptKeyChoice(FolderKey, $"the folder it is in: {parent}", parent));
        }

        return [.. options];
    }

    /// <summary>True when the name cannot be written as one line of a <c>.gitignore</c> (it holds a line break or another
    /// control character).</summary>
    public static bool IsUnwritable(string path) => path.Any(char.IsControl);

    // The part from the last dot, when the dot is not the first or the last character (".env" has none).
    private static string? ExtensionOf(string name)
    {
        var dot = name.LastIndexOf(Dot);

        return dot > 0 && dot < name.Length - 1 ? name[dot..] : null;
    }

    // Glob characters and a trailing space would be read as pattern syntax; a backslash makes them literal.
    private static string Escaped(string text)
    {
        var builder = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            var isTrailingSpace = character == Space && index == text.Length - 1;
            if (GlobSpecials.Contains(character) || isTrailingSpace)
            {
                builder.Append(Escape);
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
