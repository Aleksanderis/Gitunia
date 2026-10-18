namespace Gitunia.Git.Remotes;

/// <summary>
/// Decides how git's ssh is told never to ask for anything. ssh reads passphrases and host-key confirmations from the
/// controlling terminal, not from stdin, so a closed stdin does not stop it from drawing a prompt over the TUI.
/// </summary>
public static class SshCommandPolicy
{
    public const string BatchModeOption = "-o BatchMode=yes";

    private const string DefaultCommand = "ssh";
    private const char WordSeparator = ' ';

    /// <summary>
    /// The <c>GIT_SSH_COMMAND</c> to set, or null to leave ssh alone. The user's own command (the variable, else
    /// <c>core.sshCommand</c>) is kept and only extended, and only when it is plain <c>ssh</c>; a <c>GIT_SSH</c>
    /// program or another client (plink, a wrapper) is not ours to rewrite.
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
        var program = command.Split(WordSeparator, StringSplitOptions.RemoveEmptyEntries)[0];
        var isSsh = string.Equals(
            Path.GetFileNameWithoutExtension(program),
            DefaultCommand,
            StringComparison.OrdinalIgnoreCase);
        if (!isSsh)
        {
            return null;
        }

        if (command.Contains(BatchModeOption, StringComparison.Ordinal))
        {
            return command;
        }

        return $"{command.Trim()} {BatchModeOption}";
    }

    private static string? FirstNonEmpty(string? first, string? second) =>
        !string.IsNullOrWhiteSpace(first) ? first : string.IsNullOrWhiteSpace(second) ? null : second;
}
