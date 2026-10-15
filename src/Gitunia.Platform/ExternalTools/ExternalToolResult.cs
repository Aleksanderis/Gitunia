namespace Gitunia.Platform.ExternalTools;

/// <summary>How an external tool ended.</summary>
/// <param name="ExitCode">
/// The exit code, or <see cref="NotStartedExitCode"/> when the program could not be started.
/// </param>
/// <param name="Error">Why it could not be started, null when it ran.</param>
public sealed record ExternalToolResult(int ExitCode, string? Error = null)
{
    /// <summary>The exit code reported for a program that never ran.</summary>
    public const int NotStartedExitCode = -1;

    public bool Succeeded => Error is null && ExitCode == 0;

    /// <summary>One line for the status bar.</summary>
    public string Describe(string toolName)
    {
        if (Error is not null)
        {
            return $"{toolName}: {Error}";
        }

        return Succeeded ? $"{toolName} finished" : $"{toolName} exited with code {ExitCode}";
    }
}
