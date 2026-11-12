namespace Gitunia.Git.Remotes;

/// <summary>
/// Decides how git's ssh is told never to ask for anything. ssh reads passphrases and host-key confirmations from the
/// controlling terminal, not from stdin, so a closed stdin does not stop it from drawing a prompt over the TUI.
/// </summary>
public static class SshCommandPolicy
{
    public const string BatchModeOption = "-o BatchMode=yes";

    private const string DefaultCommand = "ssh";
    private const string WindowsProgram = "ssh.exe";
    private const char DoubleQuote = '"';
    private const char SingleQuote = '\'';

    private static readonly char[] WordSeparators = [' ', '\t'];
    private static readonly char[] DirectorySeparators = ['/', '\\'];

    /// <summary>
    /// The <c>GIT_SSH_COMMAND</c> to set, or null to leave ssh alone. The user's own command (the variable, else
    /// <c>core.sshCommand</c>) is kept and only extended, and only when it is plain <c>ssh</c>; a <c>GIT_SSH</c>
    /// program or another client (plink, a wrapper) is not ours to rewrite. Git runs the command through a shell, so a
    /// program path with spaces must be quoted (<c>"C:\Program Files\Git\usr\bin\ssh.exe" -i key</c>); an unquoted
    /// one is split by that shell and fails at once, so there is no prompt to prevent and it is left alone (#1313).
    /// </summary>
    /// <param name="sshCommandVariable">The value of <c>GIT_SSH_COMMAND</c>, if set.</param>
    /// <param name="sshProgramVariable">The value of <c>GIT_SSH</c>, if set.</param>
    /// <param name="configuredCommand">The value of <c>core.sshCommand</c>, if set.</param>
    public static string? BatchCommand(
        string? sshCommandVariable,
        string? sshProgramVariable,
        string? configuredCommand)
    {
        var configuredOrVariable = FirstNonEmpty(sshCommandVariable, configuredCommand);
        if (configuredOrVariable is null && !string.IsNullOrWhiteSpace(sshProgramVariable))
        {
            return null;
        }

        var command = configuredOrVariable ?? DefaultCommand;
        if (!IsSsh(ProgramOf(command)))
        {
            return null;
        }

        if (command.Contains(BatchModeOption, StringComparison.Ordinal))
        {
            return command;
        }

        return $"{command.Trim()} {BatchModeOption}";
    }

    /// <summary>
    /// The first word of <paramref name="command"/>: the text inside a leading pair of quotes, else everything up to
    /// the first blank. An unclosed quote, or text glued to the closing quote, gives null.
    /// </summary>
    private static string? ProgramOf(string command)
    {
        var trimmed = command.TrimStart();
        var first = trimmed[0];
        if (first is DoubleQuote or SingleQuote)
        {
            var closing = trimmed.IndexOf(first, 1);
            // The shell would join text right after the closing quote into the same word.
            var glued = closing + 1 < trimmed.Length && Array.IndexOf(WordSeparators, trimmed[closing + 1]) < 0;
            if (closing < 0 || glued)
            {
                return null;
            }

            return trimmed[1..closing];
        }

        var end = trimmed.IndexOfAny(WordSeparators);
        if (end < 0)
        {
            return trimmed;
        }

        return trimmed[..end];
    }

    /// <summary>
    /// Whether <paramref name="program"/> names ssh. Both separators count on every OS, so a Windows path in the
    /// configuration reads the same in tests on Linux.
    /// </summary>
    private static bool IsSsh(string? program)
    {
        if (program is null)
        {
            return false;
        }

        var name = program[(program.LastIndexOfAny(DirectorySeparators) + 1)..];

        return string.Equals(name, DefaultCommand, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, WindowsProgram, StringComparison.OrdinalIgnoreCase);
    }

    private static string? FirstNonEmpty(string? first, string? second)
    {
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first;
        }

        if (string.IsNullOrWhiteSpace(second))
        {
            return null;
        }

        return second;
    }
}
