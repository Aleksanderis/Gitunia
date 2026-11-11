using System.Buffers;
using System.Text;

namespace Gitunia.Platform.ExternalTools;

/// <summary>
/// Turns an editor setting (<c>$VISUAL</c> / <c>$EDITOR</c>) into the program and arguments to start, without a
/// shell. Words are split at white space; double quotes (and single quotes at the start of a word, so `O'Brien`
/// stays literal) group a word (so a quoted Program Files path is
/// one word) and a backslash is literal, because Windows paths are full of them. On Windows the program is looked up
/// through <c>PATH</c> and <c>PATHEXT</c>, so <c>code</c> finds <c>code.cmd</c>; <c>CreateProcess</c> starts a
/// <c>.cmd</c> or <c>.bat</c> file through <c>cmd.exe</c> itself. Values that only work in git's <c>sh</c>, such as
/// <c>/usr/bin/vim</c> from Git Bash, are not translated and fail with a clear message.
/// </summary>
public static class EditorCommand
{
    private static readonly SearchValues<char> CmdMetaCharacters = SearchValues.Create("&|<>^%!\"");
    private static readonly string[] DefaultExtensions = [".COM", ".EXE", ".BAT", ".CMD"];

    /// <summary>Splits the setting into words, honouring quotes. An unterminated quote runs to the end.</summary>
    public static IReadOnlyList<string> Split(string text)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        var started = false;
        var quote = '\0';
        foreach (var character in text)
        {
            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }
                else
                {
                    word.Append(character);
                }
            }
            else if (character == '"' || (character == '\'' && !started))
            {
                quote = character;
                started = true;
            }
            else if (char.IsWhiteSpace(character))
            {
                if (started)
                {
                    words.Add(word.ToString());
                    word.Clear();
                    started = false;
                }
            }
            else
            {
                word.Append(character);
                started = true;
            }
        }

        if (started)
        {
            words.Add(word.ToString());
        }

        return words;
    }

    /// <summary>
    /// Builds the request for the setting and the file to edit. Returns null and sets <paramref name="error"/> when
    /// the program cannot be found or cannot be started safely.
    /// </summary>
    /// <param name="setting">The editor setting.</param>
    /// <param name="filePath">The file to edit, appended as the last argument.</param>
    /// <param name="workingDirectory">Directory the editor starts in.</param>
    /// <param name="isWindows">True to resolve through PATHEXT and check .cmd/.bat arguments.</param>
    /// <param name="environment">Reads an environment variable (<c>PATH</c>, <c>PATHEXT</c>).</param>
    /// <param name="fileExists">Tells whether a file exists.</param>
    /// <param name="error">The reason when null is returned.</param>
    public static ExternalToolRequest? Build(
        string setting,
        string filePath,
        string workingDirectory,
        bool isWindows,
        Func<string, string?> environment,
        Func<string, bool> fileExists,
        out string error)
    {
        error = string.Empty;
        var words = Split(setting);
        if (words.Count == 0)
        {
            error = "The editor command is empty";

            return null;
        }

        var program = words[0];
        string[] arguments = [.. words.Skip(1), filePath];
        if (!isWindows)
        {
            return new ExternalToolRequest(program, arguments, workingDirectory);
        }

        var resolved = Resolve(program, environment, fileExists);
        if (resolved is null)
        {
            error = $"Could not find the editor {program}";

            return null;
        }

        if (IsBatchFile(resolved)
            && new[] { resolved }.Concat(arguments).Any(HasCmdMetaCharacter))
        {
            error = $"Cannot pass these arguments to {Path.GetFileName(resolved)} (cmd.exe special characters)";

            return null;
        }

        return new ExternalToolRequest(resolved, arguments, workingDirectory);
    }

    private static bool HasCmdMetaCharacter(string text) => text.AsSpan().IndexOfAny(CmdMetaCharacters) >= 0;

    private static bool IsBatchFile(string path) =>
        path.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);

    private static string? Resolve(string program, Func<string, string?> environment, Func<string, bool> fileExists)
    {
        var extensions = (environment("PATHEXT") ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (extensions.Length == 0)
        {
            extensions = DefaultExtensions;
        }

        var hasDirectory = program.Contains('\\') || program.Contains('/') || Path.IsPathRooted(program);
        var pathDirectories = (environment("PATH") ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var directories = hasDirectory ? [string.Empty] : pathDirectories;

        foreach (var directory in directories)
        {
            var basePath = directory.Length == 0 ? program : Path.Combine(directory.Trim('"'), program);

            foreach (var candidate in Candidates(basePath, extensions))
            {
                if (fileExists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> Candidates(string basePath, string[] extensions)
    {
        var extension = Path.GetExtension(basePath);
        if (extension.Length > 0 && extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            yield return basePath;
        }

        foreach (var added in extensions)
        {
            yield return basePath + added;
        }
    }
}
